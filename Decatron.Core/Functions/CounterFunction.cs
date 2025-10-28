using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Decatron.Core.Helpers;
using MySql.Data.MySqlClient;

namespace Decatron.Core.Functions
{
    public static class CounterFunction
    {
        private static IConfiguration _configuration;

        public static void Initialize(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public static async Task<string> Execute(string channelName, string commandName, string userName, string[] args)
        {
            int counterValue = await LoadCounterFromDatabase(channelName, commandName);
            string operation = args.Length > 0 ? args[0].ToLower() : "";
            int? newValue = null;

            switch (operation)
            {
                case "set":
                    if (await Utils.IsOwnerOrModerator(_configuration, userName, channelName))
                    {
                        if (args.Length > 1 && int.TryParse(args[1], out int setValue))
                        {
                            newValue = Math.Max(0, setValue);
                        }
                    }
                    else
                    {
                        return "Solo los moderadores o el streamer pueden usar la operación 'set' del contador.";
                    }
                    break;
                case "+":
                    int addValue = 1;
                    if (args.Length > 1 && int.TryParse(args[1], out int parsedAddValue))
                    {
                        addValue = parsedAddValue;
                    }
                    newValue = counterValue + addValue;
                    break;
                case "-":
                    int subtractValue = 1;
                    if (args.Length > 1 && int.TryParse(args[1], out int parsedSubtractValue))
                    {
                        subtractValue = parsedSubtractValue;
                    }
                    newValue = Math.Max(0, counterValue - subtractValue);
                    break;
                case "reset":
                    if (await Utils.IsOwnerOrModerator(_configuration, userName, channelName))
                    {
                        newValue = 0;
                    }
                    else
                    {
                        return "Solo los moderadores o el streamer pueden resetear el contador.";
                    }
                    break;
            }

            if (newValue.HasValue)
            {
                await SaveCounterToDatabase(channelName, commandName, newValue.Value);
                counterValue = newValue.Value;
            }

            return counterValue.ToString();
        }

        private static async Task<int> LoadCounterFromDatabase(string channelName, string commandName)
        {
            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                using var connection = new MySqlConnection(connectionString);
                await connection.OpenAsync();

                const string query = "SELECT counter_value FROM command_counters WHERE channel_name = @ChannelName AND command_name = @CommandName";
                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@ChannelName", channelName);
                command.Parameters.AddWithValue("@CommandName", commandName);

                var result = await command.ExecuteScalarAsync();
                if (result != null && int.TryParse(result.ToString(), out int value))
                {
                    return value;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error cargando el contador: {ex.Message}");
            }

            return 0; // Por defecto, inicia en 0
        }

        private static async Task SaveCounterToDatabase(string channelName, string commandName, int counterValue)
        {
            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                using var connection = new MySqlConnection(connectionString);
                await connection.OpenAsync();

                const string query = @"INSERT INTO command_counters (channel_name, command_name, counter_value)
                                     VALUES (@ChannelName, @CommandName, @CounterValue)
                                     ON DUPLICATE KEY UPDATE counter_value = @CounterValue";
                using var command = new MySqlCommand(query, connection);
                command.Parameters.AddWithValue("@ChannelName", channelName);
                command.Parameters.AddWithValue("@CommandName", commandName);
                command.Parameters.AddWithValue("@CounterValue", counterValue);
                await command.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error guardando el contador: {ex.Message}");
            }
        }
    }
}