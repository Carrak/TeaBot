﻿using Discord;
using Discord.WebSocket;
using TeaBot.Main;

namespace TeaBot.Utilities
{
    public static class TeaUtilities
    {
        /// <summary>
        ///     Discord client to be used by utility methods.
        /// </summary>
        private static DiscordSocketClient _client;

        /// <summary>
        ///     Sets the client instance to use for methods in this class.
        /// </summary>
        /// <param name="client">The client to use.</param>
        public static void SetClient(DiscordSocketClient client)
        {
            _client = client;
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
                .WithDescription("TeaBot is a bot created for various handy features, fun commands, math, anime art search and detailed server statistics.")
                .WithCurrentTimestamp()
                .WithColor(TeaEssentials.MainColor)
                .WithFooter(infoFooter);

            return embed.Build();
        }
    }
}
