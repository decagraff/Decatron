using Decatron.Services;

namespace Decatron.Core.Interfaces
{
    /// <summary>
    /// Interface para comandos de chat del bot
    /// </summary>
    public interface ICommand
    {
        /// <summary>
        /// Nombre del comando (sin !)
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Descripción del comando
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Ejecuta el comando
        /// </summary>
        /// <param name="username">Usuario que ejecutó el comando</param>
        /// <param name="channel">Canal donde se ejecutó</param>
        /// <param name="message">Mensaje completo</param>
        /// <param name="messageSender">Servicio para enviar respuestas</param>
        Task ExecuteAsync(string username, string channel, string message, IMessageSender messageSender);
    }
}