using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Rest;
using TeaBot.ReactionCallbackCommands;

namespace TeaBot.Services.ReactionRole
{
    partial class ReactionRoleService
    {
        public async Task InitCallbacksAsync()
        {
            string query = "SELECT rrid, name, guildid, channelid, messageid, color FROM reaction_role_messages.reaction_roles; " +
                "SELECT rrid, emote, roleid FROM reaction_role_messages.emote_role_pairs ORDER BY index";
            await using var cmd = _database.GetCommand(query);
            await using var reader = await cmd.ExecuteReaderAsync();

            List<RawReactionRoleMessage> rawRRmsgs = new List<RawReactionRoleMessage>();

            // Retrieve the reaction-role message info
            while (await reader.ReadAsync())
            {
                // Skip if channel or message is null
                if (await reader.IsDBNullAsync(3) || await reader.IsDBNullAsync(4))
                    continue;

                int rrid = reader.GetInt32(0);

                string name = await reader.IsDBNullAsync(1) ? null : reader.GetString(1);
                Color? color = await reader.IsDBNullAsync(5) ? (Color?)null : new Color((uint)reader.GetInt32(5));

                ulong guildId = (ulong)reader.GetInt64(2);
                ulong channelId = (ulong)reader.GetInt64(3);
                ulong messageId = (ulong)reader.GetInt64(4);

                rawRRmsgs.Add(new RawReactionRoleMessage(rrid, name, guildId, channelId, messageId, color));
            }

            await reader.NextResultAsync();
            
            List<(int, string, ulong)> emoteRoles = new List<(int, string, ulong)>();
            // Retrieve emote-role pairs
            while (await reader.ReadAsync())
            {
                int emoteRoleRRID = reader.GetInt32(0);
                string emote = reader.GetString(1);
                ulong roleid = (ulong)reader.GetInt64(2);

                emoteRoles.Add((emoteRoleRRID, emote, roleid));
            }

            await reader.CloseAsync();

            foreach (var rawRRmsg in rawRRmsgs)
            {
                var guild = _client.GetGuild(rawRRmsg.GuildId);

                // Delete this reaction-role message if its guild is not present (either because the guild was deleted or the bot was kicked/banned.
                if (guild is null)
                {
                    await GuildNotFound(rawRRmsg.GuildId);
                    continue;
                }

                var channel = guild.GetChannel(rawRRmsg.ChannelId);

                // Delete the reaction-role's info if the channel it was in is null
                if (channel is null)
                {
                    await ChannelNotFound(rawRRmsg.ChannelId);
                    continue;
                }

                var textChannel = channel as ITextChannel;
                RestUserMessage message = null;

                try
                {
                    message = await textChannel.GetMessageAsync(rawRRmsg.MessageId) as RestUserMessage;

                    // Delete the reaction-role's info about the message if the message itself is null
                    if (message is null)
                    {
                        await MessageNotFound(textChannel.Id, rawRRmsg.MessageId);
                        continue;
                    }
                } 
                catch (Discord.Net.HttpException)
                {
                    continue;
                }

                // Get emote-role pairs for this reaction-role message
                Dictionary<IEmote, IRole> pairs = new Dictionary<IEmote, IRole>();
                foreach (var pair in emoteRoles.Where(x => x.Item1 == rawRRmsg.RRID))
                {
                    IEmote emote;
                    if (Emote.TryParse(pair.Item2, out var e))
                        emote = e;
                    else if (new Emoji(pair.Item2) is Emoji emoji)
                        emote = emoji;
                    else
                        throw new NotImplementedException();

                    var role = guild.Roles.FirstOrDefault(x => x.Id == pair.Item3);

                    // Delete the role from the pair if it is no longer present
                    if (role is null)
                    {
                        await RoleNotFound(pair.Item3);
                        continue;
                    }

                    pairs.Add(emote, role);
                }

                var rrmsg = new ReactionRoleMessage(rawRRmsg.RRID, pairs, this, guild, textChannel, rawRRmsg.Color, rawRRmsg.Name, message);
                reactionRoleMessages[rrmsg.Message.Id] = rrmsg;
            }

        }
    }

    sealed class RawReactionRoleMessage {

        public int RRID { get; }

        public string Name { get; }

        public ulong GuildId { get; }
        public ulong ChannelId { get; }
        public ulong MessageId { get; }

        public Color? Color { get; }

        public RawReactionRoleMessage(int rrid, string name, ulong guildid, ulong channelid, ulong messageid, Color? color)
        {
            RRID = rrid;
            Name = name;
            GuildId = guildid;
            ChannelId = channelid;
            MessageId = messageid;
            Color = color;
        }

    }
}
