using System;
using System.Threading.Tasks;
using Discord.Addons.Interactive;
using Discord.WebSocket;
using TeaBot.Preconditions;
using Discord.Commands;
using System.Collections.Generic;
using Discord.Rest;
using Discord;
using Discord.Net;

namespace TeaBot.Commands
{
    /// <summary>
    ///     Class for defining the context for InteractiveBase (used as a module base)
    /// </summary>
    [CheckDisabledModules]
    public abstract class TeaInteractiveBase : InteractiveBase<TeaCommandContext>
    {
        private readonly int cancelLimit = 3;

        /// <summary>
        ///     Await a message that matches a condition.
        /// </summary>
        /// <param name="func">The condition of the message.</param>
        /// <param name="timeout">The time to await the message for.</param>
        /// <returns></returns>
        public async Task<SocketMessage> NextMessageWithCondition(Func<SocketMessage, bool> func, TeaCommandContext context, TimeSpan? timeout = null, string errorMessage = null)
        {
            var eventTrigger = new TaskCompletionSource<SocketMessage>();
            List<RestUserMessage> errorMessages = new List<RestUserMessage>();

            int count = 0;
            async Task Handler(SocketMessage message)
            {
                // Ensure the message is in the same channel and by the same user
                if (message.Author != Context.User || message.Channel != Context.Channel)
                    return;

                // If message matches the condition, set it as the result
                if (func(message))
                {
                    eventTrigger.SetResult(message);
                    return;
                }

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
                try
                {
                    await (context.Channel as ITextChannel).DeleteMessagesAsync(errorMessages);
                }
                catch (HttpException)
                {

                }
                return await trigger;
            }
            else
                return null;
        }
    }
}
