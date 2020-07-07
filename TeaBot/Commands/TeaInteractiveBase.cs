using System;
using System.Threading.Tasks;
using Discord.Addons.Interactive;
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
        public async Task<bool> NextMessageWithCondition(Func<SocketMessage, bool> messagePrecondition, int limit, TimeSpan? timeout, string errorMessage = null, bool cancelOnAnotherCommandExecuted = true)
        {
            timeout ??= TimeSpan.FromSeconds(10);
            
            var eventTrigger = new TaskCompletionSource<bool>();
            var cancelTrigger = new TaskCompletionSource<bool>();
            
            int count = 0;
            async Task Handler(SocketMessage message)
            {
                if (message.Author.Id != Context.User.Id ||
                    message.Channel.Id != Context.Channel.Id)
                    return;

                if (messagePrecondition(message))
                {
                    eventTrigger.SetResult(true);
                    return;
                }
                else if (!string.IsNullOrEmpty(errorMessage))
                    await Context.Channel.SendMessageAsync(errorMessage);

                if (++count == limit || (cancelOnAnotherCommandExecuted && message.Content.StartsWith(Context.Prefix, StringComparison.OrdinalIgnoreCase)))
                    eventTrigger.SetResult(false);
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
                return false;
        }

    }
}
