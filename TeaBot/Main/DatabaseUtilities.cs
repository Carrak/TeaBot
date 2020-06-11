using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Npgsql;

namespace TeaBot
{
    public class DatabaseUtilities
    {
        public static async Task InsertValuesIntoDb(SocketCommandContext context)
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

        public static async Task InsertValuesIntoDb(ulong guildId)
        {
            string query = "DO $$ BEGIN " +
                    $"PERFORM conditional_insert('guilds', 'guilds.id = {guildId}', 'id', '{guildId}'); " +
                    "END $$ LANGUAGE plpgsql;";

            await using var cmd = new NpgsqlCommand(query, TeaEssentials.DbConnection);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        ///     Sends a query to the PostgreSQL database to add the guild if it's not already present and retrieve the prefix for the given guild.
        /// </summary>
        /// <param name="guildId">ID of the guild to retrieve the prefix for.</param>
        /// <returns>Prefix for the guild.</returns>
        public static async Task<string> GetPrefixAsync(IGuild guild)
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
    }
}

