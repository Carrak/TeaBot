using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using TeaBot.Attributes;
using TeaBot.Commands;
using TeaBot.Preconditions;
using TeaBot.Utilities;
using TeaBot.Main;

namespace TeaBot.Modules
{
    [HelpCommandIgnore]
    [Summary("Commands that can only be executed in specific servers")]
    public class Exclusive : TeaInteractiveBase
    {
        [Command("quote", true)]
        [Exclusive(364771834325106689)]
        [Ratelimit(5, Measure.Seconds)]
        public async Task Quote()
        {
            var channel = Context.Client.GetChannel(639860672666271806) as ISocketMessageChannel;
            var messages = await channel.GetMessagesAsync().FlattenAsync();
            int randomIndex = new Random().Next(0, messages.Count());
            var quote = messages.ElementAt(randomIndex);

            var embed = new EmbedBuilder();

            embed.WithColor(TeaEssentials.MainColor)
                .WithDescription(quote.Content)
                .WithAuthor(quote.Author)
                .WithFooter(quote.CreatedAt.DateTime.ToString("dd.MM.yyyy HH:mm:ss"));

            if (quote.Attachments.Count() > 0)
                embed.WithImageUrl(quote.Attachments.First().Url);
            else if (quote.Embeds.Count() > 0)
                embed.WithImageUrl(quote.Embeds.First().Url);
            
            
            await ReplyAsync(embed: embed.Build());
        }
    }
}
