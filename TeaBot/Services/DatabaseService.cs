using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Discord.Commands;
using Microsoft.Collections.Extensions;
using Npgsql;
using TeaBot.Main;

namespace TeaBot.Services
{
    public class DatabaseService
    {
        /// <summary>
        ///     The connection to the PostgreSQL database.
        /// </summary>
        private NpgsqlConnection Connection { get; set; }

        private string ConnectionString { get; set; }

        /// <summary>
        ///     Prefixes for guilds.
        /// </summary>
        private Dictionary<ulong, string> CustomPrefixes { get; set; }

        /// <summary>
        ///     Modules disabled in guilds.
        /// </summary>
        private MultiValueDictionary<ulong, string> GuildDisabledModules { get; set; }

        /// <summary>
        ///     Initialize a database connection with the provided connection string.
        /// </summary>
        /// <param name="connectionString">The connenction string to use to open connection.</param>
        public async Task InitAsync(string connectionString)
        {
            ConnectionString = connectionString;

            // Open connection
            Connection = new NpgsqlConnection(connectionString);
            await Connection.OpenAsync();

            // Query for retrieving prefixes and disabled modules
            string query = $"SELECT id, prefix FROM guilds; SELECT guildid, module_name FROM disabled_modules";

            await using var cmd = GetCommand(query);
            await using var reader = await cmd.ExecuteReaderAsync();

            // Init prefixes
            CustomPrefixes = new Dictionary<ulong, string>();
            while (await reader.ReadAsync())
                CustomPrefixes.Add((ulong)reader.GetInt64(0), reader.GetString(1));

            // Advance to disabled modules
            await reader.NextResultAsync();

            // Init disabled modules
            GuildDisabledModules = MultiValueDictionary<ulong, string>.Create<HashSet<string>>();
            while (await reader.ReadAsync())
            {
                ulong guildId = (ulong)reader.GetInt64(0);
                string moduleName = reader.GetString(1);

                GuildDisabledModules.Add(guildId, moduleName);
            }

            await reader.CloseAsync();

            // Register StateChange event
            Connection.StateChange += ConnectionStateChanged;
        }

        /// <summary>
        ///     Closes and reopens connection in case its state was changed.
        /// </summary>
        private void ConnectionStateChanged(object sender, StateChangeEventArgs e)
        {
            if (e.CurrentState != ConnectionState.Open)
            {
                Connection.Close();
                Connection.Open();
                Logger.Log("Database", "Reopened connection");
            }
        }

        /// <summary>
        ///     Get an NpgsqlCommand with the predetermined connection.
        /// </summary>
        /// <param name="query">The query to use in the command</param>
        /// <returns>An instance of an NpgsqlCommand with a set query</returns>
        public NpgsqlCommand GetCommand(string query, bool newConnection = false)
        {
            if (newConnection)
            {
                NpgsqlConnection conn = new NpgsqlConnection(ConnectionString);
                conn.Open();
                return new NpgsqlCommand(query, conn);
            }
            else
                return new NpgsqlCommand(query, Connection);
        }

        /// <summary>
        ///    Sends a query to the PostgreSQL database to add the guild and the user if they aren't already present.
        /// </summary>
        /// <param name="context">The context to use values from</param>
        public async Task InsertValuesIntoDb(SocketCommandContext context)
        {
            if (context.IsPrivate)
                return;

            ulong guildId = context.Guild.Id;
            ulong userId = context.User.Id;

            string query = @"
            INSERT INTO guildusers (userid, guildid, last_message_timestamp) VALUES (@gid, @uid, @last_message)
            ON CONFLICT (userid, guildid) DO UPDATE
                SET last_message_timestamp = @last_message
            ";

            var cmd = GetCommand(query);

            cmd.Parameters.AddWithValue("gid", (long)guildId);
            cmd.Parameters.AddWithValue("uid", (long)userId);
            cmd.Parameters.AddWithValue("last_message", DateTime.UtcNow);

            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        ///     Changes the prefix for a guild.
        /// </summary>
        /// <param name="guildId">The ID of the guild to change the prefix for.</param>
        /// <param name="newPrefix">The new prefix to set.</param>
        public async Task ChangePrefix(ulong guildId, string newPrefix)
        {
            string query = "UPDATE guilds SET prefix=@prefix WHERE id=@gid";
            await using var cmd = GetCommand(query);

            cmd.Parameters.AddWithValue("gid", (long)guildId);
            cmd.Parameters.AddWithValue("prefix", newPrefix);

            await cmd.ExecuteNonQueryAsync();

            CustomPrefixes[guildId] = newPrefix;
        }

        public async Task RemovePrefix(ulong guildId)
        {
            CustomPrefixes.Remove(guildId);

            string query = "DELETE FROM guilds WHERE id=@gid";

            await using var cmd = GetCommand(query);
            cmd.Parameters.AddWithValue("gid", (long)guildId);

            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        ///     Disable a module for a guild.
        /// </summary>
        /// <param name="guildId">The ID of the guild to disable the module for.</param>
        /// <param name="moduleName">The name of the module to disable (in lowercase)</param>
        public async Task DisableModuleAsync(ulong guildId, string moduleName)
        {
            string query = $"INSERT INTO disabled_modules (guildid, module_name) VALUES (@gid, @module)";
            await using var cmd = GetCommand(query);

            cmd.Parameters.AddWithValue("gid", (long)guildId);
            cmd.Parameters.AddWithValue("module", moduleName);

            await cmd.ExecuteNonQueryAsync();

            GuildDisabledModules.Add(guildId, moduleName);
        }

        /// <summary>
        ///     Enable a module for a guild.
        /// </summary>
        /// <param name="guildId">The ID of the guild to enable the module for.</param>
        /// <param name="moduleName">The name of the module to enable (in lowercase)</param>
        public async Task EnableModuleAsync(ulong guildId, string moduleName)
        {
            string query = $"DELETE FROM disabled_modules WHERE guildid=@gid AND module_name=@module";
            await using var cmd = GetCommand(query);

            cmd.Parameters.AddWithValue("gid", (long)guildId);
            cmd.Parameters.AddWithValue("module", moduleName);

            await cmd.ExecuteNonQueryAsync();

            GuildDisabledModules.Remove(guildId, moduleName);
        }

        /// <summary>
        ///     Gets the prefix for the given guild if it is in the Prefix collection. If it isn't, returns the default prefix.
        /// </summary>
        /// <param name="guildId">The guild ID to retrieve the prefix for.</param>
        /// <returns>Prefix used in the provided guild.</returns>
        public string GetPrefix(ulong? guildId)
        {
            if (guildId.HasValue && CustomPrefixes.TryGetValue(guildId.Value, out var prefix))
                return prefix;

            return TeaEssentials.DefaultPrefix;
            
        }

        /// <summary>
        ///     Get modules disabled in the given guild.
        /// </summary>
        /// <param name="guildId">The ID of the guild to retrieve disabled modules for.</param>
        /// <returns>List containing the lowercase names of disabled modules, or an empty list if </returns>
        public IReadOnlyCollection<string> GetDisabledModules(ulong? guildId)
        {
            if (guildId is null)
                return new List<string>();

            if (GuildDisabledModules.TryGetValue(guildId.Value, out var disabledModules))
                return disabledModules;
            else
                return new List<string>();
        }
    }
}

