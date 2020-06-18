using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Npgsql;
using TeaBot.Attributes;
using TeaBot.Commands;
using TeaBot.Main;
using TeaBot.Preconditions;
using TeaBot.Services;
using TeaBot.Webservices;


namespace TeaBot.Modules
{
    [RequireNsfw(ErrorMessage = "The channel is not flagged as NSFW!")]
    [Summary("Commands for searching on rule34.xxx and managing the search.")]
    public class Rule34 : TeaInteractiveBase
    {
        private readonly DatabaseService _database;

        public Rule34(DatabaseService database)
        {
            _database = database;
        }

        [Command("r34", RunMode = RunMode.Async)]
        [Summary("Searches an image with given tags on rule34.xxx")]
        [Note("Use space to split multiple tags. If a tag contains a space, use `_`. If you want to exclude a tag from your search without adding it to your blacklist, put a `-` before it. For the full list of tags, visit https://rule34.xxx/index.php?page=tags&s=list.")]
        [Ratelimit(2, Measure.Seconds)]
        public async Task FindR34Post([Remainder] string tags)
        {
            List<string> blacklist = new List<string>();
            string prefix = Context.Prefix;

            var defaultBlacklist = await GetBlacklist(R34BlacklistType.Default);
            var userBlacklist = await GetBlacklist(R34BlacklistType.User);

            if (await CheckTags(tags, defaultBlacklist, $"Your tag combination contains tags blacklisted by default.\nDo `{prefix}blacklist` for more info.") ||
                await CheckTags(tags, userBlacklist, $"Your tag combination contains tags blacklisted by you.\nDo `{prefix}blacklist` for more info."))
                return;

            blacklist.AddRange(defaultBlacklist);
            blacklist.AddRange(userBlacklist);

            if (!Context.IsPrivate)
            {
                var guildBlacklist = await GetBlacklist(R34BlacklistType.Guild);
                if (await CheckTags(tags, guildBlacklist, $"Your tag combination contains tags blacklisted by the guild.\nDo `{prefix}blacklist` for more info."))
                    return;
                blacklist.AddRange(guildBlacklist);
            }

            bool hasSpace = tags.Contains(' ');
            tags += ' ' + string.Join(' ', blacklist.Select(x => $"-{x}"));

            int count = await Rule34Search.GetResultCountAsync(tags);
            if (count == 0)
            {
                string noResultsMessage = "The search did not yield any results.";
                if (hasSpace)
                    noResultsMessage += "\nAre you having a problem? Make sure to separate tags with a space and insert `_` instead of spaces in tags that contain them.";
                await ReplyAsync(noResultsMessage);
                return;
            }

            string json = await Rule34Search.GetRandomPostAsync(tags, count);
            if (json is null)
            {
                await ReplyAsync("Something went wrong.");
            }

            var post = Rule34Search.DeserializePost(json);

            DateTime creation = DateTime.ParseExact(post.Creation, "ddd MMM dd HH:mm:ss +0000 yyyy", new CultureInfo("en-US"));

            var embed = new EmbedBuilder();
            embed.WithImageUrl(post.FileUrl)
                .AddField("Tags", $"`{post.Tags.TrimStart().TrimEnd().Replace(" ", ", ")}`")
                .WithFooter($"Uploaded {creation:dd.MM.yyyy HH:mm:ss} UTC | {count} result{(count == 1 ? "" : "s")} with this tag combination")
                .WithUrl(post.FileUrl)
                .WithColor(TeaEssentials.MainColor);

            if (post.Tags.Contains("webm"))
            {
                await ReplyAsync(embed: embed.Build());
                await ReplyAsync(post.FileUrl);
            }
            else
            {
                embed.WithTitle("Click here if the image is not loading");
                await ReplyAsync(embed: embed.Build());
            }

        }

