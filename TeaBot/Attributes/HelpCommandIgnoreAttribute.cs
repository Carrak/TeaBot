using System;

namespace TeaBot.Attributes
{
    /// <summary>
    ///     A placeholder attribute used to mark modules to be ignored by the help command.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class HelpCommandIgnoreAttribute : Attribute
    {
    }

    // yeah i know this is a very big attribute, don't question me on this one
}
