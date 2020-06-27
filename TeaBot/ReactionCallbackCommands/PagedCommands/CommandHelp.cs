using System.Collections.Generic;
using System.Linq;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using TeaBot.Attributes;
using TeaBot.Main;
using TeaBot.ReactionCallbackCommands.PagedCommands.Base;

namespace TeaBot.ReactionCallbackCommands.PagedCommands
{
    /// <summary>
    ///     Class for paging the command help.
    /// </summary>
    class CommandHelp : SingleItemPagedMessage<CommandInfo>
    {
        public CommandHelp(InteractiveService interactive,
            SocketCommandContext context,
            IEnumerable<CommandInfo> commands) : base(interactive, context, commands)
        {
        }

        /// <inheritdoc/>
        protected override Embed ConstructEmbed(CommandInfo cmd)
        {
            var embed = new EmbedBuilder();

            if (cmd.Aliases.Count > 1)
                embed.AddField("Aliases", string.Join(", ", cmd.Aliases.Where(name => name != cmd.Name)));

            string commandName = (!string.IsNullOrEmpty(cmd.Module.Group) ? $"{cmd.Module.Group} " : "") + cmd.Name;

            embed.WithTitle($"{commandName} {string.Join(' ', cmd.Parameters.Select(x => $"[{x.Name}]"))}")
                .WithDescription($"Module [{cmd.Module.Name}]")
                .AddField("Description", cmd.Summary ?? "No description for this command yet!")
                .AddField("Parameters", cmd.Parameters.Count > 0 ? string.Join("\n", cmd.Parameters.Select(param => $"`{param.Name}`{(param.IsOptional ? " [Optional]" : "")} - {param.Summary ?? "No description for this parameter yet!"}")) : "This command does not have any parameters")
                .WithFooter($"{page + 1} / {TotalPages}")
                .WithColor(TeaEssentials.MainColor);

            if (cmd.Attributes.FirstOrDefault(attr => attr is NoteAttribute) is NoteAttribute notes)
                embed.AddField("Note", notes.Content);

            return embed.Build();
        }
    }
}
