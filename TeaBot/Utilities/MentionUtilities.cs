using Discord.WebSocket;

namespace TeaBot.Utilities
{
    public static class MentionUtilities
    {
        /// <summary>
        ///     Removes mentions from user input.
        /// </summary>
        /// <param name="text">The text to remove mentions in.</param>
        /// <param name="message">The text to use mentions from.</param>
        /// <returns>String with deafened mentions.</returns>
        public static string DeafenMentionsFromMessage(this string text, SocketMessage message)
        {
            text = text.Replace("@everyone", "everyone").Replace("@here", "here");
            foreach (var role in message.MentionedRoles)
            {
                text = text.Replace(role.Mention, $"@{role}");
            }
            foreach (var user in message.MentionedUsers)
            {
                text = text.Replace($"<@{user.Id}>", $"@{user}");
                text = text.Replace($"<@!{user.Id}>", $"@{user}");
            }
            return text;
        }
    }
}
