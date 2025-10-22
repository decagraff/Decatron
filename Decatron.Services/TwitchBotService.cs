using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Decatron.Core.Interfaces;
using Decatron.Core.Models;
using Decatron.Core.Settings;
using Decatron.Data;
using Decatron.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Events;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using ChatMessage = Decatron.Core.Models.ChatMessage;

namespace Decatron.Services
{
    public class TwitchBotService
    {
        private readonly TwitchClient _client;
        private readonly ILogger<TwitchBotService> _logger;
        private readonly IConfiguration _configuration;
        private readonly TwitchSettings _twitchSettings;
        private readonly IServiceProvider _serviceProvider;
        private readonly IMessageSender _messageSender;
        private readonly TwitchApiService _apiService;
        private readonly CommandService _commandService;

        private bool _isRunning = false;
        private DateTime _lastReconnectAttempt = DateTime.MinValue;
        private int _reconnectAttempts = 0;
        private const int MAX_RECONNECT_ATTEMPTS = 3;
        private const int RECONNECT_DELAY_MINUTES = 10;

        public TwitchBotService(
            TwitchClient client,
            ILogger<TwitchBotService> logger,
            IConfiguration configuration,
            IOptions<TwitchSettings> twitchSettings,
            IServiceProvider serviceProvider,
            IMessageSender messageSender,
            TwitchApiService apiService,
            CommandService commandService)
        {
            _client = client;
            _logger = logger;
            _configuration = configuration;
            _twitchSettings = twitchSettings.Value;
            _serviceProvider = serviceProvider;
            _messageSender = messageSender;
            _apiService = apiService;
            _commandService = commandService;

            ValidateSettings();
        }

        private void ValidateSettings()
        {
            if (string.IsNullOrEmpty(_twitchSettings.BotUsername))
                throw new InvalidOperationException("Bot username is missing in TwitchSettings");
           
        }

        private async Task<string> GetBotTokenFromDb()
        {
            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                using var connection = new MySqlConnection(connectionString);
                await connection.OpenAsync();

                const string query = @"
            SELECT ChatToken 
            FROM bot_tokens 
            WHERE BotUsername = @botUsername AND IsActive = 1 
            LIMIT 1";

                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@botUsername", _twitchSettings.BotUsername);

                var result = await command.ExecuteScalarAsync();
                if (result != null)
                {
                    var token = result.ToString();
                    _logger.LogInformation($"Token del bot obtenido desde BD: {token.Substring(0, Math.Min(10, token.Length))}...");
                    return token.StartsWith("oauth:") ? token : "oauth:" + token;
                }

                throw new InvalidOperationException("No se encontró token para el bot en la base de datos");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error obteniendo token de BD");
                throw new InvalidOperationException("Token del bot no disponible", ex); // NO usar fallback
            }
        }

        private async Task InitializeClientAsync()
        {
            try
            {
                var botToken = await GetBotTokenFromDb();
                var credentials = new ConnectionCredentials(_twitchSettings.BotUsername, botToken);
                _client.Initialize(credentials);

                _client.OnMessageReceived += Client_OnMessageReceived;
                _client.OnConnected += Client_OnConnected;
                _client.OnDisconnected += Client_OnDisconnected;
                _client.OnJoinedChannel += Client_OnJoinedChannel;
                _client.OnConnectionError += Client_OnConnectionError;

                _logger.LogInformation($"TwitchBotService inicializado - Bot: {_twitchSettings.BotUsername}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inicializando TwitchClient");
                throw;
            }
        }

        public async Task Start()
        {
            if (_isRunning)
            {
                _logger.LogWarning("TwitchBotService ya está en ejecución");
                return;
            }

            _isRunning = true;
            _logger.LogInformation("Iniciando TwitchBotService...");

            try
            {
                await InitializeClientAsync();

                if (!_client.IsConnected)
                {
                    _client.Connect();
                    await WaitForConnection();
                }

                await ConnectToEnabledChannels();
                _logger.LogInformation("TwitchBotService iniciado correctamente");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error iniciando TwitchBotService");
                _isRunning = false;
                throw;
            }
        }

        private async Task WaitForConnection()
        {
            int attempts = 0;
            while (!_client.IsConnected && attempts < 10)
            {
                await Task.Delay(1000);
                attempts++;
            }

            if (!_client.IsConnected)
                throw new Exception("No se pudo conectar al cliente de Twitch después de varios intentos");

            _logger.LogInformation("Cliente Twitch conectado exitosamente");
        }

        /// <summary>
        /// Conecta a todos los canales que tienen el bot habilitado (Users.IsActive = true Y SystemSettings.BotEnabled = true)
        /// </summary>
        private async Task ConnectToEnabledChannels()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();

                var enabledChannels = await settingsService.GetEnabledBotChannelsAsync();

                _logger.LogInformation($"Conectando a {enabledChannels.Count} canales desde configuración de settings");

                foreach (var channelName in enabledChannels)
                {
                    if (!_client.JoinedChannels.Any(c => c.Channel.Equals(channelName, StringComparison.OrdinalIgnoreCase)))
                    {
                        _client.JoinChannel(channelName);
                        _logger.LogInformation($"Conectado al canal: {channelName}");

                        await Task.Delay(100);
                    }
                    else
                    {
                        _logger.LogDebug($"Ya conectado al canal: {channelName}");
                    }
                }

