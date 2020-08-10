using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Microsoft.Collections.Extensions;
using TeaBot.ReactionCallbackCommands.ReactionRole;

namespace TeaBot.Services.ReactionRole
{
    partial class ReactionRoleService
    {
        public async Task InitCallbacksAsync()
        {
            string query = @"
            SELECT pairid, roleid FROM reaction_role_messages.allowed_roles;
            SELECT pairid, roleid FROM reaction_role_messages.prohibited_roles;
            SELECT rrid, roleid FROM reaction_role_messages.global_allowed_roles;
            SELECT rrid, roleid FROM reaction_role_messages.global_allowed_roles;
            SELECT rrid, emote, roleid, pairid FROM reaction_role_messages.emote_role_pairs ORDER BY index; 
            SELECT rrid, guildid, channelid, messageid, reaction_limit, iscustom FROM reaction_role_messages.reaction_roles;
            SELECT pairid, description FROM reaction_role_messages.emote_role_pairs_data;
            SELECT rrid, name, color, description FROM reaction_role_messages.reaction_roles_data;
            ";

            await using var cmd = _database.GetCommand(query);

            await using var reader = await cmd.ExecuteReaderAsync();

            // Key: PairID
            MultiValueDictionary<int, ulong> emoteRolePairsAllowedRoles = new MultiValueDictionary<int, ulong>();
            // 1. Allowed roles for emote-role pairs
            while (await reader.ReadAsync())
                emoteRolePairsAllowedRoles.Add(reader.GetInt32(0), (ulong)reader.GetInt64(1));

            await reader.NextResultAsync();

            // Key: PairID
            MultiValueDictionary<int, ulong> emoteRolePairsProhibitedRoles = new MultiValueDictionary<int, ulong>();
            // 2. Prohibited roles for emote-role pairs
            while (await reader.ReadAsync())
                emoteRolePairsProhibitedRoles.Add(reader.GetInt32(0), (ulong)reader.GetInt64(1));

            await reader.NextResultAsync();

            // Key: RRID
            MultiValueDictionary<int, ulong> globalAllowedRoles = new MultiValueDictionary<int, ulong>();
            // 3. Global allowed roles for reaction-role messages
            while (await reader.ReadAsync())
                globalAllowedRoles.Add(reader.GetInt32(0), (ulong)reader.GetInt64(1));

            await reader.NextResultAsync();

            // Key: RRID
            MultiValueDictionary<int, ulong> globalProhibitedRoles = new MultiValueDictionary<int, ulong>();
            // 4. Global prohibited roles for reaction-role messages
            while (await reader.ReadAsync())
                globalProhibitedRoles.Add(reader.GetInt32(0), (ulong)reader.GetInt64(1));

            await reader.NextResultAsync();

            // Key: RRID
            MultiValueDictionary<int, RawEmoteRolePair> emoteRoles = new MultiValueDictionary<int, RawEmoteRolePair>();
            // 5. Emote role-pairs
            while (await reader.ReadAsync())
            {
                int emoteRoleRRID = reader.GetInt32(0);
                string emote = reader.GetString(1);
                ulong roleid = (ulong)reader.GetInt64(2);
                int pairid = reader.GetInt32(3);

                emoteRoles.Add(emoteRoleRRID, new RawEmoteRolePair(pairid, emote, roleid, emoteRolePairsAllowedRoles.GetValueOrDefault(pairid), emoteRolePairsProhibitedRoles.GetValueOrDefault(pairid)));
            }

            await reader.NextResultAsync();

            // Key: RRID
            Dictionary<int, RawReactionRoleMessage> rawRRmsgs = new Dictionary<int, RawReactionRoleMessage>();
            // 6. Reaction-role message info
            while (await reader.ReadAsync())
            {
                // Skip if channel or message is null
                if (await reader.IsDBNullAsync(2) || await reader.IsDBNullAsync(3))
                    continue;

                int rrid = reader.GetInt32(0);

                ulong guildId = (ulong)reader.GetInt64(1);
                ulong channelId = (ulong)reader.GetInt64(2);
                ulong messageId = (ulong)reader.GetInt64(3);
                int? limit = await reader.IsDBNullAsync(4) ? (int?)null : reader.GetInt32(4);
                bool iscustom = reader.GetBoolean(5);

                rawRRmsgs.Add(rrid, new RawReactionRoleMessage(rrid, limit, guildId, channelId, messageId, iscustom, emoteRoles.GetValueOrDefault(rrid), globalAllowedRoles.GetValueOrDefault(rrid), globalProhibitedRoles.GetValueOrDefault(rrid)));
            }

            await reader.NextResultAsync();

            // Additional emote-role pairs data for full emote-role pairs
            var emoteRolesData = new Dictionary<int, EmoteRolePairData>();
            // 7. Emote-roles data
            while (await reader.ReadAsync())
            {
                int pairid = reader.GetInt32(0);
                string description = await reader.IsDBNullAsync(1) ? null : reader.GetString(1);

                emoteRolesData.Add(pairid, new EmoteRolePairData(description));
            }

            await reader.NextResultAsync();

            // Full reaction-role messages data, for non-custom messages
            Dictionary<int, ReactionRoleMessageData> fullData = new Dictionary<int, ReactionRoleMessageData>();
            // 8. Reaction-roles data
            while (await reader.ReadAsync())
            {
                int rrid = reader.GetInt32(0);
                string name = await reader.IsDBNullAsync(1) ? null : reader.GetString(1);
                Color? color = await reader.IsDBNullAsync(2) ? (Color?)null : new Color((uint)reader.GetInt32(2));
                string description = await reader.IsDBNullAsync(3) ? null : reader.GetString(3);

                fullData.Add(rrid, new ReactionRoleMessageData(name, description, color));
            }

            await reader.CloseAsync();

            // Init callbacks
            foreach (var rawRRmsg in rawRRmsgs.Values)
            {
                var rrmsg = await ReactionRoleMessage.CreateAsync(this, rawRRmsg);

                if (rrmsg is null || rrmsg.Message is null)
                    continue;

                if (rawRRmsg.IsCustom)
                {
                    rrmsg = new FullReactionRoleMessage(rrmsg, rrmsg.EmoteRolePairs.Values.Select(x => new FullEmoteRolePair(x, emoteRolesData.GetValueOrDefault(x.PairId))), fullData.GetValueOrDefault(rawRRmsg.RRID));
                }

                reactionRoleCallbacks[rrmsg.Message.Id] = rrmsg;
            }
        }
    }
}
