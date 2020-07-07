using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using TeaBot.Attributes;
using TeaBot.Commands;
using TeaBot.Main;
using TeaBot.Preconditions;
using TeaBot.Webservices;
using TeaBot.Webservices.Rule34;
using TeaBot.Utilities;

namespace TeaBot.Modules
{
    [NSFW]
    [Summary("Commands for searching on rule34.xxx and managing the search.")]
    public class Rule34 : TeaInteractiveBase
    {
        private readonly Rule34BlacklistService _r34;

        public Rule34(Rule34BlacklistService r34)
        {
            _r34 = r34;
        }

        [Command("r34", RunMode = RunMode.Async)]
        [Summary("Searches an image with given tags on rule34.xxx")]
        [Note("Use space to split multiple tags. If a tag contains a space, use `_`. If you want to exclude a tag from your search without adding it to your blacklist, put a `-` before it. For the full list of tags, visit https://rule34.xxx/index.php?page=tags&s=list.")]
        [Ratelimit(2)]
        public async Task FindR34Post(
            [Summary("The tags to search for.")] params string[] tags
            )
        {
            var defaultBlacklist = _r34.DefaultBlacklist;
            var userBlacklist = await _r34.GetBlacklistAsync(Context.User);
            var guildBlacklist = Context.IsPrivate ? Enumerable.Empty<string>() : await _r34.GetBlacklistAsync(Context.Guild);

            Rule34Search search;
            try
            {
                search = new Rule34Search(tags, defaultBlacklist, userBlacklist, guildBlacklist);
            }
            catch (R34SearchException r34se)
            {
                await ReplyAsync(r34se.Message);
                return;
            }

            int count;
            string json;
            try
            {
                count = await search.GetResultCountAsync();

                if (count == 0)
                {
                    string toReply = "The search did not yield any results.";
                    if (tags.Count() > 1)
                        toReply += "\nAre you having a problem? Make sure to separate tags with a space and insert `_` in tags that contain spaces.";
                    await ReplyAsync(toReply);
                    return;
                }

                json = await search.GetRandomPostAsync(count);
            }
            catch (HttpRequestException)
            {
                await ReplyAsync("Something went wrong. Try again?");
                return;
            }
            catch (R34SearchException r34se)
            {
                await ReplyAsync(r34se.Message);
                return;
            }

            var post = Rule34Search.DeserializePost(json);

            DateTime creation = DateTime.ParseExact(post.Creation, "ddd MMM dd HH:mm:ss +0000 yyyy", new CultureInfo("en-US"));

            var postTags = post.Tags.TrimStart().TrimEnd().Split(" ");

            // Ensure the tags fit into the limit of 1024 (minus 2 because the tags are surrouned by `) characters
            string tagSplitter = ", ";
            var shortenedTags = postTags.Shorten(1022, tagSplitter).ToArray();

            var embed = new EmbedBuilder();
            embed.WithImageUrl(post.FileUrl)
                .AddField($"Tags{(shortenedTags.Length == postTags.Length ? "" : $" (displaying first {shortenedTags.Length} tags)")}", $"`{string.Join(tagSplitter, shortenedTags)}`")
                .WithFooter($"Uploaded {creation:dd.MM.yyyy HH:mm:ss} UTC | {count} result{(count == 1 ? "" : "s")} with this tag combination")
                .WithDescription("Type `d` to delete")
                .WithUrl(post.FileUrl)
                .WithColor(TeaEssentials.MainColor);

            IUserMessage msg1 = null;
            IUserMessage msg2 = null;

            if (post.Tags.Contains("webm"))
            {
                msg1 = await ReplyAsync(embed: embed.Build());
                msg2 = await ReplyAsync(post.FileUrl);
            }
            else
            {
                embed.WithTitle("Click here if the image is not loading");
                msg1 = await ReplyAsync(embed: embed.Build());
            }

            _ = Task.Run(async () =>
            {
                if (await NextMessageWithCondition(message => message.Content.Equals("d", StringComparison.OrdinalIgnoreCase), Context, 5, TimeSpan.FromSeconds(10)) != null)
                {
                    try
                    {
                        await msg1.DeleteAsync();
                        if (msg2 != null)
                            await msg2.DeleteAsync();
                    }
                    catch (Discord.Net.HttpException)
                    {

                    }
                }
            });

        }

