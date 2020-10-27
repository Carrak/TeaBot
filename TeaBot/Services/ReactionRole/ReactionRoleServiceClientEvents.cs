using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using TeaBot.ReactionCallbackCommands.ReactionRole;

namespace TeaBot.Services.ReactionRole
{
    partial class ReactionRoleService
    {
        private async Task LeftGuild(SocketGuild guild) => await RemoveGuildFromDbAsync(guild.Id);

        private async Task ChannelDeleted(SocketChannel channel) => await RemoveChannelFromDbAsync(channel.Id);

        private async Task RoleDeleted(SocketRole role) => await RemoveRoleFromDbAsync(role.Id);

        private async Task MessageDeleted(Cacheable<IMessage, ulong> message, ISocketMessageChannel channel)
        {
            displayedRrmsgs.Remove(message.Id);
            await RemoveMessageFromDbAsync(channel.Id, message.Id);
        }

        private async Task ReactionRemoved(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            if (displayedRrmsgs.TryGetValue(message.Id, out var rr))
            {
                var user = await _client.Rest.GetGuildUserAsync(rr.Guild.Id, reaction.UserId);

                if (!user.IsBot)
                    await rr.HandleReactionRemoved(reaction, user);
            }
        }

        private async Task ReactionAdded(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel, SocketReaction reaction)
        {
            if (displayedRrmsgs.TryGetValue(message.Id, out var rr))
            {
                var user = await _client.Rest.GetGuildUserAsync(rr.Guild.Id, reaction.UserId);

                if (!user.IsBot)
                    await rr.HandleReactionAdded(reaction, user);
            }
        }

        private async Task RoleUpdated(SocketRole before, SocketRole after)
        {
            int maxRolePosition = after.Guild.CurrentUser.Roles.Max(x => x.Position);

            List<ReactionRoleMessage> toUpdate = new List<ReactionRoleMessage>();

            foreach (var rrmsg in displayedRrmsgs.Values)
                if (rrmsg.EmoteRolePairs.Values.FirstOrDefault(x => x.Role.Id == after.Id) is EmoteRolePair erp)
                {
                    erp.Role = after;
                    rrmsg.EmoteRolePairs[erp.Emote] = erp;
                    toUpdate.Add(rrmsg);
                }

            foreach (var rrmsg in toUpdate)
                await UpdateRRMSGRolesAsync(rrmsg, maxRolePosition);
        }

        private async Task GuildMemberUpdated(SocketGuildUser userBefore, SocketGuildUser userAfter)
        {
            if (userAfter.Guild.CurrentUser.Id != userAfter.Id || userBefore.Roles.Count == userAfter.Roles.Count)
                return;

            int maxRolePosition = userAfter.Roles.Max(x => x.Position);

            var toUpdate = new List<ReactionRoleMessage>(displayedRrmsgs.Values.Where(x => x.Guild.Id == userAfter.Guild.Id));
            foreach (var rrmsg in toUpdate)
                await UpdateRRMSGRolesAsync(rrmsg, maxRolePosition);
        }

        private async Task UpdateRRMSGRolesAsync(ReactionRoleMessage rrmsg, int maxRolePosition)
        {
            bool changeRequired = false;

            Dictionary<IEmote, EmoteRolePair> newPairs = new Dictionary<IEmote, EmoteRolePair>();
            foreach (var erp in rrmsg.EmoteRolePairs.Values)
            {
                if (erp.Role.Position > maxRolePosition)
                {
                    erp.Blocked = true;
                    changeRequired = true;
                }
                else if (erp.Blocked)
                {
                    erp.Blocked = false;
                    changeRequired = true;
                }
                newPairs.Add(erp.Emote, erp);
            }

            rrmsg.EmoteRolePairs = newPairs;

            if (changeRequired)
            {
                if (rrmsg is FullReactionRoleMessage frrmsg)
                {
                    if (!rrmsg.EmoteRolePairs.Values.Any(x => !x.Blocked))
                        await frrmsg.TryDeleteMessageAsync();
                    else
                        await frrmsg.DisplayAsync();
                }
                else
                    await rrmsg.AddReactionCallbackAsync();
            }
        }
    }
}
