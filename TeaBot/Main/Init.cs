using System;
using System.IO;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using TeaBot.Services;
using TeaBot.Services.ReactionRole;
using TeaBot.Webservices.Rule34;

namespace TeaBot.Main
{
    class Init
    {
        static void Main() => new Init().RunBotAsync().GetAwaiter().GetResult();

        // Discord.NET essentials.
        private static DiscordSocketClient _client;
        private static CommandService _commands;
        private static IServiceProvider _services;

        // Services
        private DatabaseService _database;
        private TeaService _tea;

        /// <summary>
        ///    Establishes the database connection, instantiates the Discord client, commands and services, registers a few events and starts the bot. 
        /// </summary>
        public async Task RunBotAsync()
        {
            // Instantiate the essentials
            _client = new DiscordSocketClient(new DiscordSocketConfig()
            {
                AlwaysDownloadUsers = true,
            });
            _commands = new CommandService(new CommandServiceConfig()
            {
                CaseSensitiveCommands = false,
            });
            _services = new ServiceCollection()
                .AddSingleton(_client)
                .AddSingleton(_commands)
                .AddSingleton<InteractiveService>()
                .AddSingleton<MessageHandler>()
                .AddSingleton<DatabaseService>()
                .AddSingleton<TeaService>()
                .AddSingleton<Rule34BlacklistService>()
                .AddSingleton<ReactionRoleService>()
                .BuildServiceProvider();

            // Set services
            _database = _services.GetRequiredService<DatabaseService>();
            _tea = _services.GetRequiredService<TeaService>();

            // Register events
            _client.Log += Log;
            _client.JoinedGuild += OnJoin;
            _client.Ready += Ready;

            // Retrieve the token and the pgsql db connection string
            JObject config = JObject.Parse(File.ReadAllText($"{TeaEssentials.ProjectDirectory}teabotconfig.json"));

            // Retrieve connection string and init db connection
            Logger.Log("Database", "Connecting to database");
            await _database.InitAsync(config["connection"].ToString());

            // Retrieve token
            string token = config["token"].ToString();

            // Init services
            Logger.Log("Services", "Initializing Rule34BlacklistService");
            await _services.GetRequiredService<Rule34BlacklistService>().InitDefaultBlacklistAsync();
            Logger.Log("Services", "Initialized Rule34BlacklistService");
            Logger.Log("Services", "Initializing MessageHandler");
            await _services.GetRequiredService<MessageHandler>().InitAsync();
            Logger.Log("Services", "Initialized MessageHandler");

            // Login and start
            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            // Start the uptime stopwatch
            TeaEssentials.BotStarted = DateTime.UtcNow;

            await _client.SetGameAsync("Fate || tea info", type: ActivityType.Watching);

            // Make sure it doesn't die
            await Task.Delay(-1);
        }

        /// <summary>
        ///     Changes the bot's status once <see cref="_client"/> fires <see cref="DiscordSocketClient.Ready"/>.
        /// </summary>
        private async Task Ready()
        {
            Logger.Log("Services", "Initializing ReactionRoleService");
            await _services.GetRequiredService<ReactionRoleService>().InitCallbacksAsync();
            Logger.Log("Services", "Initialized ReactionRoleService");
        }

        /// <summary>
        ///     Logs client information.
        /// </summary>
        /// <param name="arg">The message to log</param>
        private Task Log(LogMessage arg)
        {
            Console.WriteLine(arg);
            return Task.CompletedTask;
        }

        /// <summary>
        ///     Sends the embed created by <see cref="GetInfoEmbed(string)"/> to the system channel of the joined guild if it is present.
        /// </summary>
        private async Task OnJoin(SocketGuild guild)
        {
            if (guild.SystemChannel != null)
            {
                string prefix = await _database.GetOrAddPrefixAsync(guild.Id);
                var embed = await _tea.GetInfoEmbedAsync(prefix);
                await guild.SystemChannel.SendMessageAsync(embed: embed);
            }
        }
    }
}
