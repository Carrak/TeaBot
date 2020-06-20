using System;
using System.Threading.Tasks;
using Discord.Commands;
using TeaBot.Commands;
using TeaBot.Preconditions;
using Nekos;
using Nekos.Net.Responses;
using Nekos.Net.Endpoints;
using System.Net.Http;
using Discord;
using TeaBot.Main;
using Nekos.Net;
using System.Linq;
using System.Collections.Generic;

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
        public async Task Nhentai(int doujinID)
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

        [Command("hentai")]
        [Summary("Search for a random NSFW image or gif on nekos.life")]
        [Ratelimit(3)]
        public async Task Hentai(string tag = null)
        {
            NekosImage image;

            // Retrieve the image
            try
            {
                var allowed = GetAllowedIndexes();
                if (tag is null)
                {
                    int index = allowed.ElementAt(new Random().Next(0, allowed.Count()));
                    Console.WriteLine((NsfwEndpoint)index);
                    image = await NekosClient.GetNsfwAsync((NsfwEndpoint)index);
                }
                else
                {
                    int index = allowed.ElementAt(Array.IndexOf(GetAllowedNames().ToArray(), tag.ToLower()));
                    if (index == -1)
                    {
                        await ReplyAsync($"No such tag exists! See `{Context.Prefix}hentaitags` for the full list.");
                        return;
                    }
                    image = await NekosClient.GetNsfwAsync((NsfwEndpoint)index);
                }
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine(e.Message);
                await ReplyAsync("Something went wrong. Try again?");
                return;
            }

            // Construct the embed
            var embed = new EmbedBuilder();
            embed.WithColor(TeaEssentials.MainColor)
                .WithImageUrl(image.FileUrl)
                .WithTitle("Source image")
                .WithFooter("https://nekos.life/")
                .WithUrl(image.FileUrl);

            await ReplyAsync(embed: embed.Build());
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

        private static IEnumerable<string> GetAllowedNames()
        {
            var excluded = GetExcludedIndexes();
            return Enum.GetNames(typeof(NsfwEndpoint)).Where((x, index) => !excluded.Contains(index)).Select(x => x.ToLower());
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
