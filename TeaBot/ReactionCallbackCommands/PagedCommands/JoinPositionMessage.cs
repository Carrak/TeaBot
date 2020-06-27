using System.Collections.Generic;
using System.Linq;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using TeaBot.Main;
using TeaBot.ReactionCallbackCommands.PagedCommands.Base;

namespace TeaBot.ReactionCallbackCommands.PagedCommands
{
    /// <summary>
    ///     A class for paging the guild join position top command 
    /// </summary>
    class JoinPositionMessage : FragmentedPagedMessage<(string, int)>
    {
        private readonly bool _ignoreBots;

        public JoinPositionMessage(InteractiveService interactive,
            SocketCommandContext context,
            int displayPerPage,
            bool ignoreBots) : base(interactive, context, context.Guild.Users.OrderBy(user => user.JoinedAt.Value.DateTime)
               .Select((user, index) => (User: user, OriginalIndex: index))
               .Where(tuple => !ignoreBots || !tuple.User.IsBot)
               .Select(tuple => (tuple.User.ToString(), tuple.OriginalIndex)), displayPerPage)
        {
            _ignoreBots = ignoreBots;
        }

        /// <inheritdoc/>
        protected override Embed ConstructEmbed(IEnumerable<(string, int)> users)
        {
            var embed = new EmbedBuilder();

            embed.WithColor(TeaEssentials.MainColor)
                .WithThumbnailUrl(Context.Guild.IconUrl)
                .WithFooter($"Page {page + 1} / {TotalPages} {(_ignoreBots ? "" : "| Use <tea joinpostop nobots> to ignore bots")}")
                .WithTitle("Join position top")
                .WithDescription(string.Join("\n", users.Select((tuple, index) => $"{tuple.Item2 + 1}. {tuple.Item1}")));

            return embed.Build();
        }
    }
}
