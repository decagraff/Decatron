using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using TwitchLib.Client;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;
using MySql.Data.MySqlClient;
using System.Collections.Concurrent;
using System.Data;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Net.Http;
using Decatron.Core.Helpers;
using Decatron.Services;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;

namespace Decatron.Controllers
{
    [ApiController]
    [Route("api/twitch")]
    public class TwitchWebhookController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<TwitchWebhookController> _logger;
        private readonly TwitchClient _twitchClient;
        private readonly TwitchBotService _twitchBotService;
        private readonly string logFilePath = "logs/webhook_logs.txt";

        private static readonly ConcurrentDictionary<string, DateTime> _recentFollows = new ConcurrentDictionary<string, DateTime>();
        private static readonly TimeSpan _deduplicationPeriod = TimeSpan.FromMinutes(10);
        private static readonly ConcurrentDictionary<string, DateTime> _followCooldowns = new ConcurrentDictionary<string, DateTime>();

        // ✨ NUEVO: Cache para evitar procesar el mismo mensaje dos veces
        private static readonly ConcurrentDictionary<string, DateTime> _processedMessages = new ConcurrentDictionary<string, DateTime>();

        public TwitchWebhookController(IConfiguration configuration, ILogger<TwitchWebhookController> logger, TwitchClient twitchClient, TwitchBotService twitchBotService)
        {
            _configuration = configuration;
            _logger = logger;
            _twitchClient = twitchClient;
            _twitchBotService = twitchBotService;

            Directory.CreateDirectory("logs");
            if (!System.IO.File.Exists(logFilePath))
            {
                System.IO.File.WriteAllText(logFilePath, $"[{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}] Archivo de logs inicializado\n");
            }

            Task.Run(async () => {
                while (true)
                {
                    CleanupOldFollows();
                    CleanupOldMessages(); // ✨ NUEVO
                    await Task.Delay(TimeSpan.FromMinutes(15));
                }
            });
        }

        private void CleanupOldFollows()
        {
            var currentTime = DateTime.UtcNow;
            foreach (var follow in _recentFollows)
            {
                if (currentTime - follow.Value > _deduplicationPeriod)
                {
                    _recentFollows.TryRemove(follow.Key, out _);
                }
            }

            foreach (var cooldown in _followCooldowns)
            {
                if (currentTime - cooldown.Value > TimeSpan.FromDays(30))
                {
                    _followCooldowns.TryRemove(cooldown.Key, out _);
                }
            }
        }

        // ✨ NUEVO: Limpiar mensajes procesados (más de 5 minutos)
        private void CleanupOldMessages()
        {
            var currentTime = DateTime.UtcNow;
            foreach (var msg in _processedMessages)
            {
                if (currentTime - msg.Value > TimeSpan.FromMinutes(5))
                {
                    _processedMessages.TryRemove(msg.Key, out _);
                }
            }
        }

