using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using TeaBot.Attributes;
using TeaBot.Commands;
using TeaBot.Main;
using TeaBot.Preconditions;

namespace TeaBot.Modules
{
    [EssentialModule]
    [HelpCommandIgnore]
    [Summary("Commands that can only be executed in specific servers")]
    public class Exclusive : TeaInteractiveBase
    {
        [Command("anon")]
        [RequireContext(ContextType.DM)]
        [Ratelimit(2)]
        public async Task Anon([Remainder] string text)
        {
            var guild = Context.Client.GetGuild(573103176321073172);
            var channel = Context.Client.GetChannel(940766709831323658) as ITextChannel;
            if (guild.Users.Any(x => x.Id == Context.User.Id) && channel != null)
                await channel.SendMessageAsync(text);
        }

        [Command("quote")]
        [Exclusive(364771834325106689)]
        [Ratelimit(2)]
        public async Task Quote(IUser user = null)
        {
            var channel = Context.Client.GetChannel(639860672666271806) as ISocketMessageChannel;

            var quotes = await channel.GetMessagesAsync().FlattenAsync();
            quotes = quotes.Where(x => x.MentionedUserIds.Any());

            if (user != null)
                quotes = quotes.Where(quote => quote.MentionedUserIds.Contains(user.Id));

            int randomIndex = new Random().Next(0, quotes.Count());
            var quote = quotes.ElementAt(randomIndex);

            var embed = new EmbedBuilder();

            embed.WithColor(TeaEssentials.MainColor)
                .WithDescription(quote.Content)
                .WithAuthor(quote.Author)
                .WithFooter(quote.CreatedAt.DateTime.ToString("dd.MM.yyyy HH:mm:ss"));

            if (quote.Attachments.Any())
                embed.WithImageUrl(quote.Attachments.First().Url);
            else if (quote.Embeds.Any())
                embed.WithImageUrl(quote.Embeds.First().Url);

            await ReplyAsync(embed: embed.Build());
        }

        [Command("quotestats", RunMode = RunMode.Async)]
        [Exclusive(364771834325106689)]
        [Ratelimit(10)]
        public async Task QuoteStats()
        {
            var channel = Context.Client.GetChannel(639860672666271806) as ISocketMessageChannel;
            var quotes = await channel.GetMessagesAsync().FlattenAsync();

            List<(ulong, int)> topUsers = new List<(ulong, int)>();
            foreach (var quote in quotes)
            {
                foreach (var mentionedId in quote.MentionedUserIds.Distinct())
                {
                    var index = topUsers.FindIndex(x => x.Item1 == mentionedId);
                    if (index == -1)
                        topUsers.Add((mentionedId, 1));
                    else
                        topUsers[index] = (mentionedId, topUsers[index].Item2 + 1);
                }
            }

            topUsers = topUsers.OrderByDescending(x => x.Item2).ToList();

            var embed = new EmbedBuilder();

            embed.WithAuthor(Context.User)
                .WithColor(TeaEssentials.MainColor)
                .WithDescription(string.Join("\n", topUsers.Select((user, index) => $"{index + 1}. <@{user.Item1}> - {user.Item2} quotes")))
                .WithTitle("Poppontheon quote top")
                .WithFooter($"Quote count: {quotes.Count()}");

            await ReplyAsync(embed: embed.Build());
        }
    }
}
