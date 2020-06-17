using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Npgsql;

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
        public static string ProjectDirectory { get; } = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.FullName + Path.DirectorySeparatorChar;
        public static string ProjectDirectory2 { get; } = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        public static string ProjectDirectory3 { get; } = Directory.GetParent(@"../").FullName;

        /// <summary>
        ///     The main color used for embeds across the entire bot.
        /// </summary>
        public static Color MainColor { get; } = Color.Green;

        /// <summary>
        ///     HttpClient instance that is used across the bot.
        /// </summary>
        public static HttpClient HttpClient { get; private set; } = new HttpClient();

        /// <summary>
        ///     The connection to the PostgreSQL database.
        /// </summary>
        public static NpgsqlConnection DbConnection { get; private set; }

        public static async Task InitDbConnectionAsync(string connectionString)
        {
            // Initialize the connection to the database
            DbConnection = new NpgsqlConnection(connectionString);
            await DbConnection.OpenAsync();
        }
    }
}
