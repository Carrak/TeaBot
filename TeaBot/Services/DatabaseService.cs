using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Discord.Commands;
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

        /// <summary>
        ///     Prefixes for guilds.
        /// </summary>
        private Dictionary<ulong, string> Prefixes { get; set; }

        /// <summary>
        ///     Modules disabled in guilds.
        /// </summary>
        private Dictionary<ulong, List<string>> GuildDisabledModules { get; set; }

        /// <summary>
        ///     Initialize a database connection with the provided connection string.
        /// </summary>
        /// <param name="connectionString">The connenction string to use to open connection.</param>
        public async Task InitAsync(string connectionString)
        {
            // Open connection
            Connection = new NpgsqlConnection(connectionString);
            await Connection.OpenAsync();

            // Query for retrieving prefixes and disabled modules
            string query = $"SELECT id, prefix FROM guilds; SELECT guildid, module_name FROM disabled_modules";

            await using var cmd = GetCommand(query);
            await using var reader = await cmd.ExecuteReaderAsync();

            // Init prefixes
            Prefixes = new Dictionary<ulong, string>();
            while (await reader.ReadAsync())
            {
                Prefixes.Add((ulong)reader.GetInt64(0), reader.IsDBNull(1) ? null : reader.GetString(1));
            }

            // Advance to disabled modules
            await reader.NextResultAsync();

            // Init disabled modules
            GuildDisabledModules = new Dictionary<ulong, List<string>>();
            while (await reader.ReadAsync())
            {
                ulong guildId = (ulong)reader.GetInt64(0);
                string moduleName = reader.GetString(1);

                if (GuildDisabledModules.TryGetValue(guildId, out var modules))
                {
                    modules.Add(moduleName);
                    GuildDisabledModules[guildId] = modules;
                }
                else
                {
                    var list = new List<string>
                    {
                        moduleName
                    };
                    GuildDisabledModules.Add(guildId, list);
                }
            }

            await reader.CloseAsync();

            // Register StateChange event
            Connection.StateChange += ConnectionStateChanged;
        }

        /// <summary>
        ///     Close and reopen connection in case its state was changed.
        /// </summary>
        private void ConnectionStateChanged(object sender, StateChangeEventArgs e)
        {
            Console.WriteLine($"--- StateChange event received at {DateTime.UtcNow:dd.MM.yyyy HH:mm:ss} ---");
            Console.WriteLine($"Original state: {e.OriginalState}");
            Console.WriteLine($"Current state: {e.CurrentState}");
            if (e.CurrentState != System.Data.ConnectionState.Open)
            {
                Console.WriteLine("Closing and reopening connection...");
                Connection.Close();
                Connection.Open();
                Console.WriteLine($"Reopened connection");
            }
        }

        /// <summary>
        ///     Get an NpgsqlCommand with the predetermined connection.
        /// </summary>
        /// <param name="query">The query to use in the command</param>
        /// <returns>An instance of an NpgsqlCommand with a set query</returns>
        public NpgsqlCommand GetCommand(string query)
        {
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
            ulong channelId = context.Channel.Id;
            ulong messageId = context.Message.Id;

            string query = "DO $$ BEGIN " +
                    $"PERFORM conditional_insert('guilds', 'guilds.id = {guildId}', 'id', '{guildId}'); " +
                    $"PERFORM conditional_insert('guildusers', 'guildusers.userid = {userId} AND guildusers.guildid = {guildId}', 'userid, guildid', '{userId}, {guildId}'); " +
                    $"UPDATE guildusers SET (channelid, messageid, last_message_timestamp) = ({channelId}, {messageId}, now()::timestamp) " +
                    $"WHERE userid={userId} AND guildid={guildId}; " +
                    "END $$ LANGUAGE plpgsql; ";

            var cmd = GetCommand(query);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        ///     Changes the prefix for a guild.
        /// </summary>
        /// <param name="guildId">The ID of the guild to change the prefix for.</param>
        /// <param name="newPrefix">The new prefix to set.</param>
        public async Task ChangePrefix(ulong guildId, string newPrefix)
        {
            bool isDefaultPrefix = newPrefix == TeaEssentials.DefaultPrefix;
            newPrefix = isDefaultPrefix ? null : newPrefix;

            string query = $"UPDATE guilds SET prefix=@prefix WHERE id=@gid";
            await using var cmd = GetCommand(query);

            cmd.Parameters.AddWithValue("gid", (long)guildId);
            cmd.Parameters.AddWithValue("prefix", newPrefix);

            await cmd.ExecuteNonQueryAsync();

            Prefixes[guildId] = isDefaultPrefix ? null : newPrefix;
        }

        /// <summary>
        ///     Adds a guild with a default prefix.
        /// </summary>
        /// <param name="guildId">The ID of the guild to insert</param>
        public async Task AddGuild(ulong guildId)
        {
            string query = $"INSERT INTO guilds (id) VALUES (@gid)";
            await using var cmd = GetCommand(query);

            cmd.Parameters.AddWithValue("gid", (long)guildId);

            await cmd.ExecuteNonQueryAsync();

            Prefixes.Add(guildId, null);
        }

        /// <summary>
        ///     Removes a guild as well as its prefix.
        /// </summary>
        /// <param name="guildId">The ID of the guild to delete.</param>
        /// <returns></returns>
        public async Task RemoveGuild(ulong guildId)
        {
            string query = $"DELETE FROM guilds WHERE guilds.id=@gid";
            await using var cmd = GetCommand(query);

            cmd.Parameters.AddWithValue("gid", (long)guildId);

            await cmd.ExecuteNonQueryAsync();

            Prefixes.Remove(guildId);
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

            if (GuildDisabledModules.TryGetValue(guildId, out var disabledModules))
            {
                disabledModules.Add(moduleName);
                GuildDisabledModules[guildId] = disabledModules;
            }
            else
            {
                var list = new List<string>
                    {
                        moduleName
                    };
                GuildDisabledModules.Add(guildId, list);
            }
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

            var disabledModules = GuildDisabledModules[guildId];
            disabledModules.Remove(moduleName);
            GuildDisabledModules[guildId] = disabledModules;
        }

        /// <summary>
        ///     Retrieve the prefix for the given guild.
        /// </summary>
        /// <param name="guildId">The guild ID to retrieve the prefix for.</param>
        /// <returns>Prefix used in the provided guild.</returns>
        public string GetPrefix(ulong? guildId)
        {
            if (guildId is null)
                return TeaEssentials.DefaultPrefix;

            string prefix = Prefixes[guildId.Value];
            return prefix ?? TeaEssentials.DefaultPrefix;
        }

        /// <summary>
        ///     Get modules disabled in the given guild.
        /// </summary>
        /// <param name="guildId">The ID of the guild to retrieve disabled modules for.</param>
        /// <returns>List containing the lowercase names of disabled modules, or an empty list if </returns>
        public List<string> GetDisabledModules(ulong? guildId)
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

