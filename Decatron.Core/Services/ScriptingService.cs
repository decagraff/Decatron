// Scripting/Services/ScriptingService.cs
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Decatron.Core.Functions;
using Decatron.Core.Helpers;
using Decatron.Core.Models;
using Decatron.Scripting.Core;
using Decatron.Scripting.Exceptions;
using Decatron.Scripting.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;

namespace Decatron.Scripting.Services
{
    public class ScriptingService
    {
        private readonly string _connectionString;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ScriptingService> _logger;
        private readonly ScriptParser _parser;
        private readonly ScriptExecutor _executor;

        public ScriptingService(
            IConfiguration configuration,
            ILogger<ScriptingService> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _configuration = configuration;
            _logger = logger;
            _parser = new ScriptParser();
            _executor = new ScriptExecutor();
        }

        /// <summary>
        /// Crear un nuevo comando con script
        /// </summary>
        public async Task<bool> CreateScriptedCommandAsync(string channelName, string commandName, string scriptContent, long userId)
        {
            try
            {
                // Validar sintaxis del script
                _parser.Parse(scriptContent);

                using var conn = new MySqlConnection(_connectionString);
                await conn.OpenAsync();

                var query = @"
                    INSERT INTO scripted_commands (user_id, channel_name, command_name, script_content, is_active)
                    VALUES (@userId, @channelName, @commandName, @scriptContent, @isActive)
                    ON DUPLICATE KEY UPDATE 
                        script_content = @scriptContent,
                        updated_at = CURRENT_TIMESTAMP";

                using var cmd = new MySqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@userId", userId);
                cmd.Parameters.AddWithValue("@channelName", channelName.ToLower());
                cmd.Parameters.AddWithValue("@commandName", commandName.ToLower());
                cmd.Parameters.AddWithValue("@scriptContent", scriptContent);
                cmd.Parameters.AddWithValue("@isActive", true);

                var rowsAffected = await cmd.ExecuteNonQueryAsync();
                return rowsAffected > 0;
            }
            catch (ScriptParseException ex)
            {
                _logger.LogWarning($"Error de sintaxis en script para comando {commandName}: {ex.Message}");
                throw new ScriptingServiceException($"Error de sintaxis: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creando comando con script {commandName} en {channelName}");
                throw new ScriptingServiceException($"Error creando comando: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Ejecutar un comando con script
        /// </summary>
        public async Task<ScriptExecutionResult> ExecuteScriptedCommandAsync(
            string channelName,
            string commandName,
            string executingUser,
            string[] commandArgs = null)
        {
            try
            {
                // Obtener script de BD
                var scriptCommand = await GetScriptedCommand(channelName, commandName);
                if (scriptCommand == null)
                {
                    return new ScriptExecutionResult
                    {
                        Success = false,
                        ErrorMessage = "Comando con script no encontrado"
                    };
                }

                if (!scriptCommand.IsActive)
                {
                    return new ScriptExecutionResult
                    {
                        Success = false,
                        ErrorMessage = "Comando desactivado"
                    };
                }

                // Parsear script
                var program = _parser.Parse(scriptCommand.ScriptContent);

                // Crear contexto de ejecución
                var context = await CreateExecutionContext(channelName, commandName, executingUser, commandArgs);

                // Ejecutar script
                return await _executor.ExecuteAsync(program, context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error ejecutando comando con script {commandName} en {channelName}");
                return new ScriptExecutionResult
                {
                    Success = false,
                    ErrorMessage = $"Error de ejecución: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Verificar si un comando tiene script asociado
        /// </summary>
        public async Task<bool> IsScriptedCommandAsync(string channelName, string commandName)
        {
            try
            {
                var scriptCommand = await GetScriptedCommand(channelName, commandName);
                return scriptCommand != null && scriptCommand.IsActive;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error verificando comando con script {commandName} en {channelName}");
                return false;
            }
        }

        /// <summary>
        /// Validar sintaxis de un script sin ejecutarlo
        /// </summary>
        public async Task<ScriptValidationResult> ValidateScriptAsync(string scriptContent)
        {
            try
            {
                Console.WriteLine("[SCRIPTING SERVICE] Iniciando ValidateScriptAsync");
                Console.WriteLine($"[SCRIPTING SERVICE] Llamando a _parser.Parse() con script de {scriptContent.Length} caracteres");

                _parser.Parse(scriptContent);

                Console.WriteLine("[SCRIPTING SERVICE] _parser.Parse() completado exitosamente");
                return new ScriptValidationResult { IsValid = true };
            }
            catch (ScriptParseException ex)
            {
                Console.WriteLine($"[SCRIPTING SERVICE] ScriptParseException: {ex.Message}");
                return new ScriptValidationResult
                {
                    IsValid = false,
                    ErrorMessage = ex.Message,
                    ErrorLine = ex.LineNumber
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SCRIPTING SERVICE] Exception inesperada: {ex.Message}");
                return new ScriptValidationResult
                {
                    IsValid = false,
                    ErrorMessage = $"Error inesperado: {ex.Message}"
                };
            }
        }

        private async Task<ScriptCommand> GetScriptedCommand(string channelName, string commandName)
        {
            using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();

            var query = @"
                SELECT id, user_id, channel_name, command_name, script_content, is_active, created_at, updated_at
                FROM scripted_commands 
                WHERE channel_name = @channelName AND command_name = @commandName
                LIMIT 1";

            using var cmd = new MySqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@channelName", channelName.ToLower());
            cmd.Parameters.AddWithValue("@commandName", commandName.ToLower());

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new ScriptCommand
                {
                    Id = reader.GetInt64("id"),
                    UserId = reader.GetInt64("user_id"),
                    ChannelName = reader.GetString("channel_name"),
                    CommandName = reader.GetString("command_name"),
                    ScriptContent = reader.GetString("script_content"),
                    IsActive = reader.GetBoolean("is_active"),
                    CreatedAt = reader.GetDateTime("created_at"),
                    UpdatedAt = reader.GetDateTime("updated_at")
                };
            }

            return null;
        }

        private async Task<ScriptExecutionContext> CreateExecutionContext(
            string channelName,
            string commandName,
            string executingUser,
            string[] commandArgs)
        {
            var context = new ScriptExecutionContext
            {
                ChannelName = channelName,
                CommandName = commandName,
                ExecutingUser = executingUser
            };

            // Poblar variables builtin del sistema existente
            context.BuiltinVariables["user"] = executingUser;
            context.BuiltinVariables["channel"] = channelName;

            try
            {
                // Usar el sistema existente de procesamiento de variables como en Utils.ProcessCustomFunctions
                // Estas se procesarán igual que $(game), $(uptime), etc. en el sistema actual

                // Las variables $(game), $(uptime), $(ruser), $(touser) se procesarán 
                // cuando se ejecute el script, usando el mismo sistema que ya tienes
                context.BuiltinVariables["game"] = "$(game)"; // Se procesará con GameFunction
                context.BuiltinVariables["uptime"] = "$(uptime)"; // Se procesará con UptimeFunction
                context.BuiltinVariables["ruser"] = "$(ruser)"; // Usuario aleatorio
                context.BuiltinVariables["touser"] = ExtractMentionedUser(commandArgs) ?? "$(touser)";
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Error configurando variables del contexto: {ex.Message}");
            }

            return context;
        }

        private string ExtractMentionedUser(string[] args)
        {
            if (args == null || args.Length == 0) return "";

            foreach (var arg in args)
            {
                if (arg.StartsWith("@"))
                {
                    return arg.Substring(1); // Remover @ del inicio
                }
            }

            return "";
        }
    }

    // Excepciones específicas del servicio
    public class ScriptingServiceException : Exception
    {
        public ScriptingServiceException(string message) : base(message) { }
        public ScriptingServiceException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class ScriptValidationResult
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }
        public int ErrorLine { get; set; }
    }
}