        [Command("blacklist")]
        [Alias("bl")]
        [Summary("All tags that will be blocked in the rule34 command. Use these commands to manage your blacklist (with the prefix):\n" +
            "`bla` - add a tag to your blacklist\n" +
            "`blr` - remove a tag from your blacklist\n" +
            "`gbla` - add a tag to the server's blacklist\n" +
            "`glbr` - remove a tag from the server's blacklist")]
        [Ratelimit(10)]
        public async Task BlackList()
        {
            var defaultBlacklist = _r34.DefaultBlacklist;
            var userBlacklist = await _r34.GetBlacklistAsync(Context.User);

            var embed = new EmbedBuilder();

            embed.WithAuthor(Context.User)
                .WithCurrentTimestamp()
                .WithColor(TeaEssentials.MainColor)
                .WithDescription("This is the list of all tags that are blacklisted from your r34 search.")
                .AddField("Default blacklist (these cannot be changed)", FormatBlacklistedTags(defaultBlacklist))
                .AddField($"User blacklist (your blacklisted tags) - {Rule34BlacklistService.UserBlacklistLimit - userBlacklist.Count()} left", FormatBlacklistedTags(userBlacklist));

            if (!Context.IsPrivate)
            {
                IEnumerable<string> guildBlacklist = await _r34.GetBlacklistAsync(Context.Guild);
                embed.AddField($"Guild blacklist (tags that apply to this guild) - {Rule34BlacklistService.GuildBlacklistLimit - guildBlacklist.Count()} left", FormatBlacklistedTags(guildBlacklist));
            }

            await ReplyAsync(embed: embed.Build());

            static string FormatBlacklistedTags(IEnumerable<string> blacklist)
            {
                return blacklist.Count() == 0 ? "-" : string.Join(' ', blacklist.Select(x => $"`{x}`"));
            }
        }

        [Command("blacklistadd")]
        [Alias("bla", "bladd", "addbl")]
        [Summary("Adds a tag to your personal r34 search blacklist.")]
        [Ratelimit(3)]
        public async Task AddTag(
            [Summary("The tag to add to your blacklist.")] string tag
            )
        {
            tag = tag.ToLower();
            try
            {
                await _r34.AddTagToBlacklistAsync(Context.User, tag);
                await ReplyAsync($"Succesfully added the tag to your blacklist. `{tag}`");
            }
            catch (BlacklistException be)
            {
                await ReplyAsync(be.Message);
            }
        }

        [Command("blacklistremove")]
        [Alias("blr", "blremove", "removebl", "blrem")]
        [Summary("Removes a tag from your personal r34 search blacklist.")]
        [Ratelimit(3)]
        public async Task RemoveTag(
            [Summary("The tag to remove from your blacklist.")] string tag
            )
        {
            tag = tag.ToLower();
            try
            {
                await _r34.RemoveTagFromBlacklistAsync(Context.User, tag);
                await ReplyAsync($"Successfully removed the tag from your blacklist. `{tag}`");
            }
            catch (BlacklistException be)
            {
                await ReplyAsync(be.Message);
            }
        }

        [Command("guildblacklistadd")]
        [Alias("gbla", "gbladd", "guildbladd")]
        [Summary("Adds a tag to the guild's r34 search blacklist")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.Administrator, ErrorMessage = "You need to be an administrator to manage tags!")]
        [Ratelimit(3)]
        public async Task GuildAddTag(
            [Summary("The tag to add to the guild's blacklist.")] string tag
            )
        {
            tag = tag.ToLower();
            try
            {
                await _r34.AddTagToBlacklistAsync(Context.Guild, tag);
                await ReplyAsync($"Successfully added the tag to the guild's blacklist. `{tag}`");
            }
            catch (BlacklistException be)
            {
                await ReplyAsync(be.Message);
            }
        }

        [Command("guildblacklistremove")]
        [Alias("gblr", "gblrem", "guildblremove")]
        [Summary("Removes a tag from the guild's r34 search blacklist")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.Administrator, ErrorMessage = "You need to be an administrator to manage tags!")]
        [Ratelimit(3)]
        public async Task GuildRemoveTag(
            [Summary("The tag to remove from the guild's blacklist.")] string tag
            )
        {
            tag = tag.ToLower();
            try
            {
                await _r34.RemoveTagFromBlacklistAsync(Context.Guild, tag);
                await ReplyAsync($"Successfully removed the tag from the guild's blacklist. `{tag}`");
            }
            catch (BlacklistException be)
            {
                await ReplyAsync(be.Message);
            }
        }
    }
}