        private void LogToFile(string message)
        {
            try
            {
                if (message.Contains("ERROR") ||
                    message.Contains("ALERTA") ||
                    message.Contains("EVENTO FOLLOW") ||
                    message.Contains("EVENTO CHAT") || // ✨ NUEVO
                    message.Contains("REGISTRANDO SEGUIDOR") ||
                    message.Contains("COOLDOWN") ||
                    message.StartsWith("Webhook recibido") ||
                    message.Contains("desafío"))
                {
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    System.IO.File.AppendAllText(logFilePath, $"[{timestamp}] {message}\n");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error escribiendo en webhook_logs.txt: {ex.Message}");
            }
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> ManejadorWebhook()
        {
            LogToFile("Webhook recibido en /api/twitch/webhook - INICIO");
            _logger.LogInformation("Webhook recibido en /api/twitch/webhook - INICIO");

            string cuerpo;
            using (var lector = new StreamReader(Request.Body, Encoding.UTF8))
            {
                cuerpo = await lector.ReadToEndAsync();
            }
            LogToFile($"Cuerpo recibido: {cuerpo}");
            _logger.LogInformation($"Cuerpo recibido: {cuerpo}");

            var tipoMensaje = Request.Headers["Twitch-Eventsub-Message-Type"].ToString();
            var idMensaje = Request.Headers["Twitch-Eventsub-Message-Id"].ToString();
            var marcaTiempo = Request.Headers["Twitch-Eventsub-Message-Timestamp"].ToString();
            var encabezadoFirma = Request.Headers["Twitch-Eventsub-Message-Signature"].ToString();

            LogToFile($"Headers - Tipo: {tipoMensaje}, ID: {idMensaje}, Timestamp: {marcaTiempo}, Firma: {encabezadoFirma}");
            _logger.LogInformation($"Headers - Tipo: {tipoMensaje}, ID: {idMensaje}, Timestamp: {marcaTiempo}, Firma: {encabezadoFirma}");

            if (!VerificarFirma(idMensaje, marcaTiempo, cuerpo, encabezadoFirma))
            {
                LogToFile("Verificación de firma fallida");
                _logger.LogWarning("Verificación de firma fallida");
                return Unauthorized();
            }

            var json = JObject.Parse(cuerpo);

            switch (tipoMensaje)
            {
                case "webhook_callback_verification":
                    var desafio = json["challenge"].ToString();
                    LogToFile($"Respondiendo desafío: {desafio}");
                    _logger.LogInformation($"Respondiendo desafío: {desafio}");
                    return Content(desafio);

                case "notification":
                    LogToFile("Procesando notificación...");
                    _logger.LogInformation("Procesando notificación...");
                    await ManejarNotificacion(json);
                    return Ok();

                default:
                    LogToFile($"Tipo de mensaje desconocido: {tipoMensaje}");
                    _logger.LogWarning($"Tipo de mensaje desconocido: {tipoMensaje}");
                    return BadRequest();
            }
        }

        [HttpGet("download-logs")]
        public IActionResult DownloadWebhookLogs()
        {
            try
            {
                if (!System.IO.File.Exists(logFilePath))
                {
                    LogToFile("El archivo webhook_logs.txt no existe");
                    _logger.LogWarning("El archivo webhook_logs.txt no existe");
                    return NotFound("El archivo de logs no existe.");
                }

                var fileBytes = System.IO.File.ReadAllBytes(logFilePath);
                var contentType = "text/plain";
                var fileName = "webhook_logs.txt";

                LogToFile("Descargando webhook_logs.txt");
                _logger.LogInformation("Descargando webhook_logs.txt");

                return new FileContentResult(fileBytes, contentType)
                {
                    FileDownloadName = fileName
                };
            }
            catch (Exception ex)
            {
                LogToFile($"Error al descargar webhook_logs.txt: {ex.Message}");
                _logger.LogError($"Error al descargar webhook_logs.txt: {ex.Message}");
                return StatusCode(500, "Error al descargar el archivo de logs.");
            }
        }

        private bool VerificarFirma(string idMensaje, string marcaTiempo, string cuerpo, string encabezadoFirma)
        {
            var secreto = _configuration["TwitchSettings:WebhookSecret"];
            if (string.IsNullOrEmpty(secreto))
            {
                LogToFile("ERROR: WebhookSecret no configurado en appsettings.json");
                _logger.LogError("WebhookSecret no configurado en appsettings.json");
                return false;
            }
            var mensaje = idMensaje + marcaTiempo + cuerpo;
            using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secreto)))
            {
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(mensaje));
                var firmaCalculada = BitConverter.ToString(hash).Replace("-", "").ToLower();
                LogToFile($"Firma calculada: sha256={firmaCalculada}");
                _logger.LogInformation($"Firma calculada: sha256={firmaCalculada}");
                return encabezadoFirma == $"sha256={firmaCalculada}";
            }
        }

        private async Task ManejarNotificacion(JObject json)
        {
            var tipoEvento = json["subscription"]["type"].ToString();
            var datosEvento = json["event"] as JObject;

            LogToFile($"Notificación recibida - Tipo: {tipoEvento}");
            _logger.LogInformation($"Notificación recibida - Tipo: {tipoEvento}");

            if (tipoEvento == "channel.follow")
            {
                LogToFile("Detectado evento de follow, llamando a ManejarEventoSeguidor...");
                _logger.LogInformation("Detectado evento de follow, llamando a ManejarEventoSeguidor...");
                await ManejarEventoSeguidor(datosEvento);
            }
            else if (tipoEvento == "channel.chat.message") // ✨ NUEVO
            {
                LogToFile("Detectado evento de chat, llamando a ManejarEventoChat...");
                _logger.LogInformation("Detectado evento de chat, llamando a ManejarEventoChat...");
                await ManejarEventoChat(datosEvento);
            }
            else
            {
                LogToFile($"Evento no manejado: {tipoEvento}");
                _logger.LogWarning($"Evento no manejado: {tipoEvento}");
            }
        }

        // ✨ NUEVO: Procesar eventos de chat desde EventSub
        private async Task ManejarEventoChat(JObject datosEvento)
        {
            try
            {
                // Extraer datos del evento
                var messageId = datosEvento["message_id"]?.ToString();
                var broadcasterUserId = datosEvento["broadcaster_user_id"]?.ToString();
                var broadcasterUserName = datosEvento["broadcaster_user_name"]?.ToString();
                var chatterUserId = datosEvento["chatter_user_id"]?.ToString();
                var chatterUserName = datosEvento["chatter_user_name"]?.ToString();
                var messageText = datosEvento["message"]?["text"]?.ToString();

                // Validar datos mínimos
                if (string.IsNullOrEmpty(messageId) || string.IsNullOrEmpty(messageText) ||
                    string.IsNullOrEmpty(broadcasterUserName) || string.IsNullOrEmpty(chatterUserName))
                {
                    _logger.LogWarning("Evento de chat con datos incompletos");
                    return;
                }

                // Evitar procesar duplicados
                if (_processedMessages.ContainsKey(messageId))
                {
                    _logger.LogDebug($"[EventSub] Mensaje duplicado ignorado: {messageId}");
                    return;
                }
                _processedMessages[messageId] = DateTime.UtcNow;

                LogToFile($"EVENTO CHAT: [{broadcasterUserName}] {chatterUserName}: {messageText}");
                _logger.LogInformation($"[EventSub] Chat de {chatterUserName} en {broadcasterUserName}: {messageText}");

                // Crear objeto simulado compatible con TwitchLib para reutilizar lógica existente
                var chatMessage = new ChatMessage(
                    botUsername: _configuration["TwitchSettings:BotUsername"],
                    userId: chatterUserId,
                    userName: chatterUserName,
                    displayName: chatterUserName,
                    colorHex: null,
                    color: System.Drawing.Color.Empty,
                    emoteSet: new EmoteSet(new List<Emote>(), messageText),
                    message: messageText,
                    userType: TwitchLib.Client.Enums.UserType.Viewer,
                    channel: broadcasterUserName.ToLower(),
                    id: messageId,
                    isSubscriber: false,
                    subscribedMonthCount: 0,
                    roomId: broadcasterUserId,
                    isTurbo: false,
                    isModerator: false,
                    isMe: false,
                    isBroadcaster: false,
                    isVip: false,
                    isPartner: false,
                    isStaff: false,
                    noisy: TwitchLib.Client.Enums.Noisy.False,                    
                    rawIrcMessage: "",
                    emoteReplacedMessage: messageText,
                    badges: new List<KeyValuePair<string, string>>(),
                    cheerBadge: null,
                    bits: 0,
                    bitsInDollars: 0.0
                );

                var args = new OnMessageReceivedArgs
                {
                    ChatMessage = chatMessage
                };

                // Llamar al mismo procesador de comandos que usa IRC
                // NOTA: Esto requiere hacer público o accesible el método Client_OnMessageReceived
                // Por ahora, solo logeamos - en el siguiente paso conectaremos la lógica
                _logger.LogInformation($"[EventSub] Mensaje listo para procesar: {messageText}");

                // Procesar el comando usando la misma lógica que IRC
               // await _twitchBotService.ProcessMessageFromEventSub(args);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error procesando evento de chat desde EventSub");
            }
        }

        private async Task ManejarEventoSeguidor(JObject datosEvento)
        {
            var userId = datosEvento["user_id"].ToString();
            var userName = datosEvento["user_name"].ToString();
            var broadcasterId = datosEvento["broadcaster_user_id"].ToString();
            var broadcasterName = datosEvento["broadcaster_user_name"].ToString();
            var followedAt = DateTime.Parse(datosEvento["followed_at"].ToString(), null, System.Globalization.DateTimeStyles.AssumeUniversal);

            string followKey = $"{broadcasterId}:{userId}";

            LogToFile($"EVENTO FOLLOW: Usuario {userName} siguió a {broadcasterName}");
            Console.WriteLine($"EVENTO FOLLOW: Usuario {userName} siguió a {broadcasterName}");
            _logger.LogInformation($"Evento de follow - userId: {userId}, userName: {userName}, broadcasterId: {broadcasterId}, broadcasterName: {broadcasterName}");

            var settings = await GetFollowSettings(broadcasterId);

            bool shouldSendMessage = settings.Enabled;

            if (shouldSendMessage && settings.CooldownHours > 0)
            {
                if (_followCooldowns.TryGetValue(followKey, out DateTime lastFollowTime))
                {
                    TimeSpan timeSinceLastFollow = DateTime.UtcNow - lastFollowTime;
                    TimeSpan cooldownPeriod = TimeSpan.FromHours(settings.CooldownHours);

                    if (timeSinceLastFollow < cooldownPeriod)
                    {
                        LogToFile($"FOLLOW EN COOLDOWN: {userName} siguió a {broadcasterName} - Último follow hace {timeSinceLastFollow.TotalHours:F1}h, cooldown configurado: {settings.CooldownHours}h");
                        _logger.LogInformation($"Follow en cooldown: {userName} → {broadcasterName} ({timeSinceLastFollow.TotalHours:F1}h < {settings.CooldownHours}h)");
                        shouldSendMessage = false;
                    }
                }
            }

            if (shouldSendMessage && _recentFollows.TryGetValue(followKey, out DateTime lastTime))
            {
                if (DateTime.UtcNow - lastTime < TimeSpan.FromMinutes(1))
                {
                    LogToFile($"IGNORANDO FOLLOW DUPLICADO - {userName} siguió a {broadcasterName} - Recibido hace {(DateTime.UtcNow - lastTime).TotalSeconds} segundos");
                    _logger.LogInformation($"Ignorando follow duplicado: {userName} → {broadcasterName} (hace {(DateTime.UtcNow - lastTime).TotalSeconds}s)");
                    shouldSendMessage = false;
                }
            }

            _recentFollows[followKey] = DateTime.UtcNow;
            _followCooldowns[followKey] = DateTime.UtcNow;

            if (shouldSendMessage)
            {
                LogToFile($"Enviando mensaje al canal para {userName}...");

                string message = settings.MessageTemplate.Replace("{username}", userName);

                //await _twitchBotService.SendMessageAsync(broadcasterName, message);
                LogToFile($"Mensaje enviado al canal: {message}");
            }
            else if (!settings.Enabled)
            {
                LogToFile($"Mensajes de seguimiento desactivados para {broadcasterName}, no se envió mensaje a {userName}.");
                _logger.LogInformation($"Mensajes de seguimiento desactivados para {broadcasterName}, no se envió mensaje a {userName}.");
            }
            else
            {
                LogToFile($"No se envió mensaje para {userName} (en cooldown o duplicado).");
                _logger.LogInformation($"No se envió mensaje para {userName} (en cooldown o duplicado).");
            }

            LogToFile("A punto de registrar seguidor en la base de datos");
            await RegisterFollowerInDatabase(broadcasterId, broadcasterName, userId, userName, followedAt);
            LogToFile("Registro de seguidor completado");
        }

        private async Task<FollowSettings> GetFollowSettings(string broadcasterId)
        {
            try
            {
                using (var conn = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                {
                    await conn.OpenAsync();

                    string query = @"
                        SELECT enabled, cooldown_hours, message_template
                        FROM follow_message_settings
                        WHERE broadcaster_id = @broadcasterId";

                    using (var cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@broadcasterId", broadcasterId);

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            if (await reader.ReadAsync())
                            {
                                return new FollowSettings
                                {
                                    Enabled = reader.GetBoolean(0),
                                    CooldownHours = reader.GetInt32(1),
                                    MessageTemplate = reader.GetString(2)
                                };
                            }
                            else
                            {
                                await reader.CloseAsync();

                                await CreateDefaultFollowSettings(conn, broadcasterId);

                                return new FollowSettings
                                {
                                    Enabled = true,
                                    CooldownHours = 24,
                                    MessageTemplate = "¡Gracias por seguir el canal, @{username}!"
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al obtener configuración de follows para broadcaster {broadcasterId}");

                return new FollowSettings
                {
                    Enabled = true,
                    CooldownHours = 24,
                    MessageTemplate = "¡Gracias por seguir el canal, @{username}!"
                };
            }
        }

        private async Task CreateDefaultFollowSettings(MySqlConnection conn, string broadcasterId)
        {
            try
            {
                string query = @"
                    INSERT IGNORE INTO follow_message_settings 
                        (broadcaster_id, enabled, cooldown_hours, message_template) 
                    VALUES 
                        (@broadcasterId, TRUE, 24, '¡Gracias por seguir el canal, @{username}!')";

                using (var cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@broadcasterId", broadcasterId);
                    await cmd.ExecuteNonQueryAsync();

                    _logger.LogInformation($"Creada configuración predeterminada para broadcaster {broadcasterId}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al crear configuración predeterminada para broadcaster {broadcasterId}");
            }
        }

        private async Task<bool> IsNewFollowAsync(string broadcasterId, string userId)
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            using (var conn = new MySqlConnection(connectionString))
            {
                await conn.OpenAsync();
                string query = @"
                    SELECT COUNT(*) 
                    FROM channel_followers 
                    WHERE broadcaster_id = @broadcasterId 
                    AND user_id = @userId 
                    AND is_following = 0";

                using (var cmd = new MySqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@broadcasterId", broadcasterId);
                    cmd.Parameters.AddWithValue("@userId", userId);
                    var count = Convert.ToInt64(await cmd.ExecuteScalarAsync());
                    return count == 0;
                }
            }
        }

        private async Task<bool> AreFollowMessagesEnabled(string channelName)
        {
            try
            {
                using (var conn = new MySqlConnection(_configuration.GetConnectionString("DefaultConnection")))
                {
                    await conn.OpenAsync();
                    string query = "SELECT is_enabled FROM follow_settings WHERE channel_name = @channelName";
                    using (var cmd = new MySqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@channelName", channelName);
                        var result = await cmd.ExecuteScalarAsync();

                        if (result == null)
                        {
                            LogToFile($"No hay configuración para {channelName}, habilitando mensajes por defecto");
                            await InsertDefaultFollowSettings(conn, channelName);
                            return true;
                        }

                        bool isEnabled = Convert.ToBoolean(result);
                        LogToFile($"Mensajes de follow para {channelName}: {(isEnabled ? "habilitados" : "deshabilitados")}");
                        return isEnabled;
                    }
                }
            }
            catch (Exception ex)
            {
                LogToFile($"Error al verificar follow_settings para {channelName}: {ex.Message}");
                _logger.LogError(ex, $"Error al verificar follow_settings para {channelName}");
                return true;
            }
        }

        private async Task InsertDefaultFollowSettings(MySqlConnection conn, string channelName)
        {
            try
            {
                string insertQuery = "INSERT INTO follow_settings (channel_name, is_enabled) VALUES (@channelName, TRUE)";
                using (var cmd = new MySqlCommand(insertQuery, conn))
                {
                    cmd.Parameters.AddWithValue("@channelName", channelName);
                    await cmd.ExecuteNonQueryAsync();
                    LogToFile($"Configuración predeterminada creada para {channelName}: mensajes habilitados");
                }
            }
            catch (Exception ex)
            {
                LogToFile($"Error al insertar configuración predeterminada para {channelName}: {ex.Message}");
            }
        }

        private async Task RegisterFollowerInDatabase(string broadcasterId, string broadcasterName, string userId, string userName, DateTime followedAt)
        {
            LogToFile($"REGISTRANDO SEGUIDOR: {userName} para el canal {broadcasterName} en {followedAt}");
            _logger.LogInformation($"Registrando seguidor - broadcasterId: {broadcasterId}, userId: {userId}, userName: {userName}, followedAt: {followedAt}");

            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            if (string.IsNullOrEmpty(connectionString))
            {
                LogToFile("ERROR: Connection string está vacío");
                _logger.LogError("Connection string está vacío");
                return;
            }

            DateTime accountCreatedAt = await GetAccountCreatedAt(userId, broadcasterName);

            using (var conn = new MySqlConnection(connectionString))
            {
                await conn.OpenAsync();
                using (var transaction = await conn.BeginTransactionAsync())
                {
                    try
                    {
                        string checkQuery = @"
                            SELECT is_following, followed_at 
                            FROM channel_followers 
                            WHERE broadcaster_id = @broadcasterId AND user_id = @userId";

                        int? currentState = null;
                        DateTime? lastFollowedAt = null;

                        using (var cmd = new MySqlCommand(checkQuery, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@broadcasterId", broadcasterId);
                            cmd.Parameters.AddWithValue("@userId", userId);

                            using (var reader = await cmd.ExecuteReaderAsync())
                            {
                                if (await reader.ReadAsync())
                                {
                                    currentState = reader.GetInt32("is_following");
                                    lastFollowedAt = reader.IsDBNull(reader.GetOrdinal("followed_at"))
                                        ? (DateTime?)null
                                        : reader.GetDateTime("followed_at");
                                }
                            }
                        }

                        string upsertQuery;
                        if (currentState.HasValue)
                        {
                            if (currentState.Value == 1)
                            {
                                upsertQuery = @"
                                    UPDATE channel_followers
                                    SET is_following = 0, 
                                        unfollowed_at = NULL, 
                                        followed_at = @followedAt,
                                        user_name = @userName,
                                        account_created_at = @accountCreatedAt
                                    WHERE broadcaster_id = @broadcasterId AND user_id = @userId";
                                LogToFile($"Seguidor retornado: {userName} en {followedAt}");
                            }
                            else
                            {
                                upsertQuery = @"
                                    UPDATE channel_followers
                                    SET user_name = @userName,
                                        followed_at = @followedAt,
                                        account_created_at = @accountCreatedAt
                                    WHERE broadcaster_id = @broadcasterId AND user_id = @userId";
                                LogToFile($"Actualización de seguidor existente: {userName} en {followedAt}");
                            }
                        }
                        else
                        {
                            upsertQuery = @"
                                INSERT INTO channel_followers
                                    (broadcaster_id, broadcaster_name, user_id, user_name, followed_at, is_following, is_blocked, account_created_at)
                                VALUES
                                    (@broadcasterId, @broadcasterName, @userId, @userName, @followedAt, 0, 0, @accountCreatedAt)";
                            LogToFile($"Nuevo seguidor: {userName} en {followedAt}");
                        }

                        using (var cmd = new MySqlCommand(upsertQuery, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@broadcasterId", broadcasterId);
                            cmd.Parameters.AddWithValue("@broadcasterName", broadcasterName);
                            cmd.Parameters.AddWithValue("@userId", userId);
                            cmd.Parameters.AddWithValue("@userName", userName);
                            cmd.Parameters.AddWithValue("@followedAt", followedAt);
                            cmd.Parameters.AddWithValue("@accountCreatedAt", accountCreatedAt);
                            await cmd.ExecuteNonQueryAsync();
                        }

                        if (currentState == null || currentState.Value == 1)
                        {
                            string historyQuery = @"
                                INSERT INTO follower_history
                                    (broadcaster_id, user_id, action, action_timestamp)
                                VALUES
                                    (@broadcasterId, @userId, 0, @actionTimestamp)";

                            using (var cmd = new MySqlCommand(historyQuery, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@broadcasterId", broadcasterId);
                                cmd.Parameters.AddWithValue("@userId", userId);
                                cmd.Parameters.AddWithValue("@actionTimestamp", followedAt);
                                await cmd.ExecuteNonQueryAsync();
                            }
                            LogToFile($"Registrado evento follow en historial para: {userName} en {followedAt}");
                        }

                        await transaction.CommitAsync();
                        LogToFile($"Seguidor {userName} registrado con éxito en {followedAt}, account_created_at: {accountCreatedAt}");
                        _logger.LogInformation($"Seguidor registrado - userName: {userName}, followedAt: {followedAt}");
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        LogToFile($"ERROR AL REGISTRAR SEGUIDOR: {ex.Message}");
                        _logger.LogError($"Error al registrar seguidor: {ex.Message}");
                    }
                }
            }
        }

        private async Task<DateTime> GetAccountCreatedAt(string userId, string broadcasterName)
        {
            try
            {
                var accessToken = await Utils.GetAccessTokenFromDatabase(_configuration, broadcasterName);
                if (string.IsNullOrEmpty(accessToken))
                {
                    _logger.LogWarning($"No se pudo obtener accessToken para broadcaster {broadcasterName}");
                    return DateTime.UtcNow;
                }

                using (var httpClient = new HttpClient())
                {
                    httpClient.DefaultRequestHeaders.Add("Client-Id", _configuration["TwitchSettings:ClientId"]);
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                    var response = await httpClient.GetAsync($"https://api.twitch.tv/helix/users?id={userId}");
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning($"Error obteniendo created_at para userId {userId}: {response.StatusCode}");
                        return DateTime.UtcNow;
                    }

                    var content = await response.Content.ReadAsStringAsync();
                    var userData = JsonConvert.DeserializeObject<dynamic>(content);

                    if (userData?.data != null && userData.data.Count > 0)
                    {
                        return DateTime.Parse(userData.data[0].created_at.ToString(), null, System.Globalization.DateTimeStyles.AssumeUniversal);
                    }

                    _logger.LogWarning($"No se encontró created_at para userId {userId}");
                    return DateTime.UtcNow;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al obtener created_at para userId {userId}");
                return DateTime.UtcNow;
            }
        }

        private class FollowSettings
        {
            public bool Enabled { get; set; }
            public int CooldownHours { get; set; }
            public string MessageTemplate { get; set; }
        }
    }
}