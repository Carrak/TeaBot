using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace TeaBot.Preconditions
{
    /// <summary>
    ///     Marks commands or modules that can only be executed in NSFW channels.
    /// </summary>
    public class NSFWAttribute : PreconditionAttribute
    {
        /// <inheritdoc/>
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            var channel = context.Channel as ITextChannel;
            var scc = context as SocketCommandContext;

            return scc.IsPrivate || channel.IsNsfw ?
                Task.FromResult(PreconditionResult.FromSuccess()) :
                Task.FromResult(PreconditionResult.FromError("The channel is not flagged as NSFW!"));
        }
    }

}
