using System.Data;
using Microsoft.Extensions.Configuration;
using MySql.Data.MySqlClient;

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