        [Command("blacklist")]
        [Alias("bl")]
        [Summary("All tags that will be blocked in the rule34 command. Use these commands to manage your blacklist (with the prefix):\n" +
            "`bla` - add a tag to your blacklist\n" +
            "`blr` - remove a tag from your blacklist\n" +
            "`gbla` - add a tag to the server's blacklist\n" +
            "`glbr` - remove a tag from the server's blacklist")]
        public async Task BlackList()
        {
            List<string> defaultBlacklist = await GetBlacklist(R34BlacklistType.Default);
            List<string> userBlacklist = await GetBlacklist(R34BlacklistType.User);

            var embed = new EmbedBuilder();

            embed.WithAuthor(Context.User)
                .WithCurrentTimestamp()
                .WithColor(TeaEssentials.MainColor)
                .WithDescription("This is the list of all tags that are blacklisted from your r34 search.")
                .AddField("Default blacklist (these cannot be changed)", FormatBlacklistedTags(defaultBlacklist))
                .AddField($"User blacklist (your blacklisted tags) - {userBlacklistLimit - userBlacklist.Count} left", FormatBlacklistedTags(userBlacklist));

            if (!Context.IsPrivate)
            {
                List<string> guildBlacklist = await GetBlacklist(R34BlacklistType.Guild);
                embed.AddField($"Guild blacklist (tags that apply to this server) - {guildBlacklistLimit - guildBlacklist.Count} left", FormatBlacklistedTags(guildBlacklist));
            }

            await ReplyAsync(embed: embed.Build());

            static string FormatBlacklistedTags(List<string> blacklist)
            {
                return blacklist.Count == 0 ? "-" : string.Join(' ', blacklist.Select(x => $"`{x}`"));
            }
        }

        [Command("blacklistadd")]
        [Alias("bla", "bladd", "addbl")]
        [Summary("Adds a tag to your personal r34 search blacklist.")]
        public async Task AddTag(string tag)
        {
            await AddToBlacklist(R34BlacklistType.User, tag);
        }

        [Command("blacklistremove")]
        [Alias("blr", "blremove", "removebl", "blrem")]
        [Summary("Removes a tag from your personal r34 search blacklist.")]
        public async Task RemoveTag(string tag)
        {
            await RemoveTagFromBlacklist(R34BlacklistType.User, tag);
        }

        [Command("guildblacklistadd")]
        [Alias("gbla", "gbladd", "guildbladd")]
        [Summary("Adds a tag to the guild's r34 search blacklist")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.Administrator, ErrorMessage = "You need to be an administrator to manage tags!")]
        public async Task GuildAddTag(string tag)
        {
            await AddToBlacklist(R34BlacklistType.Guild, tag);
        }

