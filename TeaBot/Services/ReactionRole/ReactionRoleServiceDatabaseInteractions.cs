﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using TeaBot.ReactionCallbackCommands;

namespace TeaBot.Services.ReactionRole
{
    partial class ReactionRoleService
    {
        // Error messages to use in exceptions
        public const string NotExistsError = "No reaction-role message exists at such index. Use `{prefix}rr list` to see available reaction-role messages.";
        public const string NoMessagesError = "No reaction-role messages exist in this guild. Use `{prefix}rr create` to create an empty message.";

        /// <summary>
        ///     Ensures that a specific message or any messages exist for the given <paramref name="guild"/>.
        /// </summary>
        /// <param name="guild">Guild to check.</param>
        /// <param name="index">The row number of the message. If null, the latest message is used.</param>
        /// <exception cref="ReactionRoleServiceException">
        ///     1. No messages are found in the database
        ///     2. No message at the given index (row number) exists
        /// </exception>
        private async Task EnsureRRExists(SocketGuild guild, int? index)
        {
            string query =
            @$"WITH rr (rrid) AS (SELECT *, ROW_NUMBER() OVER (ORDER BY rrid {(index.HasValue ? "ASC" : "DESC")}) FROM reaction_role_messages.reaction_roles WHERE guildid=@gid)
            SELECT NOT EXISTS (SELECT rrid FROM rr),
            NOT EXISTS (SELECT rrid FROM rr WHERE ROW_NUMBER=@rn)";

            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("gid", NpgsqlTypes.NpgsqlDbType.Bigint, (long)guild.Id);
            cmd.Parameters.AddWithValue("rn", NpgsqlTypes.NpgsqlDbType.Integer, index ?? 1);

            await cmd.PrepareAsync();
            await using var reader = await cmd.ExecuteReaderAsync();

            await reader.ReadAsync();
            if (reader.GetBoolean(0))
                throw new ReactionRoleServiceException(NoMessagesError);
            else if (reader.GetBoolean(1))
                throw new ReactionRoleServiceException(NotExistsError);
            await reader.CloseAsync();
        }

