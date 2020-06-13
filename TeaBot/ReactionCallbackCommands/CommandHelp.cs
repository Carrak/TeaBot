using System;
using System.Collections.Generic;
using System.Linq;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using TeaBot.Attributes;
using TeaBot.Main;

namespace TeaBot.ReactionCallbackCommands
{
    /// <summary>
    ///     Class for paging the command help
    /// </summary>
    public class CommandHelp : PagedMessageBase
    {
        private readonly IEnumerable<CommandMatch> _commands;

        public CommandHelp(InteractiveService interactive,
            SocketCommandContext context,
            IEnumerable<CommandMatch> commands,
            RunMode runmode = RunMode.Async,
            TimeSpan? timeout = null,
            ICriterion<SocketReaction> criterion = null) : base(interactive, context, runmode, timeout, criterion)
        {
            _commands = commands;
            SetTotalPages(_commands.Count());
        }

        protected override Embed ConstructEmbed()
        {
            var cmd = _commands.ElementAt(page).Command;

            var embed = new EmbedBuilder();

            if (cmd.Aliases.Count > 1)
                embed.AddField("Aliases", string.Join(", ", cmd.Aliases.Where(name => name != cmd.Name)));

            embed.WithTitle($"{cmd.Name} {(cmd.Parameters.Count > 0 ? $"[{string.Join("] [", cmd.Parameters)}]" : "")}")
                .WithDescription($"Module [{cmd.Module.Name}]")
                .AddField("Description", cmd.Summary ?? "No description for this command yet!")
                .WithColor(TeaEssentials.MainColor);

            embed = SetDefaultFooter(embed);

            if (cmd.Attributes.FirstOrDefault(attr => attr is NoteAttribute) is NoteAttribute notes)
                embed.AddField("Note", notes.Content);

            return embed.Build();
        }
    }
}
