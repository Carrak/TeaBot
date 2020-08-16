using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using TeaBot.Services.ReactionRole;

namespace TeaBot.ReactionCallbackCommands.ReactionRole
{
    /// <summary>
    ///     Represents a message users can interact with (through reactions) to assign roles to themselves. 
    ///     This is a minimalistic representation for custom messages, but also used as the base for <see cref="FullReactionRoleMessage"/>.
    /// </summary>
    public class ReactionRoleMessage
    {
        public readonly ReactionRoleService RRService;

        /// <summary>
        ///     ID of this reaction-role message.
        /// </summary>
        public int RRID { get; }

        /// <summary>
        ///     Guild the reaction-role mmessage is in.
        /// </summary>
        public IGuild Guild { get; }

        /// <summary>
        ///     Channel the reaction-role message is in.
        /// </summary>
        public ITextChannel Channel { get; set; }

        /// <summary>
        ///     Discord message this reaction-role message is attached to.
        /// </summary>
        public IUserMessage Message { get; set; }

        /// <summary>
        ///     The limit of how many roles from a list the user can assign to themselves from the reaction-role message.
        ///     Null if there's no limit.
        /// </summary>
        public int? Limit { get; }

        /// <summary>
        ///     Allowed roles that apply to this reaction-role message
        /// </summary>
        public IEnumerable<IRole> GlobalAllowedRoles { get; }

        /// <summary>
        ///     Prohibited roles that apply to this reaction-role message
        /// </summary>
        public IEnumerable<IRole> GlobalProhibitedRoles { get; }

        /// <summary>
        ///     Emote-role pairs of the reaction-role message.
        /// </summary>
        public Dictionary<IEmote, EmoteRolePair> EmoteRolePairs { get; }

        public ReactionRoleMessage(ReactionRoleService rrservice,
            int rrid,
            int? limit,
            IGuild guild,
            ITextChannel channel,
            IUserMessage message,
            Dictionary<IEmote, EmoteRolePair> pairs,
            IEnumerable<IRole> allowedRoles,
            IEnumerable<IRole> prohibitedRoles)
        {
            RRService = rrservice;

            RRID = rrid;
            Limit = limit;

            Guild = guild;
            Channel = channel;
            Message = message;
            EmoteRolePairs = pairs;

            GlobalAllowedRoles = allowedRoles;
            GlobalProhibitedRoles = prohibitedRoles;
        }

        /// <summary>
        ///     Creates a "Discord-compatible" reaction-role message using the information from the database.
        /// </summary>
        /// <param name="rrservice">Reaction-role service for error-handling.</param>
        /// <param name="rawRRmsg">Unprocessed reaction-role message.</param>
        /// <returns>An instance of <see cref="ReactionRoleMessage"/>, or null if the guild is not present.</returns>
        public static async Task<ReactionRoleMessage> CreateAsync(ReactionRoleService rrservice,
            RawReactionRoleMessage rawRRmsg)
        {
            // The guild the message is in
            var guild = rrservice._client.GetGuild(rawRRmsg.GuildId);

            // Delete this reaction-role message if its guild is not present (either because the guild was deleted or the bot was kicked/banned).
            if (guild is null)
            {
                await rrservice.RemoveGuildFromDbAsync(rawRRmsg.GuildId);
                return null;
            }

            // The location of the reaction-role message
            ITextChannel channel = null;
            IUserMessage message = null;

            if (rawRRmsg.ChannelId.HasValue)
            {
                // Retrieve the channel
                channel = guild.GetChannel(rawRRmsg.ChannelId.Value) as ITextChannel;

                if (channel != null)
                {
                    if (rawRRmsg.MessageId.HasValue)
                    {
                        try
                        {
                            // Retrieve the message
                            message = await channel.GetMessageAsync(rawRRmsg.MessageId.Value) as IUserMessage;

                            // Remove the reaction-role's info about the message if the message itself is null
                            if (message is null)
                            {
                                await rrservice.RemoveMessageFromDbAsync(channel.Id, rawRRmsg.MessageId.Value);
                            }
                        }
                        // Discard missing permissions
                        catch (HttpException)
                        {

                        }
                    }
                }
                // If channel is null, remove it from the database
                else
                    await rrservice.RemoveChannelFromDbAsync(rawRRmsg.ChannelId.Value);
            }

            // Emote-role pairs of the message
            var pairs = new List<EmoteRolePair>();
            foreach (var rerp in rawRRmsg.EmoteRolePairs)
                if (await EmoteRolePair.CreateAsync(rrservice, guild, rerp) is EmoteRolePair preparedPair)
                    pairs.Add(preparedPair);

            // Get allowed roles
            var allowedRoles = new List<IRole>();
            foreach (ulong roleid in rawRRmsg.GlobalAllowedRoleIds)
            {
                var role = guild.GetRole(roleid);
                if (role is null)
                    await rrservice.RemoveRoleFromDbAsync(roleid);
                else
                    allowedRoles.Add(role);
            }

            // Get prohibited roles
            var prohibitedRoles = new List<IRole>();
            foreach (ulong roleid in rawRRmsg.GlobalProhibitedRoleIds)
            {
                var role = guild.GetRole(roleid);
                if (role is null)
                    await rrservice.RemoveRoleFromDbAsync(roleid);
                else prohibitedRoles.Add(role);
            }

            return new ReactionRoleMessage(rrservice, rawRRmsg.RRID, rawRRmsg.Limit, guild, channel, message, pairs.ToDictionary(x => x.Emote), allowedRoles, prohibitedRoles);
        }

