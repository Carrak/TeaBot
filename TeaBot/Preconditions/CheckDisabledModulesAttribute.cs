using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;
using Npgsql;
using TeaBot.Main;

namespace TeaBot.Preconditions
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    class CheckDisabledModulesAttribute : PreconditionAttribute
    {
        public async override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            string moduleName = command.Module.Name.ToLower();

            string query = $"SELECT EXISTS (SELECT * FROM disabled_modules WHERE guildid={context.Guild.Id} AND module_name='{moduleName}')";
            await using var cmd = new NpgsqlCommand(query, TeaEssentials.DbConnection);
            await using var reader = await cmd.ExecuteReaderAsync();

            await reader.ReadAsync();
            var moduleDisabled = reader.GetBoolean(0);
            await reader.CloseAsync();
            return moduleDisabled ?
                PreconditionResult.FromError("This module is disabled on this guild!") :
                PreconditionResult.FromSuccess();
        }
    }
}
