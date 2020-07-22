using System;
using Discord;

namespace TeaBot.Main
{
    static class Logger
    {
        public static void Log(string source, string message) => Console.WriteLine(new LogMessage(LogSeverity.Info, source, message));
    }
}
