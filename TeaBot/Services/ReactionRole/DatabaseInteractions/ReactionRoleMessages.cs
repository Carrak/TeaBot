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
        ///     Creates an empty entry for a custom reaction-role message in the database.
        /// </summary>
        /// <param name="guild">The guild the message is created in.</param>
        public async Task CreateCustomReactionRoleMessage(SocketGuild guild)
        {
            string query = @"
            INSERT INTO reaction_role_messages.reaction_roles (rrid, guildid, iscustom) VALUES (DEFAULT, @gid, TRUE)
            ";
            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("gid", (long)guild.Id);

            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        ///     Creates an empty entry for a reaction-role message in the database.
        /// </summary>
        /// <param name="guild">The guild the message is created in.</param>
        public async Task CreateReactionRoleMessage(SocketGuild guild)
        {
            string query = @"
            INSERT INTO reaction_role_messages.reaction_roles (rrid, guildid) VALUES (DEFAULT, @gid)
            ";
            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("gid", (long)guild.Id);

            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        ///     Completely removes a reaction-role message (deleting it from the database and, if it is present, from Discord as well).
        /// </summary>
        /// <param name="guild">The guild the RR message is from.</param>
        /// <param name="index">The row number of the message. If null, the latest message is used.</param>
        public async Task<bool> RemoveReactionRoleMessage(SocketGuild guild, int? index)
        {
            string query = $@"
            DELETE FROM reaction_role_messages.reaction_roles 
            WHERE rrid = reaction_role_messages.get_rrid(@gid, @rn)
            ";

            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("rn", GetIndexForParameter(index));
            cmd.Parameters.AddWithValue("gid", (long)guild.Id);

            int rows = await cmd.ExecuteNonQueryAsync();

            return rows != 0;
        }

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

        public async Task<bool> ToggleCustomAsync(SocketGuild guild, int? index)
        {
            string query = $@"
            UPDATE reaction_role_messages.reaction_roles rr SET iscustom = NOT iscustom
            WHERE rr.rrid = reaction_role_messages.get_rrid(@gid, @rn) 
            RETURNING iscustom, channelid, messageid;
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
    }
}
