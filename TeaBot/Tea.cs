using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using Npgsql;
using TeaBot.Attributes;

namespace TeaBot
{
    class Tea
    {
        static void Main() => new Tea().RunBotAsync().GetAwaiter().GetResult();

        // Discord.NET essentials.
        private static DiscordSocketClient _client;
        private static CommandService _commands;
        private static IServiceProvider _services;

        /// <summary>
        ///     Standard and default prefix used for recognizing commands.
        /// </summary>
        private const string DefaultPrefix = "tea ";

        /// <summary>
        ///     The directory of the TeaBot project used for saving exceptions.
        /// </summary>
        public static string ProjectDirectory { get; } = Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName + @"\";

        /// <summary>
        ///     The main color used for embeds across the entire bot.
        /// </summary>
        public static Color MainColor { get; } = Color.Green;

        /// <summary>
        ///     HttpClient instance that is used across the bot.
        /// </summary>
        public static HttpClient HttpClient;

        /// <summary>
        ///     The connection to the PostgreSQL database.
        /// </summary>
        public static NpgsqlConnection DbConnection { get; private set; }

        /// <summary>
        ///    Establishes the database connection, instantiates the Discord client, commands and services, registers a few events and starts the bot. 
        /// </summary>
        public async Task RunBotAsync()
        {

            // Instantiate the essentials
            _client = new DiscordSocketClient(new DiscordSocketConfig()
            {
                AlwaysDownloadUsers = true
            });
            _commands = new CommandService(new CommandServiceConfig()
            {
                CaseSensitiveCommands = false,
            });
            _services = new ServiceCollection()
                .AddSingleton(_client)
                .AddSingleton<InteractiveService>()
                .BuildServiceProvider();

            JObject config = JObject.Parse(File.ReadAllText($"{ProjectDirectory}teabotconfig.json"));

            //string token = Environment.GetEnvironmentVariable("TeaBotToken", EnvironmentVariableTarget.Machine);
            string token = config["token"].ToString();

            // Initialize the connection to the database
            var connectionString = config["connection"].ToString();
            DbConnection = new NpgsqlConnection(connectionString);
            await DbConnection.OpenAsync();

            HttpClient = new HttpClient();

            _client.Log += Log;

            await RegisterEventsAndModulesAsync();

            await _client.LoginAsync(TokenType.Bot, token);

            await _client.StartAsync();

            _client.Ready += OnStart;

            // Make sure it doesn't die
            await Task.Delay(-1);
        }

        /// <summary>
        ///     Changes the bot's status once <see cref="_client"/> fires <see cref="DiscordSocketClient.Ready"/>.
        /// </summary>
        private async Task OnStart()
        {
            await _client.SetGameAsync("Fate || tea info", type: ActivityType.Watching);
        }

        /// <summary>
        ///     Sends the embed created by <see cref="GetInfoEmbed(string)"/> to the system channel of the joined guild if it is present.
        /// </summary>
        private async Task OnJoin(SocketGuild guild)
        {
            if (guild.SystemChannel != null)
                await guild.SystemChannel.SendMessageAsync(embed: GetInfoEmbed(await GetPrefixAsync(guild.Id)));
        }

        /// <summary>
        ///     Registers a few events and command modules.
        /// </summary>
        private async Task RegisterEventsAndModulesAsync()
        {
            _client.JoinedGuild += OnJoin;
            _client.MessageReceived += HandleCommandsAsync;
            _commands.CommandExecuted += HandleCommandExecuted;
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);
        }

