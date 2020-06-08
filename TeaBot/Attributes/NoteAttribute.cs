using System;

namespace TeaBot.Attributes
{
    /// <summary>
    ///     Note/remark attached to a command.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class NoteAttribute : Attribute
    {
        public string Content { get; }
        public NoteAttribute(string note)
        {
            Content = note;
        }
    }
}
