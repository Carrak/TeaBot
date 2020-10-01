using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using TeaBot.Attributes;
using TeaBot.Commands;
using TeaBot.Main;
using TeaBot.Preconditions;
using TeaBot.ReactionCallbackCommands.PagedCommands;
using TeaBot.Services;

namespace TeaBot.Modules
{
    //[HelpCommandIgnore]
    [EssentialModule]
    [Summary("Commands that are meant to guide users through the bot")]
    public class Support : TeaInteractiveBase
    {
        private readonly CommandService _commandService;
        private readonly SupportService _support;

        public Support(CommandService commandService, SupportService support)
        {
            _commandService = commandService;
            _support = support;
        }

        [Command("stats")]
        [Summary("Information about the bot.")]
        public async Task Stats()
        {
            var embed = new EmbedBuilder();

            var client = Context.Client;
            var uptime = System.DateTime.UtcNow - TeaEssentials.BotStarted;

            embed.WithAuthor(client.CurrentUser)
                .WithColor(Color.Blue)
                .AddField("Guilds", client.Guilds.Count, true)
                .AddField("Users", client.Guilds.Sum(x => x.Users.Count), true)
                .AddField("Uptime", $"{uptime.Days}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s");

            await ReplyAsync(embed: embed.Build());
        }

        [Command("help")]
        [Summary("Do `{prefix}help help` for description")]
        [Ratelimit(3)]
        public async Task HelpGeneral()
        {
            var embed = new EmbedBuilder();

            var footer = new EmbedFooterBuilder();
            footer.WithIconUrl(Context.Message.Author.GetAvatarUrl())
                .WithText(Context.Message.Author.ToString());

            string prefix = Context.Prefix;

            embed.WithCurrentTimestamp()
              .WithColor(TeaEssentials.MainColor)
              .WithTitle("Help / Commands")
              .WithDescription("Basics of the guiding through the bot")
              .AddField("Basic help commands",
              $"Use `{prefix}help [command]` to get information about a command and its arguments/parameters. If you can't figure out how to use a command or what it needs, this is exactly what you're looking for.\n" +
              $"You can also use `{prefix}help [module]` to get the description and commands of a module\n" +
              $"For the full list of all commands the bot has, use `{prefix}commands`")
              /*
              .AddField("Command parameters", $"A command parameter is what a command requires to work. For example, the `{prefix}avatar` command needs the user to be specified. User is a parameter here.\n" +
              $"For command parameters such as users, channels and roles you can use their IDs, mentions or names (for users it's both their nickname on the server and username). Make sure to surround names that contain spaces with `\"`, else it won't work.\n" +
              $"Examples:\n" +
              $"{prefix}avatar {Context.User.Username}{(Context.User.Username.Contains(' ') ? "\n`Your name contains a space, so this will not work! Instead refer to the example below.`" : "")}\n" +
              $"{prefix}avatar \"{Context.User.Username}\"\n" +
              $"{prefix}avatar {Context.User.Id}\n" +
              $"{prefix}avatar {Context.User.Mention}\n" +
              $"All of these serve the same purpose and do the same thing.")
              */
              .WithFooter(footer);

            string modules = string.Join("\n",
                GetDisplayableModules().Select(module => $"`[{module.Name}]` - {module?.Summary.Split("\n")[0] ?? "No summary for this module!"}"));

            embed.AddField("Current modules", modules);

            await ReplyAsync(embed: embed.Build());
        }

