using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.Net;
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
        private SupportService _support;

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
                .AddSingleton<SupportService>()
                .AddSingleton<Rule34BlacklistService>()
                .AddSingleton<ReactionRoleService>()
                .BuildServiceProvider();

            // Set services
            _database = _services.GetRequiredService<DatabaseService>();
            _support = _services.GetRequiredService<SupportService>();

            ReactionRoleServiceMessages.Init(_support);

            // Register events
            _client.Log += Log;
            _client.JoinedGuild += OnJoin;
            _client.Ready += Ready;

            // Retrieve the config
            JObject config = JObject.Parse(File.ReadAllText($"{TeaEssentials.ProjectDirectory}teabotconfig.json"));

            // Retrieve connection string and init db connection
            Logger.Log("Database", "Connecting to database");
            await _database.InitAsync(config["connection"].ToString());

            // Retrieve token
            string token = config["token"].ToString();

            // Init services
            await _services.GetRequiredService<Rule34BlacklistService>().InitDefaultBlacklistAsync();
            await _services.GetRequiredService<MessageHandler>().InstallCommandsAsync();

            // Login and start
            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            // Start the uptime stopwatch
            TeaEssentials.BotStarted = DateTime.UtcNow;

            // Update status
            await _client.SetGameAsync("Fate | tea help", type: ActivityType.Watching);

            // Make sure it doesn't die
            await Task.Delay(-1);
        }

        /// <summary>
        ///     Determines the behavior when the Ready event is fired.
        /// </summary>
        private async Task Ready()
        {
            _services.GetRequiredService<MessageHandler>().BlockReceivingMessages();

            await _services.GetRequiredService<ReactionRoleService>().InitCallbacksAndLimitsAsync();

            _services.GetRequiredService<MessageHandler>().EnableReceivingMessages();
        }

        /// <summary>
        ///     Logs client information.
        /// </summary>
        /// <param name="logMessage">The message to log</param>
        private async Task Log(LogMessage logMessage)
        {
            Console.WriteLine(logMessage);

            // Don't continue if there's no exception
            if (logMessage.Exception == null || logMessage.Exception.GetType() == typeof(Exception))
                return;

            var exception = logMessage.Exception;

            var embed = new EmbedBuilder();

            embed.WithAuthor(_client.CurrentUser)
                .WithTitle("Unhandled exception")
                .WithColor(Color.Red)
                .AddField(exception.GetType().Name, exception.Message);

            // Exception stack trace
            StackTrace st = new StackTrace(exception, true);

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

            // Add the frames
            embed.WithDescription(log.ToString());

            // Add all inner exceptions
            var currentException = exception.InnerException;
            while (currentException != null)
            {
                embed.AddField(currentException.GetType().Name, currentException.Message);
                currentException = currentException.InnerException;
            }


            List<string> splitExceptionData = new List<string>();

            // Add exception data (and split it into groups to fit it into multiple fields
            List<string> temp = new List<string>();
            string splitter = "\n";
            foreach (var data in exception.Data.Cast<System.Collections.DictionaryEntry>())
            {
                string toAdd = $"{data.Key}: {data.Value}";

                if (toAdd.Length > 1024)
                    continue;

                if (temp.Sum(x => x.Length) + toAdd.Length + (temp.Count - 1) * splitter.Length < 1024)
                {
                    temp.Add(toAdd);
                }
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

            // Split the stacktrace so it can be fit into multiple messages
            List<string> splitStacktrace = new List<string>();
            for (int index = 0; index < exception.StackTrace.Length; index += 1994)
                splitStacktrace.Add(exception.StackTrace.Substring(index, Math.Min(1994, exception.StackTrace.Length - index)));

            // Send the logs to the channel
            if (_client.GetChannel(TeaEssentials.LogChannelId) is ITextChannel logChannel)
            {
                try
                {
                    // Send the stacktrace
                    foreach (string stacktrace in splitStacktrace)
                        await logChannel.SendMessageAsync($"```{stacktrace}```");
                    // Send the exception info
                    await logChannel.SendMessageAsync(embed: embed.Build());
                }
                // Discard missing permissions
                catch (HttpException) { }
            }

        }

        /// <summary>
        ///     Sends the embed created by <see cref="SupportService.GetInfoEmbedAsync(IGuild)"/> to the system channel of the joined guild if it is present.
        /// </summary>
        private async Task OnJoin(SocketGuild guild)
        {
            if (guild.SystemChannel != null)
            {
                var embed = await _support.GetInfoEmbedAsync(guild);
                await guild.SystemChannel.SendMessageAsync(embed: embed);
            }
        }
    }
}
