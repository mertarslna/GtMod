using System;

namespace GtProxyMod.Core
{
    public static class Logger
    {
        private static readonly object _lock = new();

        public static void Log(string message, ConsoleColor color = ConsoleColor.Gray)
        {
            lock (_lock)
            {
                var time = DateTime.Now.ToString("HH:mm:ss.fff");
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($"[{time}] ");
                Console.ForegroundColor = color;
                Console.WriteLine(message);
                Console.ResetColor();
            }
        }

        public static void Info(string message) => Log($"[INFO] {message}", ConsoleColor.Cyan);
        public static void Success(string message) => Log($"[SUCCESS] {message}", ConsoleColor.Green);
        public static void Warning(string message) => Log($"[WARNING] {message}", ConsoleColor.Yellow);
        public static void Error(string message) => Log($"[ERROR] {message}", ConsoleColor.Red);

        public static void Client(string message) => Log($"[CLIENT -> PROXY] {message}", ConsoleColor.Magenta);
        public static void Server(string message) => Log($"[SERVER -> PROXY] {message}", ConsoleColor.Blue);
        public static void Command(string message) => Log($"[MOD COMMAND] {message}", ConsoleColor.DarkYellow);
        public static void Proxy(string message) => Log($"[PROXY] {message}", ConsoleColor.White);
    }
}
