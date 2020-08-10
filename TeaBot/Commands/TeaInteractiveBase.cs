using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Net;
using Discord.Rest;
using Discord.WebSocket;
using TeaBot.Preconditions;

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
        /// <param name="timeout">The time to await the message for.</param>
        /// <returns></returns>
        public async Task<SocketMessage> NextMessageWithConditionAsync(Func<SocketMessage, bool> func, TeaCommandContext context, TimeSpan? timeout = null, int cancelLimit = 3, string errorMessage = null)
        {
            var eventTrigger = new TaskCompletionSource<SocketMessage>();
            List<RestUserMessage> errorMessages = new List<RestUserMessage>();

            int count = 0;

            // Temporary message handler
            async Task Handler(SocketMessage message)
            {
                // Ensure the message is in the same channel and by the same user
                if (message.Author.Id != Context.User.Id || message.Channel.Id != Context.Channel.Id)
                    return;

                // If message matches the condition, set it as the result
                if (func(message))
                {
                    eventTrigger.SetResult(message);
                    return;
                }

                // Send error message in case the message doesn't match the condition
                if (!string.IsNullOrEmpty(errorMessage))
                    errorMessages.Add(await context.Channel.SendMessageAsync(errorMessage));

                // Check if the process should be cancelled, and if it is, set the result to null
                if (++count == cancelLimit || message.Content.StartsWith(context.Prefix, StringComparison.OrdinalIgnoreCase))
                    eventTrigger.SetResult(null);
            }

            Context.Client.MessageReceived += Handler;

            var trigger = eventTrigger.Task;
            var delay = Task.Delay(timeout.Value);
            var task = await Task.WhenAny(trigger, delay);

            Context.Client.MessageReceived -= Handler;

            if (task == trigger)
            {
                // Delete messages
                try
                {
                    await (context.Channel as ITextChannel).DeleteMessagesAsync(errorMessages);
                }
                catch (HttpException)
                {

                }
                // Return the message
                return await trigger;
            }
            else
                return null;
        }
    }
}
