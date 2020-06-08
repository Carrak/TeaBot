using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using TeaBot.Attributes;
using TeaBot.Preconditions;
using TeaBot.ReactionCallbackCommands;
using TeaBot.Webservices;

namespace TeaBot.Modules
{
    [Summary("Commands without a specific category")]
    public class General : InteractiveBase
    {
        [Command("ping", RunMode = RunMode.Async)]
        [Summary("Pong! (returns the bot's ping)")]
        public async Task Ping()
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            var message = await ReplyAsync("Pong!");
            await message.ModifyAsync(x => x.Content = $"Pong! | `Ping: {sw.ElapsedMilliseconds}ms` | `Latency: {Context.Client.Latency}ms`");
        }

        [Command("say")]
        [Summary("Make the bot say something!")]
        [Note("This command does not work if a message contains a ping")]
        public async Task Say([Remainder] string text)
        {
            if (Context.Message.MentionedRoles.Count > 0 || Context.Message.MentionedUsers.Count > 0)
            {
                string prefix = await Tea.GetPrefixAsync(Context);
                await ReplyAsync($"No pinging when using `{prefix}say`!");
                return;
            }
            if (!Context.IsPrivate && await Context.Channel.GetMessageAsync(Context.Message.Id) != null)
                await Context.Message.DeleteAsync();
            await ReplyAsync(text);
        }

        [Command("rate", true)]
        [Summary("Rates whatever you throw at it")]
        public async Task Rate()
        {
            List<string> quotes = new List<string>() {
                "delet this",
                "i give this a ten...out of a hundred",
                "your parents would be disappointed if they цsaw this...luckily i'm not your parents, 10/10",
                "I have no idea what to say, I laughed, I cried and still couldn’t stop reading . I experienced every emotion reading this.",
                "9/11",
                "My taste buds are still singing from our last visit! Try out the huge selection of incredible appetizers. Everything was simply decadent. After my meal, I was knocked into a food coma. Easily earned their 5 stars!",
                "Absolutely terrible.",
                "Only Harambe can rate this.",
                "A sugary brussels sprout bouquet and scintillating papaya midtones are mixed in the 2011 Merlot from Champs de Molyneaux.",
                "incredible, absolutely phenomenal",
                "i approve",
                "nice",
                "use za hando on this",
                "really bad"
            };

            await ReplyAsync(quotes.ElementAt(new Random().Next(0, quotes.Count() - 1)));
        }

        [Command("react")]
        public async Task React()
        {
            Emote poppo = Emote.Parse("<:poppo:677567085743964165>");
            await Context.Message.AddReactionAsync(poppo);
        }

        [Command("wait", RunMode = RunMode.Async)]
        [Alias("timer")]
        [Summary("Sets a timer for the given amount of seconds")]
        public async Task Wait(double seconds)
        {
            if (seconds > 300)
            {
                await ReplyAsync("I don't want to wait that much!");
                return;
            }

            await ReplyAsync($"Ok, I'll reply in **{seconds}** seconds");

            await Task.Delay(TimeSpan.FromSeconds(seconds));

            await ReplyAsync($"I have waited for **{seconds}** seconds!");
        }

        [Command("russianize")]
        [Alias("russian", "rus")]
        [Summary("Replaces various letters in a message with Russian letters instead")]
        public async Task Russianize([Remainder] string text)
        {
            string result = text.ToLower()
                .Replace("h", "н")
                .Replace("t", "т")
                .Replace("m", "м")
                .Replace("r", "г")
                .Replace("w", "ш")
                .Replace("d", "д")
                .Replace("z", "з")
                .Replace("n", "п")
                .Replace("b", "в")
                .Replace("k", "к");

            await ReplyAsync(result);
        }

        [Command("choose")]
        [Summary("Force the bot to make a choice for you!")]
        [Note("Split the options using space or `|`")]
        public async Task Choose([Remainder] string options)
        {
            var optionsArray = options.Contains('|') ? options.Split('|') : options.Split(' ');
            await ReplyAsync(optionsArray[new Random().Next(0, optionsArray.Length)]);
        }

        [Command("poll", RunMode = RunMode.Async)]
        [Summary("Host a poll with a given amount of entries (up to 9)")]
        [RequireContext(ContextType.Guild)]
        [Ratelimit(30, Measure.Seconds)]
        public async Task Poll()
        {
            var reply1 = await ReplyAsync("What's gonna be the poll's name? `Reply with just the name`");
            var name = await NextMessageAsync(true, true, TimeSpan.FromSeconds(60));
            if (name is null) return;

            var reply2 = await ReplyAsync("What are the poll's entries? Split them with `|`");
            var entries = await NextMessageAsync(true, true, TimeSpan.FromSeconds(60));
            if (entries is null) return;

            Emoji[] unicodeNums = new string[] { "1️⃣", "2️⃣", "3️⃣", "4️⃣", "5️⃣", "6️⃣", "7️⃣", "8️⃣", "9️⃣" }.Select(x => new Emoji(x)).ToArray();
            //Emoji[] arrows = new string[] { "🔼", "🔽" }.Select(x => new Emoji(x)).ToArray();

            var entriesArr = Regex.Replace(entries.Content, @"\s+\|\s+", "|").Split("|");
            if (entriesArr.Length < 2 || entriesArr.Length > 9 || entriesArr.Any(x => x == ""))
            {
                await ReplyAsync("Incorrect input! `(the amount of options must be in range of 2 to 9 or empty entries found)`");
            }
            else
            {
                if (!Context.IsPrivate)
                    _ = Task.Run(async () =>
                    {
                        await Context.Message.DeleteAsync();
                        await reply1.DeleteAsync();
                        await name.DeleteAsync();
                        await reply2.DeleteAsync();
                        await entries.DeleteAsync();
                    });

                var message = await ReplyAsync($"Poll hosted by {Context.Message.Author.Mention}\n**{name}**\n{string.Join("\n", entriesArr.Select((x, index) => $"**{index + 1}.** {entriesArr[index]}"))}");
                _ = Task.Run(async () => await message.AddReactionsAsync(unicodeNums.Take(entriesArr.Length).ToArray()));
            }
        }

        [Command("urbandictionary")]
        [Alias("ud", "urban", "define")]
        [Summary("Finds the definitions of a word or a word combination on https://www.urbandictionary.com")]
        public async Task UrbanDictonary([Remainder] string word)
        {
            string json = await UrbanDictionary.GetDefinitionJSONAsync(word);
            if (json is null)
            {
                await ReplyAsync("Something went wrong.");
                return;
            }

            var definitions = UrbanDictionary.DeserealiseDefinition(json);
            if (definitions is null)
            {
                await ReplyAsync($"No definition exists for `{word}`");
                return;
            }

            var urbanDictionaryPaged = new UrbanDictionaryPaged(Interactive, Context, definitions);
            await urbanDictionaryPaged.DisplayAsync();
        }
    }

}
