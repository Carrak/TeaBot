using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using TeaBot.Attributes;
using TeaBot.Commands;
using TeaBot.Services;
using TeaBot.TypeReaders;
using TeaBot.Utilities;

namespace TeaBot.Main
{
    public class MessageHandler
    {
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        private readonly IServiceProvider _services;
        private readonly DatabaseService _database;
        private readonly SupportService _support;

        public void BlockReceivingMessages() => _client.MessageReceived -= HandleMessagesAsync;
        public void EnableReceivingMessages() => _client.MessageReceived += HandleMessagesAsync;

        public MessageHandler(IServiceProvider services, CommandService commands, DiscordSocketClient client, DatabaseService database, SupportService support)
        {
            _client = client;
            _commands = commands;
            _services = services;
            _database = database;
            _support = support;

            _client.MessageReceived += HandleMessagesAsync;
            _commands.CommandExecuted += HandleCommandExecuted;
        }

        /// <summary>
        ///     Installs command modules and commands themselves.
        /// </summary>
        public async Task InstallCommandsAsync()
        {
            _commands.AddTypeReader<IEmote>(new IEmoteTypeReader());
            _commands.AddTypeReader<Color>(new ColorTypeReader());

            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

            //foreach(var cmd in _commands.Commands.Where(x => !x.Preconditions.Any(attr => attr is RatelimitAttribute)))
            //    Console.WriteLine($"{cmd.Module.Name} // {cmd.Name} has no cooldown set.");
        }

