using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Net;
using Nekos.Net;
using Nekos.Net.Responses;
using TeaBot.Attributes;
using TeaBot.Commands;
using TeaBot.Main;
using TeaBot.Preconditions;
using TeaBot.ReactionCallbackCommands.PagedCommands;
using TeaBot.Utilities;
using TeaBot.Webservices;

namespace TeaBot.Modules
{
    [Summary("Commands without a specific category")]
    public class General : TeaInteractiveBase
    {
        [Command("ping", RunMode = RunMode.Async)]
        [Summary("Pong! (returns the bot's ping)")]
        [Ratelimit(3)]
        public async Task Ping()
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            var message = await ReplyAsync("Pong!");
            await message.ModifyAsync(x => x.Content = $"Pong! | `Ping: {sw.ElapsedMilliseconds}ms` | `Latency: {Context.Client.Latency}ms`");
        }

        [Command("rate", true)]
        [Summary("Rates whatever you throw at it")]
        [Ratelimit(3)]
        public async Task Rate()
        {
            List<string> quotes = new List<string>() {
                "delet this",
                "i give this a ten...out of a hundred",
                "your parents would be disappointed if they saw this...luckily i'm not your parents, 10/10",
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

        [Command("wait", RunMode = RunMode.Async)]
        [Alias("timer")]
        [Summary("Sets a timer for the given amount of seconds")]
        [Ratelimit(120)]
        public async Task Wait(
            [Summary("Time to wait for. Cannot be over 300 seconds.")] double seconds
            )
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
        [Summary("тгаnsfoгмs тeхт liке тнis")]
        [Ratelimit(3)]
        public async Task Russianize(
            [Summary("The text to transform.")][Remainder] string text
            )
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

        [Command("viewemote")]
        [Alias("emoji", "emote", "viewemoji", "view")]
        [Summary("View any emoji. For custom emotes, make sure you use ones from the guild you're using this command in.")]
        [Ratelimit(3)]
        public async Task ViewEmoji(
            [Summary("Emote or emoji to view a bigger picture of.")]IEmote emote
            )
        {
            if (emote is Emote e)
                await ReplyAsync(e.Url);
            else
            {
                var parsedEmoji = TwemojiNet.EmojiParser.GetCodepoints(emote.ToString());
                string codepoint = string.Join('-', parsedEmoji.Codepoints);
                await ReplyAsync($"https://twemoji.maxcdn.com/v/latest/72x72/{codepoint}.png");
            }
        }

        [Command("choose")]
        [Summary("Force the bot to make a choice for you!")]
        [Note("Split the options using space or `|`")]
        [Ratelimit(3)]
        public async Task Choose(
            [Summary("The options to choose from.")][Remainder] string options
            )
        {
            options = options.DeafenMentionsFromMessage(Context.Message);
            var optionsArray = options.Contains('|') ? options.Split('|') : options.Split(' ');
            await ReplyAsync(optionsArray[new Random().Next(0, optionsArray.Length)]);
        }

        [Command("poll", RunMode = RunMode.Async)]
        [Summary("Host a poll with a given amount of entries (up to 9)")]
        [RequireContext(ContextType.Guild)]
        [Ratelimit(30)]
        public async Task Poll()
        {
            var reply1 = await ReplyAsync("What's gonna be the poll's name? `Reply with just the name or type 'stop' to cancel.`");
            var name = await NextMessageAsync(true, true, TimeSpan.FromSeconds(30));
            if (name is null || name.Content.Equals("stop", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    await reply1.DeleteAsync();
                    await name.DeleteAsync();
                }
                catch (HttpException)
                { }
                return;
            }

            var reply2 = await ReplyAsync("What are the poll's entries? Split them with `|`");

            string[] entriesArr = null;
            var entriesMsg = await NextMessageWithConditionAsync(x =>
            {
                entriesArr = Regex.Split(x.Content.DeafenMentionsFromMessage(x), @"\s+\|\s+");
                int count = entriesArr.Count();
                return count >= 2 && count <= 9;
            }, Context, TimeSpan.FromSeconds(30), errorMessage: "You've input less than 2 or more than 9 entries. Try again.");

            if (entriesArr is null) return;

            entriesArr = entriesArr.Where(x => !string.IsNullOrEmpty(x)).ToArray();

            Emoji[] unicodeNums = new string[] { "1️⃣", "2️⃣", "3️⃣", "4️⃣", "5️⃣", "6️⃣", "7️⃣", "8️⃣", "9️⃣" }.Select(x => new Emoji(x)).ToArray();

            var message = await ReplyAsync($"Poll hosted by {Context.Message.Author.Mention}\n**{name}**\n{string.Join("\n", entriesArr.Select((x, index) => $"**{index + 1}.** {entriesArr[index]}"))}");
            _ = Task.Run(async () => await message.AddReactionsAsync(unicodeNums.Take(entriesArr.Count()).ToArray()));

            if (!Context.IsPrivate && Context.Guild.CurrentUser.GuildPermissions.ManageMessages)
                _ = Task.Run(async () =>
                {
                    var messages = new IMessage[] { reply1, reply2, name, entriesMsg, Context.Message };
                    try
                    {
                        await (Context.Channel as ITextChannel).DeleteMessagesAsync(messages);
                    }
                    catch (HttpException)
                    {

                    }
                });
        }

        [Command("urbandictionary")]
        [Alias("ud", "urban", "define", "dictionary", "definition")]
        [Summary("Finds the definitions of a word or a word combination on https://www.urbandictionary.com")]
        [Ratelimit(3)]
        public async Task UrbanDictonary(
            [Summary("The word or word combination to look up the definition for.")][Remainder] string word
            )
        {
            var ud = new UrbanDictionarySearch(word);

            string json;
            try
            {
                json = await ud.GetDefinitionsJSONAsync();
            }
            catch (HttpRequestException)
            {
                await ReplyAsync("Something went wrong. Try again?");
                return;
            }

            var definitions = ud.DeserealiseDefinitions(json);
            if (definitions.Count() == 0)
            {
                await ReplyAsync($"No definition exists for `{word}`");
                return;
            }

            var urbanDictionaryPaged = new UrbanDictionaryPaged(Interactive, Context, definitions);
            await urbanDictionaryPaged.DisplayAsync();
        }

        [Command("randomfact")]
        [Alias("fact")]
        [Summary("Get a random fact about anything")]
        [Ratelimit(3)]
        public async Task Fact()
        {
            NekosFact fact;

            try
            {
                fact = await NekosClient.GetFactAsync();
            }
            catch (HttpRequestException)
            {
                await ReplyAsync("Something went wrong. Try again?");
                return;
            }

            var embed = new EmbedBuilder();
            embed.WithTitle("Random fact")
                .WithDescription(fact.Fact)
                .WithColor(TeaEssentials.MainColor)
                .WithFooter("https://nekos.life/");

            await ReplyAsync(embed: embed.Build());
        }

        [Command("why")]
        [Summary("Get a random question.")]
        [Ratelimit(3)]
        public async Task Why()
        {
            NekosWhy why;

            try
            {
                why = await NekosClient.GetWhyAsync();
            }
            catch (HttpRequestException)
            {
                await ReplyAsync("Something went wrong. Try again?");
                return;
            }

            var embed = new EmbedBuilder();
            embed.WithTitle("Why?")
                .WithDescription(why.Why)
                .WithColor(TeaEssentials.MainColor)
                .WithFooter("https://nekos.life/");

            await ReplyAsync(embed: embed.Build());
        }

        [Command("8ball")]
        [Summary("Ask the eight ball anything you want!")]
        [Ratelimit(3)]
        public async Task EightBall(
            [Summary("The question you want to ask the 8ball.")][Remainder] string question
            )
        {
            Nekos8Ball eightBall;

            try
            {
                eightBall = await NekosClient.Get8BallAsync();
            }
            catch (HttpRequestException)
            {
                await ReplyAsync("Something went wrong. Try again?");
                return;
            }

            await ReplyAsync($"🎱 | {eightBall.Response}");
        }

        [Command("colour")]
        [Alias("color")]
        [Summary("Find out what a colour looks like.")]
        public async Task Colour(
            [Summary("An RGB colour represented in any of the following ways:\n" +
            "1. 255,255,255\n" +
            "2. #FFFFFF\n" +
            "3. 0xFFFFFF\n" +
            "4. Raw decimal RGB value (e.g. 16777215, same as #FFFFFF)")] [Remainder] Discord.Color colour
            )
        {
            var bm = new Bitmap(100, 100);
            var graphics = Graphics.FromImage(bm);

            using (SolidBrush brush = new SolidBrush(System.Drawing.Color.FromArgb(colour.R, colour.G, colour.B)))
            {
                graphics.FillRectangle(brush, 0, 0, 100, 100);
            }

            Stream stream = new MemoryStream();
            bm.Save(stream, System.Drawing.Imaging.ImageFormat.Jpeg);
            stream.Seek(0, SeekOrigin.Begin);

            await Context.Channel.SendFileAsync(stream, "colour.jpg", $"Here's what {colour} looks like.");
        }
    }

}
