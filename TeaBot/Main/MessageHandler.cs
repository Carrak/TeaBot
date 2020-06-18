using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using TeaBot.Attributes;
using TeaBot.Commands;
using TeaBot.Services;
using TeaBot.TypeReaders;

namespace TeaBot.Main
{
    public class MessageHandler
    {
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        private readonly IServiceProvider _services;
        private readonly DatabaseService _database;

        public MessageHandler(IServiceProvider services, CommandService commands, DiscordSocketClient client, DatabaseService database)
        {
            _client = client;
            _commands = commands;
            _services = services;
            _database = database;
        }

        /// <summary>
        ///     Registers events and command modules.
        /// </summary>
        public async Task InitAsync()
        {
            _client.MessageReceived += HandleMessagesAsync;
            _commands.CommandExecuted += HandleCommandExecuted;
            _commands.AddTypeReader<IEmote>(new IEmoteTypeReader());
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
        }

        /// <summary>
        ///     Determines the behaviour when a message is received.
        /// </summary>
        /// <param name="arg">The received message.</param>
        private async Task HandleMessagesAsync(SocketMessage arg)
        {
            if (!(arg is SocketUserMessage message) || message.Author.IsBot) return;

            var guild = (message.Channel as SocketGuildChannel)?.Guild;
            string prefix = await _database.GetPrefixAsync(guild);
            var disabledModules = await _database.GetDisabledModules(guild);
            var context = new TeaCommandContext(_client, message, prefix, disabledModules);

            await _database.InsertValuesIntoDb(context);

            // Autoresponse for bot mention
            if (message.Content.Replace("!", "") == _client.CurrentUser.Mention.Replace("!", ""))
            {
                await context.Channel.SendMessageAsync($"My prefix is `{prefix}`\nDo `{prefix}prefix [new prefix]` to change it!\nFor more information, refer to `{prefix}help prefix`");
                return;
            }

            int argPosition = 0;
            if (message.HasStringPrefix(prefix, ref argPosition, StringComparison.OrdinalIgnoreCase))
            {
                var result = await _commands.ExecuteAsync(context, argPosition, _services, MultiMatchHandling.Best);
                if (!result.IsSuccess)
                    await HandleErrorsAsync(context, result, argPosition);
            }
            else
                await HandleNonCommandsAsync(context);
        }

        /// <summary>
        ///     Determines the behaviour when a command is executed
        /// </summary>
        private Task HandleCommandExecuted(Optional<CommandInfo> commandInfo, ICommandContext context, IResult result)
        {
            if (commandInfo.IsSpecified)
                Console.WriteLine($"{DateTime.Now:HH:mm:ss} Executed    {context.User} executed the {commandInfo.Value.Name} command in " +
                    $"{(context.Guild is null ? "DM" : $"{context.Guild.Name} in channel #{context.Channel.Name}")} ({result.IsSuccess})" +
                    $"{(result.IsSuccess ? "" : $"\nCommand execution error: {result.ErrorReason}")}");
            return Task.CompletedTask;
        }

        /// <summary>
        ///     Determines the behaviour when a command fails to execute.
        /// </summary>
        private async Task HandleErrorsAsync(TeaCommandContext context, IResult result, int argPosition)
        {
            switch (result.Error)
            {
                case CommandError.BadArgCount:
                case CommandError.ParseFailed:
                    var command = _commands.Search(context, argPosition).Commands[0].Command;

                    string toSend = $"Usage: `{context.Prefix}{command.Name}{(command.Parameters.Count > 0 ? $" [{string.Join("] [", command.Parameters)}]" : "")}`";

                    if (command.Attributes.Where(x => x is NoteAttribute).FirstOrDefault() is NoteAttribute notes)
                    {
                        toSend += $"\nNote: {notes.Content}";
                    }

                    toSend += $"\nFor more information refer to `{context.Prefix}help {command.Name}`";

                    await context.Channel.SendMessageAsync(toSend);
                    break;
                case CommandError.Exception:
                    Console.WriteLine(result.ErrorReason);
                    _ = context.Channel.SendMessageAsync("An exception occured while executing this command! Please contact Carrak#8088 if this keeps happening.");

                    string directory = TeaEssentials.ProjectDirectory + Path.DirectorySeparatorChar + "Exceptions";
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    string filePath = $@"{directory}{Path.DirectorySeparatorChar}exception_{DateTime.Now:dd-MM-yyyy_HH-mm-ss}.txt";

                    string[] lines = {
                        $" Command:       {context.Message.Content}",
                        $" User:          {context.Message.Author}",
                        $" Place (guild): { (context.IsPrivate ? "Direct messages" : $"{context.Guild}") }",
                        $" Channel:       { (context.IsPrivate ? "Direct messages" : $"#{context.Channel}") }",
                        $" Time:          {DateTime.Now:HH:mm:ss dd MMMM, yyyy}",
                        $" Exception:     {result.ErrorReason}"
                    };

                    File.WriteAllLines(filePath, lines);
                    Console.WriteLine($"Exception saved at {filePath}");
                    break;
                case var _ when result.ErrorReason == "":
                case CommandError.UnknownCommand:
                    return;
                default:
                    await context.Channel.SendMessageAsync($"{result.ErrorReason}");
                    break;
            }
        }

        /// <summary>
        ///     Determines the behaviour when a message passed to <see cref="HandleMessagesAsync(SocketMessage)"/> is not a command
        /// </summary>
        private async Task HandleNonCommandsAsync(SocketCommandContext context)
        {
            if (context.IsPrivate && context.Message.Author.Id != 180648791291068417)
            {
                await MessageOwner(context.Message);
                return;
            }

            string[,] endsWithAutoresponses =
            {
                { "бах", "Композитор, кстати (c) DarvinBet#4371" },
                { "баха", "Композитор, кстати (c) DarvinBet#4371" }
            };

            string[,] containsAutoresponses =
            {
                { "who asked", "it is i, the person who asked" },
                { "whoever asked", "it is i, the person who asked" }
            };

            string message = context.Message.Content.ToLower();

            for (int i = 0; i < containsAutoresponses.GetLength(0); i++)
            {
                if (message.Contains(containsAutoresponses[i, 0]))
                {
                    await context.Channel.SendMessageAsync(containsAutoresponses[i, 1]);
                    break;
                }
            }

            for (int i = 0; i < endsWithAutoresponses.GetLength(0); i++)
            {
                if (message.EndsWith(endsWithAutoresponses[i, 0]))
                {
                    await context.Channel.SendMessageAsync(endsWithAutoresponses[i, 1]);
                    break;
                }
            }
        }


        /// <summary>
        ///     Sends the message to the bot owner.
        /// </summary>
        /// <param name="message">Message to send.</param>
        public async Task MessageOwner(SocketUserMessage message)
        {
            var user = (await _client.GetApplicationInfoAsync()).Owner;

            var embed = new EmbedBuilder();
            embed.WithAuthor(message.Author)
                .WithColor(TeaEssentials.MainColor)
                .WithDescription(message.Content)
                .WithFooter($"User ID - {message.Author.Id}");

            if (message.Attachments.Count > 0)
                embed.WithImageUrl(message.Attachments.ElementAt(0).Url);
            else if (message.Embeds.Count > 0)
                embed.WithImageUrl(message.Embeds.ElementAt(0).Url);

            await user.SendMessageAsync(embed: embed.Build());
        }

    }
}
