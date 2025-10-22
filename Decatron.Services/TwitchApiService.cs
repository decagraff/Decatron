using System;
using System.Net.Http;
using System.Threading.Tasks;
using Decatron.Core.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Decatron.Services
{
    /// <summary>
    /// Stub de TwitchApiService
    /// Si tienes una versión anterior, reemplaza este archivo con la tuya
    /// </summary>
    public class TwitchApiService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<TwitchApiService> _logger;
        private readonly TwitchSettings _twitchSettings;

        public TwitchApiService(
            HttpClient httpClient,
            ILogger<TwitchApiService> logger,
            IOptions<TwitchSettings> twitchSettings)
        {
            _httpClient = httpClient;
            _logger = logger;
            _twitchSettings = twitchSettings.Value;
        }

        /// <summary>
        /// Ejemplo: Obtener información de usuario
        /// Reemplaza con tus métodos reales
        /// </summary>
        public async Task<string> GetUserInfo(string userId)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.twitch.tv/helix/users?id={userId}");
                request.Headers.Add("Client-ID", _twitchSettings.ClientId);
                request.Headers.Add("Authorization", $"Bearer {_twitchSettings.BotToken}");

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }
                else
                {
                    _logger.LogWarning($"Error en GetUserInfo: {response.StatusCode}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en GetUserInfo");
                return null;
            }
        }

        // Agrega aquí tus otros métodos de API
    }
}