using System.Collections.Generic;
using Discord;

namespace TeaBot.ReactionCallbackCommands.ReactionRole
{
    public sealed class RawReactionRoleMessage
    {
        public int RRID { get; }
        public string Name { get; }

        public ulong GuildId { get; }
        public ulong? ChannelId { get; }
        public ulong? MessageId { get; }

        public Color? Color { get; }

        public IEnumerable<RawEmoteRolePair> EmoteRolePairs;

        public RawReactionRoleMessage(int rrid, string name, ulong guildid, ulong? channelid, ulong? messageid, Color? color, IEnumerable<RawEmoteRolePair> emoteRolePairs)
        {
            RRID = rrid;
            Name = name;
            GuildId = guildid;
            ChannelId = channelid;
            MessageId = messageid;
            Color = color;

            EmoteRolePairs = emoteRolePairs ?? new List<RawEmoteRolePair>();
        }

    }

    public sealed class RawEmoteRolePair
    {
        public string Emote { get; }
        public ulong RoleId { get; }

        public string Description { get; }
        public IEnumerable<ulong> AllowedRoleIds;
        public IEnumerable<ulong> ProhibitedRoleIds;

        public RawEmoteRolePair(string emote,
            ulong roleid
            //string description = null,
            //IEnumerable<ulong> allowedRoles = null,
            //IEnumerable<ulong> prohibitedRoles = null
            )
        {
            Emote = emote;
            RoleId = roleid;

            //Description = description;
            //AllowedRoleIds = allowedRoles ?? new List<ulong>();
            //ProhibitedRoleIds = prohibitedRoles ?? new List<ulong>();
        }
    }
}
