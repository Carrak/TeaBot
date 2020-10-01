using System;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace TeaBot.Services.ReactionRole
{
    partial class ReactionRoleService
    {
        /// <summary>
        ///     Adds a role to the list of allowed roles of an emote-role pair.
        /// </summary>
        /// <param name="guild">The guild the RR message is from.</param>
        /// <param name="index">The row number of the message. If null, the latest message is used.</param>
        /// <param name="emote">The emote of the pair.</param>
        /// <param name="role">The role to add to the allowed list.</param>
        public async Task AddAllowedRoleAsync(SocketGuild guild, int? index, IEmote emote, IRole role)
        {
            string query = @$"
            INSERT INTO reaction_role_messages.role_restrictions VALUES
            (reaction_role_messages.get_pairid(reaction_role_messages.get_rrid(@gid, @rn), @emote), @rid, TRUE)
            ";

            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("rn", GetIndexForParameter(index));
            cmd.Parameters.AddWithValue("gid", (long)guild.Id);
            cmd.Parameters.AddWithValue("emote", emote.ToString());
            cmd.Parameters.AddWithValue("rid", (long)role.Id);

            Console.WriteLine(await cmd.ExecuteNonQueryAsync());
        }

        /// <summary>
        ///     Adds a role to the list of prohibited roles of an emote-role pair.
        /// </summary>
        /// <param name="guild">The guild the RR message is from.</param>
        /// <param name="index">The row number of the message. If null, the latest message is used.</param>
        /// <param name="emote">The emote of the pair.</param>
        /// <param name="role">The role to add to the list.</param>
        public async Task AddProhibitedRoleAsync(SocketGuild guild, int? index, IEmote emote, IRole role)
        {
            string query = @$"
            INSERT INTO reaction_role_messages.role_restrictions VALUES
            (reaction_role_messages.get_pairid(reaction_role_messages.get_rrid(@gid, @rn), @emote), @rid, FALSE)
            ";

            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("rn", GetIndexForParameter(index));
            cmd.Parameters.AddWithValue("gid", (long)guild.Id);
            cmd.Parameters.AddWithValue("emote", emote.ToString());
            cmd.Parameters.AddWithValue("rid", (long)role.Id);

            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        ///     Adds a role to the list of global allowed roles.
        /// </summary>
        /// <param name="guild">The guild the RR message is from.</param>
        /// <param name="index">The row number of the message. If null, the latest message is used.</param>
        /// <param name="role">The role to add to the list of global allowed roles.</param>
        public async Task AddGlobalAllowedRoleAsync(SocketGuild guild, int? index, IRole role)
        {
            string query = @$"
            INSERT INTO reaction_role_messages.global_role_restrictions VALUES (reaction_role_messages.get_rrid(@gid, @rn), @rid, TRUE);
            ";
            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("rn", GetIndexForParameter(index));
            cmd.Parameters.AddWithValue("gid", (long)guild.Id);
            cmd.Parameters.AddWithValue("rid", (long)role.Id);

            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        ///     Adds a role to the list of global prohibited roles.
        /// </summary>
        /// <param name="guild">The guild the RR message is from.</param>
        /// <param name="index">The row number of the message. If null, the latest message is used.</param>
        /// <param name="role">The role to add to the list of global allowed roles.</param>
        public async Task AddGlobalProhibitedRoleAsync(SocketGuild guild, int? index, IRole role)
        {
            string query = @$"
            INSERT INTO reaction_role_messages.global_role_restrictions VALUES (reaction_role_messages.get_rrid(@gid, @rn), @rid, FALSE);
            ";
            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("rn", GetIndexForParameter(index));
            cmd.Parameters.AddWithValue("gid", (long)guild.Id);
            cmd.Parameters.AddWithValue("rid", (long)role.Id);

            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        ///     Removes a role from the list of allowed roles of an emote-role pair.
        /// </summary>
        /// <param name="guild">The guild the RR message is from.</param>
        /// <param name="index">The row number of the message. If null, the latest message is used.</param>
        /// <param name="emote">The emote of the pair.</param>
        /// <param name="role">The role to add to the list.</param>
        public async Task<bool> RemoveAllowedRoleAsync(SocketGuild guild, int? index, IEmote emote, IRole role)
        {
            string query = @$"
            DELETE FROM reaction_role_messages.role_restrictions ar
            WHERE ar.pairid=reaction_role_messages.get_pairid(reaction_role_messages.get_rrid(@gid, @rn), @emote)
            AND roleid=@rid
            AND allowed
            ";

            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("rn", GetIndexForParameter(index));
            cmd.Parameters.AddWithValue("gid", (long)guild.Id);
            cmd.Parameters.AddWithValue("emote", emote.ToString());
            cmd.Parameters.AddWithValue("rid", (long)role.Id);

            return await cmd.ExecuteNonQueryAsync() != 0;
        }

        /// <summary>
        ///     Removes a role from the list of prohibited roles of an emote-role pair.
        /// </summary>
        /// <param name="guild">The guild the RR message is from.</param>
        /// <param name="index">The row number of the message. If null, the latest message is used.</param>
        /// <param name="emote">The emote of the pair.</param>
        /// <param name="role">The role to add to the allowed list.</param>
        public async Task<bool> RemoveProhibitedRole(SocketGuild guild, int? index, IEmote emote, IRole role)
        {
            string query = @$"
            DELETE FROM reaction_role_messages.role_restrictions pr
            WHERE pr.pairid=reaction_role_messages.get_pairid(reaction_role_messages.get_rrid(@gid, @rn), @emote)
            AND roleid=@rid
            AND NOT allowed
            ";

            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("rn", GetIndexForParameter(index));
            cmd.Parameters.AddWithValue("gid", (long)guild.Id);
            cmd.Parameters.AddWithValue("emote", emote.ToString());
            cmd.Parameters.AddWithValue("rid", (long)role.Id);

            return await cmd.ExecuteNonQueryAsync() != 0;
        }

        /// <summary>
        ///     Removes a role from the list of global prohibited roles.
        /// </summary>
        /// <param name="guild">The guild the RR message is from.</param>
        /// <param name="index">The row number of the message. If null, the latest message is used.</param>
        /// <param name="role">The role to remove from the list of global prohibited roles.</param>
        public async Task<bool> RemoveGlobalAllowedRoleAsync(SocketGuild guild, int? index, IRole role)
        {
            string query = @$"
            DELETE FROM reaction_role_messages.global_role_restrictions grr
            WHERE grr.rrid = reaction_role_messages.get_rrid(@gid, @rn)
            AND roleid=@rid
            AND allowed
            ";

            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("rn", GetIndexForParameter(index));
            cmd.Parameters.AddWithValue("gid", (long)guild.Id);
            cmd.Parameters.AddWithValue("rid", (long)role.Id);

            return await cmd.ExecuteNonQueryAsync() != 0;
        }

        /// <summary>
        ///     Removes a role from the list of global prohibited roles.
        /// </summary>
        /// <param name="guild">The guild the RR message is from.</param>
        /// <param name="index">The row number of the message. If null, the latest message is used.</param>
        /// <param name="role">The role to remove from the list of global prohibited roles.</param>
        public async Task<bool> RemoveGlobalProhibitedRole(SocketGuild guild, int? index, IRole role)
        {
            string query = @$"
            DELETE FROM reaction_role_messages.global_role_restrictions grr
            WHERE grr.rrid = reaction_role_messages.get_rrid(@gid, @rn)
            AND roleid=@rid
            AND NOT allowed
            ";

            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("rn", GetIndexForParameter(index));
            cmd.Parameters.AddWithValue("gid", (long)guild.Id);
            cmd.Parameters.AddWithValue("rid", (long)role.Id);

            return await cmd.ExecuteNonQueryAsync() != 0;
        }

    }
}
