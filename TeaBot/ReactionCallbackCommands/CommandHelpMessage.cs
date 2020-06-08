using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using TeaBot.Attributes;

namespace TeaBot.ReactionCallback
{
    class CommandHelpMessage : IReactionCallback
    {
        public RunMode RunMode { get; }
        public ICriterion<SocketReaction> Criterion { get; }
        public TimeSpan? Timeout { get; }
        public SocketCommandContext Context { get; }
        public InteractiveService Interactive { get; }

        private IUserMessage _message;
        private IReadOnlyList<CommandMatch> _commands;

        private static readonly Emoji arrowForward = new Emoji("▶️");
        private static readonly Emoji arrowBackward = new Emoji("◀️");

        private int page = 0;

        public CommandHelpMessage(InteractiveService interactive,
            RunMode runmode,
            ICriterion<SocketReaction> criterion,
            TimeSpan timeout,
            SocketCommandContext context,
            IReadOnlyList<CommandMatch> commands)
        {
            Interactive = interactive;
            RunMode = runmode;
            Criterion = criterion;
            Timeout = timeout;
            Context = context;
            _commands = commands;
        }

        private Embed ConstructEmbed()
        {
            var cmd = _commands.ElementAt(page).Command;
            string arguments = (cmd.Parameters.Count == 0) ? "This command does not take any arguments" : $"{string.Join(", ", cmd.Parameters.Select(p => $"{p.Name}"))}";
            string description = cmd.Summary ?? "No description for this command yet!";
            var embed = new EmbedBuilder();

            /*
            // Embed variant 1
            embed.WithTitle(string.Join(", ", cmd.Aliases) + $" [{arguments}]")
                .WithDescription($"`Module [{cmd.Module.Name}]` \n {summary}")
                .WithCurrentTimestamp()
                .WithColor(Color.Green)
                .WithFooter($"Command {page + 1} / {_commands.Count}");
            */

            // Embed variant two
            if (cmd.Aliases.Count > 1)
                embed.AddField("Aliases", string.Join(", ", cmd.Aliases.Where(name => name != cmd.Name)));

            if (_commands.Count > 1)
                embed.WithFooter($"{page + 1} / {_commands.Count}");

            string parameters = cmd.Parameters.Count > 0 ? $"[{string.Join("] [", cmd.Parameters)}]" : "";
            embed.WithTitle($"{cmd.Name} {(cmd.Parameters.Count > 0 ? $"[{string.Join("] [", cmd.Parameters)}]" : "")}")
                .WithDescription($"Module [{cmd.Module.Name}]")
                //.AddField("Parameters", arguments)
                .AddField("Description", description)
                //.AddField("Aliases", string.Join(", ", cmd.Aliases.Where(name => name != cmd.Name)))
                .WithColor(Color.Green);

            NoteAttribute notes = cmd.Attributes.Where(x => x is NoteAttribute).FirstOrDefault() as NoteAttribute;
            if (notes != null)
            {
                embed.AddField("Note", notes.Content);
            }

            return embed.Build();
        }

        public async Task DisplayAsync()
        {
            var embed = ConstructEmbed();
            _message = await Context.Channel.SendMessageAsync(embed: embed);

            if (_commands.Count > 1)
            {
                _ = Task.Run(async () => await _message.AddReactionsAsync(new Emoji[] { arrowBackward, arrowForward }));

                Interactive.AddReactionCallback(_message, this);

                _ = Task.Delay(Timeout.Value).ContinueWith(_ =>
                {
                    Interactive.RemoveReactionCallback(_message);
                });
            }

        }

        public async Task<bool> HandleCallbackAsync(SocketReaction reaction)
        {
            //if (reaction.User.Value == _message.Author) return false;
            if (reaction.Emote.Equals(arrowForward))
            {
                if (page < _commands.Count - 1) page++;
                else return false;
            }
            else if (reaction.Emote.Equals(arrowBackward))
            {
                if (page > 0) page--;
                else return false;
            }
            else return false;

            var embed = ConstructEmbed();

            await _message.ModifyAsync(x => x.Embed = embed);
            await _message.RemoveReactionAsync(reaction.Emote, reaction.User.Value);

            return false;
        }
    }
}
