using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace TeaBot.Services.ReactionRole
{
    partial class ReactionRoleService
    {
        private async Task LeftGuild(SocketGuild guild) => await GuildNotFound(guild.Id);

        private async Task ChannelDeleted(SocketChannel channel) => await ChannelNotFound(channel.Id);

        private async Task RoleDeleted(SocketRole role) => await RoleNotFound(role.Id);

        private async Task MessageDeleted(Cacheable<IMessage, ulong> message, ISocketMessageChannel channel)
        {
            if (reactionRoleMessages.Values.Any(x => x.Channel.Id == channel.Id && x.Message.Id == message.Id))
                await MessageNotFound(channel.Id, message.Id);
        }

        private async Task ReactionRemoved(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            if (!reaction.User.Value.IsBot && reactionRoleMessages.TryGetValue(message.Id, out var rr))
                await rr.HandleReactionRemoved(reaction);
        }

        private async Task ReactionAdded(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            if (!reaction.User.Value.IsBot && reactionRoleMessages.TryGetValue(message.Id, out var rr))
                await rr.HandleReactionAdded(reaction);
        }
    }
}