        private Task HandleCommandExecuted(Optional<CommandInfo> commandInfo, ICommandContext context, IResult result)
        {
            if (commandInfo.IsSpecified)
                Console.WriteLine($"{DateTime.Now:HH:mm:ss} Executed    {context.User} executed the {commandInfo.Value.Name} command in {(context.Guild is null ? "DM" : $"{context.Guild.Name} in channel #{context.Channel.Name}")}");
            return Task.CompletedTask;
        }

        private Task Log(LogMessage arg)
        {
            Console.WriteLine(arg);
            return Task.CompletedTask;
        }

        /// <summary>
        ///     Determines the behaviour when a message is received.
        /// </summary>
        /// <param name="arg">The received message.</param>
        private async Task HandleCommandsAsync(SocketMessage arg)
        {
            if (!(arg is SocketUserMessage message) || message.Author.IsBot) return;

            var context = new SocketCommandContext(_client, message);

            string prefix = await GetPrefixAsync(context);

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
        ///     Determines the behaviour when a command fails to execute.
        /// </summary>
        private async Task HandleErrorsAsync(SocketCommandContext context, IResult result, int argPosition)
        {
            switch (result.Error)
            {
                case CommandError.BadArgCount:
                case CommandError.ParseFailed:
                    var command = _commands.Search(context, argPosition).Commands[0].Command;
                    //Console.WriteLine(Directory.GetParent(Environment.CurrentDirectory).Parent.FullName);
                    string prefix = await GetPrefixAsync(context);

                    string toSend = $"Usage: `{prefix}{command.Name}{(command.Parameters.Count > 0 ? $" [{string.Join("] [", command.Parameters)}]" : "")}`";

                    if (command.Attributes.Where(x => x is NoteAttribute).FirstOrDefault() is NoteAttribute notes)
                    {
                        toSend += $"\nNote: {notes.Content}";
                    }

                    toSend += $"\nFor more information refer to `{prefix}help {command.Name}`";

                    await context.Channel.SendMessageAsync(toSend);
                    break;
                case CommandError.UnknownCommand:
                    return;
                case CommandError.Exception:
                    Console.WriteLine(result.ErrorReason);
                    _ = context.Channel.SendMessageAsync("An exception occured while executing this command! Please contact Carrak#8088 if this keeps happening.");

                    string directory = ProjectDirectory + @"\Exceptions";
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    string filePath = $@"{directory}\exception_{DateTime.Now:dd-MM-yyyy_HH-mm-ss}.txt";

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
                    break;
                default:
                    await context.Channel.SendMessageAsync($"{result.ErrorReason}");

                    break;
            }
        }

        /// <summary>
        ///     Determines the behaviour when a message passed to <see cref="HandleCommandsAsync(SocketMessage)"/> is not a command
        /// </summary>
        private async Task HandleNonCommandsAsync(SocketCommandContext context)
        {
            if (context.IsPrivate && context.Message.Author.Id != 180648791291068417)
            {
                await MessageOwner(context.Message);
                return;
            }

            Dictionary<string, string> endsWithAutoresponses = new Dictionary<string, string>
            {
                { "бах", "Композитор, кстати (c) DarvinBet#4371" },
                { "баха", "Композитор, кстати (c) DarvinBet#4371" }
            };

            Dictionary<string, string> containsAutoresponses = new Dictionary<string, string>
            {
                { "who asked", "it is i, the person who asked" },
                { "whoever asked", "it is i, the person who asked" },
            };

            string message = context.Message.Content.ToLower();

            foreach (string key in containsAutoresponses.Keys)
            {
                if (message.Contains(key))
                {
                    await context.Channel.SendMessageAsync(containsAutoresponses.GetValueOrDefault(key));
                    return;
                }
            }

            foreach (string key in endsWithAutoresponses.Keys)
            {
                if (message.EndsWith(key))
                {
                    await context.Channel.SendMessageAsync(endsWithAutoresponses.GetValueOrDefault(key));
                    return;
                }
            }
        }

        /// <summary>
        ///     Sends the message to the bot owner.
        /// </summary>
        /// <param name="message">Message to send.</param>
        private async Task MessageOwner(SocketUserMessage message)
        {
            var user = (await _client.GetApplicationInfoAsync()).Owner;
            string toSend = $"`{message.Author} ({message.Author.Id})`: {message.Content.Replace("`", "")}";

            if (message.Attachments.Count > 0)
            {
                toSend += $"\n\n`Attachments:` \n{string.Join("\n", message.Attachments.Select(x => x.Url))}";
            }

            await user.SendMessageAsync(toSend);
        }

        /// <summary>
        /// Constructs a predetermined embed which has the primary information about the bot.
        /// </summary>
        /// <param name="prefix">Prefix used in the embed</param>
        /// <returns>Info embed</returns>
        public static Embed GetInfoEmbed(string prefix)
        {
            var embed = new EmbedBuilder();

            EmbedFooterBuilder infoFooter = new EmbedFooterBuilder();
            infoFooter.WithIconUrl(_client.GetUser(180648791291068417).GetAvatarUrl())
                .WithText("Carrak#8088 | Contact me for suggestions or bug reports!");

            embed.AddField("Getting started", $"Use `{prefix}help` for the list of command modules and more info.")
                .AddField("Invite the bot!", "[Click me to invite!](https://discordapp.com/oauth2/authorize?client_id=689177733464457275&scope=bot&permissions=8)")
                .WithTitle("I am TeaBot!")
                .WithDescription("TeaBot is a bot created for various handy features, fun commands, math, anime art search and detailed server statistics. The bot is pretty new, so it's still in active development. Expect the said handy features to come soon!")
                .WithCurrentTimestamp()
                .WithColor(Tea.MainColor)
                .WithFooter(infoFooter);

            return embed.Build();
        }

        /// <summary>
        ///     Sends a query to the PostgreSQL database to add the user and the guild if they're not already present and retrieve the prefix for a given guild.
        /// </summary>
        /// <param name="context">Command context used for retrieving the guild.</param>
        /// <returns>Prefix for the guild in the provided context or <see cref="DefaultPrefix"/> if the context is null.</returns>
        public static async Task<string> GetPrefixAsync(SocketCommandContext context = null)
        {
            if (context is null || context.IsPrivate)
            {
                return DefaultPrefix;
            }
            else
            {
                ulong uid = context.User.Id;
                ulong gid = context.Guild.Id;

                string query =
                    "DO $$ BEGIN " +
                    $"PERFORM conditional_insert('guilds', 'guilds.id = {gid}', 'id', '{gid}'); " +
                    $"PERFORM conditional_insert('guildusers', 'guildusers.userid = {uid} AND guildusers.guildid = {gid}', 'userid, guildid', '{uid}, {gid}'); " +
                    $"UPDATE guildusers SET (channelid, messageid, last_message_timestamp) = ({context.Channel.Id}, {context.Message.Id}, now()::timestamp) " +
                    $"WHERE userid = {uid} AND guildid = {gid}; " +
                    "END $$ LANGUAGE plpgsql; " +
                    $"SELECT prefix FROM guilds WHERE id={context.Guild.Id};";

                return await ExecutePrefixQuery(query);
            }
        }

        /// <summary>
        ///     Sends a query to the PostgreSQL database to add the guild if it's not already present and retrieve the prefix for the given guild.
        /// </summary>
        /// <param name="guildId">ID of the guild to retrieve the prefix for.</param>
        /// <returns>Prefix for the guild.</returns>
        private static async Task<string> GetPrefixAsync(ulong guildId)
        {
            string query =
                    "DO $$ BEGIN " +
                    $"PERFORM conditional_insert('guilds', 'guilds.id = {guildId}', 'id', '{guildId}'); " +
                    "END $$ LANGUAGE plpgsql; " +
                    $"SELECT prefix FROM guilds WHERE id={guildId};";

            return await ExecutePrefixQuery(query);
        }

        /// <summary>
        ///     Executes the provided query to retrieve the prefix.
        /// </summary>
        /// <param name="query">Query to execute.</param>
        /// <returns>String containing the prefix.</returns>
        private static async Task<string> ExecutePrefixQuery(string query)
        {
            await using var cmd = new NpgsqlCommand(query, DbConnection);
            await using var reader = await cmd.ExecuteReaderAsync();

            await reader.ReadAsync();

            if (!reader.IsDBNull(0))
            {
                string prefix = reader.GetString(0);
                reader.Close();
                return prefix;
            }
            else
                return DefaultPrefix;
        }
    }
}
