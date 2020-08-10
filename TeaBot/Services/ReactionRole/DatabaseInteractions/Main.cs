using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Microsoft.Collections.Extensions;
using TeaBot.ReactionCallbackCommands.ReactionRole;

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
        ///     Gets the reaction-role message from the database.
        /// </summary>
        /// <param name="guild">The guild to retrieve the RR message for.</param>
        /// <param name="index">The row number of the message. If null, the latest message is used.</param>
        public async Task<ReactionRoleMessage> GetReactionRoleMessageAsync(SocketGuild guild, int? index)
        {
            string query = @$"
            WITH erp AS (SELECT rrid, pairid FROM reaction_role_messages.emote_role_pairs)
            SELECT ar.pairid, ar.roleid FROM reaction_role_messages.allowed_roles ar, erp
            WHERE erp.rrid = reaction_role_messages.get_rrid(@gid, @rn)
            AND erp.pairid=ar.pairid;

            WITH erp AS (SELECT rrid, pairid FROM reaction_role_messages.emote_role_pairs)
            SELECT pr.pairid, pr.roleid FROM reaction_role_messages.prohibited_roles pr, erp
            WHERE erp.rrid = reaction_role_messages.get_rrid(@gid, @rn)
            AND erp.pairid=pr.pairid;

            SELECT roleid FROM reaction_role_messages.global_allowed_roles gar
            WHERE gar.rrid = reaction_role_messages.get_rrid(@gid, @rn);

            SELECT roleid FROM reaction_role_messages.global_prohibited_roles gpr
            WHERE gpr.rrid = reaction_role_messages.get_rrid(@gid, @rn);

            SELECT emote, roleid, pairid FROM reaction_role_messages.emote_role_pairs er
            WHERE er.rrid = reaction_role_messages.get_rrid(@gid, @rn)
            ORDER BY index; 

            SELECT rr.channelid, rr.messageid, rr.rrid, rr.reaction_limit, rr.iscustom FROM reaction_role_messages.reaction_roles rr
            WHERE rr.rrid = reaction_role_messages.get_rrid(@gid, @rn);

            SELECT rrd.name, rrd.color, rrd.description FROM reaction_role_messages.reaction_roles_data rrd
            WHERE rrd.rrid = reaction_role_messages.get_rrid(@gid, @rn);

            WITH erp AS (SELECT rrid, pairid FROM reaction_role_messages.emote_role_pairs)
            SELECT erpd.pairid, erpd.description FROM reaction_role_messages.emote_role_pairs_data erpd, erp 
            WHERE erp.rrid = reaction_role_messages.get_rrid(@gid, @rn)
            AND erpd.pairid=erp.pairid;
            ";

            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("rn", GetIndexForParameter(index));
            cmd.Parameters.AddWithValue("gid", (long) guild.Id);

            await using var reader = await cmd.ExecuteReaderAsync();

            // Key: PairID
            MultiValueDictionary<int, ulong> emoteRolePairsAllowedRoleIds = new MultiValueDictionary<int, ulong>();
            // 1. Allowed roles for emote-role pairs
            while (await reader.ReadAsync())
                emoteRolePairsAllowedRoleIds.Add(reader.GetInt32(0), (ulong)reader.GetInt64(1));

            await reader.NextResultAsync();

            // Key: PairID
            MultiValueDictionary<int, ulong> emoteRolePairsProhibitedRoleIds = new MultiValueDictionary<int, ulong>();
            // 2. Prohibited roles for emote-role pairs
            while (await reader.ReadAsync())
                emoteRolePairsProhibitedRoleIds.Add(reader.GetInt32(0), (ulong)reader.GetInt64(1));

            await reader.NextResultAsync();

            List<ulong> globalAllowedRoleIds = new List<ulong>();
            // 3. Global allowed roles for this reaction-role message
            while (await reader.ReadAsync())
                globalAllowedRoleIds.Add((ulong)reader.GetInt64(0));

            await reader.NextResultAsync();

            List<ulong> globalProhibitedRoleIds = new List<ulong>();
            // 4. Global prohibited roles for this reaction-role message
            while (await reader.ReadAsync())
                globalProhibitedRoleIds.Add((ulong)reader.GetInt64(0));

            await reader.NextResultAsync();

            Dictionary<int, RawEmoteRolePair> pairs = new Dictionary<int, RawEmoteRolePair>();
            // 5. Emote-role pairs
            while (await reader.ReadAsync())
            {
                string emote = reader.GetString(0);
                ulong roleid = (ulong)reader.GetInt64(1);
                int pairid = reader.GetInt32(2);
                var currentAllowedRoleIds = emoteRolePairsAllowedRoleIds.GetValueOrDefault(pairid);
                var currentProhibitedRoleIds = emoteRolePairsProhibitedRoleIds.GetValueOrDefault(pairid);
                pairs.Add(pairid, new RawEmoteRolePair(pairid, emote, roleid, currentAllowedRoleIds, currentProhibitedRoleIds));
            }

            await reader.NextResultAsync();
            await reader.ReadAsync();

            // 6. Reaction-role message properties
            ulong? channelId = await reader.IsDBNullAsync(0) ? (ulong?)null : (ulong)reader.GetInt64(0);
            ulong? messageId = await reader.IsDBNullAsync(1) ? (ulong?)null : (ulong)reader.GetInt64(1);
            int? limit = await reader.IsDBNullAsync(3) ? (int?)null : reader.GetInt32(3);
            int rrid = reader.GetInt32(2);
            bool isCustom = reader.GetBoolean(4);

            // The initial reaction-role message object
            var rrmsg = await ReactionRoleMessage.CreateAsync(this, new RawReactionRoleMessage(rrid, limit, guild.Id, channelId, messageId, isCustom, pairs.Values, globalAllowedRoleIds, globalProhibitedRoleIds));

            if (isCustom)
                return rrmsg;

            await reader.NextResultAsync();
            await reader.ReadAsync();

            // 7. Full reaction-role message properties (for non-custom messages)
            ReactionRoleMessageData data;
            if (reader.HasRows)
            {
                string name = await reader.IsDBNullAsync(0) ? null : reader.GetString(0);
                Color? color = await reader.IsDBNullAsync(1) ? (Color?)null : new Color((uint)reader.GetInt32(1));
                string description = await reader.IsDBNullAsync(2) ? null : reader.GetString(2);
                data = new ReactionRoleMessageData(name, description, color);
            }
            else
            {
                data = new ReactionRoleMessageData(null, null, null);
            }
            await reader.NextResultAsync();

            // List of full emote-role pairs
            List<FullEmoteRolePair> ferps = new List<FullEmoteRolePair>();

            // Dictionary of emote-role pairs of the existing messages
            var pairsDict = rrmsg.EmoteRolePairs.Values.ToDictionary(x => x.PairId);

            // Dictionary for emote-role pairs data (to create full emote role pairs)
            Dictionary<int, EmoteRolePairData> erpd = new Dictionary<int, EmoteRolePairData>();
            // 8. Full emote-role pairs
            while (await reader.ReadAsync())
            {
                int pairid = reader.GetInt32(0);
                string emoteRolePairDescription = await reader.IsDBNullAsync(1) ? null : reader.GetString(1);
                erpd.Add(pairid, new EmoteRolePairData(emoteRolePairDescription));
            }

            // Create full-emote role pairs
            foreach (var erp in pairsDict.Values)
            {
                var ferp = new FullEmoteRolePair(pairsDict.GetValueOrDefault(erp.PairId) ?? new FullEmoteRolePair(erp, new EmoteRolePairData(null)), erpd.GetValueOrDefault(erp.PairId));
                ferps.Add(ferp);
            }

            return new FullReactionRoleMessage(rrmsg, ferps, data);
        }

        /// <summary>
        ///     Displays a custom message 
        /// </summary>
        /// <param name="guild"></param>
        /// <param name="index"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task<ReactionRoleMessage> DisplayCustomReactionRoleMessage(SocketGuild guild, int? index)
        {
            // Retrieve the message
            var rrmsg = await GetReactionRoleMessageAsync(guild, index);


            if (rrmsg is FullReactionRoleMessage)
                throw new ReactionRoleMessageException($"Display non-custom reaction-role roles messages using {ReactionRoleServiceMessages.ReactionRoleMessageCommandString("{prefix}", "display")}");

            if (rrmsg.Message is null)
                throw new ReactionRoleMessageException($"The message is not present for this custom reaction-role message.\n" +
                    $"Use {ReactionRoleServiceMessages.ReactionRoleMessageCommandString("{prefix}", "displaycustom [message link]", index)} instead");

            if (rrmsg.EmoteRolePairs.Count == 0)
                throw new ReactionRoleMessageException($"This reaction-role message does not have any emote-role pairs.\nAdd them before displaying the message using " +
                    $"{ReactionRoleServiceMessages.ReactionRoleMessageCommandString("{prefix}", "addpair [emote] [role]", index)}");

            // Add callback
            await rrmsg.AddReactionCallbackAsync();

            return rrmsg;
        }

        /// <summary>
        ///     Gets the message from the database and commits all changes made to the message as well as adding callback to it.
        /// </summary>
        /// <param name="guild">The guild the RR message is from.</param>
        /// <param name="index">The row number of the message. If null, the latest message is used.</param>
        public async Task<FullReactionRoleMessage> DisplayFullReactionRoleMessageAsync(SocketGuild guild, int? index)
        {
            // Retrieve the message
            var rrmsg = await GetReactionRoleMessageAsync(guild, index);

            if (!(rrmsg is FullReactionRoleMessage frrmsg))
                throw new ReactionRoleMessageException($"Display custom reaction-role roles messages using " +
                    $"{ReactionRoleServiceMessages.ReactionRoleMessageCommandString("{prefix}", "displaycustom [message link]")}");

            if (rrmsg.Channel is null)
                throw new ReactionRoleMessageException("The channel is not present for this reaction-role message.\nSet the channel before displaying the message using " +
                    $"{ReactionRoleServiceMessages.ReactionRoleMessageCommandString("{prefix}", "channel [channel]", index)} or " +
                    $"{ReactionRoleServiceMessages.ReactionRoleMessageCommandString("{prefix}", "display [channel]", index)}.");

            if (rrmsg.EmoteRolePairs.Count == 0)
                throw new ReactionRoleMessageException($"This reaction-role message does not have any emote-role pairs.\nAdd them before displaying the message using " +
                    $"{ReactionRoleServiceMessages.ReactionRoleMessageCommandString("{prefix}", "addpair [emote] [role]", index)}");

            // Store the previous message
            var message = frrmsg.Message;

            // Display the message
            await frrmsg.DisplayAsync();

            // Update database entries for this RR message in case they aren't the same
            if (message == null || frrmsg.Message.Id != message.Id)
            {
                string query = "UPDATE reaction_role_messages.reaction_roles SET channelid=@cid, messageid=@mid WHERE rrid=@rrid";
                await using var cmd = _database.GetCommand(query);

                cmd.Parameters.AddWithValue("rrid", frrmsg.RRID);
                cmd.Parameters.AddWithValue("cid", (long)frrmsg.Message.Channel.Id);
                cmd.Parameters.AddWithValue("mid", (long)frrmsg.Message.Id);

                await cmd.ExecuteNonQueryAsync();
            }

            return frrmsg;
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

            Console.WriteLine(rows);
            return rows != 0;
        }

    }

    sealed class ReactionRoleMessageException : Exception
    {
        public ReactionRoleMessageException(string message) : base(message) { }
    }
}