        [Command("guildblacklistremove")]
        [Alias("gblr", "gblrem", "guildblremove")]
        [Summary("Removes a tag from the guild's r34 search blacklist")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.Administrator, ErrorMessage = "You need to be an administrator to manage tags!")]
        public async Task GuildRemoveTag(string tag)
        {
            await RemoveTagFromBlacklist(R34BlacklistType.Guild, tag);
        }

        #region Utility

        private readonly int userBlacklistLimit = 20;
        private readonly int guildBlacklistLimit = 20;

        private enum R34BlacklistType
        {
            Default,
            Guild,
            User
        }

        private async Task RemoveTagFromBlacklist(R34BlacklistType blacklistType, string tag)
        {
            string idName;
            string blacklistName;
            ulong id;
            string successMessage;
            string tagNotPresentMessage;

            switch (blacklistType)
            {
                case R34BlacklistType.User:
                    idName = "userid";
                    blacklistName = "user_blacklist";
                    id = Context.User.Id;
                    successMessage = $"Successfully removed `{tag}` from your blacklist";
                    tagNotPresentMessage = $"`{tag}` is not in your blacklist.";
                    break;
                case R34BlacklistType.Guild:
                    idName = "guildid";
                    blacklistName = "guild_blacklist";
                    id = Context.Guild.Id;
                    successMessage = $"Successfully removed `{tag}` from the guild's blacklist";
                    tagNotPresentMessage = $"`{tag}` is not in the guild's blacklist.";
                    break;
                default:
                    return;
            }

            string query = $"DELETE FROM r34.{blacklistName} WHERE tag='{tag}' AND {idName}={id}";
            await using var cmd = _database.GetCommand(query);
            int rowsAffected = await cmd.ExecuteNonQueryAsync();

            if (rowsAffected >= 1)
            {
                await ReplyAsync(successMessage);
                return;
            }
            else
            {
                await ReplyAsync(tagNotPresentMessage);
                return;
            }
        }

        private async Task AddToBlacklist(R34BlacklistType blacklistType, string tag)
        {
            int limit;
            string blacklistName;
            string idName;
            ulong id;
            string tagPresentMessage;
            string limitExceededMessage;
            string successMessage;

            switch (blacklistType)
            {
                case R34BlacklistType.User:
                    limit = userBlacklistLimit;
                    blacklistName = "user_blacklist";
                    idName = "userid";
                    id = Context.User.Id;
                    tagPresentMessage = "The tag is already present in your personal blacklist.";
                    limitExceededMessage = "You've reached your maximum of tags";
                    successMessage = $"Successfully added `{tag}` to your personal blacklist.";
                    break;
                case R34BlacklistType.Guild:
                    limit = guildBlacklistLimit;
                    blacklistName = "guild_blacklist";
                    idName = "guildid";
                    id = Context.Guild.Id;
                    tagPresentMessage = "The tag is already present in the guild's blacklist.";
                    limitExceededMessage = "The guild has reached its maximum of tags.";
                    successMessage = $"Successfully added `{tag}` to the guild's blacklist.";
                    break;
                default:
                    return;
            }

            string conditionsQuery = $"SELECT EXISTS(SELECT * FROM r34.default_blacklist WHERE tag='{tag}'), " +
                            $"EXISTS (SELECT * FROM r34.{blacklistName} WHERE tag='{tag}' AND {idName}={id}), " +
                            $"COUNT(*) >= {limit} FROM r34.{blacklistName} WHERE {idName}={id}";

            await using var cmd1 = _database.GetCommand(conditionsQuery);
            await using var reader = await cmd1.ExecuteReaderAsync();

            await reader.ReadAsync();
            if (reader.GetBoolean(0))
            {
                await ReplyAsync("The tag is already present in the default blacklist.");
                return;
            }
            else if (reader.GetBoolean(1))
            {
                await ReplyAsync(tagPresentMessage);
                return;
            }
            else if (reader.GetBoolean(2))
            {
                await ReplyAsync(limitExceededMessage);
                return;
            }
            await reader.CloseAsync();

            string query = $"INSERT INTO r34.{blacklistName} ({idName}, tag) VALUES ({id}, '{tag}')";
            await using var cmd2 = _database.GetCommand(query);
            await cmd2.ExecuteNonQueryAsync();

            await ReplyAsync(successMessage);
        }

        private async Task<List<string>> GetBlacklist(R34BlacklistType blacklistType)
        {
            string query = blacklistType switch
            {
                R34BlacklistType.Default => "SELECT * FROM r34.default_blacklist",
                R34BlacklistType.User => $"SELECT r34.user_blacklist.tag FROM r34.user_blacklist WHERE userid={Context.User.Id}",
                R34BlacklistType.Guild => $"SELECT r34.guild_blacklist.tag FROM r34.guild_blacklist WHERE guildid={Context.Guild.Id}",
                _ => null
            };

            await using var cmd = _database.GetCommand(query);
            await using var reader = await cmd.ExecuteReaderAsync();

            List<string> blacklist = new List<string>();
            while (await reader.ReadAsync())
                blacklist.Add(reader.GetString(0));

            await reader.CloseAsync();
            return blacklist;
        }

        private Task<bool> CheckTags(string tags, List<string> blacklist, string errorMessage)
        {
            var tagsArr = tags.Split(" ");
            if (blacklist.Any(tag => tagsArr.Contains(tag)))
            {
                Context.Channel.SendMessageAsync(errorMessage);
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        #endregion

    }
}
