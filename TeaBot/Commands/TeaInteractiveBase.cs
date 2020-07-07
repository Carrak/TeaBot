using System;
using System.Threading.Tasks;
using Discord.Addons.Interactive;
using Discord.WebSocket;
using TeaBot.Preconditions;
using Discord.Commands;

namespace TeaBot.Commands
{
    /// <summary>
    ///     Class for defining the context for InteractiveBase (used as a module base)
    /// </summary>
    [CheckDisabledModules]
    public abstract class TeaInteractiveBase : InteractiveBase<TeaCommandContext>
    {
        /// <summary>
        ///     Await a message that matches a condition.
        /// </summary>
        /// <param name="func">The condition of the message.</param>
        /// <param name="limit">The limit of processed messages.</param>
        /// <param name="timeout">The time to await the message for.</param>
        /// <param name="cancelOnAnotherCommandExecuted">Cancel the awaiter if another command is executed.</param>
        /// <returns></returns>
        public async Task<SocketMessage> NextMessageWithCondition(Func<SocketMessage, bool> func, TeaCommandContext context, int limit, TimeSpan? timeout = null, bool cancelOnAnotherCommandExecuted = true)
        {
            timeout ??= TimeSpan.FromSeconds(10);

            var eventTrigger = new TaskCompletionSource<SocketMessage>();
            var cancelTrigger = new TaskCompletionSource<bool>();

            int count = 0;
            Task Handler(SocketMessage message)
            {
                // Ensure the message is in the same channel and by the same user
                if (message.Author != Context.User || message.Channel != Context.Channel)
                    return Task.CompletedTask;

                // If message matches the condition, set it as the result
                if (func(message))
                {
                    eventTrigger.SetResult(message);
                    return Task.CompletedTask;
                }

                // Check if the process should be cancelled, and if it is, set the result to null
                if (++count == limit || (cancelOnAnotherCommandExecuted && message.Content.StartsWith(context.Prefix, StringComparison.OrdinalIgnoreCase)))
                    eventTrigger.SetResult(null);

                return Task.CompletedTask;
            }

            Context.Client.MessageReceived += Handler;

            var trigger = eventTrigger.Task;
            var cancel = cancelTrigger.Task;
            var delay = Task.Delay(timeout.Value);
            var task = await Task.WhenAny(trigger, delay);

            Context.Client.MessageReceived -= Handler;

            if (task == trigger)
                return await trigger;
            else
                return null;
        }

    }
}
