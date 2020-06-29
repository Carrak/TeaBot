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
        public async Task<bool> AwaitMessageWithContent(string content, int limit, TimeSpan? timeout = null, bool caseInsensitive = true, bool cancelOnAnotherCommandExecuted = true)
        {
            int count = 0;

            timeout ??= TimeSpan.FromSeconds(10);

            var eventTrigger = new TaskCompletionSource<bool>();
            var cancelTrigger = new TaskCompletionSource<bool>();

            Task Handler(SocketMessage message)
            {
                if (message.Author != Context.User && message.Channel != Context.Channel)
                    return Task.CompletedTask;

                count++;
                if (message.Content.Equals(content, caseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                    eventTrigger.SetResult(true);
                if (count == limit || (cancelOnAnotherCommandExecuted && message.Content.StartsWith(Context.Prefix, StringComparison.OrdinalIgnoreCase)))
                    eventTrigger.SetResult(false);

                return Task.CompletedTask;
            }

            Context.Client.MessageReceived += Handler;

            var trigger = eventTrigger.Task;
            var cancel = cancelTrigger.Task;
            var delay = Task.Delay(timeout.Value);
            var task = await Task.WhenAny(trigger, delay).ConfigureAwait(false);

            Context.Client.MessageReceived -= Handler;

            if (task == trigger)
                return await trigger.ConfigureAwait(false);
            else
                return false;
        }

    }
}