        /// <summary>
        ///     Adds callback to the message as well as adds reactions to the message.
        /// </summary>
        /// <returns></returns>
        public async Task AddReactionCallbackAsync()
        {
            // Disable adding reactions if it is possible
            try
            {

                // Change the bot's permissions
                var currentUser = await Guild.GetCurrentUserAsync();
                var cutempperms = Channel.GetPermissionOverwrite(currentUser);
                if (cutempperms.HasValue)
                {
                    var perms = cutempperms.Value;
                    perms = perms.Modify(addReactions: PermValue.Allow);
                    await Channel.AddPermissionOverwriteAsync(currentUser, perms);
                }
                else
                {
                    var op = new OverwritePermissions(addReactions: PermValue.Allow);
                    await Channel.AddPermissionOverwriteAsync(currentUser, op);
                }

                // Change everyone role
                var tempperms = Channel.GetPermissionOverwrite(Guild.EveryoneRole);
                if (tempperms.HasValue)
                {
                    var perms = tempperms.Value;
                    perms = perms.Modify(addReactions: PermValue.Deny);
                    await Channel.AddPermissionOverwriteAsync(Guild.EveryoneRole, perms);
                }
                else
                {
                    var op = new OverwritePermissions(addReactions: PermValue.Deny);
                    await Channel.AddPermissionOverwriteAsync(Guild.EveryoneRole, op);
                }
            }
            catch (HttpException) { }

            _ = Task.Run(async () => await Message.AddReactionsAsync(EmoteRolePairs.Keys.ToArray()));
            await RRService.AddReactionCallbackAsync(Message, this);
        }

        /// <summary>
        ///     Assign the role to the user if it's in <see cref="EmoteRolePairs"/> and if the user meets all criteria.
        /// </summary>
        /// <param name="reaction">Reaction added by the user.</param>
        public async Task HandleReactionAdded(SocketReaction reaction)
        {
            if (EmoteRolePairs.TryGetValue(reaction.Emote, out var erp))
            {
                // The user who placed the reaction
                var user = await Guild.GetUserAsync(reaction.User.Value.Id);

                // Allowed and prohibited role IDs
                var allowedRoleIds = erp.AllowedRoles.Select(x => x.Id);
                var prohibitedRoleIds = erp.ProhibitedRoles.Select(x => x.Id);
                var globalAllowedRoleIds = GlobalAllowedRoles.Select(x => x.Id);
                var globalProhibitedRoleIds = GlobalProhibitedRoles.Select(x => x.Id);

                // Check if:
                // 1. The limit is present => the amount of roles the user has from the offered list exceeds the limit
                // 2. If the user has any global prohibited roles
                // 3. If the user has any emote-role pair prohibited roles 
                // 4  If there are global allowed roles => if the user does not have a global allowed role
                // 5. If there are any allowed roles => if the user does not have an emote-role pair bound role
                // If either of the conditions is true, the user is not granted the role
                if (user.RoleIds.Count(x => EmoteRolePairs.Values.Any(erp => erp.Role.Id == x)) >= Limit ||
                    user.RoleIds.Any(x => globalProhibitedRoleIds.Contains(x)) ||
                    user.RoleIds.Any(x => prohibitedRoleIds.Contains(x)) ||
                    (globalAllowedRoleIds.Any() && !user.RoleIds.Any(x => globalAllowedRoleIds.Contains(x))) ||
                    (erp.AllowedRoles.Any() && !user.RoleIds.Any(x => allowedRoleIds.Contains(x))))
                    return;

                // Try to assign the role to the user
                try
                {
                    await user.AddRoleAsync(erp.Role);
                }
                // Discard missing permissions
                catch (HttpException)
                {

                }
            }
        }

