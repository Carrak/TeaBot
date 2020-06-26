using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;
using TeaBot.Services;

namespace TeaBot.Webservices.Rule34
{
    /// <summary>
    ///     Class for managing rule34 search blacklists
    /// </summary>
    public class Rule34BlacklistService
    {
        private readonly DatabaseService _database;

        /// <summary>
        ///     The maximum amount of tags that a user can blacklist.
        /// </summary>
        public const int UserBlacklistLimit = 20;

        /// <summary>
        ///     The maximum amount of tags that can be blacklisted in a guild.
        /// </summary>
        public const int GuildBlacklistLimit = 20;

        public Rule34BlacklistService(DatabaseService database)
        {
            _database = database;
        }

        /// <summary>
        ///     Gets the tags that are blocked by default.
        /// </summary>
        /// <returns>Collection of tags.</returns>
        public async Task<IEnumerable<string>> GetDefaultBlacklist()
        {
            return await GetBlacklistAsync(_database.GetCommand("SELECT r34.default_blacklist.tag FROM r34.default_blacklist"));
        }

        /// <summary>
        ///     Gets the tags that are blocked by a user.
        /// </summary>
        /// <param name="id">The ID of the user.</param>
        /// <returns>Collection of tags.</returns>
        public async Task<IEnumerable<string>> GetUserBlackList(ulong id)
        {
            var cmd = _database.GetCommand($"SELECT r34.user_blacklist.tag FROM r34.user_blacklist WHERE userid=@uid");
            cmd.Parameters.AddWithValue("uid", (long)id);
            return await GetBlacklistAsync(cmd);
        }

        /// <summary>
        ///     Gets the tags that are blocked by a guild.
        /// </summary>
        /// <param name="id">The ID of the guild.</param>
        /// <returns>Collection of tags.</returns>
        public async Task<IEnumerable<string>> GetGuildBlacklist(ulong id)
        {
            var cmd = _database.GetCommand($"SELECT r34.guild_blacklist.tag FROM r34.guild_blacklist WHERE guildid=@gid");
            cmd.Parameters.AddWithValue("gid", (long)id);
            return await GetBlacklistAsync(cmd);
        }

