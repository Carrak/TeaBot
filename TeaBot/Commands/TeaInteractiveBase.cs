using Discord.Addons.Interactive;
using TeaBot.Preconditions;

namespace TeaBot.Commands
{
    /// <summary>
    ///     Class for defining the context for InteractiveBase (used as a module base)
    /// </summary>
    [CheckDisabledModules]
    public abstract class TeaInteractiveBase : InteractiveBase<TeaCommandContext>
    {
    }
}
