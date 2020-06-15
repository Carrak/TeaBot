using System;
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
            if (context.Guild is null)
                return PreconditionResult.FromSuccess();

            string moduleName = command.Module.Name.ToLower();

            string query = $"SELECT EXISTS (SELECT * FROM disabled_modules WHERE guildid={context.Guild.Id} AND module_name='{moduleName}')";
            await using var cmd = new NpgsqlCommand(query, TeaEssentials.DbConnection);
            await using var reader = await cmd.ExecuteReaderAsync();

            await reader.ReadAsync();
            var moduleDisabled = reader.GetBoolean(0);
            await reader.CloseAsync();
            return moduleDisabled ?
                PreconditionResult.FromError("This command's module is disabled in this guild!") :
                PreconditionResult.FromSuccess();
        }
    }
}
