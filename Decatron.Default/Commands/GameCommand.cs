using Decatron.Core.Interfaces;
using Decatron.Core.Helpers;
using Decatron.Default.Helpers;
using Decatron.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Decatron.Default.Commands
{
    public class GameCommand : ICommand
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<GameCommand> _logger;
        private readonly ICommandStateService _commandStateService;

        public string Name => "!game";
        public string Description => "Cambia o consulta la categoría/juego del stream";

        public GameCommand(IConfiguration configuration, ILogger<GameCommand> logger, ICommandStateService commandStateService)
        {
            _configuration = configuration;
            _logger = logger;
            _commandStateService = commandStateService;
        }

        public async Task ExecuteAsync(string username, string channel, string message, IMessageSender messageSender)
        {
            try
            {
                _logger.LogInformation($"Ejecutando comando !game por {username} en {channel}");

                var isBotEnabled = await Utils.IsBotEnabledForChannelAsync(_configuration, channel);
                if (!isBotEnabled)
                {
                    return;
                }

                var isCommandEnabled = await IsCommandEnabledForChannel(channel);
                if (!isCommandEnabled)
                {
                    _logger.LogDebug($"Comando !game deshabilitado para el canal {channel}");
                    return;
                }

                var messageWithoutPrefix = message.StartsWith("!") ? message.Substring(1) : message;
                var newCategory = Utils.ParseCommandArgumentsAsString(messageWithoutPrefix);

                var userInfo = await Utils.GetUserInfoFromDatabaseAsync(_configuration, channel);
                if (userInfo == null)
                {
                    await messageSender.SendMessageAsync(channel, "Error: No se pudo obtener información del canal.");
                    return;
                }

                if (string.IsNullOrEmpty(newCategory))
                {
                    var currentCategory = await GameUtils.GetCurrentCategoryAsync(_configuration, userInfo.TwitchId, userInfo.AccessToken);
                    if (!string.IsNullOrEmpty(currentCategory))
                    {
                        await messageSender.SendMessageAsync(channel, $"Categoría actual: {currentCategory}");
                    }
                    else
                    {
                        await messageSender.SendMessageAsync(channel, "No se pudo obtener la categoría actual del stream.");
                    }
                    return;
                }

                var hasPermission = await GameUtils.HasPermissionToChangeCategoryAsync(_configuration, username, channel, _logger);
                if (!hasPermission)
                {
                    await messageSender.SendMessageAsync(channel, "Solo los moderadores o el propietario del canal pueden cambiar la categoría del stream.");
                    return;
                }

                var validationResult = GameUtils.ValidateCategory(newCategory);
                if (!validationResult.isValid)
                {
                    await messageSender.SendMessageAsync(channel, $"Error: {validationResult.errorMessage}");
                    return;
                }

                var success = await GameUtils.UpdateCategoryAsync(_configuration, userInfo.TwitchId, newCategory, userInfo.AccessToken);
                if (success)
                {
                    await messageSender.SendMessageAsync(channel, $"¡Categoría cambiada a: {newCategory}!");
                    await GameUtils.SaveCategoryToHistoryAsync(_configuration, channel, newCategory, username);
                    _logger.LogInformation($"Categoría cambiada por {username} en {channel}: {newCategory}");
                }
                else
                {
                    await messageSender.SendMessageAsync(channel, "Error al cambiar la categoría del stream.");
                    _logger.LogWarning($"Error al cambiar categoría en {channel} por {username}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error en comando game para {channel}");
                await messageSender.SendMessageAsync(channel, "Error al procesar el comando de categoría.");
            }
        }

        private async Task<bool> IsCommandEnabledForChannel(string channelLogin)
        {
            try
            {
                var userInfo = await Utils.GetUserInfoFromDatabaseAsync(_configuration, channelLogin);
                if (userInfo == null)
                {
                    return true;
                }

                return await _commandStateService.IsCommandEnabledAsync(userInfo.Id, "game");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error verificando si comando está habilitado para {channelLogin}");
                return true;
            }
        }
    }
}