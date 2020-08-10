using System.Collections.Generic;

namespace TeaBot.ReactionCallbackCommands.ReactionRole
{
    /// <summary>
    ///     A raw representation of <see cref="ReactionRoleMessage"/> that contains info from the database.
    /// </summary>
    public sealed class RawReactionRoleMessage
    {
        public int RRID { get; }

        public ulong GuildId { get; }
        public ulong? ChannelId { get; }
        public ulong? MessageId { get; }

        public int? Limit { get; }
        public bool IsCustom { get; }

        public IEnumerable<RawEmoteRolePair> EmoteRolePairs { get; }

        public IEnumerable<ulong> GlobalAllowedRoleIds { get; }
        public IEnumerable<ulong> GlobalProhibitedRoleIds { get; }

        public RawReactionRoleMessage(int rrid, 
            int? limit, 
            ulong guildId, 
            ulong? channelId, 
            ulong? messageId, 
            bool isCustom,
            IEnumerable<RawEmoteRolePair> emoteRolePairs,
            IEnumerable<ulong> allowedRoles,
            IEnumerable<ulong> prohibitedRoles)
        {
            RRID = rrid;
            Limit = limit;
            GuildId = guildId;
            ChannelId = channelId;
            MessageId = messageId;
            IsCustom = isCustom;
            EmoteRolePairs = emoteRolePairs;
            GlobalAllowedRoleIds = allowedRoles ?? new List<ulong>();
            GlobalProhibitedRoleIds = prohibitedRoles ?? new List<ulong>();
        }
    }

    /// <summary>
    ///     A raw representation of <see cref="EmoteRolePair"/> that contains info from the database.
    /// </summary>
    public sealed class RawEmoteRolePair
    {
        public int PairId { get; }

        public string Emote { get; }
        public ulong RoleId { get; }

        public IEnumerable<ulong> AllowedRoleIds { get; }
        public IEnumerable<ulong> ProhibitedRoleIds { get; }

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
