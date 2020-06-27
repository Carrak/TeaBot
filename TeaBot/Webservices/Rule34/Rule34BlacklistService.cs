using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Microsoft.Extensions.Caching.Memory;
using Npgsql;
using TeaBot.Services;

namespace TeaBot.Webservices.Rule34
{
    /// <summary>
    ///     Class for managing rule34 search blacklists.
    /// </summary>
    public class Rule34BlacklistService
    {
        private readonly DatabaseService _database;

        /// <summary>
        ///     The cache to use for blacklists.
        /// </summary>
        private readonly MemoryCache _cache = new MemoryCache(new MemoryCacheOptions());

        /// <summary>
        ///     The list of tags that are blocked by default.
        /// </summary>
        public IEnumerable<string> DefaultBlacklist { get; private set; }

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
        ///     Retrieve tags that are blocked by default.
        /// </summary>
        public async Task InitDefaultBlacklistAsync()
        {
            DefaultBlacklist = await ExecuteBlacklistQueryAsync(_database.GetCommand("SELECT r34.default_blacklist.tag FROM r34.default_blacklist"));
        }

        /// <summary>
        ///     Gets the tags that are blocked by a user.
        /// </summary>
        /// <param name="user">The user to retrieve the blacklist for.</param>
        /// <returns>Collection of tags.</returns>
        public async Task<IEnumerable<string>> GetBlacklistAsync(IUser user)
        {
            if (_cache.TryGetValue<IEnumerable<string>>(user, out var bl))
            {
                return bl;
            }
            else
            {
                // retrieve from db
                var cmd = _database.GetCommand($"SELECT r34.user_blacklist.tag FROM r34.user_blacklist WHERE userid=@uid");
                cmd.Parameters.AddWithValue("uid", (long)user.Id);
                var blacklist = await ExecuteBlacklistQueryAsync(cmd);

                // add to cache
                AddToCacheOrSet(user, blacklist);

                return blacklist;
            }
        }

        /// <summary>
        ///     Gets the tags that are blocked by a guild.
        /// </summary>
        /// <param name="guild">The guild to retrieve the blacklist for.</param>
        /// <returns>Collection of tags.</returns>
        public async Task<IEnumerable<string>> GetBlacklistAsync(IGuild guild)
        {
            if (_cache.TryGetValue<IEnumerable<string>>(guild, out var bl))
            {
                return bl;
            }
            else
            {
                // retrieve from db
                var cmd = _database.GetCommand($"SELECT r34.guild_blacklist.tag FROM r34.guild_blacklist WHERE guildid=@gid");
                cmd.Parameters.AddWithValue("gid", (long)guild.Id);
                var blacklist = await ExecuteBlacklistQueryAsync(cmd);

                // add to cache
                AddToCacheOrSet(guild, blacklist);

                return blacklist;
            }
        }

