using System;
using System.IO;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using TeaBot.Utilities;

namespace TeaBot.Main
{
    class Init
    {
        static void Main() => new Init().RunBotAsync().GetAwaiter().GetResult();

        // Discord.NET essentials.
        private DiscordSocketClient _client;
        private CommandService _commands;
        private IServiceProvider _services;

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

            _database = new DatabaseService(_commands);
            _tea = new TeaService(_client);

            // Create a message handler
            var messageHandler = new MessageHandler(_services, _commands, _client, _database);
            await messageHandler.InitAsync();

            // Register events
            _client.Log += Log;
            _client.JoinedGuild += OnJoin;
            _client.Ready += OnStart;

            // Retrieve the token and the pgsql db connection string
            JObject config = JObject.Parse(File.ReadAllText($"{TeaEssentials.ProjectDirectory}teabotconfig.json"));
            await TeaEssentials.InitDbConnectionAsync(config["connection"].ToString());
            string token = config["token"].ToString();

            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            // Make sure it doesn't die
            await Task.Delay(-1);
        }

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
