using Decatron.Core.Interfaces;
using Decatron.Core.Helpers;
using Decatron.Default.Helpers;
using Decatron.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Decatron.Default.Commands
{
    /// <summary>
    /// Comando para cambiar o consultar el título del stream
    /// Uso: !title [nuevo título] o !t [nuevo título] 
    /// También funciona: title [nuevo título] o t [nuevo título] (sin !)
    /// </summary>
    public class TitleCommand : ICommand
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<TitleCommand> _logger;
        private readonly ICommandStateService _commandStateService;

        public string Name => "!title";
        public string Description => "Cambia o consulta el título del stream";

        public TitleCommand(IConfiguration configuration, ILogger<TitleCommand> logger, ICommandStateService commandStateService)
        {
            _configuration = configuration;
            _logger = logger;
            _commandStateService = commandStateService;
        }

        public async Task ExecuteAsync(string username, string channel, string message, IMessageSender messageSender)
        {
            try
            {
                // Verificar que el bot esté habilitado para este canal
                var isBotEnabled = await Utils.IsBotEnabledForChannelAsync(_configuration, channel);
                if (!isBotEnabled)
                {
                    return; // Bot deshabilitado, no responder
                }

                // Verificar si el comando específico está habilitado para este canal
                var isCommandEnabled = await IsCommandEnabledForChannel(channel);
                if (!isCommandEnabled)
                {
                    _logger.LogDebug($"Comando !title deshabilitado para el canal {channel}");
                    return; // Comando deshabilitado, no responder
                }

                // Parsear argumentos del comando (sin el prefijo !)
                var messageWithoutPrefix = message.StartsWith("!") ? message.Substring(1) : message;
                var newTitle = Utils.ParseCommandArgumentsAsString(messageWithoutPrefix);

                // Obtener información del usuario/canal
                var userInfo = await Utils.GetUserInfoFromDatabaseAsync(_configuration, channel);
                if (userInfo == null)
                {
                    await messageSender.SendMessageAsync(channel, "Error: No se pudo obtener información del canal.");
                    return;
                }

                // Si no hay nuevo título, mostrar el actual
                if (string.IsNullOrEmpty(newTitle))
                {
                    var currentTitle = await TitleUtils.GetCurrentTitleAsync(_configuration, userInfo.TwitchId, userInfo.AccessToken);
                    if (!string.IsNullOrEmpty(currentTitle))
                    {
                        var responseMessage = TitleUtils.FormatCurrentTitleMessage(currentTitle);
                        await messageSender.SendMessageAsync(channel, responseMessage);
                    }
                    else
                    {
                        await messageSender.SendMessageAsync(channel, "No se pudo obtener el título actual del stream.");
                    }
                    return;
                }

                // Verificar permisos para cambiar título
                var hasPermission = await TitleUtils.HasPermissionToChangeTitleAsync(_configuration, username, channel, _logger);
                if (!hasPermission)
                {
                    await messageSender.SendMessageAsync(channel, "Solo los moderadores o el propietario del canal pueden cambiar el título del stream.");
                    return;
                }

                // Validar el nuevo título
                var (isValid, errorMessage) = TitleUtils.ValidateTitle(newTitle);
                if (!isValid)
                {
                    await messageSender.SendMessageAsync(channel, $"Error: {errorMessage}");
                    return;
                }

                // Actualizar el título
                var success = await TitleUtils.UpdateTitleAsync(_configuration, userInfo.TwitchId, newTitle, userInfo.AccessToken);
                if (success)
                {
                    var successMessage = TitleUtils.FormatTitleChangedMessage(newTitle, username);
                    await messageSender.SendMessageAsync(channel, successMessage);

                    // Guardar en historial (opcional)
                    await TitleUtils.SaveTitleToHistoryAsync(_configuration, channel, newTitle, username);

                    _logger.LogInformation($"Título cambiado por {username} en {channel}: {newTitle}");
                }
                else
                {
                    await messageSender.SendMessageAsync(channel, "Error al cambiar el título del stream.");
                    _logger.LogWarning($"Error al cambiar título en {channel} por {username}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error en comando title para {channel}");
                await messageSender.SendMessageAsync(channel, "Error al procesar el comando de título.");
            }
        }

        /// <summary>
        /// Verifica si el comando title está habilitado para un canal específico
        /// </summary>
        private async Task<bool> IsCommandEnabledForChannel(string channelLogin)
        {
            try
            {
                // Obtener el UserId del canal
                var userInfo = await Utils.GetUserInfoFromDatabaseAsync(_configuration, channelLogin);
                if (userInfo == null)
                {
                    // Si no se encuentra el usuario, asumir que está habilitado por defecto
                    return true;
                }

                // Usar el servicio para verificar el estado del comando
                return await _commandStateService.IsCommandEnabledAsync(userInfo.Id, "title");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error verificando si comando está habilitado para {channelLogin}");
                // En caso de error, asumir que está habilitado para no bloquear funcionamiento
                return true;
            }
        }
    }
}