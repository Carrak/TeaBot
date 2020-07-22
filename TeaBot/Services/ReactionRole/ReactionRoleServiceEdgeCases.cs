using System.Threading.Tasks;

namespace TeaBot.Services.ReactionRole
{
    partial class ReactionRoleService
    {
        private async Task GuildNotFound(ulong guildId)
        {
            string guildDeletionQuery = "DELETE FROM reaction_role_messages.reaction_roles WHERE guildid=@gid; " +
                        "DELETE FROM reaction_role_messages.emote_role_pairs WHERE rrid IN (SELECT rrid FROM reaction_role_messages.reaction_roles WHERE guildid=@gid)";
            await using var guildDeletionCmd = _database.GetCommand(guildDeletionQuery);

            guildDeletionCmd.Parameters.AddWithValue("gid", (long)guildId);

            await guildDeletionCmd.ExecuteNonQueryAsync();
        }

        private async Task ChannelNotFound(ulong channelId)
        {
            string updateQuery = "UPDATE reaction_role_messages.reaction_roles SET channelid=NULL, messageid=NULL WHERE channelid=@cid";
            await using var updateCmd = _database.GetCommand(updateQuery);

            updateCmd.Parameters.AddWithValue("cid", (long)channelId);

            await updateCmd.ExecuteNonQueryAsync();
        }

        private async Task MessageNotFound(ulong channelId, ulong messageId)
        {
            reactionRoleMessages.Remove(messageId);

            string query = "UPDATE reaction_role_messages.reaction_roles SET messageid=NULL WHERE channelid=@cid AND messageid=@mid";
            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("mid", (long)messageId);
            cmd.Parameters.AddWithValue("cid", (long)channelId);

            await cmd.ExecuteNonQueryAsync();
        }

        private async Task RoleNotFound(ulong roleId)
        {
            string query = "DELETE FROM reaction_role_messages.emote_role_pairs WHERE roleid=@rid";
            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("rid", (long)roleId);

            await cmd.ExecuteNonQueryAsync();
        }

    }
}