        /// <summary>
        ///     Adds a tag to a user's personal blacklist.
        /// </summary>
        /// <param name="id">The ID of the user.</param>
        /// <param name="tag">The tag to add to the blacklist.</param>
        public async Task AddTagToUserBlacklist(ulong id, string tag)
        {
            var preconditionsCmd = _database.GetCommand($"SELECT EXISTS(SELECT * FROM r34.default_blacklist WHERE tag=@tag), " +
                            $"EXISTS (SELECT * FROM r34.user_blacklist WHERE tag=@tag AND userid=@uid), " +
                            $"COUNT(*) >= @limit FROM r34.user_blacklist WHERE userid=@uid");
            preconditionsCmd.Parameters.AddWithValue("tag", tag);
            preconditionsCmd.Parameters.AddWithValue("uid", (long)id);
            preconditionsCmd.Parameters.AddWithValue("limit", UserBlacklistLimit);

            await EnsureTagsAllowedToAdd(preconditionsCmd, $"The tag is already present in your personal blacklist. `{tag}`", "You've reached your maximum of tags.");

            var cmd = _database.GetCommand($"INSERT INTO r34.user_blacklist (userid, tag) VALUES (@uid, @tag)");
            cmd.Parameters.AddWithValue("uid", (long)id);
            cmd.Parameters.AddWithValue("tag", tag);

            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        ///     Adds a tag to a guild's blacklist.
        /// </summary>
        /// <param name="id">The ID of the guild.</param>
        /// <param name="tag">The tag to add to the blacklist.</param>
        public async Task AddTagToGuildBlacklist(ulong id, string tag)
        {
            var preconditionsCmd = _database.GetCommand($"SELECT EXISTS(SELECT * FROM r34.default_blacklist WHERE tag=@tag), " +
                            $"EXISTS (SELECT * FROM r34.guild_blacklist WHERE tag=@tag AND guildid=@gid), " +
                            $"COUNT(*) >= @limit FROM r34.guild_blacklist WHERE guildid=@gid");
            preconditionsCmd.Parameters.AddWithValue("tag", tag);
            preconditionsCmd.Parameters.AddWithValue("gid", (long)id);
            preconditionsCmd.Parameters.AddWithValue("limit", GuildBlacklistLimit);

            await EnsureTagsAllowedToAdd(preconditionsCmd, $"The tag is already present in the guild's blacklist. `{tag}`", "The guild has reached its maximum of tags.");

            var cmd = _database.GetCommand($"INSERT INTO r34.guild_blacklist (guildid, tag) VALUES (@gid, @tag)");
            cmd.Parameters.AddWithValue("gid", (long)id);
            cmd.Parameters.AddWithValue("tag", tag);

            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        ///     Removes a tag from a guild's blacklist.
        /// </summary>
        /// <param name="id">The ID of the guild.</param>
        /// <param name="tag">The tag to remove.</param>
        public async Task RemoveTagFromGuildBlacklist(ulong id, string tag)
        {
            var cmd = _database.GetCommand($"DELETE FROM r34.guild_blacklist WHERE tag=@tag AND guildid=@gid");
            cmd.Parameters.AddWithValue("tag", tag);
            cmd.Parameters.AddWithValue("gid", (long)id);

            int rowsAffected = await cmd.ExecuteNonQueryAsync();
            if (rowsAffected != 1)
                throw new BlacklistException($"This tag is not blocked for this guild. `{tag}`");
        }

        /// <summary>
        ///     Removes a tag from a user's personal blacklist.
        /// </summary>
        /// <param name="id">The ID of the user.</param>
        /// <param name="tag">The tag to remove.</param>
        public async Task RemoveTagFromUserBlacklist(ulong id, string tag)
        {
            var cmd = _database.GetCommand($"DELETE FROM r34.user_blacklist WHERE tag=@tag AND userid=@uid");
            cmd.Parameters.AddWithValue("tag", tag);
            cmd.Parameters.AddWithValue("uid", (long)id);

            int rowsAffected = await cmd.ExecuteNonQueryAsync();
            if (rowsAffected != 1)
                throw new BlacklistException($"This tag is not blocked by you. `{tag}`");
        }

        /// <summary>
        ///     Gets the blacklist.
        /// </summary>
        /// <param name="cmd">The command to execute to retrieve the blacklist.</param>
        /// <returns>Collection of tags.</returns>
        private async Task<IEnumerable<string>> GetBlacklistAsync(NpgsqlCommand cmd)
        {
            await using var reader = await cmd.ExecuteReaderAsync();

            List<string> blacklist = new List<string>();
            while (await reader.ReadAsync())
                blacklist.Add(reader.GetString(0));
            await reader.CloseAsync();

            return blacklist;
        }

        /// <summary>
        ///     Ensures a tag can be added to the blacklist.
        /// </summary>
        /// <param name="cmd">The command to execute</param>
        /// <param name="alreadyPresentError">The error message when a tag cannot be added because it is already present in the blacklist.</param>
        /// <param name="maximumReachedError">The error message when a tag cannot be added because the guild/user has reached its maximum of tags.</param>
        /// <returns></returns>
        private async Task EnsureTagsAllowedToAdd(NpgsqlCommand cmd, string alreadyPresentError, string maximumReachedError)
        {
            await using var reader = await cmd.ExecuteReaderAsync();

            await reader.ReadAsync();
            if (reader.GetBoolean(0))
            {
                await reader.CloseAsync();
                throw new BlacklistException("The tag is already present in the default blacklist.");
            }
            else if (reader.GetBoolean(1))
            {
                await reader.CloseAsync();
                throw new BlacklistException(alreadyPresentError);
            }
            else if (reader.GetBoolean(2))
            {
                await reader.CloseAsync();
                throw new BlacklistException(maximumReachedError);
            }
            await reader.CloseAsync();
        }

    }

    /// <summary>
    ///     A small custom exception class for proper control flow and handling cases when a blacklist operation cannot be completed.
    /// </summary>
    class BlacklistException : Exception
    {

        public BlacklistException(string message)
            : base(message)
        {
        }

    }
}
