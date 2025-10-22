using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using System.Text;
using Decatron.Core.Helpers;

namespace Decatron.Default.Helpers
{
    public static class GameUtils
    {
        /// <summary>
        /// Obtiene la categoría actual del stream desde Twitch API
        /// </summary>
        public static async Task<string?> GetCurrentCategoryAsync(IConfiguration configuration, string twitchId, string accessToken)
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("Client-ID", configuration["TwitchSettings:ClientId"]);
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

                var response = await httpClient.GetAsync($"https://api.twitch.tv/helix/channels?broadcaster_id={twitchId}");

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var channelData = JsonConvert.DeserializeObject<TwitchChannelResponse>(jsonResponse);

                    return channelData?.Data?.FirstOrDefault()?.GameName;
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Actualiza la categoría del stream via Twitch API
        /// </summary>
        public static async Task<bool> UpdateCategoryAsync(IConfiguration configuration, string twitchId, string categoryName, string accessToken)
        {
            try
            {
                // Primero buscar el game_id
                var gameInfo = await GetGameInfoAsync(configuration, categoryName, accessToken);
                if (gameInfo == null)
                {
                    return false;
                }

                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("Client-ID", configuration["TwitchSettings:ClientId"]);
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

                var updateData = new { game_id = gameInfo.Id };
                var jsonData = JsonConvert.SerializeObject(updateData);
                var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

                var response = await httpClient.PatchAsync($"https://api.twitch.tv/helix/channels?broadcaster_id={twitchId}", content);

                return response.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Obtiene información de un juego desde Twitch API
        /// </summary>
        public static async Task<GameInfo?> GetGameInfoAsync(IConfiguration configuration, string gameName, string accessToken)
        {
            try
            {
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("Client-ID", configuration["TwitchSettings:ClientId"]);
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

                var encodedName = Uri.EscapeDataString(gameName);
                var response = await httpClient.GetAsync($"https://api.twitch.tv/helix/games?name={encodedName}");

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var gameData = JsonConvert.DeserializeObject<TwitchGameResponse>(jsonResponse);

                    var game = gameData?.Data?.FirstOrDefault();
                    if (game != null)
                    {
                        return new GameInfo
                        {
                            Id = game.Id,
                            Name = game.Name
                        };
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
        /// Valida el nombre de una categoría
        /// </summary>
        public static (bool isValid, string errorMessage) ValidateCategory(string categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                return (false, "El nombre de la categoría no puede estar vacío");
            }

            if (categoryName.Length > 255)
            {
                return (false, "El nombre de la categoría es demasiado largo (máximo 255 caracteres)");
            }

            if (categoryName.Trim() != categoryName)
            {
                return (false, "El nombre de la categoría no puede empezar o terminar con espacios");
            }

            return (true, "");
        }

        /// <summary>
        /// Guarda un cambio de categoría en el historial
        /// </summary>
        public static async Task SaveCategoryToHistoryAsync(IConfiguration configuration, string channelLogin, string categoryName, string changedBy)
        {
            try
            {
                var connectionString = configuration.GetConnectionString("DefaultConnection");
                using var connection = new MySqlConnection(connectionString);
                await connection.OpenAsync();

                const string query = @"
                    INSERT INTO game_history (channel_login, category_name, changed_by, changed_at)
                    VALUES (@channelLogin, @categoryName, @changedBy, @changedAt)";

                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@channelLogin", channelLogin);
                command.Parameters.AddWithValue("@categoryName", categoryName);
                command.Parameters.AddWithValue("@changedBy", changedBy);
                command.Parameters.AddWithValue("@changedAt", DateTime.UtcNow);

                await command.ExecuteNonQueryAsync();
            }
            catch (Exception)
            {
                // Log error pero no fallar el proceso principal
            }
        }

        /// <summary>
        /// Busca una categoría en la base de datos local
        /// </summary>
        public static async Task<string?> SearchCategoryInDatabaseAsync(IConfiguration configuration, string searchTerm)
        {
            try
            {
                var connectionString = configuration.GetConnectionString("DefaultConnection");
                using var connection = new MySqlConnection(connectionString);
                await connection.OpenAsync();

                const string query = @"
                    SELECT name
                    FROM categories
                    WHERE 
                        -- Coincidencia por iniciales
                        (
                            SELECT CONCAT(
                                SUBSTRING(SUBSTRING_INDEX(name, ' ', 1), 1, 1),
                                IFNULL(SUBSTRING(SUBSTRING_INDEX(SUBSTRING_INDEX(name, ' ', 2), ' ', -1), 1, 1), ''),
                                IFNULL(SUBSTRING(SUBSTRING_INDEX(SUBSTRING_INDEX(name, ' ', 3), ' ', -1), 1, 1), '')
                            )
                        ) COLLATE utf8mb4_unicode_ci LIKE CONCAT(@searchTerm, '%')
                        OR
                        -- Coincidencia al inicio de cada palabra
                        name COLLATE utf8mb4_unicode_ci LIKE CONCAT(@searchTerm, '%')
                        OR name COLLATE utf8mb4_unicode_ci LIKE CONCAT('% ', @searchTerm, '%')
                        OR
                        -- Coincidencia en cualquier parte del nombre
                        name COLLATE utf8mb4_unicode_ci LIKE CONCAT('%', @searchTerm, '%')
                    ORDER BY 
                        -- Prioridad primero (1 antes que 0)
                        priority DESC,
                        -- Luego por tipo de coincidencia
                        CASE 
                            WHEN (
                                SELECT CONCAT(
                                    SUBSTRING(SUBSTRING_INDEX(name, ' ', 1), 1, 1),
                                    IFNULL(SUBSTRING(SUBSTRING_INDEX(SUBSTRING_INDEX(name, ' ', 2), ' ', -1), 1, 1), ''),
                                    IFNULL(SUBSTRING(SUBSTRING_INDEX(SUBSTRING_INDEX(name, ' ', 3), ' ', -1), 1, 1), '')
                                )
                            ) COLLATE utf8mb4_unicode_ci LIKE CONCAT(@searchTerm, '%') THEN 1
                            WHEN name COLLATE utf8mb4_unicode_ci LIKE CONCAT(@searchTerm, '%') THEN 2
                            WHEN name COLLATE utf8mb4_unicode_ci LIKE CONCAT('% ', @searchTerm, '%') THEN 3
                            ELSE 4
                        END,
                        -- Finalmente por nombre
                        name
                    LIMIT 1";

                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@searchTerm", searchTerm);

                var result = await command.ExecuteScalarAsync();
                return result?.ToString();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Verifica si un usuario tiene permisos para cambiar categorías
        /// </summary>
        public static async Task<bool> HasPermissionToChangeCategoryAsync(IConfiguration configuration, string username, string channel, ILogger logger)
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

                logger.LogDebug($"Usuario {username} no tiene permisos para cambiar categoría en {channel}");
                return false;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Error verificando permisos de {username} en {channel}");
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
                    var moderatorData = JsonConvert.DeserializeObject<TwitchModeratorsResponse>(jsonResponse);

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

        /// <summary>
        /// Formatea el mensaje de respuesta para categoría actual
        /// </summary>
        public static string FormatCurrentCategoryMessage(string categoryName)
        {
            return $"La categoría actual del canal es: {categoryName}";
        }

        /// <summary>
        /// Formatea el mensaje de respuesta para categoría cambiada
        /// </summary>
        public static string FormatCategoryChangedMessage(string categoryName, string username)
        {
            return $"¡Categoría del stream cambiada a: {categoryName}!";
        }
    }

    // Clases para deserializar respuestas de Twitch API
    public class TwitchChannelResponse
    {
        public TwitchChannelData[]? Data { get; set; }
    }

    public class TwitchChannelData
    {
        [JsonProperty("broadcaster_id")]
        public string BroadcasterId { get; set; } = "";

        [JsonProperty("broadcaster_login")]
        public string BroadcasterLogin { get; set; } = "";

        [JsonProperty("broadcaster_name")]
        public string BroadcasterName { get; set; } = "";

        [JsonProperty("game_name")]
        public string GameName { get; set; } = "";

        [JsonProperty("game_id")]
        public string GameId { get; set; } = "";

        [JsonProperty("title")]
        public string Title { get; set; } = "";
    }

    public class TwitchGameResponse
    {
        public TwitchGameData[]? Data { get; set; }
    }

    public class TwitchGameData
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
    }

    public class TwitchModeratorsResponse
    {
        public TwitchModeratorData[]? Data { get; set; }
    }

    public class TwitchModeratorData
    {
        [JsonProperty("user_id")]
        public string UserId { get; set; } = "";

        [JsonProperty("user_login")]
        public string UserLogin { get; set; } = "";

        [JsonProperty("user_name")]
        public string UserName { get; set; } = "";
    }

    public class GameInfo
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
    }
}