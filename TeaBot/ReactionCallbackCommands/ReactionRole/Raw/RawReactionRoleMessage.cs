using System.Collections.Generic;
using Newtonsoft.Json;

namespace TeaBot.ReactionCallbackCommands.ReactionRole
{
    /// <summary>
    ///     A raw representation of <see cref="ReactionRoleMessage"/> that contains info from the database.
    /// </summary>
    public class RawReactionRoleMessage
    {
        [JsonProperty("rrid")]
        public int RRID { get; }

        [JsonProperty("guildid")]
        public ulong GuildId { get; }

        [JsonProperty("channelid")]
        public ulong? ChannelId { get; }

        [JsonProperty("messageid")]
        public ulong? MessageId { get; }

        [JsonProperty("limitid")]
        public int? LimitId { get; }

        [JsonProperty("pairs")]
        public IEnumerable<RawEmoteRolePair> EmoteRolePairs { get; }

        [JsonProperty("allowed_roles")]
        public IEnumerable<ulong> GlobalAllowedRoleIds { get; }

        [JsonProperty("prohibited_roles")]
        public IEnumerable<ulong> GlobalProhibitedRoleIds { get; }

        [JsonConstructor]
        public RawReactionRoleMessage(int rrid, ulong guildId, ulong? channelId, ulong? messageId, int? limitId, IEnumerable<RawEmoteRolePair> emoteRolePairs, IEnumerable<ulong> globalAllowedRoleIds, IEnumerable<ulong> globalProhibitedRoleIds)
        {
            RRID = rrid;
            GuildId = guildId;
            ChannelId = channelId;
            MessageId = messageId;
            LimitId = limitId;
            EmoteRolePairs = emoteRolePairs ?? new List<RawEmoteRolePair>();
            GlobalAllowedRoleIds = globalAllowedRoleIds ?? new List<ulong>();
            GlobalProhibitedRoleIds = globalProhibitedRoleIds ?? new List<ulong>();
        }
    }

    /// <summary>
    ///     A raw representation of <see cref="EmoteRolePair"/> that contains info from the database.
    /// </summary>
    public class RawEmoteRolePair
    {
        [JsonProperty("pairid")]
        public int PairId { get; }

        [JsonProperty("emote")]
        public string Emote { get; }

        [JsonProperty("roleid")]
        public ulong RoleId { get; }

        [JsonProperty("allowed_roles")]
        public IEnumerable<ulong> AllowedRoleIds { get; }

        [JsonProperty("prohibited_roles")]
        public IEnumerable<ulong> ProhibitedRoleIds { get; }

        [JsonConstructor]
        public RawEmoteRolePair(int pairId, string emote, ulong roleId, IEnumerable<ulong> allowedRoleIds, IEnumerable<ulong> prohibitedRoleIds)
        {
            PairId = pairId;
            Emote = emote;
            RoleId = roleId;
            AllowedRoleIds = allowedRoleIds ?? new List<ulong>();
            ProhibitedRoleIds = prohibitedRoleIds ?? new List<ulong>();
        }
    }
}
