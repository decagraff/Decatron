using Decatron.Core.Interfaces;
using Decatron.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Decatron.Default.Commands
{
    public class TCommand : ICommand
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<TCommand> _logger;
        private readonly ICommandStateService _commandStateService;

        public string Name => "t";
        public string Description => "Alias de !title - Cambia o consulta el título del stream";

        public TCommand(IConfiguration configuration, ILogger<TCommand> logger, ICommandStateService commandStateService)
        {
            _configuration = configuration;
            _logger = logger;
            _commandStateService = commandStateService;
        }

        public async Task ExecuteAsync(string username, string channel, string message, IMessageSender messageSender)
        {
            try
            {
                _logger.LogInformation($"Ejecutando comando !t (alias de !title) por {username} en {channel}");

                // Convertir comando !t a formato !title para delegar
                var titleMessage = message.Replace("!t", "!title");
                if (message.StartsWith("t "))
                {
                    titleMessage = message.Replace("t ", "title ");
                }

                // Crear instancia de TitleCommand para delegar
                var titleCommand = new TitleCommand(_configuration,
                    Microsoft.Extensions.Logging.LoggerFactory.Create(b => b.AddConsole()).CreateLogger<TitleCommand>(),
                    _commandStateService);

                // Delegar al comando TitleCommand
                await titleCommand.ExecuteAsync(username, channel, titleMessage, messageSender);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error en comando !t para {channel} por {username}");
                await messageSender.SendMessageAsync(channel, "Error al procesar el comando.");
            }
        }
    }
}