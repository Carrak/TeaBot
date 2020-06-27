using System;
using System.Linq;
using System.Threading.Tasks;
using Discord.Commands;
using TeaBot.Commands;

namespace TeaBot.Preconditions
{
    /// <summary>
    ///     Precondition for checking whether the command's module is disabled in the guild.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    class CheckDisabledModulesAttribute : PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            var teaContext = context as TeaCommandContext;

            if (context.Guild is null)
                return Task.FromResult(PreconditionResult.FromSuccess());

            return teaContext.DisabledModules.Contains(command.Module.Name.ToLower()) ?
                Task.FromResult(PreconditionResult.FromError("This command's module is disabled in this guild!")) :
                Task.FromResult(PreconditionResult.FromSuccess());
        }
    }
}
