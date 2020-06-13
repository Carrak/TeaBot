using System.Linq;
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
        CommandService _commands;

        public Admin(CommandService commands)
        {
            _commands = commands;
        }

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

        [Command("disablemodule")]
        [Summary("Prevents a specific module's commands from being executed on the guild.")]
        [RequireContext(ContextType.Guild)]
        [Note("Some modules are marked as essential.")]
        public async Task DisableModule(string moduleName)
        {
            moduleName = moduleName.ToLower();
            var module = _commands.Modules.FirstOrDefault(x => x.Name.ToLower() == moduleName);

            if (module is null)
            {
                await ReplyAsync($"Such module does not exist! `{moduleName}`");
                return;
            }

            if (module.Attributes.Any(attr => attr is EssentialModuleAttribute))
            {
                await ReplyAsync($"This module is essential! `{moduleName}`");
                return;
            }

            string conditionQuery = $"SELECT EXISTS (SELECT * FROM disabled_modules WHERE guildid={Context.Guild.Id} AND module_name='{moduleName}')";
            await using var conditionCmd = new NpgsqlCommand(conditionQuery, TeaEssentials.DbConnection);
            await using var conditionReader = await conditionCmd.ExecuteReaderAsync();
            await conditionReader.ReadAsync();
            if (conditionReader.GetBoolean(0))
            {
                await ReplyAsync("The module is already disabled.");
                return;
            }
            await conditionReader.CloseAsync();

            string query = $"INSERT INTO disabled_modules (guildid, module_name) VALUES ({Context.Guild.Id}, '{moduleName}')";
            await using var cmd = new NpgsqlCommand(query, TeaEssentials.DbConnection);
            await cmd.ExecuteNonQueryAsync();

            await ReplyAsync($"Successfully disabled module `{moduleName}`");
        }

        [Command("enablemodule")]
        [Summary("Enables a module back in case it was disabled in the guild.")]
        public async Task EnableModule(string moduleName)
        {
            moduleName = moduleName.ToLower();
            if (!_commands.Modules.Any(x => x.Name.ToLower() == moduleName))
            {
                await ReplyAsync($"Such module does not exist! `{moduleName}`");
                return;
            }

            string query = $"DELETE FROM disabled_modules WHERE guildid={Context.Guild.Id} AND module_name='{moduleName}'";
            await using var cmd = new NpgsqlCommand(query, TeaEssentials.DbConnection);
            int rowsAffected = await cmd.ExecuteNonQueryAsync();
            if (rowsAffected == 0)
            {
                await ReplyAsync($"This module is not disabled. `{moduleName}`");
            } 
            else
            {
                await ReplyAsync($"Successfully enabled this module back. `{moduleName}`");
            }
        }
    }
}