        /// <summary>
        ///     Adds a tag to a user's personal blacklist.
        /// </summary>
        /// <param name="user">The user whose blacklist is used.</param>
        /// <param name="tag">The tag to add to the blacklist.</param>
        public async Task AddTagToBlacklistAsync(IUser user, string tag)
        {
            // check if tag can be added
            if (DefaultBlacklist.Contains(tag))
                throw new BlacklistException($"The tag is already present in the default blacklist `{tag}`");

            var blacklist = await GetBlacklistAsync(user);

            if (blacklist.Contains(tag))
                throw new BlacklistException($"The tag is already present in your personal blacklst. `{tag}`");
            else if (blacklist.Count() >= UserBlacklistLimit)
                throw new BlacklistException("You've reached your maximum of tags.");

            // add to cache
            var list = blacklist.ToList();
            list.Add(tag);
            AddToCacheOrSet(user, list);

            // add to db
            var cmd = _database.GetCommand($"INSERT INTO r34.user_blacklist (userid, tag) VALUES (@uid, @tag)");
            cmd.Parameters.AddWithValue("uid", (long)user.Id);
            cmd.Parameters.AddWithValue("tag", tag);

            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        ///     Adds a tag to a guild's blacklist.
        /// </summary>
        /// <param name="guild">The guild to add a tag to.</param>
        /// <param name="tag">The tag to add to the blacklist.</param>
        public async Task AddTagToBlacklistAsync(IGuild guild, string tag)
        {
            // check if tag can be added
            if (DefaultBlacklist.Contains(tag))
                throw new BlacklistException($"The tag is already present in the default blacklist `{tag}`");

            var blacklist = await GetBlacklistAsync(guild);

            if (blacklist.Contains(tag))
                throw new BlacklistException($"The tag is already present in the guild's blacklist. `{tag}`");
            else if (blacklist.Count() >= GuildBlacklistLimit)
                throw new BlacklistException("The guild has reached its maximum of tags.");

            // add to cache
            var list = blacklist.ToList();
            list.Add(tag);
            AddToCacheOrSet(guild, list);

            // add to db
            var cmd = _database.GetCommand($"INSERT INTO r34.guild_blacklist (guildid, tag) VALUES (@gid, @tag)");
            cmd.Parameters.AddWithValue("gid", (long)guild.Id);
            cmd.Parameters.AddWithValue("tag", tag);

            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        ///     Removes a tag from a guild's blacklist.
        /// </summary>
        /// <param name="guild">The guild to remove a tag from.</param>
        /// <param name="tag">The tag to remove.</param>
        public async Task RemoveTagFromBlacklistAsync(IGuild guild, string tag)
        {
            if (_cache.TryGetValue<IEnumerable<string>>(guild, out var blacklist))
            {
                var list = blacklist.ToList();
                list.Remove(tag);
                AddToCacheOrSet(guild, list);
            }

            var cmd = _database.GetCommand($"DELETE FROM r34.guild_blacklist WHERE tag=@tag AND guildid=@gid");
            cmd.Parameters.AddWithValue("tag", tag);
            cmd.Parameters.AddWithValue("gid", (long)guild.Id);

            int rowsAffected = await cmd.ExecuteNonQueryAsync();
            if (rowsAffected != 1)
                throw new BlacklistException($"This tag is not blocked for this guild. `{tag}`");
        }

        /// <summary>
        ///     Removes a tag from a user's personal blacklist.
        /// </summary>
        /// <param name="user">The user whose blacklist is used.</param>
        /// <param name="tag">The tag to remove.</param>
        public async Task RemoveTagFromBlacklistAsync(IUser user, string tag)
        {
            if (_cache.TryGetValue<IEnumerable<string>>(user, out var blacklist))
            {
                var list = blacklist.ToList();
                list.Remove(tag);
                AddToCacheOrSet(user, list);
            }

            var cmd = _database.GetCommand($"DELETE FROM r34.user_blacklist WHERE tag=@tag AND userid=@uid");
            cmd.Parameters.AddWithValue("tag", tag);
            cmd.Parameters.AddWithValue("uid", (long)user.Id);

            int rowsAffected = await cmd.ExecuteNonQueryAsync();
            if (rowsAffected != 1)
                throw new BlacklistException($"This tag is not present in your blacklist. `{tag}`");
        }

        /// <summary>
        ///     Gets the blacklist from the database using the provided command.
        /// </summary>
        /// <param name="cmd">The command to execute to retrieve the blacklist.</param>
        /// <returns>Collection of tags.</returns>
        private async Task<IEnumerable<string>> ExecuteBlacklistQueryAsync(NpgsqlCommand cmd)
        {
            await using var reader = await cmd.ExecuteReaderAsync();

            List<string> blacklist = new List<string>();
            while (await reader.ReadAsync())
                blacklist.Add(reader.GetString(0));
            await reader.CloseAsync();

            return blacklist;
        }

        /// <summary>
        ///     Adds or overwrites an object in cache.
        /// </summary>
        /// <param name="key">The key of the object.</param>
        /// <param name="value">The value to set.</param>
        private void AddToCacheOrSet(object key, object value)
        {
            var options = new MemoryCacheEntryOptions()
            {
                SlidingExpiration = TimeSpan.FromMinutes(10)
            };

            _cache.Set(key, value, options);
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
