using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;

namespace TeaBot.ReactionCallbackCommands
{
    /// <summary>
    ///     The base abstract class for paged, reaction-based messages.
    /// </summary>
    public abstract class PagedMessageBase : IReactionCallback
    {
        public RunMode RunMode { get; }
        public ICriterion<SocketReaction> Criterion { get; }
        public TimeSpan? Timeout { get; }
        public SocketCommandContext Context { get; }
        public InteractiveService Interactive { get; }

        protected IUserMessage _message;

        private static readonly Emoji arrowForward = new Emoji("▶️");
        private static readonly Emoji arrowBackward = new Emoji("◀️");

        protected int page = 0;
        public int TotalPages { get; private set; }

        protected PagedMessageBase(InteractiveService interactive,
            SocketCommandContext context,
            RunMode runmode = RunMode.Async,
            TimeSpan? timeout = null,
            ICriterion<SocketReaction> criterion = null)
        {
            Interactive = interactive;
            Context = context;
            RunMode = runmode;
            Criterion = criterion ?? new EmptyCriterion<SocketReaction>();
            Timeout = timeout ?? TimeSpan.FromMinutes(2);
        }

        /// <summary>
        ///     Creates an embed to be displayed using <see cref="CurrentPage{T}"/> for paging
        /// </summary>
        /// <returns>Embed displaying the current page</returns>
        protected abstract Embed ConstructEmbed();

        /// <summary>
        ///     Sets the upper boundary for pages.
        /// </summary>
        /// <param name="count">The value of total pages to set</param>
        protected void SetTotalPages(int count)
        {
            TotalPages = count;
        }

        /// <summary>
        ///     Sets the upper boundary for pages by splitting a given amount of elements.
        /// </summary>
        /// <param name="elementsCount">Total amount of elements to be split.</param>
        /// <param name="displayPerPage">Value that determines how many elements are present in a page.</param>
        protected void SetTotalPages(int elementsCount, int displayPerPage)
        {
            TotalPages = (int)Math.Ceiling(elementsCount / (float)displayPerPage);
        }

        /// <summary>
        ///     Sets the predermined footer to a given EmbedBuilder.
        /// </summary>
        /// <param name="eb">EmbedBuilder to set the footer to.</param>
        /// <returns>An EmbedBuilder with a default footer.</returns>
        protected EmbedBuilder SetDefaultFooter(EmbedBuilder eb)
        {
            return eb.WithFooter($"Page {page + 1} / {TotalPages}");
        }

        /// <summary>
        ///     Selects a part of a given IEnumberable basing on the <see cref="page"></see> variable
        /// </summary>
        /// <param name="enumerable">IEnumerable object to select a part from.</param>
        /// <param name="displayPerPage">Amount of objects to select for this page</param>
        /// <returns>Part of the given IEnumerable</returns>
        protected IEnumerable<T> CurrentPage<T>(IEnumerable<T> enumerable, int displayPerPage)
        {
            return enumerable.Where((element, index) => index >= page * displayPerPage && index < (page + 1) * displayPerPage);
        }

        /// <summary>
        ///     Instantiates <see cref="_message"/> by sending to the channel the command was invoked in and adds reaction callback if necessary.
        /// </summary>
        public async Task DisplayAsync()
        {
            _message = await Context.Channel.SendMessageAsync(embed: ConstructEmbed());
            if (TotalPages > 1)
                await AddCallback();
        }

        /// <summary>
        ///     Adds reaction callback to <see cref="_message"/> that timeouts after the value of <see cref="Timeout"/>
        /// </summary>
        private async Task AddCallback()
        {
            await _message.AddReactionsAsync(new Emoji[] { arrowBackward, arrowForward });
            Interactive.AddReactionCallback(_message, this);

            _ = Task.Delay(Timeout.Value).ContinueWith(_ =>
            {
                Interactive.RemoveReactionCallback(_message);
                _message.RemoveReactionsAsync(Context.Client.CurrentUser, new Emoji[] { arrowBackward, arrowForward });
            });
        }

        /// <summary>
        ///     Triggers when a reaction is added to <see cref="_message"/> 
        ///     and modifies the message if the reaction is 
        ///     either <see cref="arrowBackward"/> or <see cref="arrowForward"/>
        /// </summary>
        /// <param name="reaction">The added reaction</param>
        /// <returns>Bool value determining whether the callback should be removed</returns>
        public async Task<bool> HandleCallbackAsync(SocketReaction reaction)
        {
            if (reaction.Emote.Equals(arrowForward))
            {
                if (page < TotalPages - 1)
                    page++;
                else
                    page = 0;
            }
            else if (reaction.Emote.Equals(arrowBackward))
            {
                if (page > 0)
                    page--;
                else
                    page = TotalPages - 1;
            }
            else return false;

            var embed = ConstructEmbed();

            await _message.ModifyAsync(x => x.Embed = embed);
            await _message.RemoveReactionAsync(reaction.Emote, reaction.User.Value);

            return false;
        }
    }
}
