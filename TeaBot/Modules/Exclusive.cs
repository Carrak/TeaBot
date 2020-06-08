using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using TeaBot.Attributes;

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
            }
        }

        [Command("head", true)]
        [Exclusive(618581538963062789)]
        public async Task Head()
        {
            await ReplyAsync("I don't have a head and jame horny btw");
        }
    }
}
