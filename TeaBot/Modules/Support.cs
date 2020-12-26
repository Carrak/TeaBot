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

        [Command("botstats")]
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
              $"`{prefix}help [command]` - information about a command and its arguments/parameters.\n" +
              $"`{prefix}help [module]` - information about a module and its commands.\n" +
              $"`{prefix}commands` - full command list");

            var modules = GetDisplayableModules();

            if (Context.Channel is ITextChannel channel && !channel.IsNsfw)
            {
                modules = modules.Where(x => !x.Preconditions.Any(attribute => attribute is NSFWAttribute));
                embed.WithFooter("NSFW modules are hidden in non-NSFW channels.");
            }

            string modulesString = string.Join("\n",
                modules.Select(module => $"`[{module.Name}]` - {module?.Summary.Split("\n")[0] ?? "No summary for this module!"}"));

            embed.AddField("Current modules", modulesString);

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

                bool essential = module.Attributes.Any(x => x is EssentialModuleAttribute);

                embed.WithTitle($"{module.Name} commands")
                    .WithDescription(
                    $"{module.Summary}\n\n" +
                    $"This module **{(essential ? "is" : "is not")}** essential. It {(essential ? "cannot" : "can")} be disabled.\n" +
                    $"`*` notation means there's more than one command with a given name\n")
                    .WithColor(TeaEssentials.MainColor)
                    .AddField(module.Name, string.Join("\n", FormatModuleCommands(module)));

                foreach (var submodule in module.Submodules)
                    embed.AddField(submodule.Name, string.Join("\n", FormatModuleCommands(submodule)));

                await ReplyAsync(embed: embed.Build());
            }
        }

        [Command("info", true)]
        [Summary("Information about the bot")]
        [Ratelimit(3)]
        public async Task Info()
        {
            var embed = await _support.GetInfoEmbedAsync(Context.Guild);
            await ReplyAsync(embed: embed);
        }

        [Command("commands")]
        [Summary("Replies with all commands that the bot has")]
        [Ratelimit(3)]
        public async Task AllCommands()
        {
            var embed = new EmbedBuilder();
            var modules = GetDisplayableModules().OrderByDescending(x => _support.GetModuleTree(x).Select(module => module.Commands.Count).Sum());

            IEnumerable<EmbedFieldBuilder> fields2 = new List<EmbedFieldBuilder>();
            foreach (var module in modules.Where(x => !x.IsSubmodule))
            {
                var localModules = _support.GetModuleTree(module);
            }

            var fields = modules.Where(x => !x.IsSubmodule).Select(module => new EmbedFieldBuilder
            {
                Name = module.Name,
                Value = string.Join("\n", FormatModuleCommands(module, true)),
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

        [Command("invite")]
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
        private IEnumerable<ModuleInfo> GetDisplayableModules() => _commandService.Modules.Where(module => !module.Attributes.Any(attribute => attribute is HelpCommandIgnoreAttribute) && !Context.DisabledModules.Contains(module.Name.ToLower()) && !module.IsSubmodule);
    }
}
