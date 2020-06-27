using System.Collections.Generic;
using System.Linq;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using TeaBot.Main;
using TeaBot.ReactionCallbackCommands.PagedCommands.Base;

namespace TeaBot.ReactionCallbackCommands.PagedCommands
{
    /// <summary>
    ///     Class for paging all roles and the amount of users who have them
    /// </summary>
    class RolesMessage : FragmentedPagedMessage<SocketRole>
    {
        public RolesMessage(InteractiveService interactive,
            SocketCommandContext context,
            int displayPerPage) : base(interactive, context, context.Guild.Roles.Where(role => !role.IsEveryone)
                .OrderByDescending(role => role.Position), displayPerPage)
        {
        }

        protected override Embed ConstructEmbed(IEnumerable<SocketRole> roles)
        {
            var embed = new EmbedBuilder();

            embed.WithTitle($"{Context.Guild} roles")
                .WithColor(TeaEssentials.MainColor)
                .WithDescription(string.Join("\n", roles.Select(role => $"{role.Mention} - {role.Members.Count()} members")))
                .WithThumbnailUrl(Context.Guild.IconUrl)
                .WithCurrentTimestamp();

            embed = SetDefaultFooter(embed);

            return embed.Build();
        }
    }
}
