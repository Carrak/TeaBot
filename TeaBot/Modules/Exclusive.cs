﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using TeaBot.Attributes;
using TeaBot.Commands;
using TeaBot.Main;
using TeaBot.Preconditions;
using System.Collections.Generic;

namespace TeaBot.Modules
{
    [HelpCommandIgnore]
    [Summary("Commands that can only be executed in specific servers")]
    public class Exclusive : TeaInteractiveBase
    {
        [Command("quote", true)]
        [Exclusive(364771834325106689)]
        [Ratelimit(2, Measure.Seconds)]
        public async Task Quote()
        {
            var channel = Context.Client.GetChannel(639860672666271806) as ISocketMessageChannel;
            var quotes = await channel.GetMessagesAsync().FlattenAsync();
            int randomIndex = new Random().Next(0, quotes.Count());
            var quote = quotes.ElementAt(randomIndex);

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

        [Command("quotestats")]
        [Exclusive(364771834325106689)]
        [Ratelimit(10, Measure.Seconds)]
        public async Task QuoteStats()
        {
            var channel = Context.Client.GetChannel(639860672666271806) as ISocketMessageChannel;
            var quotes = await channel.GetMessagesAsync().FlattenAsync();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            List<(ulong, int)> topUsers = new List<(ulong, int)>();
            foreach (var quote in quotes)
            {
                foreach (var mentionedId in quote.MentionedUserIds)
                {
                    var index = topUsers.FindIndex(x => x.Item1 == mentionedId);
                    if (index == -1)
                        topUsers.Add((mentionedId, 1));
                    else
                        topUsers[index] = (mentionedId, topUsers[index].Item2 + 1);
                }
            }

            topUsers = topUsers.OrderByDescending(x => x.Item2).ToList();
            Console.WriteLine(sw.ElapsedMilliseconds);
            var embed = new EmbedBuilder();

            embed.WithAuthor(Context.User)
                .WithColor(TeaEssentials.MainColor)
                .WithDescription(string.Join("\n", topUsers.Select((user, index) => $"{index+1}. <@{user.Item1}> - {user.Item2} quotes")))
                .WithTitle("Poppontheon quote top");

            await ReplyAsync(embed: embed.Build());
        }
    }
}
