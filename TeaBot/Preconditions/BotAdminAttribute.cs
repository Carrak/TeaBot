﻿using System;
using System.Threading.Tasks;
using Discord.Commands;
using Npgsql;

namespace TeaBot.Attributes
{
    /// <summary>
    ///     Marks commands or modules can only be executed by users whose IDs are stored in the database in the table "botadmins"
    /// </summary>
    public class BotAdminAttribute : PreconditionAttribute
    {
        public override async Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            string query = "SELECT userid FROM botadmins";
            await using var cmd = new NpgsqlCommand(query, TeaEssentials.DbConnection);
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                ulong userid = (ulong)reader.GetInt64(0);
                if (userid == context.User.Id)
                    return PreconditionResult.FromSuccess();
            }

            return PreconditionResult.FromError("You need to be a bot admin to use this command!");

        }
    }
}
