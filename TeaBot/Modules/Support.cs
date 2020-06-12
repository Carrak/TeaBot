using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using TeaBot.Attributes;
using TeaBot.Main;
using TeaBot.ReactionCallbackCommands;
using TeaBot.Commands;
using System.Collections.Generic;
using TeaBot.Utilities;

namespace TeaBot.Modules
{
    //[HelpCommandIgnore]
    [Summary("Commands that are meant to guide users through the bot")]
    public class Support : TeaInteractiveBase
    {
        readonly CommandService _commandService;

        public Support(CommandService commandService)
        {
            _commandService = commandService;
        }

        [Command("help")]
        [Summary("Do `tea help help` for description")]
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
              .AddField("Command parameters", $"A command parameter is what a command requires to work. For example, the `{prefix}avatar` command needs the user to be specified. User is a parameter here.\n" +
              $"For command parameters such as users, channels and roles you can use their IDs, mentions or names (for users it's both their nickname on the server and username). Make sure to surround names that contain spaces with `\"`, else it won't work.\n" +
              $"Examples:\n" +
              $"{prefix}avatar {Context.User.Username}{(Context.User.Username.Contains(' ') ? "\n`Your name contains a space, so this will not work! Instead refer to the example below.`" : "")}\n" +
              $"{prefix}avatar \"{Context.User.Username}\"\n" +
              $"{prefix}avatar {Context.User.Id}\n" +
              $"{prefix}avatar {Context.User.Mention}\n" +
              $"All of these serve the same purpose and do the same thing.")
              .WithFooter(footer);

            string modules = string.Join("\n",
                _commandService.Modules.Where(x => !x.Attributes.Any(attribute => attribute is HelpCommandIgnoreAttribute))
                .Select(module => $"`[{module.Name}]` - {module.Summary ?? "No summary for this module!"}"));

            embed.AddField("Current modules", modules);

            await ReplyAsync(embed: embed.Build());
        }

        [Command("help")]
        [Summary("Get help on a specific command or module")]
        public async Task HelpCommandModule([Remainder] string name)
        {
            var result = _commandService.Search(Context, name);

            if (result.IsSuccess && 
                (result.Commands.Where(x => !x.Command.Module.Attributes.Any(attr => attr is HelpCommandIgnoreAttribute)) is IEnumerable<CommandMatch> commands) && 
                commands.Count() > 0)
            {
                var commandHelp = new CommandHelp(Interactive, Context, commands);
                await commandHelp.DisplayAsync();
            }
            else
            {
                var module = _commandService.Modules.FirstOrDefault(x => x.Name.ToLower() == name.ToLower() && !x.Attributes.Any(attr => attr is HelpCommandIgnoreAttribute));

                if (module is null)
                {
                    await ReplyAsync($"Such command/module does not exist! `[{name}]`");
                    return;
                }

                var embed = new EmbedBuilder();

                var footer = new EmbedFooterBuilder();
                footer.WithIconUrl(Context.Message.Author.GetAvatarUrl())
                        .WithText(Context.Message.Author.ToString());

                string moduleCommands = string.Join(" ", module.Commands.Select(command => $"`{command.Name}`").Distinct());

                embed.WithTitle($"{module.Name} commands")
                    .WithDescription(module.Summary)
                    .WithColor(TeaEssentials.MainColor)
                    .WithCurrentTimestamp()
                    .WithFooter(footer)
                    .AddField("Commands", moduleCommands);

                await ReplyAsync(embed: embed.Build());

            }
        }

        [Command("info", true)]
        [Summary("Information about the bot")]
        public async Task Info()
        {
            var embed = TeaUtilities.GetInfoEmbed(await DatabaseUtilities.GetPrefixAsync(Context.Guild));
            await ReplyAsync(embed: embed);
        }

        [Command("commands", true)]
        [Summary("Replies with all commands that the bot has")]
        public async Task AllCommands()
        {
            var embed = new EmbedBuilder();
            var modules = _commandService.Modules.Where(module => !module.Attributes.Any(attribute => attribute is HelpCommandIgnoreAttribute));

            var fields = modules.Select(module => new EmbedFieldBuilder
            {
                Name = module.Name,
                Value = string.Join(" ", module.Commands.Select(command => $"`{command.Name}` ").Distinct())
            });

            var footer = new EmbedFooterBuilder();
            footer.WithIconUrl(Context.Message.Author.GetAvatarUrl())
                .WithText(Context.Message.Author.ToString());

            embed.WithFields(fields)
                .WithTitle("TeaBot commands")
                .WithDescription("This is a list of all commands categorized by their respective modules")
                .WithCurrentTimestamp()
                .WithFooter(footer)
                .WithColor(TeaEssentials.MainColor);

            await ReplyAsync(embed: embed.Build());
        }

        [Command("invite", true)]
        [Summary("A message that contains the bot invite link")]
        public async Task Invite()
        {
            await ReplyAsync("Invite me to your server!\n<https://discordapp.com/oauth2/authorize?client_id=689177733464457275&scope=bot&permissions=8>");
        }
    }
}
