using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Npgsql;
using TeaBot.Attributes;
using TeaBot.Commands;
using TeaBot.Main;

namespace TeaBot.Modules
{
    [RequireUserPermission(GuildPermission.Administrator, ErrorMessage = "This command requires **Administrator** permissions!")]
    [RequireContext(ContextType.Guild, ErrorMessage = "")]
    [Summary("Commands that can only be executed by server admins")]
    public class Admin : TeaInteractiveBase
    {
        [Command("prefix")]
        [Summary("Change the prefix of the bot for this server")]
        [Note("If you want a space in the prefix, cover it with `\"` from both sides")]
        public async Task Prefix(string newPrefix)
        {
            newPrefix = newPrefix.TrimStart();

            if (newPrefix == "")
            {
                await ReplyAsync("Can't set any empty prefix!");
                return;
            }

            if (newPrefix.Length > 10)
            {
                await ReplyAsync("The prefix should not be over 10 symbols!");
                return;
            }

            string query = $"UPDATE guilds SET prefix={(newPrefix == "tea " ? "NULL" : $"'{newPrefix}'")} WHERE id={Context.Guild.Id}";
            NpgsqlCommand cmd = new NpgsqlCommand(query, TeaEssentials.DbConnection);
            await cmd.ExecuteNonQueryAsync();

            await ReplyAsync($"Successfully changed prefix to `{newPrefix}`");
        }
    }
}
