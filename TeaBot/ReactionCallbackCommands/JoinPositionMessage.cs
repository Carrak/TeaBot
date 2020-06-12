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
    ///     A class for paging the guild join position top command 
    /// </summary>
    class JoinPositionMessage : PagedMessageBase
    {
        private readonly int _displayPerPage;
        private readonly bool _ignoreBots;
        private readonly IEnumerable<(string, int)> _users;

        public JoinPositionMessage(InteractiveService interactive,
            SocketCommandContext context,
            int displayPerPage,
            bool ignoreBots,
            RunMode runmode = RunMode.Async,
            TimeSpan? timeout = null,
            ICriterion<SocketReaction> criterion = null) : base(interactive, context, runmode, timeout, criterion)
        {
            _ignoreBots = ignoreBots;
            _displayPerPage = displayPerPage;
            _users = Context.Guild.Users.OrderBy(user => user.JoinedAt.Value.DateTime)
                .Select((user, index) => (User: user, OriginalIndex: index))
                .Where(tuple => !_ignoreBots || !tuple.User.IsBot)
                .Select(tuple => (tuple.User.ToString(), tuple.OriginalIndex));

            SetTotalPages(_users.Count(), _displayPerPage);
        }

        /// <inheritdoc/>
        protected override Embed ConstructEmbed()
        {
            var embed = new EmbedBuilder();

            var users = CurrentPage(_users, _displayPerPage).Select((tuple, index) => $"{tuple.Item2 + 1}. {tuple.Item1}"); ;

            embed.WithColor(TeaEssentials.MainColor)
                .WithThumbnailUrl(Context.Guild.IconUrl)
                .WithFooter($"Page {page + 1} / {TotalPages} {(_ignoreBots ? "" : "| Use <tea joinpostop nobots> to ignore bots")}")
                .WithTitle("Join position top")
                .WithDescription(string.Join("\n", users));

            return embed.Build();
        }
    }
}
