using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Nekos.Net;
using Nekos.Net.Endpoints;
using Nekos.Net.Responses;
using TeaBot.Commands;
using TeaBot.Main;
using TeaBot.Preconditions;

namespace TeaBot.Modules
{
    [NSFW]
    [Summary("Commands that can only be executed in NSFW channels")]
    public class NSFW : TeaInteractiveBase
    {
        [Command("nhentai")]
        [Alias("nh")]
        [Summary("Sends the specified doujin")]
        [Ratelimit(3)]
        public async Task Nhentai(
            [Summary("The ID of the doujinshi. Aka the 6 digits (or less).")] int doujinID
            )
        {
            if (doujinID <= 0)
            {
                await ReplyAsync("The ID must be above 0!");
                return;
            }

            await ReplyAsync("https://nhentai.net/g/" + doujinID);
        }

        [Command("nhentai")]
        [Alias("nh")]
        [Summary("Sends a random doujin")]
        [Ratelimit(3)]
        public async Task Nhentai()
        {
            int randomDoujin = new Random().Next(1, 305878);
            await ReplyAsync("https://nhentai.net/g/" + randomDoujin);
            return;
        }

        [Command("hentai", RunMode = RunMode.Async)]
        [Summary("Search for a random NSFW image or gif on nekos.life. Use the `hentaitags` command for the list of tags.")]
        [Ratelimit(3)]
        public async Task Hentai(
            [Summary("The tag to look for. Leave empty for a random tag.")] string tag = null
            )
        {
            NekosImage image;

            // Retrieve the image
            try
            {
                var allowed = GetAllowedIndexes();
                if (tag is null)
                {
                    int index = allowed.ElementAt(new Random().Next(0, allowed.Count()));
                    image = await NekosClient.GetNsfwAsync((NsfwEndpoint)index);
                }
                else
                {
                    int index = Array.IndexOf(GetAllowedNames().Select(x => x.ToLower()).ToArray(), tag.ToLower());
                    if (index == -1)
                    {
                        await ReplyAsync($"No such tag exists! See `{Context.Prefix}hentaitags` for the list of tags.");
                        return;
                    }
                    int endpoint = GetAllowedIndexes().ElementAt(index);
                    image = await NekosClient.GetNsfwAsync((NsfwEndpoint)endpoint);
                }
            }
            catch (HttpRequestException)
            {
                await ReplyAsync("Something went wrong. Try again?");
                return;
            }

            // Construct the embed
            var embed = new EmbedBuilder();
            embed.WithColor(TeaEssentials.MainColor)
                .WithImageUrl(image.FileUrl)
                .WithDescription("Type `d` to delete")
                .WithTitle("Source image")
                .WithFooter("https://nekos.life/")
                .WithUrl(image.FileUrl);

            var msg = await ReplyAsync(embed: embed.Build());

            _ = Task.Run(async () =>
            {
                if (await NextMessageWithCondition(message => message.Content.Equals("d", StringComparison.OrdinalIgnoreCase), Context, 5, TimeSpan.FromSeconds(10)) != null)
                {
                    try
                    {
                        await msg.DeleteAsync();
                    }
                    catch (Discord.Net.HttpException)
                    {

                    }
                }
            });

        }

        [Command("hentaitags")]
        [Summary("Represents the list of tags for the `hentai` command.")]
        public async Task HentaiTags()
        {
            string tags = string.Join(", ", GetAllowedNames().Select(x => $"`{x}`"));

            var footer = new EmbedFooterBuilder()
            {
                Text = Context.User.ToString(),
                IconUrl = Context.User.GetAvatarUrl()
            };

            var embed = new EmbedBuilder();
            embed.WithColor(TeaEssentials.MainColor)
                .WithTitle("Tags for the the hentai command")
                .WithDescription(tags)
                .WithFooter(footer);

            await ReplyAsync(embed: embed.Build());
        }

        // A couple of utility methods for nekos.life NSFW search because of the tags appear to be broken and should be excluded from everywhere

        private static IEnumerable<string> GetAllowedNames()
        {
            var excluded = GetExcludedIndexes();
            return Enum.GetNames(typeof(NsfwEndpoint)).Where((x, index) => !excluded.Contains(index));
        }

        private static IEnumerable<int> GetAllowedIndexes()
        {
            HashSet<int> excluded = GetExcludedIndexes();
            return Enumerable.Range(0, Enum.GetNames(typeof(NsfwEndpoint)).Length).Where(x => !excluded.Contains(x));
        }

        private static HashSet<int> GetExcludedIndexes()
        {
            return new HashSet<int>() { 2, 11, 20, 21 };
        }
    }
}
