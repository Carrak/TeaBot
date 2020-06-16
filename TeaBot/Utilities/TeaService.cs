using Discord;
using Discord.WebSocket;
using TeaBot.Main;

namespace TeaBot.Utilities
{
    public class TeaService
    {
        /// <summary>
        ///     Discord client to be used by utility methods.
        /// </summary>
        private DiscordSocketClient _client;

        public TeaService(DiscordSocketClient client)
        {
            _client = client;
        }

        /// <summary>
        /// Constructs a predetermined embed which has the primary information about the bot.
        /// </summary>
        /// <param name="prefix">Prefix used in the embed</param>
        /// <returns>Info embed</returns>
        public Embed GetInfoEmbed(string prefix)
        {
            var embed = new EmbedBuilder();

            EmbedFooterBuilder infoFooter = new EmbedFooterBuilder();
            infoFooter.WithIconUrl(_client.GetUser(180648791291068417).GetAvatarUrl())
                .WithText("Carrak#8088 | Contact me for suggestions or bug reports!");

            embed.AddField("Getting started", $"Use `{prefix}help` for the list of command modules and more info.")
                .AddField("Invite the bot!", "[Click me to invite!](https://discordapp.com/oauth2/authorize?client_id=689177733464457275&scope=bot&permissions=8)")
                .WithTitle("I am TeaBot!")
                .WithDescription("TeaBot is a bot created for various handy features, fun commands, math, anime art search and detailed server statistics.")
                .WithCurrentTimestamp()
                .WithColor(TeaEssentials.MainColor)
                .WithFooter(infoFooter);

            return embed.Build();
        }
    }
}
