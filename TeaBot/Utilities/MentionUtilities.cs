using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Discord;
using Discord.WebSocket;
using System.Linq;
using Discord.Commands;

namespace TeaBot.Utilities
{
    public static class MentionUtilities
    {
        public static string DeafenMentions(this string text, SocketMessage message)
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
