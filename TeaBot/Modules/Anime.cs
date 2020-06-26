using System;
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
    [Summary("Commands dedicated to anime or searching anime art. (SFW)")]
    public class Anime : TeaInteractiveBase
    {
        [Command("kemo")]
        [Summary("Search for a random SFW image or gif of a kemonomimi girl on nekos.life")]
        [Ratelimit(3)]
        public async Task Nekos()
        {
            NekosImage image;

            // Retrieve the image
            try
            {
                SfwEndpoint[] endpoints = new[] { SfwEndpoint.Neko, SfwEndpoint.Fox_Girl };
                image = await NekosClient.GetSfwAsync(endpoints[new Random().Next(0, endpoints.Length)]);
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
                .WithTitle("Source image")
                .WithFooter("https://nekos.life/")
                .WithUrl(image.FileUrl);

            await ReplyAsync(embed: embed.Build());
        }
    }
}
