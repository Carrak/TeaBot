using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord.Commands;

namespace TeaBot.Preconditions
{
    // Credit for this entire class goes to Joe4ever
    // https://github.com/Joe4evr

    /// <summary>
    ///     Sets how often a user is allowed to use this command
    ///     or any command in this module.
    /// </summary>
    /// <remarks>
    ///     <note type="warning">
    ///         This is backed by an in-memory collection
    ///         and will not persist with restarts.
    ///     </note>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class RatelimitAttribute : PreconditionAttribute
    {
        public readonly TimeSpan InvokeLimitPeriod;

        private readonly uint _invokeLimit;
        private readonly Dictionary<ulong, CommandTimeout> _invokeTracker = new Dictionary<ulong, CommandTimeout>();

        /// <param name="times">
        ///     The number of times a user may use the command within a certain period.
        /// </param>
        /// <param name="seconds">
        ///     The amount of time since first invoke a user has until the limit is lifted.
        /// </param>
        public RatelimitAttribute(double seconds, uint times = 1)
        {
            _invokeLimit = times;
            InvokeLimitPeriod = TimeSpan.FromSeconds(seconds);
        }

        /// <inheritdoc/>
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            DateTime now = DateTime.UtcNow;
            ulong key = context.Channel.Id;

            CommandTimeout timeout = _invokeTracker.TryGetValue(key, out CommandTimeout t)
                && now - t.FirstInvoke < InvokeLimitPeriod
                    ? t : new CommandTimeout(now);

            timeout.TimesInvoked++;

            if (timeout.TimesInvoked <= _invokeLimit)
            {
                _invokeTracker[key] = timeout;
                return Task.FromResult(PreconditionResult.FromSuccess());
            }
            else
            {
                if (!timeout.Warned)
                {
                    double cooldown = InvokeLimitPeriod.TotalSeconds - (DateTime.UtcNow - t.FirstInvoke).TotalSeconds;
                    timeout.Warned = true;
                    return Task.FromResult(PreconditionResult.FromError($"Cooldown! **{cooldown:0.00}** more seconds!"));
                }
                else
                {
                    return Task.FromResult(PreconditionResult.FromError(""));
                }
            }
        }

        private sealed class CommandTimeout
        {
            public uint TimesInvoked { get; set; }
            public DateTime FirstInvoke { get; }
            public bool Warned { get; set; } = false;

            public CommandTimeout(DateTime timeStarted)
            {
                FirstInvoke = timeStarted;
            }
        }
    }
}

