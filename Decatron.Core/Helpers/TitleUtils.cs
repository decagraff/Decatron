using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text;
using Decatron.Core.Helpers;

namespace Decatron.Default.Helpers
{
    /// <summary>
    /// Utilidades específicas para el manejo de títulos de stream
    /// </summary>
    public static class TitleUtils
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        /// <summary>
        /// Obtiene el título actual del stream desde la API de Twitch
        /// </summary>
        public static async Task<string> GetCurrentTitleAsync(IConfiguration configuration, string broadcasterId, string accessToken)
        {
            try
            {
                var apiUrl = $"https://api.twitch.tv/helix/channels?broadcaster_id={broadcasterId}";

                Utils.ConfigureTwitchApiHeaders(_httpClient, configuration, accessToken);

                var response = await _httpClient.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var apiResponse = JsonConvert.DeserializeObject<dynamic>(jsonResponse);

                    if (apiResponse?.data?.Count > 0)
                    {
                        return apiResponse.data[0].title?.ToString();
                    }
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Actualiza el título del stream usando la API de Twitch
        /// </summary>
        public static async Task<bool> UpdateTitleAsync(IConfiguration configuration, string broadcasterId, string newTitle, string accessToken)
        {
            try
            {
                var apiUrl = $"https://api.twitch.tv/helix/channels?broadcaster_id={broadcasterId}";

                Utils.ConfigureTwitchApiHeaders(_httpClient, configuration, accessToken);

                var requestData = new { title = newTitle };
                var jsonData = JsonConvert.SerializeObject(requestData);
                var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

                var response = await _httpClient.PatchAsync(apiUrl, content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Valida que el título no sea demasiado largo o contenga caracteres no válidos
        /// </summary>
        public static (bool IsValid, string ErrorMessage) ValidateTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return (false, "El título no puede estar vacío.");
            }

            if (title.Length > 140)
            {
                return (false, "El título no puede tener más de 140 caracteres.");
            }

            // Verificar caracteres problemáticos
            var forbiddenChars = new[] { '\n', '\r', '\t' };
            if (title.Any(c => forbiddenChars.Contains(c)))
            {
                return (false, "El título contiene caracteres no válidos.");
            }

            return (true, string.Empty);
        }

        /// <summary>
        /// Guarda el título en la base de datos para historial (opcional)
        /// </summary>
        public static async Task<bool> SaveTitleToHistoryAsync(IConfiguration configuration, string channelLogin, string title, string changedBy)
        {
            try
            {
                var connectionString = configuration.GetConnectionString("DefaultConnection");
                using var connection = new MySql.Data.MySqlClient.MySqlConnection(connectionString);
                await connection.OpenAsync();

                const string query = @"
                    INSERT INTO title_history (channel_login, title, changed_by, changed_at)
                    VALUES (@channelLogin, @title, @changedBy, @changedAt)";

                using var command = new MySql.Data.MySqlClient.MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@channelLogin", channelLogin);
                command.Parameters.AddWithValue("@title", title);
                command.Parameters.AddWithValue("@changedBy", changedBy);
                command.Parameters.AddWithValue("@changedAt", DateTime.UtcNow);

                var result = await command.ExecuteNonQueryAsync();
                return result > 0;
            }
            catch (Exception)
            {
                // Si la tabla no existe, no es crítico
                return false;
            }
        }
       
        /// <summary>
        /// Formatea el mensaje de respuesta cuando se cambia el título
        /// </summary>
        public static string FormatTitleChangedMessage(string newTitle, string username)
        {
            return $"¡Título del stream cambiado a: {newTitle}!";
        }      

        /// <summary>
        /// Verifica si un usuario tiene permisos para cambiar el título del stream
        /// Solo el streamer (propietario del canal) puede cambiar el título
        /// </summary>
        public static async Task<bool> HasPermissionToChangeTitleAsync(IConfiguration configuration, string username, string channel, ILogger logger)
        {
            try
            {
                // Verificar si es el propietario del canal (streamer)
                if (string.Equals(username, channel, StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogDebug($"Usuario {username} es el propietario del canal {channel}");
                    return true;
                }

                // Verificar si es moderador del canal via Twitch API
                var isModerator = await IsUserModeratorAsync(configuration, username, channel, logger);
                if (isModerator)
                {
                    logger.LogDebug($"Usuario {username} es moderador en el canal {channel}");
                    return true;
                }

                logger.LogDebug($"Usuario {username} no tiene permisos para cambiar título en {channel}");
                return false;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error verificando permisos de {username} en {channel}");
                // En caso de error, denegar permisos por seguridad
                return false;
            }
        }

        /// <summary>
        /// Verifica si un usuario es moderador del canal via Twitch API
        /// </summary>
        private static async Task<bool> IsUserModeratorAsync(IConfiguration configuration, string username, string channel, ILogger logger)
        {
            try
            {
                // Obtener información del canal
                var channelInfo = await Utils.GetUserInfoFromDatabaseAsync(configuration, channel);
                if (channelInfo == null)
                {
                    logger.LogWarning($"No se encontró información para el canal {channel}");
                    return false;
                }

                // Obtener información del usuario que ejecuta el comando
                var userInfo = await Utils.GetUserInfoFromDatabaseAsync(configuration, username);
                if (userInfo == null)
                {
                    logger.LogDebug($"Usuario {username} no encontrado en la base de datos");
                    return false;
                }

                // Llamar a Twitch API para verificar si es moderador
                var twitchApiUrl = $"https://api.twitch.tv/helix/moderation/moderators?broadcaster_id={channelInfo.TwitchId}&user_id={userInfo.TwitchId}";

                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {channelInfo.AccessToken}");
                httpClient.DefaultRequestHeaders.Add("Client-Id", configuration["TwitchSettings:ClientId"]);

                var response = await httpClient.GetAsync(twitchApiUrl);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var moderatorData = System.Text.Json.JsonSerializer.Deserialize<TwitchModeratorsResponse>(jsonResponse);

                    // Si hay datos en la respuesta, el usuario es moderador
                    var isModerator = moderatorData?.Data?.Length > 0;
                    logger.LogDebug($"Verificación de moderador para {username} en {channel}: {isModerator}");
                    return isModerator;
                }
                else
                {
                    logger.LogWarning($"Error consultando API de Twitch para verificar moderador: {response.StatusCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error verificando si {username} es moderador de {channel}");
                return false;
            }
        }

        // Clases para deserializar respuesta de Twitch API
        public class TwitchModeratorsResponse
        {
            public TwitchModeratorData[]? Data { get; set; }
        }

        public class TwitchModeratorData
        {
            public string UserId { get; set; } = "";
            public string UserLogin { get; set; } = "";
            public string UserName { get; set; } = "";
        }

        /// <summary>
        /// Formatea el mensaje de respuesta cuando se consulta el título actual
        /// </summary>
        public static string FormatCurrentTitleMessage(string currentTitle)
        {
            return $"El título actual del stream es: {currentTitle}";
        }



    }
}