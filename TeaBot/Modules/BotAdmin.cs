using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using TeaBot.Attributes;
using TeaBot.Commands;
using TeaBot.Main;
using TeaBot.Preconditions;

namespace TeaBot.Modules
{
    [EssentialModule]
    [HelpCommandIgnore]
    [BotAdmin]
    [Summary("Commands that can only be used by bot admins")]
    public class BotAdmin : TeaInteractiveBase
    {
        [Command("message", RunMode = RunMode.Async)]
        [Alias("msg")]
        [Summary("Sends a private message to the specified user or channel.")]
        public async Task Message(ulong id, [Remainder] string message = "")
        {
            if (Context.Message.Attachments.Count > 0)
            {
                message += $"\n{string.Join("\n", Context.Message.Attachments.Select(x => x.Url))}";
            }
            else if (message == "")
            {
                await ReplyAsync("Can't send an empty message without attachments!");
                return;
            }

            var user = Context.Client.GetUser(id);
            var channel = Context.Client.GetChannel(id) as IMessageChannel;

            (bool, bool) pair = (user is null, channel is null);

            switch (pair)
            {
                case (true, true):
                    await ReplyAsync($"`[{Context.Message.Author}] Couldn't deliver the message to {id}! [ Both user and channel are null ]`");
                    break;
                case (false, true):
                    await DM();
                    break;
                case (true, false):
                    await Channel();
                    break;
                case (false, false):
                    await ReplyAsync($"`[{Context.Message.Author}] Both message and channel are not null, where would you like to send the message to? (Reply with \"user\" or \"channel\")`");
                    var response = await NextMessageAsync(true, true, TimeSpan.FromSeconds(20));

                    if (response == null)
                        return;

                    switch (response.Content.ToLower())
                    {
                        case "user":
                            await DM();
                            break;
                        case "channel":
                            await Channel();
                            break;
                    }

                    break;
            }

            async Task DM()
            {
                var dmchannel = await user.GetOrCreateDMChannelAsync();
                await dmchannel.SendMessageAsync(message);
                await ReplyAsync($"`[{Context.Message.Author}] Message delivered to {user}!` \n`Message:` {HidePreviews(message)}");
            }

            async Task Channel()
            {
                await channel.SendMessageAsync(message);
                await ReplyAsync($"`[{Context.Message.Author}] Message delivered to #{channel} in {(channel as IGuildChannel).Guild.Name}!` \n`Message:`{HidePreviews(message)}");
            }
        }

        [Command("guilds")]
        [Summary("Displays the guilds the bot is in")]
        public async Task Guilds()
        {
            var guilds = Context.Client.Guilds;
            await ReplyAsync($"**Total guilds: {guilds.Count}**\n{string.Join("\n", guilds)}");
        }

        [Command("rename")]
        [Summary("Changes the bot's guild nickname")]
        public async Task Rename([Remainder] string newName)
        {
            if (newName.Length <= 32)
            {
                await Context.Guild.CurrentUser.ModifyAsync(x => x.Nickname = newName);
                await ReplyAsync("Successfully renamed!");
            }
            else await ReplyAsync("Couldn't rename! [The name must be 32 or fewer in length]");
        }

        [Command("sqlquery")]
        [Alias("sqlq")]
        public async Task SQLEval([Remainder] string query)
        {
            await using var cmd = new NpgsqlCommand(query, TeaEssentials.DbConnection);
            await using var reader = await cmd.ExecuteReaderAsync();

            List<List<object>> rows = new List<List<object>>();
            List<int> maxLengths = new List<int>(new int[reader.GetColumnSchema().Count]);

            while (await reader.ReadAsync())
            {
                List<object> row = new List<object>();

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    object value = reader.GetValue(i);
                    maxLengths[i] = Math.Max(maxLengths[i], value.ToString().Length);
                    row.Add(reader.IsDBNull(i) ? "null" : value);
                }

                rows.Add(row);
            }

            StringBuilder result = new StringBuilder("");
            for (int i = 0; i < maxLengths.Count; i++)
            {
                string name = reader.GetName(i);
                maxLengths[i] = Math.Max(name.Length, maxLengths[i]);
                result.Append(string.Format("|{0, -" + maxLengths[i] + "}", name));
            }
            result.Append("|\n");

            for (int i = 0; i < maxLengths.Count; i++)
            {
                result.Append("+");
                result.Append('-', maxLengths[i]);
            }
            result.Append("+\n");

            for (int i = 0; i < rows.Count; i++)
            {
                string toAdd = "";
                var row = rows[i];
                for (int j = 0; j < row.Count; j++)
                {
                    toAdd += string.Format("|{0, -" + maxLengths[j] + "}", row[j]);
                }
                toAdd += "|\n";
                result.Append(toAdd);
            }

            await reader.CloseAsync();
            await ReplyAsync($"`{result.Replace("\n", "`\n`")}`".TrimEnd('`'));
        }

        [Command("sqlnonquery")]
        [Alias("sqlnq")]
        public async Task SQlEvalNonQuery([Remainder] string query)
        {
            NpgsqlCommand cmd = new NpgsqlCommand(query, TeaEssentials.DbConnection);
            int rowsAffected = await cmd.ExecuteNonQueryAsync();
            await ReplyAsync($"Success! {rowsAffected} rows affected.");
        }

        [Command("consolewrite")]
        public Task ConsoleWrite([Remainder] string text)
        {
            Console.WriteLine(text);
            return Task.CompletedTask;
        }
    }
}
