using System;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using TeaBot.ReactionCallbackCommands.ReactionRole;

namespace TeaBot.Services.ReactionRole
{
    partial class ReactionRoleService
    {
        /// <summary>
        ///     Gets the reaction-role message from the database.
        /// </summary>
        /// <param name="guild">The guild to retrieve the RR message for.</param>
        /// <param name="index">The row number of the message. If null, the latest message is used.</param>
        public async Task<ReactionRoleMessage> GetReactionRoleMessageAsync(SocketGuild guild, int? index)
        {
            string query = @"
            SELECT reaction_role_messages.get_reaction_role_message(reaction_role_messages.get_rrid(@gid, @rn));
            ";

            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("rn", GetIndexForParameter(index));
            cmd.Parameters.AddWithValue("gid", (long)guild.Id);

            await using var reader = await cmd.ExecuteReaderAsync();
            await reader.ReadAsync();
            string json = reader.GetString(0);
            await reader.CloseAsync();

            return await ReactionRoleMessage.CreateAsync(this, DeserealizeReactionRoleMessage(json));
        }

        public async Task<FullReactionRoleMessage> DisplayFullReactionRoleMessageAsync(SocketGuild guild, int? index, ITextChannel channel)
        {
            if (!(await GetReactionRoleMessageAsync(guild, index) is FullReactionRoleMessage frrmsg))
                throw new ReactionRoleMessageException($"Display custom messages using {ReactionRoleServiceMessages.ReactionRoleMessageCommandString("{prefix}", "displaycustom")}");

            if (frrmsg.EmoteRolePairs.Count == 0)
                throw new ReactionRoleMessageException($"This reaction-role message does not have any emote-role pairs.\nAdd them before displaying the message using " +
                    $"{ReactionRoleServiceMessages.ReactionRoleMessageCommandString("{prefix}", "addpair")}");

            var message = frrmsg.Message;

            await frrmsg.DisplayAsync(channel);

            // Update database entries for this RR message in case they aren't the same
            if (message == null || frrmsg.Message.Id != message.Id)
                await ChangeMessageAsync(guild, index, frrmsg.Message);

            if (frrmsg.LimitId.HasValue)
                await UpdateLimitAsync(frrmsg.LimitId.Value);

            return frrmsg;
        }

        public async Task<ReactionRoleMessage> DisplayCustomReactionRoleMessageAsync(SocketGuild guild, int? index, IUserMessage message)
        {
            var rrmsg = await GetReactionRoleMessageAsync(guild, index);

            if (rrmsg is FullReactionRoleMessage)
                throw new ReactionRoleMessageException($"Display non-custom messages using {ReactionRoleServiceMessages.ReactionRoleMessageCommandString("{prefix}", "display")}");

            if (rrmsg.EmoteRolePairs.Count == 0)
                throw new ReactionRoleMessageException($"This reaction-role message does not have any emote-role pairs.\nAdd them before displaying the message using " +
                    $"{ReactionRoleServiceMessages.ReactionRoleMessageCommandString("{prefix}", "addpair")}");

            var prevMessage = rrmsg.Message;
            rrmsg.Message = message;

            await rrmsg.AddReactionCallbackAsync();

            if (prevMessage == null || message.Id != prevMessage.Id)
                await ChangeMessageAsync(guild, index, message);

            if (rrmsg.LimitId.HasValue)
                await UpdateLimitAsync(rrmsg.LimitId.Value);

            return rrmsg;
        }

        /// <summary>
        ///     Gets the message from the database and commits all changes made to the message as well as adding callback to it.
        /// </summary>
        /// <param name="guild">The guild the RR message is from.</param>
        /// <param name="index">The row number of the message. If null, the latest message is used.</param>
        public async Task<ReactionRoleMessage> DisplayReactionRoleMessageAsync(SocketGuild guild, int? index)
        {
            var rrmsg = await GetReactionRoleMessageAsync(guild, index);

            if (rrmsg.EmoteRolePairs.Count == 0)
                throw new ReactionRoleMessageException($"This reaction-role message does not have any emote-role pairs.\nAdd them before displaying the message using " +
                    $"{ReactionRoleServiceMessages.ReactionRoleMessageCommandString("{prefix}", "addpair")}");

            if (rrmsg is FullReactionRoleMessage frrmsg)
            {
                if (frrmsg.Message is null)
                    throw new ReactionRoleMessageException("Display using " +
                        $"{ReactionRoleServiceMessages.ReactionRoleMessageCommandString("{prefix}", "display")}.");

                await frrmsg.DisplayAsync();

                if (frrmsg.LimitId.HasValue)
                    await UpdateLimitAsync(frrmsg.LimitId.Value);

                return frrmsg;
            }
            else
            {
                if (rrmsg.Message is null)
                    throw new ReactionRoleMessageException($"Display using " +
                        $"{ReactionRoleServiceMessages.ReactionRoleMessageCommandString("{prefix}", "displaycustom")}");

                await rrmsg.AddReactionCallbackAsync();

                if (rrmsg.LimitId.HasValue)
                    await UpdateLimitAsync(rrmsg.LimitId.Value);

                return rrmsg;
            }
        }

        private async Task ChangeMessageAsync(SocketGuild guild, int? index, IUserMessage message)
        {
            string query = @$"
            UPDATE reaction_role_messages.reaction_roles rr SET channelid=@cid, messageid=@mid
            WHERE rrid=reaction_role_messages.get_rrid(@gid, @rn);
            ";

            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("rn", GetIndexForParameter(index));
            cmd.Parameters.AddWithValue("gid", (long)guild.Id);
            cmd.Parameters.AddWithValue("cid", (long)message.Channel.Id);
            cmd.Parameters.AddWithValue("mid", (long)message.Id);

            await cmd.ExecuteNonQueryAsync();
        }
    }

    sealed class ReactionRoleMessageException : Exception
    {
        public ReactionRoleMessageException(string message) : base(message) { }
    }
}
