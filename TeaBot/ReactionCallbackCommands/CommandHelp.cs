using System;
using System.Collections.Generic;
using System.Linq;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using TeaBot.Attributes;

namespace TeaBot.ReactionCallbackCommands
{
    /// <summary>
    ///     Class for paging the command help
    /// </summary>
    public class CommandHelp : PagedMessageBase
    {
        private readonly IReadOnlyList<CommandMatch> _commands;

        public CommandHelp(InteractiveService interactive,
            SocketCommandContext context,
            IReadOnlyList<CommandMatch> commands,
            RunMode runmode = RunMode.Async,
            TimeSpan? timeout = null,
            ICriterion<SocketReaction> criterion = null) : base(interactive, context, runmode, timeout, criterion)
        {
            _commands = commands;
            SetTotalPages(_commands.Count);
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
                .WithColor(Tea.MainColor)
                .WithFooter($"Page {page + 1} / {TotalPages}");

            if (cmd.Attributes.Where(x => x is NoteAttribute).FirstOrDefault() is NoteAttribute notes)
                embed.AddField("Note", notes.Content);

            return embed.Build();
        }
    }
}
