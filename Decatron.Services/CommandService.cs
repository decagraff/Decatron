using Decatron.Core.Interfaces;
using Decatron.Default.Commands;
using Decatron.Services;
using Decatron.Data;
using Decatron.Default.Helpers;
using Decatron.Core.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace Decatron.Services
{
    public class CommandService
    {
        private readonly ILogger<CommandService> _logger;
        private readonly IMessageSender _messageSender;
        private readonly IConfiguration _configuration;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ICommandStateService _commandStateService;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly Dictionary<string, ICommand> _commands;
        private readonly Dictionary<string, Dictionary<string, string>> _microCommandsCache;

        public CommandService(
            ILogger<CommandService> logger,
            IMessageSender messageSender,
            IConfiguration configuration,
            ILoggerFactory loggerFactory,
            ICommandStateService commandStateService,
            IServiceScopeFactory serviceScopeFactory)
        {
            _logger = logger;
            _messageSender = messageSender;
            _configuration = configuration;
            _loggerFactory = loggerFactory;
            _commandStateService = commandStateService;
            _serviceScopeFactory = serviceScopeFactory;
            _commands = new Dictionary<string, ICommand>();
            _microCommandsCache = new Dictionary<string, Dictionary<string, string>>();
            LoadCommands();
        }

        private void LoadCommands()
        {
            // Comandos básicos
            RegisterCommand(new Commands.HolaCommand());

            // Comandos por defecto
            RegisterCommand(new TitleCommand(_configuration, _loggerFactory.CreateLogger<TitleCommand>(), _commandStateService));
            RegisterCommand(new TCommand(_configuration, _loggerFactory.CreateLogger<TCommand>(), _commandStateService));
            RegisterCommand(new GameCommand(_configuration, _loggerFactory.CreateLogger<GameCommand>(), _commandStateService));

            // Cargar micro comandos asíncronamente
            _ = Task.Run(LoadMicroCommandsAsync);

            _logger.LogInformation($"Comandos base cargados: {_commands.Count}");
            foreach (var cmd in _commands.Keys)
            {
                _logger.LogDebug($"  - !{cmd}");
            }
        }

        private async Task LoadMicroCommandsAsync()
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<DecatronDbContext>();

                var microCommands = await dbContext.MicroGameCommands
                    .Select(mc => new { mc.ChannelName, mc.ShortCommand, mc.CategoryName })
                    .ToListAsync();

                foreach (var mc in microCommands)
                {
                    await RegisterMicroCommand(mc.ChannelName, mc.ShortCommand, mc.CategoryName);
                }

                _logger.LogInformation($"Micro comandos cargados: {microCommands.Count}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cargando micro comandos");
            }
        }

        private async Task RegisterMicroCommand(string channelName, string commandName, string categoryName)
        {
            try
            {
                var normalizedCommand = commandName.StartsWith("!") ? commandName.Substring(1) : commandName;

                if (!_microCommandsCache.ContainsKey(channelName))
                {
                    _microCommandsCache[channelName] = new Dictionary<string, string>();
                }
                _microCommandsCache[channelName][normalizedCommand] = categoryName;

                _logger.LogDebug($"Micro comando registrado: {commandName} -> {categoryName} para canal {channelName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error registrando micro comando {commandName}");
            }
        }

        private void RegisterCommand(ICommand command)
        {
            _commands[command.Name.ToLower()] = command;
        }

        public async Task ProcessMessageAsync(string username, string channel, string chatMessage)
        {
            try
            {
                if (!chatMessage.StartsWith("!"))
                    return;

                var parts = chatMessage.Substring(1).Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0)
                    return;

                var commandName = parts[0].ToLower();

                // IMPORTANTE: Manejar comando !g especialmente
                if (commandName == "g")
                {
                    _logger.LogInformation($"Ejecutando comando !g por {username} en {channel}");
                    await ProcessGCommand(username, channel, chatMessage);
                    return;
                }

                // Verificar comandos estándar
                if (_commands.TryGetValue(commandName, out var command))
                {
                    _logger.LogInformation($"Ejecutando comando !{commandName} por {username} en {channel}");
                    await command.ExecuteAsync(username, channel, chatMessage, _messageSender);
                    return;
                }

                // Verificar micro comandos
                if (_microCommandsCache.ContainsKey(channel) &&
                    _microCommandsCache[channel].ContainsKey(commandName))
                {
                    await ProcessMicroCommand(username, channel, commandName, _microCommandsCache[channel][commandName]);
                    return;
                }

                _logger.LogDebug($"Comando desconocido: !{commandName} por {username} en {channel}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error procesando comando de {username} en {channel}: {chatMessage}");
            }
        }

        private async Task ProcessGCommand(string username, string channel, string chatMessage)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<DecatronDbContext>();

                var gCommand = new GCommand(
                    _configuration,
                    _loggerFactory.CreateLogger<GCommand>(),
                    _commandStateService,
                    dbContext);

                await gCommand.ExecuteAsync(username, channel, chatMessage, _messageSender);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error procesando comando !g de {username} en {channel}: {chatMessage}");
            }
        }

        private async Task ProcessMicroCommand(string username, string channel, string commandName, string categoryName)
        {
            try
            {
                _logger.LogInformation($"Ejecutando micro comando !{commandName} por {username} en {channel}");

                var isBotEnabled = await Utils.IsBotEnabledForChannelAsync(_configuration, channel);
                if (!isBotEnabled)
                {
                    return;
                }

                var userInfo = await Utils.GetUserInfoFromDatabaseAsync(_configuration, channel);
                if (userInfo == null)
                {
                    await _messageSender.SendMessageAsync(channel, "Error: No se pudo obtener información del canal.");
                    return;
                }

                var isCommandEnabled = await _commandStateService.IsCommandEnabledAsync(userInfo.Id, "game");
                if (!isCommandEnabled)
                {
                    _logger.LogDebug($"Comando !game (micro comandos) deshabilitado para el canal {channel}");
                    return;
                }

                var hasPermission = await GameUtils.HasPermissionToChangeCategoryAsync(_configuration, username, channel, _logger);
                if (!hasPermission)
                {
                    await _messageSender.SendMessageAsync(channel, $"Solo los moderadores o el propietario del canal pueden usar !{commandName}.");
                    return;
                }

                var validationResult = GameUtils.ValidateCategory(categoryName);
                if (!validationResult.isValid)
                {
                    await _messageSender.SendMessageAsync(channel, $"Error: La categoría '{categoryName}' no es válida.");
                    return;
                }

                var success = await GameUtils.UpdateCategoryAsync(_configuration, userInfo.TwitchId, categoryName, userInfo.AccessToken);
                if (success)
                {
                    await _messageSender.SendMessageAsync(channel, $"¡Categoría cambiada a: {categoryName}! (usando !{commandName})");
                    await GameUtils.SaveCategoryToHistoryAsync(_configuration, channel, categoryName, username);
                    _logger.LogInformation($"Categoría cambiada por {username} en {channel} usando micro comando !{commandName}: {categoryName}");
                }
                else
                {
                    await _messageSender.SendMessageAsync(channel, $"Error al cambiar la categoría usando !{commandName}.");
                    _logger.LogWarning($"Error al cambiar categoría en {channel} por {username} con micro comando !{commandName}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error procesando micro comando !{commandName} para {channel}");
                await _messageSender.SendMessageAsync(channel, "Error al procesar el micro comando.");
            }
        }

        public async Task RefreshMicroCommandsForChannelAsync(string channelName)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<DecatronDbContext>();

                var microCommands = await dbContext.MicroGameCommands
                    .Where(mc => mc.ChannelName == channelName)
                    .Select(mc => new { mc.ShortCommand, mc.CategoryName })
                    .ToListAsync();

                if (_microCommandsCache.ContainsKey(channelName))
                {
                    _microCommandsCache[channelName].Clear();
                }
                else
                {
                    _microCommandsCache[channelName] = new Dictionary<string, string>();
                }

                foreach (var mc in microCommands)
                {
                    await RegisterMicroCommand(channelName, mc.ShortCommand, mc.CategoryName);
                }

                _logger.LogInformation($"Micro comandos refrescados para canal {channelName}: {microCommands.Count}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error refrescando micro comandos para canal {channelName}");
            }
        }

        public List<string> GetAvailableCommands()
        {
            var commands = _commands.Keys.ToList();
            commands.Add("g"); // Agregar !g manualmente
            return commands;
        }

        public List<string> GetAvailableMicroCommands(string channel)
        {
            if (_microCommandsCache.ContainsKey(channel))
            {
                return _microCommandsCache[channel].Keys.ToList();
            }
            return new List<string>();
        }

        public ICommand GetCommand(string name)
        {
            _commands.TryGetValue(name.ToLower(), out var command);
            return command;
        }
    }
}