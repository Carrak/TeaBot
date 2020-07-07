using System.Collections.Generic;
using Discord;

namespace TeaBot.Utilities
{
    /// <summary>
    ///     Utility class for working with guild and channel permissions.
    /// </summary>
    public static class PermissionUtilities
    {
        /// <summary>
        ///     Creates a string containing the primary permissions of a user.
        /// </summary>
        /// <param name="gp">User permissions to create a string from.</param>
        /// <returns>String containing main permissions of a user.</returns>
        public static string MainGuildPermissionsString(GuildPermissions gp)
        {
            List<string> permissions = new List<string>();

            if (gp.Administrator) return "Administrator (all permissions)";
            if (gp.BanMembers) permissions.Add("Ban members");
            if (gp.KickMembers) permissions.Add("Kick members");
            if (gp.ManageChannels) permissions.Add("Manage channels");
            if (gp.ManageEmojis) permissions.Add("Manage emojis");
            if (gp.ManageGuild) permissions.Add("Manage guild");
            if (gp.ManageMessages) permissions.Add("Manage messages");
            if (gp.ManageNicknames) permissions.Add("Manage nicknames");
            if (gp.ManageRoles) permissions.Add("Manage roles");
            if (gp.ManageWebhooks) permissions.Add("Manage webhooks");
            if (gp.MentionEveryone) permissions.Add("Mention everyone");
            if (gp.MoveMembers) permissions.Add("Move members");
            if (gp.DeafenMembers) permissions.Add("Deafen members");
            if (gp.MuteMembers) permissions.Add("Mute members");
            if (gp.ViewAuditLog) permissions.Add("View audit log");
            if (gp.CreateInstantInvite) permissions.Add("Create invites");

            return permissions.Count != 0 ? string.Join(", ", permissions) : "-";
        }

        public static string MainChannelPermissionsString(ChannelPermissions cp)
        {
            List<string> permissions = new List<string>();

            if (cp.ManageRoles) permissions.Add("Manage roles");
            if (cp.SendMessages) permissions.Add("Send messages");
            if (cp.ReadMessageHistory) permissions.Add("Read message history");
            if (cp.AddReactions) permissions.Add("Add reactions");
            if (cp.AttachFiles) permissions.Add("Attach files");
            if (cp.ManageChannel) permissions.Add("Manage channel");
            if (cp.ManageMessages) permissions.Add("Manage messages");
            if (cp.ManageWebhooks) permissions.Add("Manage webhooks");

            return permissions.Count != 0 ? string.Join(", ", permissions) : "-";
        }
    }
}
