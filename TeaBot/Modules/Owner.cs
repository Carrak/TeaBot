using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Npgsql;
using TeaBot.Attributes;
using TeaBot.Commands;
using TeaBot.Main;
using System.Linq;
using System.Text.RegularExpressions;

namespace TeaBot.Modules
{
    [HelpCommandIgnore]
    [RequireOwner]
    public class Owner : TeaInteractiveBase
    {
        public class Globals
        {
            public TeaCommandContext Context;
            public TeaInteractiveBase InteractiveBase;
        }

        [Command("eval", RunMode = RunMode.Async)]
        public async Task Eval([Remainder] string toEvaluate)
        {
            try
            {
                var globals = new Globals { Context = Context, InteractiveBase = this };
                var sopts = ScriptOptions.Default
                    .WithImports("System", "System.Linq", "Discord", "Discord.Commands", "TeaBot.Main", "TeaBot.Commands")
                    .WithReferences(AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))); ;
                var a = await CSharpScript.EvaluateAsync(toEvaluate, sopts, globals);
            }
            catch (CompilationErrorException e)
            {
                await ReplyAsync(string.Join(Environment.NewLine, e.Diagnostics));
            }

            //class Globals { }
        }

        [Command("sqlquery")]
        [Alias("sqlq")]
        public async Task SQLEval([Remainder] string query)
        {
            await using var cmd = new NpgsqlCommand(query, TeaEssentials.DbConnection);
            await using var reader = await cmd.ExecuteReaderAsync();

            List<List<object>> rows = new List<List<object>>();
            List<int> maxLengths = new List<int>(new int[reader.GetColumnSchema().Count]);

            while (await reader.ReadAsync())
            {
                List<object> row = new List<object>();

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    object value = reader.GetValue(i);
                    maxLengths[i] = Math.Max(maxLengths[i], value.ToString().Length);
                    row.Add(reader.IsDBNull(i) ? "null" : value);
                }

                rows.Add(row);
            }

            StringBuilder result = new StringBuilder("");
            for (int i = 0; i < maxLengths.Count; i++)
            {
                string name = reader.GetName(i);
                maxLengths[i] = Math.Max(name.Length, maxLengths[i]);
                result.Append(string.Format("|{0, -" + maxLengths[i] + "}", name));
            }
            result.Append("|\n");

            for (int i = 0; i < maxLengths.Count; i++)
            {
                result.Append("+");
                result.Append('-', maxLengths[i]);
            }
            result.Append("+\n");

            for (int i = 0; i < rows.Count; i++)
            {
                string toAdd = "";
                var row = rows[i];
                for (int j = 0; j < row.Count; j++)
                {
                    toAdd += string.Format("|{0, -" + maxLengths[j] + "}", row[j]);
                }
                toAdd += "|\n";
                result.Append(toAdd);
            }

            await reader.CloseAsync();
            await ReplyAsync($"`{result.Replace("\n", "`\n`")}`".TrimEnd('`'));
        }

        [Command("sqlnonquery")]
        [Alias("sqlnq")]
        public async Task SQlEvalNonQuery([Remainder] string query)
        {
            NpgsqlCommand cmd = new NpgsqlCommand(query, TeaEssentials.DbConnection);
            int rowsAffected = await cmd.ExecuteNonQueryAsync();
            await ReplyAsync($"Success! {rowsAffected} rows affected.");
        }

        [Command("consolewrite")]
        public Task ConsoleWrite([Remainder] string text)
        {
            Console.WriteLine(text);
            return Task.CompletedTask;
        }

    }
}