        /// <summary>
        ///     Creates an empty entry for a reaction-role message in the database.
        /// </summary>
        /// <param name="guild">The guild the message is created in.</param>
        /// <exception cref="ReactionRoleServiceException">
        ///     Thrown when there's already 5 messages in the given guild.
        /// </exception>
        public async Task CreateReactionRoleMessage(SocketGuild guild)
        {
            string countQuery = "SELECT COUNT(*) >= 5 FROM reaction_role_messages.reaction_roles WHERE guildid=@gid";
            await using var countCmd = _database.GetCommand(countQuery);

            countCmd.Parameters.AddWithValue("gid", (long)guild.Id);

            await using var reader = await countCmd.ExecuteReaderAsync();
            await reader.ReadAsync();
            if (reader.GetBoolean(0))
                throw new ReactionRoleServiceException("You've reached the limit of reaction-role messages for this guild.\n Use `{prefix}rr delete [index]` to remove existing messages.");
            await reader.CloseAsync();

            string query = "INSERT INTO reaction_role_messages.reaction_roles (rrid, name, guildid, channelid, messageid, color) " +
                $"VALUES (DEFAULT, NULL, @gid, NULL, NULL, NULL)";
            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("gid", (long)guild.Id);

            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        ///     Gets the reaction-role message from the database.
        /// </summary>
        /// <param name="guild">The guild to retrieve the RR message for.</param>
        /// <param name="index">The row number of the message. If null, the latest message is used.</param>
        /// <exception cref="ReactionRoleServiceException">
        ///     1. No messages are found in the database
        ///     2. No message at the given index (row number) exists
        /// </exception>
        public async Task<ReactionRoleMessage> GetReactionRoleMessageAsync(SocketGuild guild, int? index)
        {
            string order = index.HasValue ? "ASC" : "DESC";
            string query = @$"
            WITH rr (rrid) AS (SELECT *, ROW_NUMBER() OVER (ORDER BY rrid {order}) FROM reaction_role_messages.reaction_roles WHERE guildid=@gid)
            SELECT NOT EXISTS (SELECT rrid FROM rr),
            NOT EXISTS (SELECT rrid FROM rr WHERE ROW_NUMBER=@rn);

            WITH rrtemp (rrid) AS (SELECT *, ROW_NUMBER() OVER (ORDER BY rrid {order}) FROM reaction_role_messages.reaction_roles WHERE guildid=@gid)
            SELECT emote, roleid FROM reaction_role_messages.emote_role_pairs er, rrtemp
            WHERE rrtemp.rrid = er.rrid
            AND rrtemp.ROW_NUMBER = @rn
            ORDER BY index; 

            WITH rrtemp (rrid) AS (SELECT *, ROW_NUMBER() OVER (ORDER BY rrid {order}) FROM reaction_role_messages.reaction_roles WHERE guildid=@gid)
            SELECT rr.name, rr.color, rr.channelid, rr.messageid, rr.rrid FROM reaction_role_messages.reaction_roles rr, rrtemp 
            WHERE rr.rrid = rrtemp.rrid
            AND rrtemp.ROW_NUMBER = @rn;
            ";

            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("gid", (long) guild.Id);
            cmd.Parameters.AddWithValue("rn", index ?? 1);

            await using var reader = await cmd.ExecuteReaderAsync();

            await reader.ReadAsync();

            // 1. Check if the message or any messages exist
            if (reader.GetBoolean(0))
                throw new ReactionRoleServiceException(NoMessagesError);
            else if (reader.GetBoolean(1))
                throw new ReactionRoleServiceException(NotExistsError);

            await reader.NextResultAsync();

            // 2. Retrieve raw emote-role pairs
            List<RawEmoteRolePair> rawPairs = new List<RawEmoteRolePair>();
            while (await reader.ReadAsync())
                rawPairs.Add(new RawEmoteRolePair(reader.GetString(0), (ulong)reader.GetInt64(1)));

            await reader.NextResultAsync();
            await reader.ReadAsync();

            // 3. Retrieve basic RR message properties
            string name = await reader.IsDBNullAsync(0) ? null : reader.GetString(0);
            Color? color = await reader.IsDBNullAsync(1) ? (Color?)null : new Color((uint)reader.GetInt32(1));
            ulong? channelId = await reader.IsDBNullAsync(2) ? (ulong?)null : (ulong)reader.GetInt64(2);
            ulong? messageId = await reader.IsDBNullAsync(3) ? (ulong?)null : (ulong)reader.GetInt64(3);
            int rrid = reader.GetInt32(4);

            await reader.CloseAsync();

            return await PrepareReactionRoleMessageAsync(new RawReactionRoleMessage(rrid, name, guild.Id, channelId, messageId, color, rawPairs));
        }

        /// <summary>
        ///     Gets the message from the database and commits all changes made to the message as well as adding callback to it.
        /// </summary>
        /// <param name="guild">The guild the RR message is from.</param>
        /// <param name="index">The row number of the message. If null, the latest message is used.</param>
        /// <exception cref="ReactionRoleServiceException">
        ///     1. No messages are found in the database
        ///     2. No message at the given index (row number) exists
        ///     3. The channel of the reaction-role message is not present
        ///     4. The reaction-role message does not have emote-role pairs
        /// </exception>
        public async Task<ReactionRoleMessage> UpdateOrDisplayReactionRoleMessageAsync(SocketGuild guild, int? index)
        {
            // Retrieve the message
            var rrmsg = await GetReactionRoleMessageAsync(guild, index);

            // Check if the message is eligible for displaying
            if (rrmsg.Channel is null)
                throw new ReactionRoleServiceException("The channel is not present for this reaction-role message. Set the channel before displaying the message using " +
                    $"`{{prefix}}rr channel [channel]{(index.HasValue ? $" {index.Value}" : "")}` or `{{prefix}}rr display [channel]{(index.HasValue ? $" {index.Value}" : "")}`.");
            else if (rrmsg.EmoteRolePairs.Count == 0)
                throw new ReactionRoleServiceException($"This reaction-role message does not have any emote-role pairs. Add them before displaying the message using " +
                    $"`{{prefix}}rr addpair [emote] [role]`");

            // Store the previous message
            var message = rrmsg.Message;

            // Display the message (either updating the current one or sending a new one)
            await rrmsg.DisplayAsync();

            // Update database entries for this RR message in case they aren't the same
            if (message == null || rrmsg.Message.Id != message.Id)
            {
                string query = "UPDATE reaction_role_messages.reaction_roles SET channelid=@cid, messageid=@mid WHERE rrid=@rrid";
                await using var cmd = _database.GetCommand(query);

                cmd.Parameters.AddWithValue("rrid", rrmsg.RRID);
                cmd.Parameters.AddWithValue("cid", (long)rrmsg.Message.Channel.Id);
                cmd.Parameters.AddWithValue("mid", (long)rrmsg.Message.Id);

                await cmd.ExecuteNonQueryAsync();
            }

            return rrmsg;
        }

        /// <summary>
        ///     Completely removes a reaction-role message (deleting it from the database and, if it is present, from Discord as well).
        /// </summary>
        /// <param name="guild">The guild the RR message is from.</param>
        /// <param name="index">The row number of the message. If null, the latest message is used.</param>
        /// <exception cref="ReactionRoleServiceException">
        ///     1. No messages are found in the database
        ///     2. No message at the given index (row number) exists
        /// </exception>
        public async Task RemoveReactionRoleMessage(SocketGuild guild, int? index)
        {
            await EnsureRRExists(guild, index);

            var rrmsg1 = await GetReactionRoleMessageAsync(guild, index);
            if (rrmsg1 != null && reactionRoleMessages.Values.FirstOrDefault(x => rrmsg1.RRID == x.RRID) is ReactionRoleMessage rrmsg2)
                await rrmsg1.TryDeleteMessageAsync();

            string query = @$"WITH rr (rrid) AS (SELECT *, ROW_NUMBER() OVER (ORDER BY rrid {(index.HasValue ? "ASC" : "DESC")}) FROM reaction_role_messages.reaction_roles WHERE guildid=@gid)
            DELETE FROM reaction_role_messages.emote_role_pairs er USING rr 
            WHERE er.rrid = rr.rrid
            AND rr.ROW_NUMBER = @rn;
            WITH rrtemp (rrid) AS (SELECT *, ROW_NUMBER() OVER (ORDER BY rrid {(index.HasValue ? "ASC" : "DESC")}) FROM reaction_role_messages.reaction_roles WHERE guildid=@gid)
            DELETE FROM reaction_role_messages.reaction_roles rr USING rrtemp
            WHERE rr.rrid = rrtemp.rrid
            AND rrtemp.ROW_NUMBER = @rn";

            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("rn", index ?? 1);
            cmd.Parameters.AddWithValue("gid", (long)guild.Id);

            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        ///     Adds an emote-role pair to a reaction-role message.
        /// </summary>
        /// <param name="guild">The guild the RR message is from.</param>
        /// <param name="index">The row number of the message. If null, the latest message is used.</param>
        /// <param name="emote">The emote of the pair.</param>
        /// <param name="role">The role of the pair.</param>
        /// <exception cref="ReactionRoleServiceException">
        ///     1. No messages are found in the database
        ///     2. No message at the given index (row number) exists
        ///     3. A pair with the same emote or role already exists
        ///     4. The message has 20 emote-role pairs
        /// </exception>
        public async Task AddPairAsync(SocketGuild guild, int? index, IEmote emote, IRole role)
        {
            // If the emote is a custom emote from a different guild, throw an exception
            if (emote is Emote e && !guild.Emotes.Contains(e))
                throw new ReactionRoleServiceException("Cannot add a pair with an emote that is not from this guild!");

            string conditionQuery = @$"WITH rr (rrid) AS (SELECT *, ROW_NUMBER() OVER (ORDER BY rrid {(index.HasValue ? "ASC" : "DESC")}) FROM reaction_role_messages.reaction_roles WHERE guildid=@gid)
            SELECT NOT EXISTS (SELECT rrid FROM rr),

            NOT EXISTS (SELECT rrid FROM rr WHERE ROW_NUMBER=@rn),

            EXISTS (SELECT * FROM reaction_role_messages.emote_role_pairs er, rr
            WHERE er.rrid = rr.rrid
            AND rr.ROW_NUMBER = @rn
            AND (emote=@emote OR roleid=@rid)),

            COUNT(er.emote) >= 20 
            FROM reaction_role_messages.emote_role_pairs er, rr 
            WHERE er.rrid = rr.rrid
            AND rr.ROW_NUMBER = @rn";

            await using var conditionCmd = _database.GetCommand(conditionQuery);

            conditionCmd.Parameters.AddWithValue("gid", (long)guild.Id);
            conditionCmd.Parameters.AddWithValue("rn", index ?? 1);
            conditionCmd.Parameters.AddWithValue("emote", emote.ToString());
            conditionCmd.Parameters.AddWithValue("rid", (long)role.Id);

            await using var reader = await conditionCmd.ExecuteReaderAsync();
            
            // Check if the pair is allowed to be added.
            await reader.ReadAsync();
            if (reader.GetBoolean(0))
                throw new ReactionRoleServiceException(NoMessagesError);
            else if (reader.GetBoolean(1))
                throw new ReactionRoleServiceException(NotExistsError);
            else if (reader.GetBoolean(2))
                throw new ReactionRoleServiceException("A pair with this emote or role already exists. You can remove existing pairs using `{prefix}rr removepair`.");
            else if (reader.GetBoolean(3))
                throw new ReactionRoleServiceException("This message has reached the maximum of emote-role pairs. Create a new reaction-role message using `{prefix}rr create` or remove existing pairs using `{prefix}rr removepair`.");
            await reader.CloseAsync();

            string query = @$"WITH rr (rrid) AS (SELECT *, ROW_NUMBER() OVER (ORDER BY rrid {(index.HasValue ? "ASC" : "DESC")}) FROM reaction_role_messages.reaction_roles WHERE guildid=@gid)
            INSERT INTO reaction_role_messages.emote_role_pairs (rrid, emote, roleid) VALUES ((SELECT rrid FROM rr WHERE ROW_NUMBER=@rn), @emote, @rid)";

            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("gid", (long)guild.Id);
            cmd.Parameters.AddWithValue("rn", index ?? 1);
            cmd.Parameters.AddWithValue("emote", emote.ToString());
            cmd.Parameters.AddWithValue("rid", (long)role.Id);

            // If all conditions are passed, add the emote-role pair to the reaction-role message
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        ///     Remove an emote-role pair frm a reaction-role message.
        /// </summary>
        /// <param name="guild">The guild the RR message is from.</param>
        /// <param name="index">The row number of the message. If null, the latest message is used.</param>
        /// <param name="emote">The emote of the pair.</param>
        /// <exception cref="ReactionRoleServiceException">
        ///     1. No messages are found in the database
        ///     2. No message at the given index (row number) exists
        ///     3. Emote-role pair with such emote does not exist
        /// </exception>
        public async Task RemovePairAsync(SocketGuild guild, int? index, IEmote emote)
        {
            await EnsureRRExists(guild, index);

            string query = @$"WITH rr (rrid) AS (SELECT *, ROW_NUMBER() OVER (ORDER BY rrid {(index.HasValue ? "ASC" : "DESC")}) FROM reaction_role_messages.reaction_roles WHERE guildid=@gid) 
            DELETE FROM reaction_role_messages.emote_role_pairs er USING rr
            WHERE er.rrid = rr.rrid 
            AND rr.ROW_NUMBER = @rn
            AND emote = @emote";
            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("gid", (long)guild.Id);
            cmd.Parameters.AddWithValue("rn", index ?? 1);
            cmd.Parameters.AddWithValue("emote", emote.ToString());

            // If no rows are updated, throw an exception notifying that a pair with such emote does not exist
            if (await cmd.ExecuteNonQueryAsync() == 0)
                throw new ReactionRoleServiceException($"{emote} is not present in {(index.HasValue ? $"reaction-role message with index `{index.Value}`" : "the latest reaction-role message")}" +
                    $"Use `{{prefix}}rr info {(index.HasValue ? $"{index.Value} " : "")}` to view emote-role pairs of this RR message.");
        }

        /// <summary>
        ///     Remove an emote-role pair frm a reaction-role message.
        /// </summary>
        /// <param name="guild">The guild the RR message is from.</param>
        /// <param name="index">The row number of the message. If null, the latest message is used.</param>
        /// <param name="role">The role of the pair.</param>
        /// <exception cref="ReactionRoleServiceException">
        ///     1. No messages are found in the database
        ///     2. No message at the given index (row number) exists
        ///     3. Emote-role pair with such role does not exist
        /// </exception>
        public async Task RemovePairAsync(SocketGuild guild, int? index, IRole role)
        {
            await EnsureRRExists(guild, index);

            string query = @"WITH rr (rrid) AS (SELECT *, ROW_NUMBER() OVER (ORDER BY rrid DESC) FROM reaction_role_messages.reaction_roles WHERE guildid=@gid) 
            DELETE FROM reaction_role_messages.emote_role_pairs er USING rr
            WHERE er.rrid = rr.rrid 
            AND rr.ROW_NUMBER = @rn
            AND emote = @emote";
            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("gid", (long)guild.Id);
            cmd.Parameters.AddWithValue("rn", index ?? 1);
            cmd.Parameters.AddWithValue("rid", (long)role.Id);

            // If no rows are updated, throw an exception notifying that a pair with such role does not exist
            if (await cmd.ExecuteNonQueryAsync() == 0)
                throw new ReactionRoleServiceException($"`{role.Name}` is not present in {(index.HasValue ? $"reaction-role message with index `{index.Value}`" : "the latest reaction-role message")}\n" +
                    $"Use `{{prefix}}rr info {(index.HasValue ? $"{index.Value} " : "")}` to view emote-role pairs of this RR message.");
        }

        /// <summary>
        ///     Changes the emote of an emote-role pair.
        /// </summary>
        /// <param name="guild">The guild the RR message is from.</param>
        /// <param name="index">The row number of the message. If null, the latest message is used.</param>
        /// <param name="role">The role of the pair.</param>
        /// <param name="emote">The emote of the pair.</param>
        /// <exception cref="ReactionRoleServiceException">
        ///     1. No messages are found in the database
        ///     2. No message at the given index (row number) exists
        ///     3. Emote-role pair with such role does not exist
        /// </exception>
        public async Task ChangeEmote(SocketGuild guild, int? index, IRole role, IEmote emote) 
        { 
            await EnsureRRExists(guild, index);

            string query = @$"WITH rr (rrid) AS (SELECT *, ROW_NUMBER() OVER (ORDER BY rrid {(index.HasValue ? "ASC" : "DESC")}) FROM reaction_role_messages.reaction_roles WHERE guildid=@gid) 
            UPDATE reaction_role_messages.emote_role_pairs AS er SET emote=@emote
            FROM rr
            WHERE er.rrid = rr.rrid
            AND rr.ROW_NUMBER = @rn
            AND roleid=@rid
            ";

            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("gid", (long)guild.Id);
            cmd.Parameters.AddWithValue("rn", index ?? 1);
            cmd.Parameters.AddWithValue("emote", emote.ToString());
            cmd.Parameters.AddWithValue("rid", (long)role.Id);

            if (await cmd.ExecuteNonQueryAsync() == 0)
                throw new ReactionRoleServiceException($"No such emote or role exists in {(index.HasValue ? $"reaction-role message with index `{index.Value}`" : "the latest reaction-role message")}.\n" +
                    $"Use `{{prefix}}rr info {(index.HasValue ? $"{index.Value} " : "")}` to view emote-role pairs of this RR message.");
        }

        /// <summary>
        ///     Changes the role of an emote-role pair.
        /// </summary>
        /// <param name="guild">The guild the RR message is from.</param>
        /// <param name="index">The row number of the message. If null, the latest message is used.</param>
        /// <param name="role">The role of the pair.</param>
        /// <param name="emote">The emote of the pair.</param>
        /// <exception cref="ReactionRoleServiceException">
        ///     1. No messages are found in the database
        ///     2. No message at the given index (row number) exists
        ///     3. Emote-role pair with such emote does not exist
        /// </exception>
        public async Task ChangeRole(SocketGuild guild, int? index, IRole role, IEmote emote)
        {
            await EnsureRRExists(guild, index);

            string query = @$"WITH rr (rrid) AS (SELECT *, ROW_NUMBER() OVER (ORDER BY rrid {(index.HasValue ? "ASC" : "DESC")}) FROM reaction_role_messages.reaction_roles WHERE guildid=@gid) 
            UPDATE reaction_role_messages.emote_role_pairs AS er SET roleid=@rid
            FROM rr
            WHERE er.rrid = rr.rrid
            AND rr.ROW_NUMBER = @rn
            AND emote=@emote
            ";

            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("gid", (long)guild.Id);
            cmd.Parameters.AddWithValue("rn", index ?? 1);
            cmd.Parameters.AddWithValue("emote", emote.ToString());
            cmd.Parameters.AddWithValue("rid", (long)role.Id);

            if (await cmd.ExecuteNonQueryAsync() == 0)
                throw new ReactionRoleServiceException($"No such emote or role exists in {(index.HasValue ? $"reaction-role message with index `{index.Value}`" : "the latest reaction-role message")}.\n" +
                    $"Use `{{prefix}}rr info {(index.HasValue ? $"{index.Value} " : "")}` to view emote-role pairs of this RR message.");
        }

        /// <summary>
        ///     Swaps the positions of two emote-role pairs.
        /// </summary>
        /// <param name="guild">The guild the RR message is from.</param>
        /// <param name="index">The row number of the message. If null, the latest message is used.</param>
        /// <param name="emote1">An emote from an emote-pair.</param>
        /// <param name="emote2">An emote from the pair to swap with.</param>
        /// <exception cref="ReactionRoleServiceException">
        ///     1. No messages are found in the database
        ///     2. No message at the given index (row number) exists
        ///     3. One or both emotes are not found in the reaction-role message
        /// </exception>
        public async Task ChangeOrder(SocketGuild guild, int? index, IEmote emote1, IEmote emote2)
        {
            await EnsureRRExists(guild, index);

            string query = @$"WITH rr (rrid) AS (SELECT *, ROW_NUMBER() OVER (ORDER BY rrid {(index.HasValue ? "ASC" : "DESC")}) FROM reaction_role_messages.reaction_roles WHERE guildid=@gid) 
            UPDATE reaction_role_messages.emote_role_pairs AS er SET index=ertemp.index
            FROM reaction_role_messages.emote_role_pairs AS ertemp, rr
            WHERE rr.rrid = er.rrid AND rr.rrid = ertemp.rrid
            AND rr.ROW_NUMBER = @rn
            AND er.emote IN (@emotea, @emoteb)
            AND ertemp.emote IN (@emotea, @emoteb)
            AND er.emote <> ertemp.emote";

            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("gid", (long)guild.Id);
            cmd.Parameters.AddWithValue("rn", index ?? 1);
            cmd.Parameters.AddWithValue("emotea", emote1.ToString());
            cmd.Parameters.AddWithValue("emoteb", emote2.ToString());

            if (await cmd.ExecuteNonQueryAsync() == 0)
                throw new ReactionRoleServiceException($"{(index.HasValue ? $"Reaction-role message with index `{index.Value}`" : "The latest reaction-role message")} message does not contain one emote or both emotes.\n" +
                    $"Use `{{prefix}}rr info {(index.HasValue ? $"{index.Value} " : "")}` to view emote-role pairs of this RR message.");
        }

        /// <summary>
        ///     Changes the name of a reaction-role message.
        /// </summary>
        /// <param name="guild">The guild the RR message is from.</param>
        /// <param name="index">The row number of the message. If null, the latest message is used.</param>
        /// <param name="newName">The new name to set.</param>
        /// <exception cref="ReactionRoleServiceException">
        ///     1. No messages are found in the database
        ///     2. No message at the given index (row number) exists
        /// </exception>
        public async Task ChangeNameAsync(SocketGuild guild, int? index, string newName) 
        {
            await EnsureRRExists(guild, index);

            string query = @$"WITH rrtemp (rrid) AS (SELECT *, ROW_NUMBER() OVER (ORDER BY rrid {(index.HasValue ? "ASC" : "DESC")}) FROM reaction_role_messages.reaction_roles WHERE guildid=@gid)
            UPDATE reaction_role_messages.reaction_roles AS rr SET name=@name
            FROM rrtemp
            WHERE rrtemp.ROW_NUMBER = @rn
            AND rr.rrid = rrtemp.rrid
            ";

            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("gid", (long)guild.Id);
            cmd.Parameters.AddWithValue("rn", index ?? 1);
            cmd.Parameters.AddWithValue("name", newName);

            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        ///     Changes the color of a reaction-role message.
        /// </summary>
        /// <param name="guild">The guild the RR message is from.</param>
        /// <param name="index">The row number of the message. If null, the latest message is used.</param>
        /// <param name="newColor">The new color to set. If null, the default color will be used.</param>
        /// <exception cref="ReactionRoleServiceException">
        ///     1. No messages are found in the database
        ///     2. No message at the given index (row number) exists
        /// </exception>
        public async Task ChangeColorAsync(SocketGuild guild, int? index, Color? newColor)
        {
            await EnsureRRExists(guild, index);

            string query = @$"WITH rrtemp (rrid) AS (SELECT *, ROW_NUMBER() OVER (ORDER BY rrid {(index.HasValue ? "ASC" : "DESC")}) FROM reaction_role_messages.reaction_roles WHERE guildid=@gid)
            UPDATE reaction_role_messages.reaction_roles AS rr SET color=@color
            FROM rrtemp
            WHERE rrtemp.ROW_NUMBER = @rn
            AND rr.rrid = rrtemp.rrid
            ";

            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("gid", (long)guild.Id);
            cmd.Parameters.AddWithValue("rn", index ?? 1);

            if (newColor.HasValue)
                cmd.Parameters.AddWithValue("color", (int) newColor.Value.RawValue);
            else
                cmd.Parameters.AddWithValue("color", DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>
        ///     Changes the channel of a reaction-role message.
        /// </summary>
        /// <param name="guild">The guild the RR message is from.</param>
        /// <param name="index">The row number of the message. If null, the latest message is used.</param>
        /// <param name="channelId">The ID of the new channel.</param>
        /// <exception cref="ReactionRoleServiceException">
        ///     1. No messages are found in the database
        ///     2. No message at the given index (row number) exists
        /// </exception>
        public async Task ChangeChannelAsync(SocketGuild guild, int? index, ulong channelId)
        {
            await EnsureRRExists(guild, index);

            string query = @$"WITH rrtemp (rrid) AS (SELECT *, ROW_NUMBER() OVER (ORDER BY rrid {(index.HasValue ? "ASC" : "DESC")}) FROM reaction_role_messages.reaction_roles WHERE guildid=@gid)
            UPDATE reaction_role_messages.reaction_roles AS rr SET channelid=@cid, messageid=NULL
            FROM rrtemp
            WHERE rrtemp.ROW_NUMBER = @rn
            AND rr.rrid = rrtemp.rrid
            ";

            await using var cmd = _database.GetCommand(query);

            cmd.Parameters.AddWithValue("gid", (long)guild.Id);
            cmd.Parameters.AddWithValue("rn", index ?? 1);
            cmd.Parameters.AddWithValue("cid", (long)channelId);

            await cmd.ExecuteNonQueryAsync();
        }
    }
}