using System.Collections.Generic;
using Discord;
using Discord.Addons.Interactive;
using TeaBot.Commands;
using TeaBot.Main;
using TeaBot.ReactionCallbackCommands.PagedCommands.Base;
using TeaBot.Utilities;
using TeaBot.Webservices;

namespace TeaBot.ReactionCallbackCommands.PagedCommands
{
    /// <summary>
    ///     Class for paging urban dictionary search results
    /// </summary>
    class UrbanDictionaryPaged : SingleItemPagedMessage<UrbanDictionaryDefinition>
    {
        public UrbanDictionaryPaged(InteractiveService interactive,
            TeaCommandContext context,
            IEnumerable<UrbanDictionaryDefinition> definitions) : base(interactive, context, definitions)
        {
        }

        protected override Embed ConstructEmbed(UrbanDictionaryDefinition definition)
        {
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
