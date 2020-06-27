using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;

namespace TeaBot.ReactionCallbackCommands.PagedCommands.Base
{
    /// <summary>
    ///     Base class for creating paged messages that are controlled by reactions.
    /// </summary>
    /// <typeparam name="T">The type of a single element.</typeparam>
    /// <typeparam name="U">The type returned by <see cref="CurrentPage"/></typeparam>
    abstract class PagedMessageBase<T, U> : IReactionCallback
    {
        public RunMode RunMode { get; }
        public ICriterion<SocketReaction> Criterion { get; }
        public TimeSpan? Timeout { get; }
        public SocketCommandContext Context { get; }
        public InteractiveService Interactive { get; }

        /// <summary>
        ///     The message with the paged embed.
        /// </summary>
        private IUserMessage message;

        private static readonly Emoji arrowForward = new Emoji("▶️");
        private static readonly Emoji arrowBackward = new Emoji("◀️");

        /// <summary>
        ///     The collection to use elements from.
        /// </summary>
        protected IEnumerable<T> _collection;

        /// <summary>
        ///     Current page that is used in the message.
        /// </summary>
        protected int page = 0;

        /// <summary>
        ///     The total amount of pages for the given collection.
        /// </summary>
        public int TotalPages { get; private set; }

        protected PagedMessageBase(InteractiveService interactive,
            SocketCommandContext context,
            IEnumerable<T> collection,
            int totalPages,
            RunMode runmode = RunMode.Async,
            TimeSpan? timeout = null,
            ICriterion<SocketReaction> criterion = null)
        {
            Interactive = interactive;
            Context = context;
            _collection = collection;
            TotalPages = totalPages;

            RunMode = runmode;
            Criterion = criterion ?? new EmptyCriterion<SocketReaction>();
            Timeout = timeout ?? TimeSpan.FromMinutes(2);
        }

        /// <summary>
        ///     Builds an embed using the fragment of the collection that it is given.
        /// </summary>
        /// <param name="currentPage"></param>
        /// <returns>The prepared embed to be displayed.</returns>
        protected abstract Embed ConstructEmbed(U currentPage);

        /// <summary>
        ///     Chops a fragment from the <see cref="_collection"/> using <see cref="page"/>.
        /// </summary>
        /// <returns>An element or a subcollection.</returns>
        protected abstract U CurrentPage();

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
        ///     Instantiates <see cref="message"/> by sending to the channel the command was invoked in and adds reaction callback if necessary.
        /// </summary>
        public async Task DisplayAsync()
        {
            message = await Context.Channel.SendMessageAsync(embed: ConstructEmbed(CurrentPage()));
            if (TotalPages > 1)
                await AddCallback();
        }

        /// <summary>
        ///     Adds reaction callback to <see cref="message"/> that timeouts after the value of <see cref="Timeout"/>
        /// </summary>
        private async Task AddCallback()
        {
            await message.AddReactionsAsync(new Emoji[] { arrowBackward, arrowForward });
            Interactive.AddReactionCallback(message, this);

            _ = Task.Delay(Timeout.Value).ContinueWith(_ =>
            {
                Interactive.RemoveReactionCallback(message);
                message.RemoveReactionsAsync(Context.Client.CurrentUser, new Emoji[] { arrowBackward, arrowForward });
            });
        }

        /// <summary>
        ///     Triggers when a reaction is added to <see cref="message"/> 
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

            var embed = ConstructEmbed(CurrentPage());

            await message.ModifyAsync(x => x.Embed = embed);
            await message.RemoveReactionAsync(reaction.Emote, reaction.User.Value);

            return false;
        }
    }
}
