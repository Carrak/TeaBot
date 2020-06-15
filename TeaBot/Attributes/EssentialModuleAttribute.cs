using System;

namespace TeaBot.Attributes
{
    /// <summary>
    ///     Used for marking modules that cannot be disabled in guilds
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    class EssentialModuleAttribute : Attribute
    {
    }
}
