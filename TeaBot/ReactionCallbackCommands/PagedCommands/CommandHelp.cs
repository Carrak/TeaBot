using System.Collections.Generic;
using System.Linq;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using TeaBot.Attributes;
using TeaBot.Commands;
using TeaBot.Main;
using TeaBot.Preconditions;
using TeaBot.ReactionCallbackCommands.PagedCommands.Base;
using TeaBot.Services;

namespace TeaBot.ReactionCallbackCommands.PagedCommands
{
    /// <summary>
    ///     Class for paging the command help.
    /// </summary>
    class CommandHelp : SingleItemPagedMessage<CommandInfo>
    {
        private readonly string _prefix;
        private readonly SupportService _tea;

        public CommandHelp(InteractiveService interactive,
            SupportService tea,
            TeaCommandContext context,
            IEnumerable<CommandInfo> commands) : base(interactive, context, commands)
        {
            _prefix = context.Prefix;
            _tea = tea;
        }

        /// <inheritdoc/>
        protected override Embed ConstructEmbed(CommandInfo cmd)
        {
            var embed = new EmbedBuilder();

            if (cmd.Aliases.Count > 1)
                embed.AddField("Aliases", string.Join(", ", cmd.Aliases.Where(name => name != cmd.Name)));

            embed.WithTitle($"{_prefix}{_tea.GetCommandHeader(cmd)}")
                .WithDescription($"Module [{cmd.Module.Name}]")
                .AddField("Description", cmd.Summary?.Replace("{prefix}", _prefix) ?? "No description for this command yet!")
                .AddField("Parameters",
                cmd.Parameters.Count > 0 ?
                string.Join("\n\n", cmd.Parameters.Where(x => !x.Type.IsEnum).Select((param, index) =>
                $"**{index + 1}.** `{param.Name}` {(param.IsOptional ? " [Optional]" : "")}\n{param.Summary?.Replace("{prefix}", _prefix) ?? "No description for this parameter yet!"}")) :
                $"This command does not have any parameters. Its usage would be `{_prefix}{_tea.GetFullCommandName(cmd)}` without any additional parameters.")
                .AddField("Cooldown", (cmd.Preconditions.FirstOrDefault(x => x is RatelimitAttribute) as RatelimitAttribute)?.InvokeLimitPeriod.TotalSeconds.ToString() + " seconds" ?? "-")
                .WithFooter($"{page + 1} / {TotalPages}")
                .WithColor(TeaEssentials.MainColor);

            if (cmd.Attributes.FirstOrDefault(attr => attr is NoteAttribute) is NoteAttribute notes)
                embed.AddField("Note", notes.Content.Replace("{prefix}", _prefix));

            return embed.Build();
        }
    }
}
