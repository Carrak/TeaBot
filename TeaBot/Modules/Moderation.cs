using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using TeaBot.Attributes;
using TeaBot.Commands;

namespace TeaBot.Modules
{
    [EssentialModule]
    [Summary("Commands meant for moderating the server.")]
    public class Moderation : TeaInteractiveBase
    {
        [Command("purge")]
        [Summary("Delete the last `count` messages. There is a hard limit of 100")]
        [RequireBotPermission(GuildPermission.ManageMessages)]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        [RequireContext(ContextType.Guild)]
        public async Task Purge(int count)
        {
            count = Math.Min(100, count);
            var channel = Context.Channel as ITextChannel;
            var messages = await channel.GetMessagesAsync(count).FlattenAsync();
            await channel.DeleteMessagesAsync(messages);
        }
    }
}
