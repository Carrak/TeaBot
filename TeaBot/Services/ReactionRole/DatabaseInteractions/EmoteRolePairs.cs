﻿using Discord.WebSocket;
using Discord;
using System.Threading.Tasks;

namespace TeaBot.Services.ReactionRole
{
    partial class ReactionRoleService
    {
        /// <summary>
        ///     Adds an emote-role pair to a reaction-role message.
        /// </summary>
        /// <param name="guild">The guild the RR message is from.</param>
        /// <param name="index">The row number of the message. If null, the latest message is used.</param>
        /// <param name="emote">The emote of the pair.</param>
        /// <param name="role">The role of the pair.</param>
        public async Task AddPairAsync(SocketGuild guild, int? index, IEmote emote, IRole role)
        {
            string query = @$"
            INSERT INTO reaction_role_messages.emote_role_pairs (rrid, emote, roleid) 
            VALUES (reaction_role_messages.get_rrid(@gid, @rn), @emote, @rid)
            ";

            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("rn", GetIndexForParameter(index));
            cmd.Parameters.AddWithValue("gid", (long)guild.Id);
            cmd.Parameters.AddWithValue("emote", emote.ToString());
            cmd.Parameters.AddWithValue("rid", (long)role.Id);

            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        ///     Remove an emote-role pair frm a reaction-role message.
        /// </summary>
        /// <param name="guild">The guild the RR message is from.</param>
        /// <param name="index">The row number of the message. If null, the latest message is used.</param>
        /// <param name="emote">The emote of the pair.</param>
        public async Task RemovePairAsync(SocketGuild guild, int? index, IEmote emote)
        {
            string query = @$"
            DELETE FROM reaction_role_messages.emote_role_pairs er
            WHERE er.pairid = reaction_role_messages.get_pairid(reaction_role_messages.get_rrid(@gid, @rn), @emote);
            ";

            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("rn", GetIndexForParameter(index));
            cmd.Parameters.AddWithValue("gid", (long)guild.Id);
            cmd.Parameters.AddWithValue("emote", emote.ToString());

            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        ///     Remove an emote-role pair frm a reaction-role message.
        /// </summary>
        /// <param name="guild">The guild the RR message is from.</param>
        /// <param name="index">The row number of the message. If null, the latest message is used.</param>
        /// <param name="role">The role of the pair.</param>
        public async Task RemovePairAsync(SocketGuild guild, int? index, IRole role)
        {
            string query = @$"
            DELETE FROM reaction_role_messages.emote_role_pairs erp 
            WHERE erp.pairid = reaction_role_messages.get_pairid(reaction_role_messages.get.rrid(@gid, @rn), @rid));
            ";

            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("rn", GetIndexForParameter(index));
            cmd.Parameters.AddWithValue("gid", (long)guild.Id);
            cmd.Parameters.AddWithValue("rid", (long)role.Id);

            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        ///     Changes the emote of an emote-role pair.
        /// </summary>
        /// <param name="guild">The guild the RR message is from.</param>
        /// <param name="index">The row number of the message. If null, the latest message is used.</param>
        /// <param name="role">The role of the pair.</param>
        /// <param name="emote">The emote of the pair.</param>
        public async Task ChangeEmote(SocketGuild guild, int? index, IRole role, IEmote emote)
        {
            string query = @$"
            UPDATE reaction_role_messages.emote_role_pairs er SET emote=@emote
            WHERE er.pairid = reaction_role_messages.get_pairid(reaction_role_messages.get_rrid(@gid, @rn), @rid);
            ";

            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("rn", GetIndexForParameter(index));
            cmd.Parameters.AddWithValue("gid", (long)guild.Id);
            cmd.Parameters.AddWithValue("emote", emote.ToString());
            cmd.Parameters.AddWithValue("rid", (long)role.Id);

            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        ///     Changes the role of an emote-role pair.
        /// </summary>
        /// <param name="guild">The guild the RR message is from.</param>
        /// <param name="index">The row number of the message. If null, the latest message is used.</param>
        /// <param name="role">The role of the pair.</param>
        /// <param name="emote">The emote of the pair.</param>
        public async Task ChangeRole(SocketGuild guild, int? index, IRole role, IEmote emote)
        {
            string query = @$"
            UPDATE reaction_role_messages.emote_role_pairs er SET roleid=@rid
            WHERE er.pairid = reaction_role_messages.get_pairid(reaction_role_messages.get_rrid(@gid, @rn), @emote);
            ";

            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("rn", GetIndexForParameter(index));
            cmd.Parameters.AddWithValue("gid", (long)guild.Id);
            cmd.Parameters.AddWithValue("emote", emote.ToString());
            cmd.Parameters.AddWithValue("rid", (long)role.Id);

            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        ///     Swaps the positions of two emote-role pairs.
        /// </summary>
        /// <param name="guild">The guild the RR message is from.</param>
        /// <param name="index">The row number of the message. If null, the latest message is used.</param>
        /// <param name="emote1">An emote from an emote-pair.</param>
        /// <param name="emote2">An emote from the pair to swap with.</param>
        public async Task ChangeOrder(SocketGuild guild, int? index, IEmote emote1, IEmote emote2)
        {
            string query = @$"
            UPDATE reaction_role_messages.emote_role_pairs erp SET index=erptemp.index
            FROM reaction_role_messages.emote_role_pairs AS erptemp
            WHERE erp.pairid IN (reaction_role_messages.get_pairid(reaction_role_messages.get_rrid(@gid, @rn), @emotea), reaction_role_messages.get_pairid(reaction_role_messages.get_rrid(@gid, @rn), @emoteb))
            AND erptemp.pairid IN (reaction_role_messages.get_pairid(reaction_role_messages.get_rrid(@gid, @rn), @emotea), reaction_role_messages.get_pairid(reaction_role_messages.get_rrid(@gid, @rn), @emoteb))
            AND erp.pairid <> erptemp.pairid
            ";

            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("rn", GetIndexForParameter(index));
            cmd.Parameters.AddWithValue("gid", (long)guild.Id);
            cmd.Parameters.AddWithValue("emotea", emote1.ToString());
            cmd.Parameters.AddWithValue("emoteb", emote2.ToString());

            await cmd.ExecuteNonQueryAsync();
        }

    }
}