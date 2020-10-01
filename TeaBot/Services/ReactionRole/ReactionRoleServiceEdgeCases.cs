using System.Linq;
using System.Threading.Tasks;
using TeaBot.ReactionCallbackCommands.ReactionRole;

namespace TeaBot.Services.ReactionRole
{
    partial class ReactionRoleService
    {
        /// <summary>
        ///     Deletes the guild and all of its RR messages from the database.
        /// </summary>
        /// <param name="guildId">The ID of the guild.</param>
        public async Task RemoveGuildFromDbAsync(ulong guildId)
        {
            string query = "DELETE FROM reaction_role_messages.reaction_roles WHERE guildid=@gid; " +
                        "DELETE FROM reaction_role_messages.emote_role_pairs WHERE rrid IN (SELECT rrid FROM reaction_role_messages.reaction_roles WHERE guildid=@gid)";
            await using var cmd = _database.GetCommand(query, true);

            cmd.Parameters.AddWithValue("gid", (long)guildId);

            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        ///     Deletes the channel and all of the RR messages related to it from the database.
        /// </summary>
        /// <param name="channelId">The ID of the channel.</param>
        public async Task RemoveChannelFromDbAsync(ulong channelId)
        {
            foreach (var rr in displayedRrmsgs.Values.Where(x => x.Message.Channel.Id == channelId))
                displayedRrmsgs.Remove(rr.Message.Id);

            string query = "UPDATE reaction_role_messages.reaction_roles SET channelid=NULL, messageid=NULL WHERE channelid=@cid";
            await using var cmd = _database.GetCommand(query, true);

            cmd.Parameters.AddWithValue("cid", (long)channelId);

            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        ///     Deletes the message from a reaction-role message from the database.
        /// </summary>
        /// <param name="channelId">The ID of the channel the message is in.</param>
        /// <param name="messageId">The ID of the message.</param>
        public async Task RemoveMessageFromDbAsync(ulong channelId, ulong messageId)
        {
            displayedRrmsgs.Remove(messageId);

            string query = "UPDATE reaction_role_messages.reaction_roles SET channelid=NULL, messageid=NULL WHERE channelid=@cid AND messageid=@mid";
            await using var cmd = _database.GetCommand(query, true);

            cmd.Parameters.AddWithValue("mid", (long)messageId);
            cmd.Parameters.AddWithValue("cid", (long)channelId);

            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        ///     Deletes all entries in the databases where roleid is <paramref name="roleId"/> and removes the role from existing reaction-role messages.
        /// </summary>
        /// <param name="roleId">The ID of the role.</param>
        public async Task RemoveRoleFromDbAsync(ulong roleId)
        {
            foreach (var rrmsg in displayedRrmsgs.Values.Where(x => x.EmoteRolePairs.Values.Any(y => y.Role.Id == roleId)))
            {
                var emote = rrmsg.EmoteRolePairs.Values.FirstOrDefault(x => x.Role.Id == roleId).Emote;
                if (rrmsg.EmoteRolePairs.Remove(emote) && rrmsg is FullReactionRoleMessage frrmsg)
                    await frrmsg.DisplayAsync();
            }

            string query = @"
            DELETE FROM reaction_role_messages.emote_role_pairs WHERE roleid=@rid;
            DELETE FROM reaction_role_messages.allowed_roles WHERE roleid=@rid;
            DELETE FROM reaction_role_messages.prohibited_roles WHERE roleid=@rid;
            ";
            await using var cmd = _database.GetCommand(query, true);

            cmd.Parameters.AddWithValue("rid", (long)roleId);

            await cmd.ExecuteNonQueryAsync();
        }

    }
}
