using Discord.Commands;
using Discord.WebSocket;

namespace TeaBot.Commands
{
    /// <inheritdoc/>
    public class TeaCommandContext : SocketCommandContext
    {
        /// <summary>
        ///     Prefix that was used to execute the command.
        /// </summary>
        public string Prefix { get; }

        /// <summary>
        ///     Initializes a new <see cref="TeaCommandContext" /> class with the provided client and message.
        /// </summary>
        /// <param name="client">The underlying client.</param>
        /// <param name="message">The underlying message.</param>
        public TeaCommandContext(DiscordSocketClient client, SocketUserMessage message, string prefix) : base(client, message)
        {
            Prefix = prefix;
        }
    }
}
