using Discord;

namespace TeaBot.ReactionCallbackCommands.ReactionRole
{
    /// <summary>
    ///     Extra data for <see cref="FullReactionRoleMessage"/>.
    /// </summary>
    public class ReactionRoleMessageData
    {
        public string Name { get; }
        public string Description { get; }
        public Color? Color { get; }

        public ReactionRoleMessageData(string name, string description, Color? color)
        {
            Name = name;
            Description = description;
            Color = color;
        }
    }

    /// <summary>
    ///     Extra data for <see cref="FullEmoteRolePair"/>.
    /// </summary>
    public class EmoteRolePairData
    { 
        public string Description { get; }

        public EmoteRolePairData(string description) => Description = description;
    }
}
