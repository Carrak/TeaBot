using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using TeaBot.Attributes;
using TeaBot.Commands;
using TeaBot.Preconditions;

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
        [Note("Messages must be less than 2 weeks old.")]
        [Ratelimit(5)]
        public async Task Purge(int count)
        {
            count = Math.Min(100, count + 1);
            var channel = Context.Channel as ITextChannel;
            var messages = await channel.GetMessagesAsync(count).FlattenAsync();
            await channel.DeleteMessagesAsync(messages);
        }

        [Command("purge")]
        [Summary("Delete the last `count` messages from a specific user. There is a hard limit of 100")]
        [RequireBotPermission(GuildPermission.ManageMessages)]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        [RequireContext(ContextType.Guild)]
        [Note("Bots can only retrieve 100 messages per request, so it is not guaranteed for all messages to be purged at once. " +
            "Only the ones that are within these 100 can be purged. Messages also must be less than 2 weeks old.")]
        [Ratelimit(5)]
        public async Task Purge(IUser user, int count)
        {
            count = Math.Min(100, count + 1);
            var channel = Context.Channel as ITextChannel;
            var messages = await channel.GetMessagesAsync().FlattenAsync();
            messages = messages.Where(x => x.Author == user);
            messages = messages.Take(Math.Min(messages.Count(), count));
            await channel.DeleteMessagesAsync(messages);
        }
    }
}
