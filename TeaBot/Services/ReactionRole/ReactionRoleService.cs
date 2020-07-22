using System;
using System.Collections.Generic;
using Discord;
using Discord.WebSocket;
using TeaBot.ReactionCallbackCommands;
using System.Linq;
using System.Threading.Tasks;

namespace TeaBot.Services.ReactionRole
{
    public partial class ReactionRoleService
    {
        private readonly DatabaseService _database;
        private readonly DiscordSocketClient _client;
        private readonly Dictionary<ulong, ReactionRoleMessage> reactionRoleMessages = new Dictionary<ulong, ReactionRoleMessage>();

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

        /// <summary>
        ///     Adds callback to the given message and deletes the previous message if its .
        /// </summary>
        /// <param name="message">Discord message to add callback to.</param>
        /// <param name="rrmsg">RR message to add callback to.</param>
        public async Task AddReactionCallback(IUserMessage message, ReactionRoleMessage rrmsg) {
            if (rrmsg is null || rrmsg.Message is null)
                throw new ReactionRoleServiceException("Cannot add reaction callback to a null message.");

            if (reactionRoleMessages.Values.FirstOrDefault(x => x.RRID == rrmsg.RRID && x.Channel.Id != rrmsg.Channel.Id) is ReactionRoleMessage rrtemp)
                await rrtemp.TryDeleteMessageAsync();

            reactionRoleMessages[message.Id] = rrmsg;
        }

        public void RemoveReactionCallback(IUserMessage message)
        {
            reactionRoleMessages.Remove(message.Id);
        }
    }

    class ReactionRoleServiceException : Exception
    {
        public ReactionRoleServiceException(string message) 
            : base(message)
        {

        }
    }
}
