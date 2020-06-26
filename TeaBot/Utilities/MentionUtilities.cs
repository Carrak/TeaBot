using Discord.WebSocket;

namespace TeaBot.Utilities
{
    public static class MentionUtilities
    {
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