        /// <summary>
        ///     Determines the behaviour when a message is received.
        /// </summary>
        /// <param name="arg">The received message.</param>
        private async Task HandleMessagesAsync(SocketMessage arg)
        {
            // Return if the message is from a bot
            if (!(arg is SocketUserMessage message) || message.Author.IsBot)
                return;

            var channel = message.Channel as SocketGuildChannel;
            var guild = channel?.Guild;

            // Return if bot can't reply
            if (guild != null && (!guild.CurrentUser.GuildPermissions.SendMessages || !guild.CurrentUser.GetPermissions(channel).SendMessages))
                return;

            string prefix = _database.GetPrefix(guild?.Id);
            var disabledModules = _database.GetDisabledModules(guild?.Id);

            var context = new TeaCommandContext(_client, message, prefix, disabledModules);

            await _database.InsertValuesIntoDb(context);

            // Autoresponse for bot mention
            if (message.Content.Replace("!", "") == _client.CurrentUser.Mention.Replace("!", "") && !context.IsPrivate)
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
                Logger.Log("Executed", $"{context.User} executed the {commandInfo.Value.Name} command in " +
                    $"{(context.Guild is null ? "DM" : $"{context.Guild.Name} in channel #{context.Channel.Name}")} ({result.IsSuccess})\n" +
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

                    string toSend = $"{result.ErrorReason}\n\nUsage: `{context.Prefix}{_support.GetCommandHeader(command)}`";

                    if (command.Attributes.Where(x => x is NoteAttribute).FirstOrDefault() is NoteAttribute notes)
                    {
                        toSend += $"\nNote: {notes.Content}";
                    }

                    toSend += $"\nFor more information refer to `{context.Prefix}help {(!string.IsNullOrEmpty(command.Module.Group) ? $"{command.Module.Group} " : "")}{command.Name}`";

                    await context.Channel.SendMessageAsync(toSend);
                    break;
                case CommandError.Exception:
                    var executeResult = (result as ExecuteResult?).Value;

                    // Send the message, but don't await it
                    _ = Task.Run(async () => await context.Channel.SendMessageAsync("An exception occured while executing this command! Contact Carrak#8088 if this keeps happening."));

                    var embed = new EmbedBuilder();

                    // Environment descriptors
                    List<(string, string, ulong?)> descriptors = new List<(string, string, ulong?)>()
                    {
                        ("Guild", context.IsPrivate ? "DM" : context.Guild.ToString(), context.Guild?.Id ),
                        ("Channel", context.IsPrivate ? "DM" : $"<#{context.Channel.Id}>", context.Channel.Id),
                        ("User", context.User.Mention, context.User.Id)
                    };

                    // Footer with the user
                    var footer = new EmbedFooterBuilder()
                    {
                        Text = context.User.ToString(),
                        IconUrl = context.User.GetAvatarUrl()
                    };

                    // Exception stack trace
                    StackTrace st = new StackTrace(executeResult.Exception, true);

                    // The log with all non-empty frames
                    System.Text.StringBuilder log = new System.Text.StringBuilder();

                    // Get frames where the line is specified
                    for (int i = 0; i < st.FrameCount; i++)
                    {
                        StackFrame sf = st.GetFrame(i);
                        int line = sf.GetFileLineNumber();

                        if (line == 0)
                            continue;

                        log.Append($"In {sf.GetFileName()}\nAt line {line}\n");
                    }

                    embed.WithColor(Color.Red)
                        .WithTitle($"Command exception")
                        .WithAuthor(_client.CurrentUser)
                        .WithDescription(log.ToString())
                        .WithFooter(footer)
                        .AddField(executeResult.Exception.GetType().Name, executeResult.Exception.Message.Substring(0, Math.Min(executeResult.Exception.Message.Length, 1024)));

                    // Add all inner exceptions
                    var currentException = executeResult.Exception.InnerException;
                    while (currentException != null)
                    {
                        embed.AddField(currentException.GetType().Name, currentException.Message.Substring(0, Math.Min(currentException.Message.Length, 1024)));
                        currentException = currentException.InnerException;
                    }

                    List<string> splitExceptionData = new List<string>();

                    // Add exception data (and split it into groups to fit it into multiple fields
                    List<string> temp = new List<string>();
                    string splitter = "\n";
                    foreach (var data in executeResult.Exception.Data.Cast<System.Collections.DictionaryEntry>())
                    {
                        string toAdd = $"{data.Key}: {data.Value}";

                        if (toAdd.Length > 1024)
                            continue;

                        if (temp.Sum(x => x.Length) + toAdd.Length + (temp.Count - 1) * splitter.Length < 1024)
                            temp.Add(toAdd);
                        else
                        {
                            splitExceptionData.Add(string.Join(splitter, temp));
                            temp.Clear();
                            temp.Add(toAdd);
                        }
                    }

                    // Add the remains (if any are present)
                    if (temp.Count > 0)
                        splitExceptionData.Add(string.Join(splitter, temp));

                    // Add a field for each exception data fraction
                    for (int i = 0; i < splitExceptionData.Count; i++)
                        embed.AddField($"Exception data {i + 1}", splitExceptionData[i]);

                    // Message info
                    embed.AddField("Message content", context.Message.Content, true)
                        .AddField("Message URL", $"[Take me to the message!]({context.Message.GetJumpUrl()})", true);

                    // if the exception occured in a guild, add permissions info
                    if (!context.IsPrivate)
                    {
                        embed.AddField("Guild permissions", context.IsPrivate ? "DM" : PermissionUtilities.MainGuildPermissionsString(context.Guild.CurrentUser.GuildPermissions))
                        .AddField("Channel permissions", context.IsPrivate ? "DM" : PermissionUtilities.MainChannelPermissionsString(context.Guild.CurrentUser.GetPermissions(context.Channel as IGuildChannel)));
                    }

                    // Exception environment
                    embed.AddField("Descriptor", string.Join("\n", descriptors.Select(x => x.Item1)), true)
                        .AddField("Content", string.Join("\n", descriptors.Select(x => x.Item2)), true)
                        .AddField("ID", string.Join("\n", descriptors.Select(x => x.Item3 is null ? "-" : x.Item3.Value.ToString())), true);

                    List<string> splitStacktrace = new List<string>();
                    for (int index = 0; index < executeResult.Exception.StackTrace.Length; index += 1994)
                        splitStacktrace.Add(executeResult.Exception.StackTrace.Substring(index, Math.Min(1994, executeResult.Exception.StackTrace.Length - index)));

                    // Send the logs to the channel
                    if (_client.GetChannel(TeaEssentials.LogChannelId) is ITextChannel logChannel)
                    {
                        try
                        {
                            foreach (string stacktrace in splitStacktrace)
                                await logChannel.SendMessageAsync($"```{stacktrace}```");
                            await logChannel.SendMessageAsync(embed: embed.Build());
                        }
                        catch (HttpException) { }
                    }


                    break;
                case CommandError.UnknownCommand:
                    return;
                default:
                    if (string.IsNullOrEmpty(result.ErrorReason))
                        return;
                    else
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
