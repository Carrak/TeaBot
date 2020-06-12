using System;
using System.Collections.Generic;
using System.Linq;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using TeaBot.Main;

namespace TeaBot.ReactionCallbackCommands
{
    /// <summary>
    ///     Class for paging all roles and the amount of users who have them
    /// </summary>
    class RolesMessage : PagedMessageBase
    {
        private readonly int _displayPerPage;
        private readonly IEnumerable<SocketRole> _roles;

        public RolesMessage(InteractiveService interactive,
            SocketCommandContext context,
            int displayPerPage,
            RunMode runmode = RunMode.Async,
            TimeSpan? timeout = null,
            ICriterion<SocketReaction> criterion = null) : base(interactive, context, runmode, timeout, criterion)
        {
            _displayPerPage = displayPerPage;
            _roles = Context.Guild.Roles.Where(role => !role.IsEveryone)
                .OrderByDescending(role => role.Position);
            SetTotalPages(Context.Guild.Roles.Count, _displayPerPage);
        }

        protected override Embed ConstructEmbed()
        {
            var roles = CurrentPage(_roles, _displayPerPage).Select(role => $"{role.Mention} - {role.Members.Count()} members");

            var embed = new EmbedBuilder();

            embed.WithTitle($"{Context.Guild} roles")
                .WithColor(TeaEssentials.MainColor)
                .WithDescription(string.Join("\n", roles))
                .WithThumbnailUrl(Context.Guild.IconUrl)
                .WithCurrentTimestamp();

            embed = SetDefaultFooter(embed);

            return embed.Build();
        }
    }
}