                _logger.LogInformation($"Bot conectado a {_client.JoinedChannels.Count} canales habilitados");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error conectando a canales habilitados, intentando canal por defecto");

                try
                {
                    var defaultChannel = _twitchSettings.ChannelId;
                    if (!string.IsNullOrEmpty(defaultChannel))
                    {
                        _client.JoinChannel(defaultChannel);
                        _logger.LogInformation($"Conectado al canal por defecto: {defaultChannel}");
                    }
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "Error conectando al canal por defecto");
                }
            }
        }

        private void Client_OnConnected(object sender, OnConnectedArgs e)
        {
            _logger.LogInformation("IRC conectado exitosamente");
            _reconnectAttempts = 0;
        }

        private void Client_OnDisconnected(object sender, OnDisconnectedEventArgs e)
        {
            var timeSinceLastAttempt = DateTime.Now - _lastReconnectAttempt;

            if (_reconnectAttempts < MAX_RECONNECT_ATTEMPTS &&
                timeSinceLastAttempt.TotalMinutes >= RECONNECT_DELAY_MINUTES)
            {
                _lastReconnectAttempt = DateTime.Now;
                _reconnectAttempts++;

                _logger.LogWarning($"IRC disconnected. Intento {_reconnectAttempts}/{MAX_RECONNECT_ATTEMPTS}");

                Task.Run(async () =>
                {
                    await Task.Delay(5000);
                    try
                    {
                        _client.Connect();
                        // Reconectar a canales habilitados después de reconectar IRC
                        await Task.Delay(2000);
                        await ConnectToEnabledChannels();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error reconectando IRC");
                    }
                });
            }
        }

        private void Client_OnJoinedChannel(object sender, OnJoinedChannelArgs e)
        {
            _logger.LogInformation($"Bot unido al canal: {e.Channel}");
        }

        private void Client_OnConnectionError(object sender, OnConnectionErrorArgs e)
        {
            _logger.LogError($"Error de conexión IRC: {e.Error.Message}");
        }

        private void Client_OnMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            try
            {
                var message = e.ChatMessage.Message.Trim();
                var username = e.ChatMessage.Username;
                var channel = e.ChatMessage.Channel;
                var userId = e.ChatMessage.UserId;
                var timestamp = DateTime.UtcNow;

                var fechaPeru = timestamp.AddHours(-5);
                _logger.LogDebug($"[{channel}] [{username}] [{message}] [{fechaPeru:dd/MM/yyyy HH:mm:ss}]");

                // Verificar si el bot está habilitado para este canal antes de procesar
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();

                        var isBotEnabled = await settingsService.IsBotEnabledForChannelAsync(channel);
                        if (!isBotEnabled)
                        {
                            _logger.LogDebug($"Bot deshabilitado para canal {channel}, ignorando mensaje");
                            return;
                        }

                        var dbContext = scope.ServiceProvider.GetRequiredService<DecatronDbContext>();

                        // Guardar mensaje en BD
                        var chatMessage = new ChatMessage
                        {
                            Channel = channel,
                            Username = username,
                            UserId = userId,
                            Message = message,
                            Timestamp = timestamp
                        };

                        dbContext.ChatMessages.Add(chatMessage);
                        await dbContext.SaveChangesAsync();

                        // Procesar comandos usando CommandService
                        await _commandService.ProcessMessageAsync(username, channel, message);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error procesando mensaje");
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error procesando mensaje");
            }
        }

        public async Task SendMessage(string channel, string message)
        {
            // Verificar si el bot está habilitado para este canal antes de enviar mensaje
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var settingsService = scope.ServiceProvider.GetRequiredService<ISettingsService>();

                var isBotEnabled = await settingsService.IsBotEnabledForChannelAsync(channel);
                if (!isBotEnabled)
                {
                    _logger.LogDebug($"Bot deshabilitado para canal {channel}, no enviando mensaje");
                    return;
                }

                await _messageSender.SendMessageAsync(channel, message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error verificando estado del bot para canal {channel}");
                // En caso de error, enviar el mensaje de todas formas para no romper funcionalidad
                await _messageSender.SendMessageAsync(channel, message);
            }
        }

        public bool IsConnected => _client.IsConnected;

        public List<string> GetConnectedChannels()
        {
            return _client.JoinedChannels.Select(c => c.Channel).ToList();
        }

        public void JoinChannel(string channel)
        {
            if (_client.IsConnected)
            {
                _client.JoinChannel(channel);
                _logger.LogInformation($"Unido al canal: {channel}");
            }
            else
            {
                _logger.LogWarning($"Cliente no conectado, no se puede unir al canal: {channel}");
            }
        }

        public void LeaveChannel(string channel)
        {
            if (_client.JoinedChannels.Any(c => c.Channel.Equals(channel, StringComparison.OrdinalIgnoreCase)))
            {
                _client.LeaveChannel(channel);
                _logger.LogInformation($"Salido del canal: {channel}");
            }
            else
            {
                _logger.LogDebug($"Bot no estaba conectado al canal: {channel}");
            }
        }

        public void Stop()
        {
            _isRunning = false;
            if (_client.IsConnected)
            {
                _client.Disconnect();
                _logger.LogInformation("TwitchBotService detenido");
            }
        }
    }
}