using System;
using System.Collections.Generic;
using Discord;
using Discord.WebSocket;
using System.Linq;
using System.Threading.Tasks;
using TeaBot.ReactionCallbackCommands.ReactionRole;

namespace TeaBot.Services.ReactionRole
{
    public partial class ReactionRoleService
    {
        // Error messages to use in exceptions
        public const string NotExistsError = "No reaction-role message exists at such index. Use `{prefix}rr list` to see available reaction-role messages.";
        public const string NoMessagesError = "No reaction-role messages exist in this guild.\n" +
            "Use `{prefix}rr create` to create an empty message or use `{prefix}rr createcustom` to create a custom message.";

        private readonly DatabaseService _database;
        public readonly DiscordSocketClient _client;

        /// <summary>
        ///     Displayed reaction-role messages. The key is the message ID.
        /// </summary>
        private readonly Dictionary<ulong, ReactionRoleMessage> reactionRoleCallbacks = new Dictionary<ulong, ReactionRoleMessage>();

        public ReactionRoleService(DatabaseService database, DiscordSocketClient client)
        {
            _client = client;
            _database = database;

            _client.ReactionAdded += ReactionAdded;
            _client.ReactionRemoved += ReactionRemoved;

            _client.ChannelDestroyed += ChannelDeleted;
            _client.LeftGuild += LeftGuild;
            _client.MessageDeleted += MessageDeleted;
            _client.RoleDeleted += RoleDeleted;
        }

        private object GetIndexForParameter(int? index)
        {
            if (index.HasValue)
                return index.Value;
            else
                return DBNull.Value;
        }

        /// <summary>
        ///     Adds callback to the given message and deletes the previous message (if it's not a custom message)
        /// </summary>
        /// <param name="message">Discord message to add callback to.</param>
        /// <param name="rrmsg">RR message to add callback to.</param>
        /// <exception cref="ArgumentException">
        ///     1. <paramref name="rrmsg"/> is null
        ///     2. The message of the reaction-role message is null
        /// </exception>
        public async Task AddReactionCallbackAsync(IUserMessage message, ReactionRoleMessage rrmsg) {
            if (rrmsg is null || rrmsg.Message is null)
                throw new ArgumentException("Cannot add reaction callback to a null message.");

            // Delete the previous message if it is found
            if (reactionRoleCallbacks.Values.FirstOrDefault(x => x.RRID == rrmsg.RRID && x.Channel.Id != rrmsg.Channel.Id) is FullReactionRoleMessage rrtemp)
                await rrtemp.TryDeleteMessageAsync();

            reactionRoleCallbacks[message.Id] = rrmsg;
        }

        /// <summary>
        ///     Removes callback from a message and removes the message itself.
        /// </summary>
        /// <param name="message">The message to remove callback from.</param>
        public async Task RemoveReactionCallbackAsync(IUserMessage message)
        {
            if (reactionRoleCallbacks.GetValueOrDefault(message.Id) is FullReactionRoleMessage frrmsg)
                await frrmsg.TryDeleteMessageAsync();

            reactionRoleCallbacks.Remove(message.Id);
        }
    }
}
