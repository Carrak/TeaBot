using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using TeaBot.Attributes;
using TeaBot.Commands;
using TeaBot.Main;
using TeaBot.Services;

namespace TeaBot.Modules
{
    [EssentialModule]
    [RequireUserPermission(GuildPermission.Administrator, ErrorMessage = "This command requires **Administrator** permissions!")]
    [RequireContext(ContextType.Guild, ErrorMessage = "")]
    [Summary("Commands that can only be executed by server admins")]
    public class Admin : TeaInteractiveBase
    {
        private readonly CommandService _commands;
        private readonly DatabaseService _database;

        public Admin(CommandService commands, DatabaseService database)
        {
            _commands = commands;
            _database = database;
        }

        [Command("prefix")]
        [Summary("Change the prefix of the bot for this server")]
        [Note("If you want a space in the prefix, cover it with `\"` from both sides")]
        public async Task Prefix(
            [Summary("The new prefix to set.")] string newPrefix
            )
        {
            newPrefix = newPrefix.TrimStart();

            if (newPrefix == "")
            {
                await ReplyAsync("Can't set any empty prefix!");
                return;
            }
            else if (newPrefix.Length > 10)
            {
                await ReplyAsync("The prefix should not be over 10 symbols!");
                return;
            }
            else if (newPrefix == Context.Prefix)
            {
                await ReplyAsync("That is the current prefix!");
                return;
            }

            await _database.ChangePrefix(Context.Guild.Id, newPrefix);

            await ReplyAsync($"Successfully changed prefix to `{newPrefix}`");
        }

        [Command("modules")]
        [Summary("Information about disabled modules in the guild.")]
        [RequireContext(ContextType.Guild)]
        public async Task Modules()
        {
            var embed = new EmbedBuilder();
            string disabledModulesString = string.Join(" ", Context.DisabledModules.Select(x => $"`{x}`"));

            embed.WithAuthor(Context.User)
                .WithColor(TeaEssentials.MainColor)
                .WithTitle($"Modules disabled in {Context.Guild.Name}")
                .WithDescription($"These modules' commands cannot be used in this guild and they also don't appear in `{Context.Prefix}help` and `{Context.Prefix}commands`")
                .AddField($"Disabled modules", string.IsNullOrEmpty(disabledModulesString) ? "None" : disabledModulesString)
                .WithFooter($"Do {Context.Prefix}enablemodule [module name] to enable a module back.");

            await ReplyAsync(embed: embed.Build());
        }

        [Command("disablemodule")]
        [Summary("Prevents a specific module's commands from being executed on the guild.")]
        [RequireContext(ContextType.Guild)]
        [Note("Some modules are marked as essential, and therefore cannot be disabled.")]
        public async Task DisableModule(
            [Summary("The module to disable.")] string moduleName
            )
        {
            moduleName = moduleName.ToLower();
            var module = _commands.Modules.FirstOrDefault(x => x.Name.ToLower() == moduleName);

            if (module is null)
            {
                await ReplyAsync($"Such module does not exist! `{moduleName}`");
                return;
            }
            else if (module.Attributes.Any(attr => attr is EssentialModuleAttribute))
            {
                await ReplyAsync($"This module is essential! `{moduleName}`");
                return;
            }
            else if (Context.DisabledModules.Contains(moduleName))
            {
                await ReplyAsync($"The module is already disabled! `{moduleName}`");
                return;
            }

            await _database.DisableModuleAsync(Context.Guild.Id, moduleName);

            await ReplyAsync($"Successfully disabled the module. `{moduleName}`");
        }

        [Command("enablemodule")]
        [Summary("Enables a module back in case it was disabled in the guild.")]
        public async Task EnableModule(
            [Summary("The module to enable back in case it was disabled.")] string moduleName
            )
        {
            moduleName = moduleName.ToLower();

            if (!_commands.Modules.Any(x => x.Name.ToLower() == moduleName))
            {
                await ReplyAsync($"Such module does not exist! `{moduleName}`");
                return;
            }
            else if (!Context.DisabledModules.Contains(moduleName))
            {
                await ReplyAsync($"This module is not disabled. `{moduleName}`");
                return;
            }

            await _database.EnableModuleAsync(Context.Guild.Id, moduleName);

            await ReplyAsync($"Successfully enabled the module back. `{moduleName}`");
        }
    }
}
