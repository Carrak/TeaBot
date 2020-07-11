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
        [Ratelimit(3)]
        public async Task Purge(
            [Summary("The amount of messages to purge.")] int count
            )
        {
            count = Math.Min(100, count + 1);
            var channel = Context.Channel as ITextChannel;
            var messages = await channel.GetMessagesAsync(count).FlattenAsync();
            messages = messages.Where(x => DateTimeOffset.UtcNow - x.CreatedAt < TimeSpan.FromDays(14));
            await channel.DeleteMessagesAsync(messages);
        }

        [Command("purge")]
        [Summary("Delete the last `count` messages from a specific user. There is a hard limit of 100.")]
        [RequireBotPermission(GuildPermission.ManageMessages)]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        [RequireContext(ContextType.Guild)]
        [Note("Messages must be less than 2 weeks old. It is not guaranteed for all of the user's messages to be purged, only the messages that are within the last 100 in the channel.")]
        [Ratelimit(3)]
        public async Task Purge(
            [Summary("The user to exclusively purge messages from.")] IUser user,
            [Summary("The amount of messages to purge.")] int count
            )
        {
            var channel = Context.Channel as ITextChannel;
            var messages = await channel.GetMessagesAsync().FlattenAsync();
            messages = messages.Where(x => x.Author == user);
            messages = messages.Take(Math.Min(messages.Count(), count));
            messages = messages.Where(x => DateTimeOffset.UtcNow - x.CreatedAt < TimeSpan.FromDays(14));
            await channel.DeleteMessagesAsync(messages);
        }
    }
}
