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
        public SocketGuild Guild { get; }

        /// <summary>
        ///     Discord message this reaction-role message is attached to.
        /// </summary>
        public IUserMessage Message { get; set; }

        /// <summary>
        ///     The limit of how many roles from a list the user can assign to themselves from the reaction-role message.
        ///     Null if there's no limit.
        /// </summary>
        public int? LimitId { get; }

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
        public Dictionary<IEmote, EmoteRolePair> EmoteRolePairs { get; set; }

        public ReactionRoleMessage(ReactionRoleService rrservice,
            int rrid,
            int? limitid,
            SocketGuild guild,
            IUserMessage message,
            Dictionary<IEmote, EmoteRolePair> pairs,
            IEnumerable<IRole> allowedRoles,
            IEnumerable<IRole> prohibitedRoles)
        {
            RRService = rrservice;

            RRID = rrid;
            LimitId = limitid;

            Guild = guild;
            Message = message;
            EmoteRolePairs = pairs;

            GlobalAllowedRoles = allowedRoles;
            GlobalProhibitedRoles = prohibitedRoles;
        }

        /// <summary>
        ///     Creates a "Discord-compatible" reaction-role message using the information from the database.
        /// </summary>
        /// <param name="rrservice">Reaction-role service for error-handling.</param>
        /// <param name="rawrrmsg">Unprocessed reaction-role message.</param>
        /// <returns>An instance of <see cref="ReactionRoleMessage"/>, or null if the guild is not present.</returns>
        public static async Task<ReactionRoleMessage> CreateAsync(
            ReactionRoleService rrservice,
            RawReactionRoleMessage rawrrmsg)
        {
            // The guild the message is in
            var guild = rrservice._client.GetGuild(rawrrmsg.GuildId);

            // Delete this reaction-role message if its guild is not present (either because the guild was deleted or the bot was kicked/banned).
            if (guild is null)
            {
                await rrservice.RemoveGuildFromDbAsync(rawrrmsg.GuildId);
                return null;
            }

            // The location of the reaction-role message
            IUserMessage message = null;

            if (rawrrmsg.ChannelId.HasValue)
            {
                // Retrieve the channel

                if (guild.GetChannel(rawrrmsg.ChannelId.Value) is ITextChannel channel)
                {
                    if (rawrrmsg.MessageId.HasValue)
                    {
                        try
                        {
                            // Retrieve the message
                            message = await channel.GetMessageAsync(rawrrmsg.MessageId.Value) as IUserMessage;

                            // Remove the reaction-role's info about the message if the message itself is null
                            if (message is null)
                            {
                                await rrservice.RemoveMessageFromDbAsync(channel.Id, rawrrmsg.MessageId.Value);
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
                    await rrservice.RemoveChannelFromDbAsync(rawrrmsg.ChannelId.Value);
            }

            // Get allowed roles
            var allowedRoles = new List<IRole>();
            foreach (ulong roleid in rawrrmsg.GlobalAllowedRoleIds)
            {
                var role = guild.GetRole(roleid);
                if (role is null)
                    await rrservice.RemoveRoleFromDbAsync(roleid);
                else
                    allowedRoles.Add(role);
            }

            // Get prohibited roles
            var prohibitedRoles = new List<IRole>();
            foreach (ulong roleid in rawrrmsg.GlobalProhibitedRoleIds)
            {
                var role = guild.GetRole(roleid);
                if (role is null)
                    await rrservice.RemoveRoleFromDbAsync(roleid);
                else prohibitedRoles.Add(role);
            }

            var pairs = new List<EmoteRolePair>();
            var blockedPairs = new List<EmoteRolePair>();

            int maxRolePosition = guild.CurrentUser.Roles.Max(x => x.Position);

            foreach (var rerp in rawrrmsg.EmoteRolePairs)
                if (await EmoteRolePair.CreateAsync(rrservice, guild, rerp) is EmoteRolePair preparedPair)
                {
                    if (preparedPair.Role.Position > maxRolePosition)
                        preparedPair.Blocked = true;

                    pairs.Add(preparedPair);
                }

            var baseMessage = new ReactionRoleMessage(rrservice, rawrrmsg.RRID, rawrrmsg.LimitId, guild, message, pairs.ToDictionary(x => x.Emote), allowedRoles, prohibitedRoles);

            if (rawrrmsg is RawFullReactionRoleMessage frawrrmsg)
                return new FullReactionRoleMessage(baseMessage, frawrrmsg.Data);
            else
                return baseMessage;
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
                var channel = Message.Channel as SocketTextChannel;
                var currentUser = Guild.CurrentUser;
                var cutempperms = channel.GetPermissionOverwrite(currentUser);
                if (cutempperms.HasValue)
                {
                    var perms = cutempperms.Value;
                    perms = perms.Modify(addReactions: PermValue.Allow);
                    await channel.AddPermissionOverwriteAsync(currentUser, perms);
                }
                else
                {
                    var op = new OverwritePermissions(addReactions: PermValue.Allow);
                    await channel.AddPermissionOverwriteAsync(currentUser, op);
                }

                // Change everyone role
                var tempperms = channel.GetPermissionOverwrite(Guild.EveryoneRole);
                if (tempperms.HasValue)
                {
                    var perms = tempperms.Value;
                    perms = perms.Modify(addReactions: PermValue.Deny);
                    await channel.AddPermissionOverwriteAsync(Guild.EveryoneRole, perms);
                }
                else
                {
                    var op = new OverwritePermissions(addReactions: PermValue.Deny);
                    await channel.AddPermissionOverwriteAsync(Guild.EveryoneRole, op);
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
        public async Task HandleReactionAdded(SocketReaction reaction, IGuildUser user)
        {
            if (EmoteRolePairs.TryGetValue(reaction.Emote, out var erp))
            {
                // Return if the selected pair is blocked
                if (erp.Blocked)
                    return;

                // The user who placed the reaction
                //var user = await Guild.GetUserAsync(reaction.UserId) as SocketGuildUser;

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
                if (LimitId.HasValue && RRService.CheckLimitReached(LimitId.Value, user.RoleIds) ||
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
        public async Task HandleReactionRemoved(SocketReaction reaction, IGuildUser user)
        {
            if (EmoteRolePairs.TryGetValue(reaction.Emote, out var erp))
            {
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
        public IRole Role { get; set; }

        public IEnumerable<IRole> AllowedRoles { get; }
        public IEnumerable<IRole> ProhibitedRoles { get; }

        public bool Blocked { get; set; }

        public EmoteRolePair(int pairid, IEmote emote, IRole role, IEnumerable<IRole> allowedRoles, IEnumerable<IRole> prohibitedRoles, bool blocked)
        {
            PairId = pairid;
            Emote = emote;
            Role = role;
            AllowedRoles = allowedRoles;
            ProhibitedRoles = prohibitedRoles;
            Blocked = blocked;
        }

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

            var currentUser = await guild.GetCurrentUserAsync() as SocketGuildUser;
            bool blocked = assignedRole.Position >= currentUser.Roles.Max(x => x.Position);

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

            EmoteRolePair erp = new EmoteRolePair(rerp.PairId, emote, assignedRole, allowedRoles, prohibitedRoles, blocked);
            if (rerp is RawFullEmoteRolePair rferp)
                return new FullEmoteRolePair(erp, rferp.Data);
            else
                return erp;
        }
    }
}
