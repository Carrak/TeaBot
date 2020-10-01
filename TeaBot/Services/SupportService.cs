using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using TeaBot.Main;
namespace TeaBot.Services
{
    public class SupportService
    {
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;

        public SupportService(DiscordSocketClient client, CommandService commands)
        {
            _client = client;
            _commands = commands;
        }

        public string FormatCommandForHelp(ModuleInfo module, CommandInfo command) => $"`{GetFullCommandName(command)}{(module.Commands.Count(x => x.Name == command.Name) > 1 ? "*" : "")}`";

        /// <summary>
        ///     Constructs a predetermined embed which has the primary information about the bot.
        /// </summary>
        /// <param name="prefix">Prefix used in the embed</param>
        /// <returns>Info embed</returns>
        public async Task<Embed> GetInfoEmbedAsync(string prefix)
        {
            var embed = new EmbedBuilder();

            var owner = (await _client.Rest.GetApplicationInfoAsync()).Owner;

            EmbedFooterBuilder infoFooter = new EmbedFooterBuilder()
            {
                Text = $"{owner} | Contact me for suggestions or bug reports!",
                IconUrl = owner.GetAvatarUrl()
            };

            embed.AddField("Getting started", $"Use `{prefix}help` for the list of command modules and more info.")
                .AddField("Invite the bot!", "[Click me to invite!](https://discordapp.com/oauth2/authorize?client_id=689177733464457275&scope=bot&permissions=8)")
                .WithTitle("I am TeaBot!")
                .WithDescription("TeaBot is a bot created for various handy features, fun commands, math, anime art search and detailed server statistics.")
                .WithCurrentTimestamp()
                .WithColor(TeaEssentials.MainColor)
                .WithFooter(infoFooter);

            return embed.Build();
        }
