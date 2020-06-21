using System.Collections.Generic;
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
        ///     Reprents modules disabled for this context's guild.
        /// </summary>
        public IEnumerable<string> DisabledModules { get; }

        /// <summary>
        ///     Initializes a new <see cref="TeaCommandContext"/> instance with the provided client and message.
        /// </summary>
        /// <param name="client">The underlying client.</param>
        /// <param name="message">The underlying message.</param>
        public TeaCommandContext(DiscordSocketClient client, SocketUserMessage message, string prefix, IEnumerable<string> disabledModules) : base(client, message)
        {
            Prefix = prefix;
            DisabledModules = disabledModules;
        }
    }
}
