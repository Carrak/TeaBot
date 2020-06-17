using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Npgsql;
using TeaBot.Main;

namespace TeaBot.Services
{
    public class DatabaseService
    {
        private readonly CommandService _commands;

        public DatabaseService(CommandService commands)
        {
            _commands = commands;
        }

        /// <summary>
        ///    Sends a query to the PostgreSQL database to add the guild and the user information if they aren't already present.
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

            var cmd = new NpgsqlCommand(query, TeaEssentials.DbConnection);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        ///    Sends a query to the PostgreSQL database to add the guild if it's not already present.
        /// </summary>
        /// <param name="guildId">The guild ID to insert</param>
        public async Task InsertValuesIntoDb(ulong guildId)
        {
            string query = "DO $$ BEGIN " +
                    $"PERFORM conditional_insert('guilds', 'guilds.id = {guildId}', 'id', '{guildId}'); " +
                    "END $$ LANGUAGE plpgsql;";

            await using var cmd = new NpgsqlCommand(query, TeaEssentials.DbConnection);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        ///     Retrieve the prefix for the given guild.
        /// </summary>
        /// <param name="guild">The guild to retrieve the prefix for.</param>
        /// <returns>Prefix used in the provided guild.</returns>
        public async Task<string> GetPrefixAsync(IGuild guild)
        {
            if (guild is null)
                return TeaEssentials.DefaultPrefix;

            string query = $"SELECT prefix FROM guilds WHERE id={guild.Id};";

            await using var cmd = new NpgsqlCommand(query, TeaEssentials.DbConnection);
            await using var reader = await cmd.ExecuteReaderAsync();

            await reader.ReadAsync();
            string prefix = reader.IsDBNull(0) ? TeaEssentials.DefaultPrefix : reader.GetString(0);
            await reader.CloseAsync();

            return prefix;
        }

        public async Task<List<ModuleInfo>> GetDisabledModules(IGuild guild)
        {
            List<ModuleInfo> disabledModules = new List<ModuleInfo>();

            if (guild is null)
                return disabledModules;

            string query = $"SELECT module_name FROM disabled_modules WHERE guildid = {guild.Id}";
            await using var cmd = new NpgsqlCommand(query, TeaEssentials.DbConnection);
            await using var reader = await cmd.ExecuteReaderAsync();

            if (reader.HasRows)
            {
                while (await reader.ReadAsync())
                    disabledModules.Add(_commands.Modules.First(module => module.Name.ToLower() == reader.GetString(0)));
            }
            await reader.CloseAsync();
            return disabledModules;
        }
    }
}

