using System;
using System.Collections.Generic;
using System.Linq;
using Discord.Addons.Interactive;
using TeaBot.Commands;

namespace TeaBot.ReactionCallbackCommands.PagedCommands.Base
{
    /// <summary>
    ///     Abstract class for paging collections which are paged by a certain amount of elements displayed per page.
    /// </summary>
    /// <inheritdoc/>
    abstract class FragmentedPagedMessage<T, U> : PagedMessageBase<T, U>
    {
        private readonly int _displayPerPage;

        protected FragmentedPagedMessage(InteractiveService interactive,
            TeaCommandContext context,
            IEnumerable<T> collection,
            int displayPerPage) : base(interactive, context, collection, (int)Math.Ceiling(collection.Count() / (float)displayPerPage))
        {
            _displayPerPage = displayPerPage;
        }

        protected override U CurrentPage()
        {
            return (U)_collection.Skip(_displayPerPage * page).Take(_displayPerPage);
        }
    }

    /// <inheritdoc/>
    abstract class FragmentedPagedMessage<T> : FragmentedPagedMessage<T, IEnumerable<T>>
    {
        protected FragmentedPagedMessage(InteractiveService interactive,
            TeaCommandContext context,
            IEnumerable<T> collection,
            int displayPerPage) : base(interactive, context, collection, displayPerPage)
        {
        }
    }
}
