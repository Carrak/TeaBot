using System;
using System.Collections.Generic;
using System.Linq;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;

namespace TeaBot.ReactionCallbackCommands
{
    /// <summary>
    ///     Paged information on users who have the provided role
    /// </summary>
    public class RoleMessage : PagedMessageBase
    {
        private readonly IEnumerable<string> users;
        private readonly int _displayPerPage;
        private readonly IRole _role;

        public RoleMessage(InteractiveService interactive,
            SocketCommandContext context,
            IRole role,
            int displayPerPage,
            RunMode runmode = RunMode.Async,
            TimeSpan? timeout = null,
            ICriterion<SocketReaction> criterion = null) : base(interactive, context, runmode, timeout, criterion)
        {
            _role = role;
            users = context.Guild.Users.Where(user => user.Roles.Contains(role as SocketRole)).Select(user => user.Mention);
            _displayPerPage = displayPerPage;
            SetTotalPages(users.Count(), _displayPerPage);
        }

        protected override Embed ConstructEmbed()
        {
            var embed = new EmbedBuilder();

            embed.WithTitle(_role.Name)
                .WithColor(Tea.MainColor)
                .AddField("Colour", _role.Color)
                .AddField($"Users - {users.Count()} members", string.Join(" ", CurrentPage(users, _displayPerPage)));

            embed = SetDefaultFooter(embed);

            return embed.Build();
        }
    }
}
