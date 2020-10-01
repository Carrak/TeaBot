using System.Collections.Generic;
using Newtonsoft.Json;

namespace TeaBot.ReactionCallbackCommands.ReactionRole
{
    public class RawFullReactionRoleMessage : RawReactionRoleMessage
    {
        [JsonProperty("data")]
        public ReactionRoleMessageData Data;

        [JsonProperty("pairs")]
        new public IEnumerable<RawFullEmoteRolePair> EmoteRolePairs;

        [JsonConstructor]
        public RawFullReactionRoleMessage(
            int rrid,
            ulong guildId,
            ulong? channelId,
            ulong? messageId,
            int? limitId,
            IEnumerable<ulong> globalAllowedRoleIds,
            IEnumerable<ulong> globalProhibitedRoleIds,
            ReactionRoleMessageData data,
            IEnumerable<RawFullEmoteRolePair> emoteRolePairs) : base(rrid, guildId, channelId, messageId, limitId, emoteRolePairs, globalAllowedRoleIds, globalProhibitedRoleIds)
        {
            Data = data;
            EmoteRolePairs = emoteRolePairs ?? new List<RawFullEmoteRolePair>();
        }
    }

    public class RawFullEmoteRolePair : RawEmoteRolePair
    {
        [JsonProperty("data")]
        public EmoteRolePairData Data;

        public RawFullEmoteRolePair(
            int pairId,
            string emote,
            ulong roleId,
            IEnumerable<ulong> allowedRoleIds,
            IEnumerable<ulong> prohibitedRoleIds,
            EmoteRolePairData data) : base(pairId, emote, roleId, allowedRoleIds, prohibitedRoleIds)
        {
            Data = data;
        }
    }
}
