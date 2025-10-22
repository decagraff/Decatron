using Decatron.Core.Helpers;
using Decatron.Core.Interfaces;
using Decatron.Core.Models;
using Decatron.Data;
using Decatron.Default.Helpers;
using Decatron.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Decatron.Default.Commands
{
    public class GCommand : ICommand
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<GCommand> _logger;
        private readonly ICommandStateService _commandStateService;
        private readonly DecatronDbContext _dbContext;

        public string Name => "g";
        public string Description => "Comando de gestión de categorías y micro comandos";

        public GCommand(
            IConfiguration configuration,
            ILogger<GCommand> logger,
            ICommandStateService commandStateService,
            DecatronDbContext dbContext)
        {
            _configuration = configuration;
            _logger = logger;
            _commandStateService = commandStateService;
            _dbContext = dbContext;
        }

        public async Task ExecuteAsync(string username, string channel, string message, IMessageSender messageSender)
        {
            try
            {
                _logger.LogInformation($"Ejecutando comando !g por {username} en {channel}");

                var isBotEnabled = await Utils.IsBotEnabledForChannelAsync(_configuration, channel);
                if (!isBotEnabled)
                {
                    return;
                }

                var isCommandEnabled = await IsCommandEnabledForChannel(channel);
                if (!isCommandEnabled)
                {
                    _logger.LogDebug($"Comando !g deshabilitado para el canal {channel}");
                    return;
                }

                var messageWithoutPrefix = message.StartsWith("!") ? message.Substring(1) : message;
                var args = messageWithoutPrefix.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                // Solo "!g" sin argumentos - mostrar categoría actual
                if (args.Length == 1)
                {
                    await ShowCurrentCategory(username, channel, messageSender);
                    return;
                }

                var subCommand = args[1].ToLower();

                switch (subCommand)
                {
                    case "help":
                        await ShowHelpMessage(channel, messageSender);
                        break;
                    case "list":
                        await ShowMicroCommandsList(channel, messageSender);
                        break;
                    case "set":
                        await HandleSetCommand(username, channel, args, messageSender);
                        break;
                    case "remove":
                    case "delete":
                        await HandleRemoveCommand(username, channel, args, messageSender);
                        break;
                    default:
                        // Si no es un subcomando, cambiar categoría directamente
                        await HandleDirectCategoryChange(username, channel, args, messageSender);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error en comando !g para {channel}");
                await messageSender.SendMessageAsync(channel, "Error al procesar el comando !g.");
            }
        }

        private async Task ShowCurrentCategory(string username, string channel, IMessageSender messageSender)
        {
            try
            {
                var userInfo = await Utils.GetUserInfoFromDatabaseAsync(_configuration, channel);
                if (userInfo == null)
                {
                    await messageSender.SendMessageAsync(channel, "Error: No se pudo obtener información del canal.");
                    return;
                }

                var currentCategory = await GameUtils.GetCurrentCategoryAsync(_configuration, userInfo.TwitchId, userInfo.AccessToken);
                if (!string.IsNullOrEmpty(currentCategory))
                {
                    await messageSender.SendMessageAsync(channel, $"Categoría actual: {currentCategory}");
                }
                else
                {
                    await messageSender.SendMessageAsync(channel, "No se pudo obtener la categoría actual del stream.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error mostrando categoría actual para {channel}");
                await messageSender.SendMessageAsync(channel, "Error al obtener la categoría actual.");
            }
        }

        private async Task ShowHelpMessage(string channel, IMessageSender messageSender)
        {
            var helpMessage = "Uso del comando !g: " +
                              "!g = categoría actual | " +
                              "!g [categoría] = cambiar categoría | " +
                              "!g help = esta ayuda | " +
                              "!g list = listar micro comandos | " +
                              "!g set [!comando] [categoría] = crear micro comando | " +
                              "!g remove [!comando] = eliminar micro comando";

            await messageSender.SendMessageAsync(channel, helpMessage);
        }

        private async Task ShowMicroCommandsList(string channel, IMessageSender messageSender)
        {
            try
            {
                var microCommands = await _dbContext.MicroGameCommands
                    .Where(mc => mc.ChannelName == channel)
                    .Take(10)
                    .Select(mc => mc.ShortCommand)
                    .ToListAsync();

                if (microCommands.Any())
                {
                    var commandsList = string.Join(", ", microCommands);
                    await messageSender.SendMessageAsync(channel, $"Micro comandos disponibles: {commandsList}");
                }
                else
                {
                    await messageSender.SendMessageAsync(channel, "No hay micro comandos configurados. Usa !g set [!comando] [categoría] para crear uno.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error listando micro comandos para {channel}");
                await messageSender.SendMessageAsync(channel, "Error al listar micro comandos.");
            }
        }

        private async Task HandleSetCommand(string username, string channel, string[] args, IMessageSender messageSender)
        {
            try
            {
                if (args.Length < 4)
                {
                    await messageSender.SendMessageAsync(channel, "Uso: !g set [!comando] [categoría]. Ejemplo: !g set !apex Apex Legends");
                    return;
                }

                var hasPermission = await GameUtils.HasPermissionToChangeCategoryAsync(_configuration, username, channel, _logger);
                if (!hasPermission)
                {
                    await messageSender.SendMessageAsync(channel, "Solo los moderadores o el propietario del canal pueden crear micro comandos.");
                    return;
                }

                var command = args[2];
                var category = string.Join(" ", args.Skip(3));

                if (!command.StartsWith("!"))
                {
                    command = "!" + command;
                }

                // Verificar palabras reservadas
                var reservedWords = new[] { "!g", "!game", "!set", "!remove", "!delete", "!list", "!help", "!title", "!t" };
                if (reservedWords.Contains(command.ToLower()))
                {
                    await messageSender.SendMessageAsync(channel, $"'{command}' es una palabra reservada y no se puede usar.");
                    return;
                }

                var validationResult = GameUtils.ValidateCategory(category);
                if (!validationResult.isValid)
                {
                    await messageSender.SendMessageAsync(channel, $"Error: {validationResult.errorMessage}");
                    return;
                }

                var existingCommand = await _dbContext.MicroGameCommands
                    .FirstOrDefaultAsync(mc => mc.ChannelName == channel && mc.ShortCommand == command);

                if (existingCommand != null)
                {
                    existingCommand.CategoryName = category;
                    existingCommand.UpdatedAt = DateTime.UtcNow;
                    await messageSender.SendMessageAsync(channel, $"Micro comando {command} actualizado a: {category}");
                }
                else
                {
                    var newCommand = new MicroGameCommands
                    {
                        ChannelName = channel,
                        ShortCommand = command,
                        CategoryName = category,
                        CreatedBy = username,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };

                    _dbContext.MicroGameCommands.Add(newCommand);
                    await messageSender.SendMessageAsync(channel, $"Micro comando {command} creado para: {category}");
                }

                await _dbContext.SaveChangesAsync();
                _logger.LogInformation($"Micro comando {command} configurado por {username} en {channel}: {category}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creando micro comando para {channel}");
                await messageSender.SendMessageAsync(channel, "Error al crear el micro comando.");
            }
        }

        private async Task HandleRemoveCommand(string username, string channel, string[] args, IMessageSender messageSender)
        {
            try
            {
                if (args.Length < 3)
                {
                    await messageSender.SendMessageAsync(channel, "Uso: !g remove [!comando]. Ejemplo: !g remove !apex");
                    return;
                }

                var hasPermission = await GameUtils.HasPermissionToChangeCategoryAsync(_configuration, username, channel, _logger);
                if (!hasPermission)
                {
                    await messageSender.SendMessageAsync(channel, "Solo los moderadores o el propietario del canal pueden eliminar micro comandos.");
                    return;
                }

                var command = args[2];
                if (!command.StartsWith("!"))
                {
                    command = "!" + command;
                }

                var microCommand = await _dbContext.MicroGameCommands
                    .FirstOrDefaultAsync(mc => mc.ChannelName == channel && mc.ShortCommand == command);

                if (microCommand == null)
                {
                    await messageSender.SendMessageAsync(channel, $"Micro comando {command} no encontrado.");
                    return;
                }

                _dbContext.MicroGameCommands.Remove(microCommand);
                await _dbContext.SaveChangesAsync();

                await messageSender.SendMessageAsync(channel, $"Micro comando {command} eliminado.");
                _logger.LogInformation($"Micro comando {command} eliminado por {username} en {channel}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error eliminando micro comando para {channel}");
                await messageSender.SendMessageAsync(channel, "Error al eliminar el micro comando.");
            }
        }

        private async Task HandleDirectCategoryChange(string username, string channel, string[] args, IMessageSender messageSender)
        {
            try
            {
                var hasPermission = await GameUtils.HasPermissionToChangeCategoryAsync(_configuration, username, channel, _logger);
                if (!hasPermission)
                {
                    await messageSender.SendMessageAsync(channel, "Solo los moderadores o el propietario del canal pueden cambiar la categoría.");
                    return;
                }

                var category = string.Join(" ", args.Skip(1));
                var validationResult = GameUtils.ValidateCategory(category);
                if (!validationResult.isValid)
                {
                    await messageSender.SendMessageAsync(channel, $"Error: {validationResult.errorMessage}");
                    return;
                }

                var userInfo = await Utils.GetUserInfoFromDatabaseAsync(_configuration, channel);
                if (userInfo == null)
                {
                    await messageSender.SendMessageAsync(channel, "Error: No se pudo obtener información del canal.");
                    return;
                }

                var success = await GameUtils.UpdateCategoryAsync(_configuration, userInfo.TwitchId, category, userInfo.AccessToken);
                if (success)
                {
                    await messageSender.SendMessageAsync(channel, $"¡Categoría cambiada a: {category}!");
                    await GameUtils.SaveCategoryToHistoryAsync(_configuration, channel, category, username);
                    _logger.LogInformation($"Categoría cambiada por {username} en {channel} usando !g: {category}");
                }
                else
                {
                    await messageSender.SendMessageAsync(channel, "Error al cambiar la categoría.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error cambiando categoría directamente para {channel}");
                await messageSender.SendMessageAsync(channel, "Error al cambiar la categoría.");
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
                _logger.LogError(ex, $"Error verificando si comando !g está habilitado para {channelLogin}");
                return true;
            }
        }
    }
}