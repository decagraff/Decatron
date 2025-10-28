// UserFunction.cs adaptado
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
using Newtonsoft.Json.Linq;
using Decatron.Core.Helpers;

namespace Decatron.Core.Functions
{
    public class UserFunction
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly string _clientId;
        private string _accessToken;

        public UserFunction(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClient = new HttpClient();

            // Obtener ClientId desde configuración
            _clientId = _configuration["TwitchSettings:ClientId"];

            if (string.IsNullOrEmpty(_clientId))
            {
                throw new InvalidOperationException("TwitchSettings:ClientId no está configurado");
            }
        }

        public string Execute(string userName, string[] args)
        {
            if (args != null && args.Length > 0 && args[0].ToLower() == "touser")
            {
                if (args.Length > 1)
                {
                    return args[1];
                }
                return userName;
            }
            return userName;
        }

        public async Task<string> GetRandomUser(string channelName)
        {
            try
            {
                // Usar el token del bot desde Utils
                _accessToken ??= await Utils.GetBotAccessToken(_configuration);

                if (string.IsNullOrEmpty(_accessToken))
                {
                    return "Error al obtener token de autenticación";
                }

                Utils.ConfigureTwitchApiHeaders(_httpClient, _configuration, _accessToken);

                var broadcasterId = await Utils.GetBroadcasterIdFromDatabaseAsync(_configuration, channelName);
                if (string.IsNullOrEmpty(broadcasterId))
                {
                    return "Error al obtener ID del canal";
                }

                // Obtener ID del bot moderador
                int modId = 880616625; // Hardcoded por ahora, podrías moverlo a configuración
                var response = await _httpClient.GetAsync($"https://api.twitch.tv/helix/chat/chatters?broadcaster_id={broadcasterId}&moderator_id={modId}");
                var content = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        _accessToken = null;
                        return await GetRandomUser(channelName);
                    }
                    return "Error al obtener lista de usuarios";
                }

                var json = JObject.Parse(content);
                var chatters = json["data"] as JArray;

                if (chatters == null || chatters.Count == 0)
                {
                    return "No hay usuarios activos en el chat";
                }

                var random = new Random();
                var randomChatter = chatters[random.Next(chatters.Count)];
                return randomChatter["user_login"].ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GetRandomUser: {ex.Message}");
                return "Error al seleccionar usuario aleatorio";
            }
        }
    }
}
