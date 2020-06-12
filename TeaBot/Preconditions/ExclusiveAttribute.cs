using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord.Commands;

namespace TeaBot.Preconditions
{
    /// <summary>
    ///     Marks commands or modules that can only be executed in a specific set of guilds
    /// </summary>
    public class ExclusiveAttribute : PreconditionAttribute
    {
        readonly HashSet<ulong> _allowedGuilds;

        public ExclusiveAttribute(params ulong[] allowedGuilds)
        {
            _allowedGuilds = new HashSet<ulong>(allowedGuilds);
        }

        public ExclusiveAttribute(ulong allowedGuild)
        {
            _allowedGuilds = new HashSet<ulong>
            {
                allowedGuild
            };
        }

        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            return context.Guild == null || !_allowedGuilds.Contains(context.Guild.Id) ?
                Task.FromResult(PreconditionResult.FromError("This is an exclusive command!")) :
                Task.FromResult(PreconditionResult.FromSuccess());
        }
    }
}
