using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using TeaBot.Attributes;
using System.Text.RegularExpressions;

namespace TeaBot.Modules
{
    //[HelpCommandIgnore]
    [Summary("Commands that can only be executed in specific servers")]
    public class Exclusive : InteractiveBase
    {

        [Command("quote", true)]
        [Exclusive(364771834325106689)]
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
                    text = text.Replace(pingString, $"@{Context.Client.GetUser(MentionUtils.ParseUser(pingString)).ToString()}");
                }

                return text;

                /*
                int pingStart = text.IndexOf("<@");
                int pingEnd = text.IndexOf(">");

                while (pingStart != -1 && pingEnd != -1)
                {
                    string toParse = text.Substring(pingStart, pingEnd - pingStart + 1);
                    ulong id = MentionUtils.ParseUser(toParse);
                    text = text.Replace(toParse, $"@{Context.Client.GetUser(id)}");

                    pingStart = text.IndexOf("<@");
                    pingEnd = text.IndexOf(">");
                }

                return text;
                */
                
            }
        }
    }
}
