using System.Data;
using Decatron.Core.Functions;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Decatron.Core.Helpers
{
    public static class Utils
    {
        public static async Task<string> GetAccessTokenFromDatabase(IConfiguration configuration, string broadcasterName)
        {
            try
            {
                var connectionString = configuration.GetConnectionString("DefaultConnection");
                
                using (var connection = new MySqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    
                    var query = "SELECT access_token FROM bot_tokens WHERE broadcaster_name = @broadcasterName LIMIT 1";
                    
                    using (var command = new MySqlCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@broadcasterName", broadcasterName);
                        
                        var result = await command.ExecuteScalarAsync();
                        return result?.ToString() ?? string.Empty;
                    }
                }
            }
            catch
            {
                return string.Empty;
            }
        }
        /// <summary>
        /// Obtiene el access token de un canal desde la base de datos
        /// </summary>
        public static async Task<string> GetAccessTokenFromDatabaseAsync(IConfiguration configuration, string channelLogin)
        {
            try
            {
                var connectionString = configuration.GetConnectionString("DefaultConnection");
                using var connection = new MySqlConnection(connectionString);
                await connection.OpenAsync();

                const string query = @"
                    SELECT AccessToken 
                    FROM users 
                    WHERE Login = @channelLogin AND IsActive = 1 
                    LIMIT 1";

                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@channelLogin", channelLogin);

                var result = await command.ExecuteScalarAsync();
                return result?.ToString();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Obtiene el broadcaster ID (TwitchId) de un canal desde la base de datos
        /// </summary>
        public static async Task<string> GetBroadcasterIdFromDatabaseAsync(IConfiguration configuration, string channelLogin)
        {
            try
            {
                var connectionString = configuration.GetConnectionString("DefaultConnection");
                using var connection = new MySqlConnection(connectionString);
                await connection.OpenAsync();

                const string query = @"
                    SELECT TwitchId 
                    FROM users 
                    WHERE Login = @channelLogin AND IsActive = 1 
                    LIMIT 1";

                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@channelLogin", channelLogin);

                var result = await command.ExecuteScalarAsync();
                return result?.ToString();
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Obtiene información completa de usuario desde la base de datos
        /// </summary>
        public static async Task<UserInfo> GetUserInfoFromDatabaseAsync(IConfiguration configuration, string channelLogin)
        {
            try
            {
                var connectionString = configuration.GetConnectionString("DefaultConnection");
                using var connection = new MySqlConnection(connectionString);
                await connection.OpenAsync();

                const string query = @"
                    SELECT Id, TwitchId, Login, DisplayName, AccessToken, RefreshToken
                    FROM users 
                    WHERE Login = @channelLogin AND IsActive = 1 
                    LIMIT 1";

                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@channelLogin", channelLogin);

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    return new UserInfo
                    {
                        Id = reader.GetInt64("Id"),
                        TwitchId = reader.GetString("TwitchId"),
                        Login = reader.GetString("Login"),
                        DisplayName = reader.IsDBNull("DisplayName") ? null : reader.GetString("DisplayName"),
                        AccessToken = reader.GetString("AccessToken"),
                        RefreshToken = reader.IsDBNull("RefreshToken") ? null : reader.GetString("RefreshToken")
                    };
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Verifica si el usuario es propietario del canal
        /// </summary>
        public static bool IsChannelOwner(string username, string channelLogin)
        {
            return username.Equals(channelLogin, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Configura headers HTTP para requests a la API de Twitch
        /// </summary>
        public static void ConfigureTwitchApiHeaders(HttpClient httpClient, IConfiguration configuration, string accessToken)
        {
            var clientId = configuration["TwitchSettings:ClientId"];

            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("Client-ID", clientId);
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");
        }

        /// <summary>
        /// Verifica si el bot está habilitado para un canal específico
        /// </summary>
        public static async Task<bool> IsBotEnabledForChannelAsync(IConfiguration configuration, string channelLogin)
        {
            try
            {
                var connectionString = configuration.GetConnectionString("DefaultConnection");
                using var connection = new MySqlConnection(connectionString);
                await connection.OpenAsync();

                const string query = @"
                    SELECT COALESCE(s.BotEnabled, 1) as BotEnabled
                    FROM users u
                    LEFT JOIN system_settings s ON u.Id = s.UserId
                    WHERE u.Login = @channelLogin AND u.IsActive = 1
                    LIMIT 1";

                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@channelLogin", channelLogin);

                var result = await command.ExecuteScalarAsync();
                return result != null && Convert.ToBoolean(result);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Parsea argumentos de un mensaje de comando
        /// Ejemplo: "!title nuevo título aquí" ? ["nuevo", "título", "aquí"]
        /// </summary>
        public static string[] ParseCommandArguments(string message)
        {
            var parts = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length <= 1 ? Array.Empty<string>() : parts.Skip(1).ToArray();
        }

        /// <summary>
        /// Parsea argumentos de comando y los une en un string
        /// Ejemplo: "!title nuevo título aquí" ? "nuevo título aquí"
        /// </summary>
        public static string ParseCommandArgumentsAsString(string message)
        {
            var args = ParseCommandArguments(message);
            return args.Length > 0 ? string.Join(" ", args).Trim() : string.Empty;
        }


        /// <summary>
        /// Verifica si el usuario es owner o moderador del canal
        /// </summary>
        public static async Task<bool> IsOwnerOrModerator(IConfiguration configuration, string username, string channelName)
        {
            try
            {
                // Si el usuario es el mismo que el canal, es el owner
                if (IsChannelOwner(username, channelName))
                {
                    return true;
                }

                // Verificar si es moderador usando la API de Twitch
                var accessToken = await GetBotAccessToken(configuration);
                if (string.IsNullOrEmpty(accessToken))
                {
                    Console.WriteLine("No se pudo obtener el token del bot para verificar moderadores");
                    return false;
                }

                var clientId = configuration["TwitchSettings:ClientId"];
                var broadcasterId = await GetBroadcasterIdFromDatabaseAsync(configuration, channelName);

                if (string.IsNullOrEmpty(broadcasterId))
                {
                    return false;
                }

                using var httpClient = new HttpClient();
                ConfigureTwitchApiHeaders(httpClient, configuration, accessToken);

                var userId = await GetUserId(httpClient, username);
                if (string.IsNullOrEmpty(userId))
                {
                    return false;
                }

                var response = await httpClient.GetAsync($"https://api.twitch.tv/helix/moderation/moderators?broadcaster_id={broadcasterId}&user_id={userId}");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var data = JObject.Parse(json);
                    return data["data"]?.HasValues == true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error verificando permisos de moderador: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Obtiene el ID de usuario de Twitch
        /// </summary>
        public static async Task<string> GetUserId(HttpClient httpClient, string username)
        {
            try
            {
                var response = await httpClient.GetAsync($"https://api.twitch.tv/helix/users?login={username}");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    dynamic data = JsonConvert.DeserializeObject(json);
                    return data.data[0].id;
                }
                else
                {
                    Console.WriteLine($"Error fetching user ID. Status code: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception fetching user ID: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Obtiene el token del bot desde la base de datos
        /// </summary>
        public static async Task<string> GetBotAccessToken(IConfiguration configuration)
        {
            try
            {
                var connectionString = configuration.GetConnectionString("DefaultConnection");
                using var connection = new MySqlConnection(connectionString);
                await connection.OpenAsync();

                // Buscar en la tabla bot_tokens
                const string query = @"
            SELECT AccessToken 
            FROM bot_tokens 
            WHERE IsActive = 1 
            ORDER BY CreatedAt DESC 
            LIMIT 1";

                using var command = new MySqlCommand(query, connection);
                var result = await command.ExecuteScalarAsync();
                return result?.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo el token del bot: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Verifica si el usuario puede usar un comando según las restricciones
        /// </summary>
        public static async Task<bool> CanUseCommand(IConfiguration configuration, string username, string channelName, string restriction)
        {
            try
            {
                switch (restriction?.ToLower())
                {
                    case "all":
                        return true;
                    case "mod":
                        return await IsOwnerOrModerator(configuration, username, channelName);
                    case "vip":
                        return await IsVipOrHigher(configuration, username, channelName);
                    case "sub":
                        return await IsSubscriberOrHigher(configuration, username, channelName);
                    default:
                        return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error verificando permisos del comando: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Verifica si el usuario es VIP o tiene permisos superiores
        /// </summary>
        private static async Task<bool> IsVipOrHigher(IConfiguration configuration, string username, string channelName)
        {
            // Primero verificar si es mod o owner
            if (await IsOwnerOrModerator(configuration, username, channelName))
            {
                return true;
            }

            // Verificar VIP usando API de Twitch
            var accessToken = await GetBotAccessToken(configuration);
            var broadcasterId = await GetBroadcasterIdFromDatabaseAsync(configuration, channelName);

            if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(broadcasterId))
            {
                return false;
            }

            using var httpClient = new HttpClient();
            ConfigureTwitchApiHeaders(httpClient, configuration, accessToken);

            var userId = await GetUserId(httpClient, username);
            if (string.IsNullOrEmpty(userId))
            {
                return false;
            }

            var response = await httpClient.GetAsync($"https://api.twitch.tv/helix/channels/vips?broadcaster_id={broadcasterId}&user_id={userId}");

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var data = JObject.Parse(json);
                return data["data"]?.HasValues == true;
            }

            return false;
        }

        /// <summary>
        /// Verifica si el usuario es suscriptor o tiene permisos superiores
        /// </summary>
        private static async Task<bool> IsSubscriberOrHigher(IConfiguration configuration, string username, string channelName)
        {
            // Primero verificar permisos superiores
            if (await IsVipOrHigher(configuration, username, channelName))
            {
                return true;
            }

            // Verificar suscripción usando API de Twitch
            var accessToken = await GetBotAccessToken(configuration);
            var broadcasterId = await GetBroadcasterIdFromDatabaseAsync(configuration, channelName);

            if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(broadcasterId))
            {
                return false;
            }

            using var httpClient = new HttpClient();
            ConfigureTwitchApiHeaders(httpClient, configuration, accessToken);

            var userId = await GetUserId(httpClient, username);
            if (string.IsNullOrEmpty(userId))
            {
                return false;
            }

            var response = await httpClient.GetAsync($"https://api.twitch.tv/helix/subscriptions?broadcaster_id={broadcasterId}&user_id={userId}");

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var data = JObject.Parse(json);
                return data["data"]?.HasValues == true;
            }

            return false;
        }

        /// <summary>
        /// Procesa funciones personalizadas en el texto de respuesta
        /// </summary>
        public static async Task<string> ProcessCustomFunctions(string response, string channelName, string commandName, string username,
            UptimeFunction uptimeFunction, UserFunction userFunction, GameFunction gameFunction, string[] args)
        {
            if (string.IsNullOrEmpty(response))
                return response;

            try
            {
                var processedResponse = response;

                // Procesar $(uptime)
                if (processedResponse.Contains("$(uptime)"))
                {
                    var uptimeResult = await uptimeFunction.Execute(channelName);
                    processedResponse = processedResponse.Replace("$(uptime)", uptimeResult);
                }

                // Procesar $(game)
                if (processedResponse.Contains("$(game)"))
                {
                    var gameResult = await gameFunction.Execute(channelName);
                    processedResponse = processedResponse.Replace("$(game)", gameResult);
                }

                // Procesar $(user)
                if (processedResponse.Contains("$(user)"))
                {
                    var userResult = userFunction.Execute(username, args);
                    processedResponse = processedResponse.Replace("$(user)", userResult);
                }

                // Procesar $(channel)
                if (processedResponse.Contains("$(channel)"))
                {
                    processedResponse = processedResponse.Replace("$(channel)", channelName);
                }

                // Procesar $(count)
                if (processedResponse.Contains("$(count)"))
                {
                    var countResult = await CounterFunction.Execute(channelName, commandName, username, args);
                    processedResponse = processedResponse.Replace("$(count)", countResult);
                }

                return processedResponse;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error procesando funciones personalizadas: {ex.Message}");
                return response;
            }
        }



    }
    /// <summary>
    /// Clase auxiliar para información de usuario
    /// </summary>
    public class UserInfo
    {
        public long Id { get; set; }
        public string TwitchId { get; set; }
        public string Login { get; set; }
        public string DisplayName { get; set; }
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
    }
}