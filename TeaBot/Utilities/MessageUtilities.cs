using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;

namespace TeaBot.Utilities
{
    static class MessageUtilities
    {
        /// <summary>
        ///     Removes mentions from user input.
        /// </summary>
        /// <param name="text">The text to remove mentions in.</param>
        /// <param name="message">The text to use mentions from.</param>
        /// <returns>String with deafened mentions.</returns>
        public static string DeafenMentionsFromMessage(this string text, SocketMessage message)
        {
            while (true)
            {
                string changed = text.Replace("@everyone", "").Replace("@here", "");

                if (text != changed)
                    text = changed;
                else
                    break;
            }

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

        public async static Task<IMessage> ParseMessageFromLinkAsync(SocketCommandContext context, string link)
        {
            string pattern = @"(http|https?:\/\/)?(www\.)?(discord\.(gg|io|me|li|com)|discord(app)?\.com\/channels)\/(?<Guild>\w+)\/(?<Channel>\w+)\/(?<Message>\w+)";
            var match = System.Text.RegularExpressions.Regex.Match(link, pattern);

            // Check if the given link matches the pattern
            if (!match.Success)
                throw new FormatException("The given link does not match Discord message link pattern - `https://discordapp.com/channels/{guild}/{channel}/{id}`");

            // Check if the guild in the link is valid
            if (!ulong.TryParse(match.Groups["Guild"].Value, out var guildid))
                throw new FormatException("Invalid guild ID.");

            // Check if the channel is valid
            if (!ulong.TryParse(match.Groups["Channel"].Value, out var channelid))
                throw new FormatException("Invalid channel ID.");

            // Check if the guild is the same as this one
            if (context.Guild.Id != guildid)
                throw new FormatException("The specified message is in another guild.");

            // Check if the message is valid
            if (!ulong.TryParse(match.Groups["Message"].Value, out var messageid))
                throw new FormatException("Invalid message ID.");

            // Check if the channel exists
            if (!(context.Guild.GetTextChannel(channelid) is ITextChannel channel))
                throw new ChannelNotFoundException($"Channel with ID `{channelid}` does not exist.");

            // Return the message
            return await channel.GetMessageAsync(messageid);
        }
    }

    class ChannelNotFoundException : Exception
    {
        public ChannelNotFoundException(string message) : base(message) { }
    }
}
