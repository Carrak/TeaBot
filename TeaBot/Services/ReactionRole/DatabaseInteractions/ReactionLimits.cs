using System.Threading.Tasks;
using Discord.WebSocket;

namespace TeaBot.Services.ReactionRole
{
    partial class ReactionRoleService
    {
        public async Task SetLimitAsync(SocketGuild guild, int? index, int newLimit)
        {
            string query = @"
            SELECT reaction_role_messages.update_or_insert_limit(reaction_role_messages.get_rrid(@gid, @rn), @lim)
            ";

            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("rn", GetIndexForParameter(index));
            cmd.Parameters.AddWithValue("gid", (long)guild.Id);
            cmd.Parameters.AddWithValue("lim", newLimit);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task RemoveLimitAsync(SocketGuild guild, int? index)
        {
            string query = @"
            SELECT reaction_role_messages.remove_reaction_limit(reaction_role_messages.get_rrid(@gid, @rn))
            ";

            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("rn", GetIndexForParameter(index));
            cmd.Parameters.AddWithValue("gid", (long)guild.Id);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<bool> ChainLimits(SocketGuild guild, int index1, int index2)
        {
            string query = @"
            INSERT INTO reaction_role_messages.reaction_limit_relations (rrid, limitid)
            SELECT reaction_role_messages.get_rrid(@gid, @rn2), limitid
            FROM reaction_role_messages.reaction_limit_relations
            WHERE rrid = reaction_role_messages.get_rrid(@gid, @rn1)
            ";

            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("rn1", index1);
            cmd.Parameters.AddWithValue("rn2", index2);
            cmd.Parameters.AddWithValue("gid", (long)guild.Id);

            return await cmd.ExecuteNonQueryAsync() != 0;
        }
    }
}