        [Command("help")]
        [Summary("Get help on a specific command or module")]
        [Ratelimit(3)]
        public async Task HelpCommandModule(
            [Summary("The name of the module or the command to get help on.")] [Remainder] string name
            )
        {
            var result = _commandService.Search(name);

            if (result.IsSuccess &&
                (result.Commands.Where(x => !x.Command.Module.Attributes.Any(attr => attr is HelpCommandIgnoreAttribute)) is IEnumerable<CommandMatch> commands) &&
                commands.Any())
            {
                var commandHelp = new CommandHelp(Interactive, _support, Context, commands.Select(cm => cm.Command));
                await commandHelp.DisplayAsync();
            }
            else
            {
                var module = _commandService.Modules.FirstOrDefault(x => x.Name.ToLower() == name.ToLower() && !x.Attributes.Any(attr => attr is HelpCommandIgnoreAttribute));

                if (module is null)
                {
                    await ReplyAsync($"Such command/module does not exist! `{name}`");
                    return;
                }

                var embed = new EmbedBuilder();

                var footer = new EmbedFooterBuilder();
                footer.WithIconUrl(Context.Message.Author.GetAvatarUrl())
                        .WithText(Context.Message.Author.ToString());

                string moduleCommands = ModuleCommandsString(module);

                embed.WithTitle($"{module.Name} commands")
                    .WithDescription($"{module.Summary}\n\n`*` notation means there's more than one command with a given name")
                    .WithColor(TeaEssentials.MainColor)
                    .WithCurrentTimestamp()
                    .WithFooter(footer)
                    .AddField("Commands", moduleCommands)
                    .AddField("Essential module (cannot be disabled)", module.Attributes.Any(attribute => attribute is EssentialModuleAttribute));

                await ReplyAsync(embed: embed.Build());

            }
        }

        [Command("info", true)]
        [Summary("Information about the bot")]
        [Ratelimit(3)]
        public async Task Info()
        {
            var embed = await _support.GetInfoEmbedAsync(Context.Prefix);
            await ReplyAsync(embed: embed);
        }

        [Command("commands", true)]
        [Summary("Replies with all commands that the bot has")]
        [Ratelimit(3)]
        public async Task AllCommands()
        {
            var embed = new EmbedBuilder();
            var modules = GetDisplayableModules().OrderByDescending(x => x.Commands.Sum(cmd => cmd.Name.Length));

            var fields = modules.Select(module => new EmbedFieldBuilder
            {
                Name = module.Name,
                Value = ModuleCommandsString(module),
                IsInline = true
            });

            var footer = new EmbedFooterBuilder();
            footer.WithIconUrl(Context.Message.Author.GetAvatarUrl())
                .WithText(Context.Message.Author.ToString());

            embed.WithFields(fields)
                .WithTitle("TeaBot commands")
                .WithDescription($"This is a list of all commands categorized by their respective modules\n`*` notation means there's more than one command with a given name\nDo `{Context.Prefix}help [command]` for more information about a command.")
                .WithCurrentTimestamp()
                .WithFooter(footer)
                .WithColor(TeaEssentials.MainColor);

            await ReplyAsync(embed: embed.Build());
        }

        [Command("invite", true)]
        [Summary("A message that contains the bot invite link")]
        [Ratelimit(3)]
        public async Task Invite() => await ReplyAsync("Invite me to your server!\n<https://discordapp.com/oauth2/authorize?client_id=689177733464457275&scope=bot&permissions=8>");

        private IEnumerable<string> FormatModuleCommands(ModuleInfo module, bool includeSubmodules = false)
        {
            if (includeSubmodules)
            {
                List<string> commands = new List<string>();
                var modules = _support.GetModuleTree(module);
                foreach (var currModule in modules)
                    foreach (var command in currModule.Commands)
                        commands.Add(_support.FormatCommandForHelp(currModule, command));

                return commands.Distinct();
            }
            else
                return module.Commands.Select(command => _support.FormatCommandForHelp(module, command)).Distinct();
        }

        /// <summary>
        ///     Sorts out modules that are meant to be ignored or that are disabled in the guild.
        /// </summary>
        /// <returns>Collection of modules</returns>
        private IEnumerable<ModuleInfo> GetDisplayableModules() => _commandService.Modules.Where(module => !module.Attributes.Any(attribute => attribute is HelpCommandIgnoreAttribute) && !Context.DisabledModules.Contains(module.Name.ToLower()));
    }
}
