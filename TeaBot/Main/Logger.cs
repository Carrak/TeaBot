using System;

namespace TeaBot.Main
{
    static class Logger
    {
        public static void Log(string typeOfAction, string message) => Console.WriteLine($"{DateTime.Now:HH:mm:ss} {typeOfAction}    {message}");
    }
}
