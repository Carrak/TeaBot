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
        public async Task<ReactionRoleMessage> PrepareReactionRoleMessageAsync(RawReactionRoleMessage rawRRmsg)
        {
            var guild = _client.GetGuild(rawRRmsg.GuildId);

            // Delete this reaction-role message if its guild is not present (either because the guild was deleted or the bot was kicked/banned).
            if (guild is null)
            {
                await RemoveGuildEntry(rawRRmsg.GuildId);
                return null;
            }

            ITextChannel channel = null;
            RestUserMessage message = null;

            if (rawRRmsg.ChannelId.HasValue)
            {
                channel = guild.GetChannel(rawRRmsg.ChannelId.Value) as ITextChannel;

                if (channel != null)
                {
                    if (rawRRmsg.MessageId.HasValue)
                    {
                        try
                        {
                            message = await channel.GetMessageAsync(rawRRmsg.MessageId.Value) as RestUserMessage;

                            // Delete the reaction-role's info about the message if the message itself is null
                            if (message is null)
                            {
                                await RemoveMessageFromRRMAsync(channel.Id, rawRRmsg.MessageId.Value);
                            }
                        }
                        catch (Discord.Net.HttpException)
                        {

                        }
                    }
                }
                else
                    await RemoveChannelFromRRMAsync(rawRRmsg.ChannelId.Value);
            } 

            // Get emote-role pairs for this reaction-role message
            Dictionary<IEmote, EmoteRolePair> pairs = new Dictionary<IEmote, EmoteRolePair>();
            foreach (var pair in rawRRmsg.EmoteRolePairs)
            {
                IEmote emote;
                if (Emote.TryParse(pair.Emote, out var e))
                    emote = e;
                else if (new Emoji(pair.Emote) is Emoji emoji)
                    emote = emoji;
                else
                    throw new NotImplementedException();

                var role = guild.Roles.FirstOrDefault(x => x.Id == pair.RoleId);

                // Delete the role from the pair if it is no longer present
                if (role is null)
                {
                    await RemoveEmoteRolePair(pair.RoleId);
                    continue;
                }

                pairs.Add(emote, new EmoteRolePair(emote, role));
            }

            return new ReactionRoleMessage(rawRRmsg.RRID, pairs, this, guild, channel, rawRRmsg.Color, rawRRmsg.Name, message);
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
