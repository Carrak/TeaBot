using System;
using System.Collections.Generic;
using System.Linq;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using TeaBot.Main;
using TeaBot.Webservices;

namespace TeaBot.ReactionCallbackCommands
{
    /// <summary>
    ///     Class for paging urban dictionary search results
    /// </summary>
    class UrbanDictionaryPaged : PagedMessageBase
    {
        private readonly IEnumerable<UrbanDictionaryDefinition> _definitions;

        public UrbanDictionaryPaged(InteractiveService interactive,
            SocketCommandContext context,
            IEnumerable<UrbanDictionaryDefinition> definitions,
            RunMode runmode = RunMode.Async,
            TimeSpan? timeout = null,
            ICriterion<SocketReaction> criterion = null) : base(interactive, context, runmode, timeout, criterion)
        {
            _definitions = definitions;
            SetTotalPages(definitions.Count());
        }

        protected override Embed ConstructEmbed()
        {
            UrbanDictionaryDefinition definition = _definitions.ElementAt(page);

            var embed = new EmbedBuilder();
            embed.WithColor(TeaEssentials.MainColor)
                .WithAuthor(Context.User)
                .WithTitle($"Definition of {definition.Word}")
                .WithUrl($"https://www.urbandictionary.com/define.php?term={WebUtilities.FormatStringForURL(definition.Word)}")
                .WithDescription(definition.Definition)
                .AddField("Example", string.IsNullOrEmpty(definition.Example) ? "-" : definition.Example)
                .AddField("Rating", $"👍 {definition.ThumbsUp}   👎 {definition.ThumbsDown}")
                .WithFooter($"Page {page + 1} / {TotalPages} | Definition by {definition.Author}");

            return embed.Build();
        }
    }
}
