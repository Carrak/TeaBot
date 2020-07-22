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
                    var embed = new EmbedBuilder();
                    embed.WithAuthor(Context.User)
                        .WithColor(Color.Red)
                        .WithTitle("Couldn't deliver message")
                        .WithDescription("Both user and channel are null");
                    await ReplyAsync(embed: embed.Build());
                    break;
                case (false, true):
                    await DM();
                    break;
                case (true, false):
                    await Channel();
                    break;
                case (false, false):
                    await ReplyAsync("Both message and channel are not null, where would you like to send the message to? (Reply with \"user\" or \"channel\")");
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

                var embed = new EmbedBuilder();
                embed.WithAuthor(Context.User)
                    .WithDescription(message)
                    .WithColor(TeaEssentials.MainColor)
                    .WithFooter($"Message delivered to {user}");

                await ReplyAsync(embed: embed.Build());
            }

            async Task Channel()
            {
                var msg = await channel.SendMessageAsync(message);

                var embed = new EmbedBuilder();
                embed.WithAuthor(Context.User)
                    .WithDescription(message)
                    .WithColor(TeaEssentials.MainColor)
                    .WithFooter($"Message delivered to #{channel} in {(channel as IGuildChannel).Guild.Name}");

                await ReplyAsync(embed: embed.Build());
            }
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
    }
}
