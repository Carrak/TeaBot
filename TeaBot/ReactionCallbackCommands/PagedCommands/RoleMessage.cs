using System;
using System.Collections.Generic;
using System.Linq;
using Discord;
using Discord.Addons.Interactive;
using Discord.WebSocket;
using TeaBot.Commands;
using TeaBot.ReactionCallbackCommands.PagedCommands.Base;

namespace TeaBot.ReactionCallbackCommands
{
    /// <summary>
    ///     Paged information on users who have the provided role
    /// </summary>
    class RoleMessage : FragmentedPagedMessage<SocketGuildUser>
    {
        private readonly IRole _role;

        public RoleMessage(InteractiveService interactive,
            TeaCommandContext context,
            IRole role,
            int displayPerPage) : base(interactive, context, context.Guild.Users.Where(user => user.Roles.Contains(role as SocketRole)), displayPerPage)
        {
            _role = role;
        }

        /// <inheritdoc/>
        protected override Embed ConstructEmbed(IEnumerable<SocketGuildUser> users)
        {
            var embed = new EmbedBuilder();

            embed.WithTitle(_role.Name)
                .WithColor(_role.Color)
                .AddField("Colour", _role.Color)
                .AddField($"Users - {users.Count()} members", string.Join(" ", users.Select(user => user.Mention)));

            embed = SetDefaultFooter(embed);

            return embed.Build();
        }
    }
}
