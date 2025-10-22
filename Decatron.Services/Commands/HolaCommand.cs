using Decatron.Core.Interfaces;
using Decatron.Services;

namespace Decatron.Services.Commands
{
    /// <summary>
    /// Comando de ejemplo: !hola
    /// </summary>
    public class HolaCommand : ICommand
    {
        public string Name => "hola";
        public string Description => "Saluda al usuario";

        public async Task ExecuteAsync(string username, string channel, string message, IMessageSender messageSender)
        {
            var response = $"¡Hola @{username}! 👋 ¿Cómo estás?";
            await messageSender.SendMessageAsync(channel, response);
        }
    }
}