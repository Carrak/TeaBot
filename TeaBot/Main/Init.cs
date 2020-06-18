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

namespace TeaBot.Main
{
    class Init
    {
        static void Main() => new Init().RunBotAsync().GetAwaiter().GetResult();

        // Discord.NET essentials.
        private DiscordSocketClient _client;
        private CommandService _commands;
        private IServiceProvider _services;

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
                ExclusiveBulkDelete = true
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
                .BuildServiceProvider();

            // Set services
            _database = _services.GetRequiredService<DatabaseService>();
            _tea = _services.GetRequiredService<TeaService>();

            // Init the message handler
            await _services.GetRequiredService<MessageHandler>().InitAsync();

            // Register events
            _client.Log += Log;
            _client.JoinedGuild += OnJoin;
            _client.Ready += OnStart;

            // Retrieve the token and the pgsql db connection string
            JObject config = JObject.Parse(File.ReadAllText($"{TeaEssentials.ProjectDirectory}teabotconfig.json"));

            // Retrieve connection string and init db connection
            await _database.InitDbConnectionAsync(config["connection"].ToString());
            // Retrieve token
            string token = config["token"].ToString();

            // Login and start
            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            // Make sure it doesn't die
            await Task.Delay(-1);
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
            {
                await _database.InsertValuesIntoDb(guild.Id);
                string prefix = await _database.GetPrefixAsync(guild);
                var embed = await _tea.GetInfoEmbedAsync(prefix);
                await guild.SystemChannel.SendMessageAsync(embed: embed);
            }
        }

    }
}
