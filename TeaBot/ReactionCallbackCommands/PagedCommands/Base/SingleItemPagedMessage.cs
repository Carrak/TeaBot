using System.Collections.Generic;
using System.Linq;
using Discord.Addons.Interactive;
using TeaBot.Commands;

namespace TeaBot.ReactionCallbackCommands.PagedCommands.Base
{
    /// <summary>
    ///     Abstract class for paging a collection where each element of the collection is a page.
    /// </summary>
    /// <inheritdoc/> 
    abstract class SingleItemPagedMessage<T, U> : PagedMessageBase<T, U> where U : T
    {
        protected SingleItemPagedMessage(InteractiveService interactive,
            TeaCommandContext context,
            IEnumerable<T> collection) : base(interactive, context, collection, collection.Count())
        {
        }

        protected override U CurrentPage()
        {
            return (U)_collection.ElementAt(page);
        }
    }

    /// <inheritdoc/>
    abstract class SingleItemPagedMessage<T> : SingleItemPagedMessage<T, T>
    {
        protected SingleItemPagedMessage(InteractiveService interactive,
            TeaCommandContext context,
            IEnumerable<T> collection) : base(interactive, context, collection)
        {
        }
    }
}
