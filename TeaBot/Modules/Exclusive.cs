using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using TeaBot.Attributes;
using TeaBot.Preconditions;

namespace TeaBot.Modules
{
    [HelpCommandIgnore]
    [Summary("Commands that can only be executed in specific servers")]
    public class Exclusive : InteractiveBase
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
            await ReplyAsync(ReplacePings(quote.Content) + "\n" + string.Join("\n", quote.Attachments.Select(x => x.Url)));

            string ReplacePings(string text)
            {
                var pings = Regex.Matches(text, @"\<\@[\s\S]*?\>");

                foreach (var ping in pings)
                {
                    string pingString = ping.ToString();
                    text = text.Replace(pingString, $"@{Context.Client.GetUser(MentionUtils.ParseUser(pingString))}");
                }

                return text;
            }
        }
    }
}
