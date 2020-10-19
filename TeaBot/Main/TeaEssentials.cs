using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using Discord;

namespace TeaBot.Main
{
    public static class TeaEssentials
    {
        /// <summary>
        ///     Standard and default prefix used for recognizing commands.
        /// </summary>
        public const string DefaultPrefix = "tea ";

        /// <summary>
        ///     The directory of the TeaBot project.
        /// </summary>
        public static string ProjectDirectory { get; } = Directory.GetParent(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)).Parent.Parent.FullName + Path.DirectorySeparatorChar;

        /// <summary>
        ///     The main color used for embeds across the entire bot.
        /// </summary>
        public static Color MainColor { get; } = Color.Green;

        /// <summary>
        ///     The ID of the channel for exception logging.
        /// </summary>
        public static ulong LogChannelId = 726427607788421132;

        /// <summary>
        ///     HttpClient instance that is used across the bot.
        /// </summary>
        public static HttpClient HttpClient { get; private set; } = new HttpClient();

        /// <summary>
        ///     Represents the time the bot was started at (for measuring uptime).
        /// </summary>
        public static DateTime BotStarted;
    }
}
