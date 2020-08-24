using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;
using Npgsql;
using TeaBot.Attributes;
using TeaBot.Commands;
using TeaBot.Services;
using Newtonsoft.Json.Linq;
using TeaBot.Webservices.Rule34;

namespace TeaBot.Modules
{
    [HelpCommandIgnore]
    [RequireOwner]
    public class Owner : TeaInteractiveBase
    {
        private readonly DatabaseService _database;
        private readonly Rule34BlacklistService _blacklists;

        public Owner(DatabaseService database, Rule34BlacklistService blacklists)
        {
            _database = database;
            _blacklists = blacklists;
        }

        public sealed class Globals
        {
            public TeaCommandContext Context;
        }

        [Command("reloaddefault")]
        public async Task ReloadDefault()
        {
            await _blacklists.InitDefaultBlacklistAsync();
            await ReplyAsync($"Reloaded default blacklist.");
        }

        [Command("logs")]
        public async Task Logs(int n)
        {
            ProcessStartInfo procStartInfo = new ProcessStartInfo("journalctl", $"-n {n} -o cat -u teabot.service")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            Process proc = new Process
            {
                StartInfo = procStartInfo
            };
            proc.Start();

            string result = proc.StandardOutput.ReadToEnd();

            await ReplyAsync(string.Join("\n", result.Split("\n").Where(x => !string.IsNullOrEmpty(x)).Select(x => x = $"`{x}`")));
        }

        [Command("eval", RunMode = RunMode.Async)]
        public async Task Eval([Remainder] string toEvaluate)
        {
            // Extract the code from the code block if it is present
            var code = Regex.Match(toEvaluate, @"(?s)(?<=```cs\n|```(?!cs)).*?(?=```)");
            if (!code.Success)
            {
                // If it isn't present, return
                await ReplyAsync("Wrap the code in a code block.");
                return;
            }

            // Create and send the initial embed
            var embed = new EmbedBuilder();
            embed.WithColor(Color.Gold)
                .WithDescription("Compiling...");
            var message = await ReplyAsync(embed: embed.Build());

            // Initialize the globals and script options
            var globals = new Globals
            {
                Context = Context
            };
            var sopts = ScriptOptions.Default
                .WithImports("System", "System.Linq", "Discord", "Discord.Commands", "TeaBot.Main", "TeaBot.Commands", "System.IO", "System.Reflection", "System.Threading.Tasks", "System.Threading")
                .WithReferences(AppDomain.CurrentDomain.GetAssemblies().Where(assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location)));

            // Compile
            var compilationTime = Stopwatch.StartNew();
            var script = CSharpScript.Create(code.Value, sopts, typeof(Globals));
            var diagnostics = script.Compile();
            compilationTime.Stop();

            // If compilation fails, return
            if (diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
            {
                embed = new EmbedBuilder();
                embed.WithDescription($"Compilation failed. `{compilationTime.ElapsedMilliseconds}ms`")
                    .WithColor(Color.Red);

                foreach (var diagnostic in diagnostics.Take(5))
                {
                    var ls = diagnostic.Location.GetLineSpan();
                    embed.AddField($"Error at line {ls.StartLinePosition.Line}", diagnostic.GetMessage());
                }

                await message.ModifyAsync(msg => msg.Embed = embed.Build());
                return;
            }

            // If compilation is success, continue
            embed = new EmbedBuilder();
            embed.WithColor(Color.Gold)
                .WithDescription($"Compiled. `{compilationTime.ElapsedMilliseconds}ms`\nRunning...");

            await message.ModifyAsync(msg => msg.Embed = embed.Build());

            // Executing
            Exception exception = null;
            var executionTime = Stopwatch.StartNew();
            try
            {
                await script.RunAsync(globals);
            }
            catch (Exception e)
            {
                exception = e;
            }
            executionTime.Stop();

            // If an exception was raised while executing, return
            if (exception != null)
            {
                embed = new EmbedBuilder();
                embed.WithColor(Color.Red)
                    .WithDescription($"Compiled. `{compilationTime.ElapsedMilliseconds}ms`\nRuntime error. `{executionTime.ElapsedMilliseconds}ms`")
                    .AddField(exception.GetType().ToString(), exception.Message);

                await message.ModifyAsync(msg => msg.Embed = embed.Build());
                return;
            }

            // If execution didn't raise any exception, evaluation succeeded
            embed = new EmbedBuilder();
            embed.WithColor(Color.Green)
                .WithDescription($"Compiled. `{compilationTime.ElapsedMilliseconds}ms`\nExecuted. `{executionTime.ElapsedMilliseconds}ms`");

            await message.ModifyAsync(msg => msg.Embed = embed.Build());
        }

        [Command("sqlquery")]
        [Alias("sqlq")]
        public async Task SQLEval([Remainder] string query)
        {
            await using var cmd = _database.GetCommand(query);
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
            NpgsqlCommand cmd = _database.GetCommand(query);
            int rowsAffected = await cmd.ExecuteNonQueryAsync();
            await ReplyAsync($"Success! {rowsAffected} rows affected.");
        }

        [Command("sqljson")]
        public async Task SqlJSON([Remainder] string query)
        {
            await using var cmd = _database.GetCommand(query);
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                JObject jobj = JObject.Parse(reader.GetString(0));
                Console.Write(jobj.ToString());
                await ReplyAsync($"```json\n{jobj}```");
            }
        }

        [Command("sqljsona")]
        public async Task SqlJSONA([Remainder] string query)
        {
            await using var cmd = _database.GetCommand(query);
            await using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                JArray jobj = JArray.Parse(reader.GetString(0));
                await ReplyAsync($"```json\n{jobj}```");
            }
        }
    }
}