        /// <summary>
        ///     Remove the role from the user if it's in <see cref="EmoteRolePairs"/> and if the user meets all criteria.
        /// </summary>
        /// <param name="reaction">Reaction removed by the user.</param>
        public async Task HandleReactionRemoved(SocketReaction reaction)
        {
            if (EmoteRolePairs.TryGetValue(reaction.Emote, out var erp))
            {
                var user = await Guild.GetUserAsync(reaction.User.Value.Id);

                // Allowed and prohibited role IDs
                var allowedRoleIds = erp.AllowedRoles.Select(x => x.Id);
                var prohibitedRoleIds = erp.ProhibitedRoles.Select(x => x.Id);

                // Check if:
                // 1. If the user has any prohibited roles (ones that prevent them from acquiring a role from the list)
                // 2. If there are any allowed roles => if the user does not have any allowed roles
                // If either of the conditions is true, cancel all interactions
                if (user.RoleIds.Any(x => prohibitedRoleIds.Contains(x)) ||
                    (erp.AllowedRoles.Any() && !user.RoleIds.Any(x => allowedRoleIds.Contains(x))))
                    return;

                // Try removing the role from the user
                try
                {
                    await user.RemoveRoleAsync(erp.Role);
                }
                // Discard missing permissions
                catch (HttpException)
                {

                }
            }
        }
    }

    public class EmoteRolePair
    {
        public int PairId { get; }

        public IEmote Emote { get; }
        public IRole Role { get; }

        public IEnumerable<IRole> AllowedRoles { get; }
        public IEnumerable<IRole> ProhibitedRoles { get; }

        public EmoteRolePair(int pairid, IEmote emote, IRole role, IEnumerable<IRole> allowedRoles, IEnumerable<IRole> prohibitedRoles)
        {
            PairId = pairid;
            Emote = emote;
            Role = role;
            AllowedRoles = allowedRoles;
            ProhibitedRoles = prohibitedRoles;
        }

        /// <summary>
        ///     
        /// </summary>
        /// <param name="rrservice"></param>
        /// <param name="guild"></param>
        /// <param name="rerp"></param>
        /// <returns></returns>
        public static async Task<EmoteRolePair> CreateAsync(
            ReactionRoleService rrservice,
            IGuild guild,
            RawEmoteRolePair rerp
            )
        {
            IEmote emote;
            if (Discord.Emote.TryParse(rerp.Emote, out var e))
                emote = e;
            else if (new Emoji(rerp.Emote) is Emoji emoji)
                emote = emoji;
            else
                throw new NotImplementedException();

            var roles = guild.Roles.ToDictionary(x => x.Id);

            var assignedRole = roles.GetValueOrDefault(rerp.RoleId);

            // Delete the role from the pair if it is no longer present
            if (assignedRole is null)
            {
                await rrservice.RemoveRoleFromDbAsync(rerp.RoleId);
                return null;
            }

            // Get allowed roles
            List<IRole> allowedRoles = new List<IRole>();
            foreach (var roleId in rerp.AllowedRoleIds)
            {
                var role = roles.GetValueOrDefault(roleId);

                if (role is null)
                {
                    await rrservice.RemoveRoleFromDbAsync(roleId);
                    continue;
                }

                allowedRoles.Add(role);
            }

            // Get prohibited roles
            List<IRole> prohibitedRoles = new List<IRole>();
            foreach (var roleId in rerp.ProhibitedRoleIds)
            {
                var role = roles.GetValueOrDefault(roleId);

                if (role is null)
                {
                    await rrservice.RemoveRoleFromDbAsync(roleId);
                    continue;
                }

                prohibitedRoles.Add(role);
            }

            return new EmoteRolePair(rerp.PairId, emote, assignedRole, allowedRoles, prohibitedRoles);
        }
    }
}
