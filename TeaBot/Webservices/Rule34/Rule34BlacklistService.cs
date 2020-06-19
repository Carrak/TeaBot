using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TeaBot.Services;

namespace TeaBot.Webservices.Rule34
{
    /// <summary>
    ///     Class for managing rule34 search blacklists
    /// </summary>
    public class Rule34BlacklistService
    {
        private readonly DatabaseService _database;

        public const int UserBlacklistLimit = 20;
        public const int GuildBlacklistLimit = 20;

        public enum R34BlacklistType
        {
            Default,
            Guild,
            User
        }

        public Rule34BlacklistService(DatabaseService database)
        {
            _database = database;
        }

        public async Task<IEnumerable<string>> GetBlacklistAsync(R34BlacklistType blacklistType, ulong? id = null)
        {
            string query = blacklistType switch
            {
                R34BlacklistType.Default when id is null => "SELECT r34.default_blacklist.tag FROM r34.default_blacklist",
                R34BlacklistType.User => $"SELECT r34.user_blacklist.tag FROM r34.user_blacklist WHERE userid={id}",
                R34BlacklistType.Guild => $"SELECT r34.guild_blacklist.tag FROM r34.guild_blacklist WHERE guildid={id}",
                _ => throw new NotImplementedException()
            };

            await using var cmd = _database.GetCommand(query);
            await using var reader = await cmd.ExecuteReaderAsync();

            List<string> blacklist = new List<string>();
            while (await reader.ReadAsync())
                blacklist.Add(reader.GetString(0));
            await reader.CloseAsync();

            return blacklist;
        }

        public async Task AddToBlacklistAsync(R34BlacklistType type, ulong id, string tag)
        {
            string conditionsQuery;
            string insertQuery;
            string alreadyPresentError;
            string maximumReachedError;
            switch (type)
            {
                case R34BlacklistType.User:
                    conditionsQuery = $"SELECT EXISTS(SELECT * FROM r34.default_blacklist WHERE tag='{tag}'), " +
                            $"EXISTS (SELECT * FROM r34.user_blacklist WHERE tag='{tag}' AND userid={id}), " +
                            $"COUNT(*) >= {UserBlacklistLimit} FROM r34.user_blacklist WHERE userid={id}";
                    insertQuery = $"INSERT INTO r34.user_blacklist (userid, tag) VALUES ({id}, '{tag}')";
                    alreadyPresentError = $"The tag is already present in your personal blacklist. `{tag}`";
                    maximumReachedError = "You've reached your maximum of tags.";
                    break;
                case R34BlacklistType.Guild:
                    conditionsQuery = $"SELECT EXISTS(SELECT * FROM r34.default_blacklist WHERE tag='{tag}'), " +
                            $"EXISTS (SELECT * FROM r34.guild_blacklist WHERE tag='{tag}' AND guildid={id}), " +
                            $"COUNT(*) >= {GuildBlacklistLimit} FROM r34.guild_blacklist WHERE guildid={id}";
                    insertQuery = $"INSERT INTO r34.guild_blacklist (guildid, tag) VALUES ({id}, '{tag}')";
                    alreadyPresentError = $"The tag is already present in the guild's blacklist. `{tag}`";
                    maximumReachedError = "The guild has reached its maximum of tags.";
                    break;
                default:
                    throw new NotImplementedException();
            }

            await using var cmd1 = _database.GetCommand(conditionsQuery);
            await using var reader = await cmd1.ExecuteReaderAsync();

            await reader.ReadAsync();
            if (reader.GetBoolean(0))
                throw new BlacklistException("The tag is already present in the default blacklist.");
            else if (reader.GetBoolean(1))
                throw new BlacklistException(alreadyPresentError);
            else if (reader.GetBoolean(2))
                throw new BlacklistException(maximumReachedError);
            await reader.CloseAsync();

            await using var cmd2 = _database.GetCommand(insertQuery);
            await cmd2.ExecuteNonQueryAsync();
        }

        public async Task<bool> RemoveTagFromBlacklistAsync(R34BlacklistType type, ulong id, string tag)
        {
            string query = type switch
            {
                R34BlacklistType.Guild => $"DELETE FROM r34.guild_blacklist WHERE tag='{tag}' AND guildid={id}",
                R34BlacklistType.User => $"DELETE FROM r34.user_blacklist WHERE tag='{tag}' AND userid={id}",
                _ => throw new NotImplementedException()
            };
            await using var cmd = _database.GetCommand(query);
            int rowsAffected = await cmd.ExecuteNonQueryAsync();
            return rowsAffected == 1;
        }
    }

    class BlacklistException : Exception
    {

        public BlacklistException(string message)
            : base(message)
        {
        }

    }
}
