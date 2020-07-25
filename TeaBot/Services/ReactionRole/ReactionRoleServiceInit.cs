using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using TeaBot.ReactionCallbackCommands.ReactionRole;

namespace TeaBot.Services.ReactionRole
{
    partial class ReactionRoleService
    {
        public async Task InitCallbacksAsync()
        {
            string query = "SELECT rrid, emote, roleid FROM reaction_role_messages.emote_role_pairs ORDER BY index; " +
                "SELECT rrid, name, guildid, channelid, messageid, color FROM reaction_role_messages.reaction_roles";

            await using var cmd = _database.GetCommand(query);
            await using var reader = await cmd.ExecuteReaderAsync();

            List<(int, RawEmoteRolePair)> emoteRoles = new List<(int, RawEmoteRolePair)>();

            // Retrieve emote-role pairs
            while (await reader.ReadAsync())
            {
                int emoteRoleRRID = reader.GetInt32(0);
                string emote = reader.GetString(1);
                ulong roleid = (ulong)reader.GetInt64(2);

                emoteRoles.Add((emoteRoleRRID, new RawEmoteRolePair(emote, roleid)));
            }

            await reader.NextResultAsync();

            List<RawReactionRoleMessage> rawRRmsgs = new List<RawReactionRoleMessage>();

            // Retrieve the reaction-role message info
            while (await reader.ReadAsync())
            {
                // Skip if channel or message is null
                if (await reader.IsDBNullAsync(3) || await reader.IsDBNullAsync(4))
                    continue;

                int rrid = reader.GetInt32(0);

                string name = await reader.IsDBNullAsync(1) ? null : reader.GetString(1);
                Color? color = await reader.IsDBNullAsync(5) ? (Color?)null : new Color((uint)reader.GetInt32(5));

                ulong guildId = (ulong)reader.GetInt64(2);
                ulong channelId = (ulong)reader.GetInt64(3);
                ulong messageId = (ulong)reader.GetInt64(4);

                rawRRmsgs.Add(new RawReactionRoleMessage(rrid, name, guildId, channelId, messageId, color, emoteRoles.Where(x => x.Item1 == rrid).Select(x => x.Item2)));
            }

            await reader.CloseAsync();

            foreach (var rawRRmsg in rawRRmsgs)
            {
                var rrmsg = await PrepareReactionRoleMessageAsync(rawRRmsg);

                if (rrmsg != null && rrmsg.Channel != null && rrmsg.Message != null)
                    reactionRoleCallbacks[rrmsg.Message.Id] = rrmsg;
            }
        }
    }
}
