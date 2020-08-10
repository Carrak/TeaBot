using System;
using System.Threading.Tasks;
using Discord;
using Discord.Net;
using Discord.WebSocket;

namespace TeaBot.Services.ReactionRole
{
    partial class ReactionRoleService
    {
        /// <summary>
        ///     Changes the name of a reaction-role message.
        /// </summary>
        /// <param name="guild">The guild the RR message is from.</param>
        /// <param name="index">The row number of the message. If null, the latest message is used.</param>
        /// <param name="newName">The new name to set.</param>
        public async Task ChangeNameAsync(SocketGuild guild, int? index, string newName)
        {
            string query = @$"
            INSERT INTO reaction_role_messages.reaction_roles_data (rrid, name) VALUES (reaction_role_messages.get_rrid(@gid, @rn), @name)
            ON CONFLICT (rrid)
                DO UPDATE SET name = @name
            ";

            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("rn", GetIndexForParameter(index));
            cmd.Parameters.AddWithValue("gid", (long)guild.Id);

            if (newName is null)
                cmd.Parameters.AddWithValue("name", DBNull.Value);
            else
                cmd.Parameters.AddWithValue("name", newName);

            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        ///     Changes the color of a reaction-role message.
        /// </summary>
        /// <param name="guild">The guild the RR message is from.</param>
        /// <param name="index">The row number of the message. If null, the latest message is used.</param>
        /// <param name="newColor">The new color to set. If null, the default color will be used.</param>
        public async Task ChangeColorAsync(SocketGuild guild, int? index, Color? newColor)
        {
            string query = @$"
            INSERT INTO reaction_role_messages.reaction_roles_data (rrid, color) VALUES (reaction_role_messages.get_rrid(@gid, @rn), @color)
            ON CONFLICT (rrid)
                DO UPDATE SET color=@color
            ";

            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("rn", GetIndexForParameter(index));
            cmd.Parameters.AddWithValue("gid", (long)guild.Id);

            if (newColor.HasValue)
                cmd.Parameters.AddWithValue("color", (int)newColor.Value.RawValue);
            else
                cmd.Parameters.AddWithValue("color", DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        ///     Changes the channel of a reaction-role message.
        /// </summary>
        /// <param name="guild">The guild the RR message is from.</param>
        /// <param name="index">The row number of the message. If null, the latest message is used.</param>
        /// <param name="channel">The new channel of the message.</param>
        public async Task ChangeChannelAsync(SocketGuild guild, int? index, ITextChannel channel)
        {
            string query = @$"
            SELECT reaction_role_messages.ensure_not_custom_on_only_channel_update(reaction_role_messages.get_rrid(@gid, @rn));

            UPDATE reaction_role_messages.reaction_roles AS rr SET channelid = @cid, messageid = NULL
            WHERE rrid = reaction_role_messages.get_rrid(@gid, @rn)
            ";

            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("rn", GetIndexForParameter(index));
            cmd.Parameters.AddWithValue("gid", (long)guild.Id);
            cmd.Parameters.AddWithValue("cid", (long)channel.Id);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task ChangeMessageAsync(SocketGuild guild, int? index, IUserMessage message)
        {
            string query = @$"
            SELECT reaction_role_messages.ensure_custom_on_only_message_update(reaction_role_messages.get_rrid(@gid, @rn));

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

        /// <summary>
        ///     Changes the reaction limit of a reaction-role message (how many roles a user can assign to themselves through a reaction-role message)
        /// </summary>
        /// <param name="guild">The guild the RR message is from.</param>
        /// <param name="index">The row number of the message. If null, the latest message is used.</param>
        /// <param name="limit">The new limit to set.</param>
        public async Task ChangeLimit(SocketGuild guild, int? index, int limit)
        {
            string query = @$"
            UPDATE reaction_role_messages.reaction_roles rr SET reaction_limit=@limit
            WHERE rr.rrid = reaction_role_messages.get_rrid(@gid, @rn);
            ";

            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("rn", GetIndexForParameter(index));
            cmd.Parameters.AddWithValue("gid", (long)guild.Id);
            cmd.Parameters.AddWithValue("limit", limit);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<bool> ToggleCustom(SocketGuild guild, int? index)
        {
            string query = $@"
            UPDATE reaction_role_messages.reaction_roles rr SET iscustom = NOT iscustom
            WHERE rr.rrid = reaction_role_messages.get_rrid(@gid, @rn) RETURNING iscustom, channelid, messageid;
            ";

            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("rn", GetIndexForParameter(index));
            cmd.Parameters.AddWithValue("gid", (long)guild.Id);

            await using var reader = await cmd.ExecuteReaderAsync();
            await reader.ReadAsync();

            bool iscustom = reader.GetBoolean(0);

            if (iscustom && !await reader.IsDBNullAsync(1) && !await reader.IsDBNullAsync(2))
            {
                var channel = guild.GetChannel((ulong)reader.GetInt64(1)) as ITextChannel;
                try
                {
                    if (await channel.GetMessageAsync((ulong)reader.GetInt64(2)) is IUserMessage message)
                        await RemoveReactionCallbackAsync(message);
                }
                catch (HttpException) { }
            }

            return iscustom;
        }

        public async Task ChangeReactionRoleMessageDescription(SocketGuild guild, int? index, string description)
        {
            string query = $@"
            INSERT INTO reaction_role_messages.reaction_roles_data (rrid, description) VALUES (reaction_role_messages.get_rrid(@gid, @rn), @description)
            ON CONFLICT (rrid)
                DO UPDATE SET description=@description
            ";

            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("rn", GetIndexForParameter(index));
            cmd.Parameters.AddWithValue("gid", (long)guild.Id);

            if (string.IsNullOrEmpty(description))
                cmd.Parameters.AddWithValue("description", DBNull.Value);
            else
                cmd.Parameters.AddWithValue("description", description);

            await cmd.ExecuteNonQueryAsync();
        }


        public async Task ChangePairDescription(SocketGuild guild, int? index, IEmote emote, string description)
        {
            string query = $@"
            INSERT INTO reaction_role_messages.emote_role_pairs_data VALUES (reaction_role_messages.get_pairid(reaction_role_messages.get_rrid(@gid, @rn), @emote), @description)
            ON CONFLICT (pairid)
                DO UPDATE SET description=@description
            ";

            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("rn", GetIndexForParameter(index));
            cmd.Parameters.AddWithValue("gid", (long)guild.Id);
            cmd.Parameters.AddWithValue("emote", emote.ToString());

            if (string.IsNullOrEmpty(description))
                cmd.Parameters.AddWithValue("description", DBNull.Value);
            else
                cmd.Parameters.AddWithValue("description", description);

            await cmd.ExecuteNonQueryAsync();
        }
    }
}
