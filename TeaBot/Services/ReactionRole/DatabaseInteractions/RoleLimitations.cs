using Discord.WebSocket;
using Discord;
using System.Threading.Tasks;
using System;

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
        public async Task AddAllowedRole(SocketGuild guild, int? index, IEmote emote, IRole role)
        {
            string query = @$"
            INSERT INTO reaction_role_messages.allowed_roles VALUES
            (reaction_role_messages.get_pairid(reaction_role_messages.get_rrid(@gid, @rn), @emote), @rid)
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
        public async Task AddProhibitedRole(SocketGuild guild, int? index, IEmote emote, IRole role)
        {
            string query = @$"
            INSERT INTO reaction_role_messages.prohibited_roles VALUES
            (reaction_role_messages.get_pairid(reaction_role_messages.get_rrid(@gid, @rn), @emote), @rid)
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
        public async Task AddGlobalAllowedRole(SocketGuild guild, int? index, IRole role)
        {
            string query = @$"
            INSERT INTO reaction_role_messages.global_allowed_roles VALUES (reaction_role_messages.get_rrid(@gid, @rn), @rid)
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
        public async Task AddGlobalProhibitedRole(SocketGuild guild, int? index, IRole role)
        {
            string query = @$"
            INSERT INTO reaction_role_messages.global_prohibited_roles VALUES (reaction_role_messages.get_rrid(@gid, @rn), @rid);
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
        public async Task<bool> RemoveAllowedRole(SocketGuild guild, int? index, IEmote emote, IRole role)
        {
            string query = @$"
            DELETE FROM reaction_role_messages.allowed_roles ar
            WHERE ar.pairid=reaction_role_messages.get_pairid(reaction_role_messages.get_rrid(@gid, @rn), @emote)
            AND roleid=@rid
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
            DELETE FROM reaction_role_messages.prohibited_roles pr
            WHERE pr.pairid=reaction_role_messages.get_pairid(reaction_role_messages.get_rrid(@gid, @rn), @emote)
            AND roleid=@rid
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
        public async Task<bool> RemoveGlobalAllowedRole(SocketGuild guild, int? index, IRole role)
        {
            string query = @$"
            DELETE FROM reaction_role_messages.global_allowed_roles gar
            WHERE gar.rrid = reaction_role_messages.get_rrid(@gid, @rn)
            AND roleid=@rid
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
            DELETE FROM reaction_role_messages.global_prohibited_roles gpr
            WHERE gpr.rrid = reaction_role_messages.get_rrid(@gid, @rn)
            AND roleid=@rid
            ";

            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("rn", GetIndexForParameter(index));
            cmd.Parameters.AddWithValue("gid", (long)guild.Id);
            cmd.Parameters.AddWithValue("rid", (long)role.Id);

            return await cmd.ExecuteNonQueryAsync() != 0;
        }

    }
}
