using System;
using System.IO;

namespace Shared.Logging
{
    public static class AuditLogger
    {
        private static readonly object _lock = new();

        public static string LogPath { get; set; } =
            Path.Combine(AppContext.BaseDirectory, "logs", "pracenje.txt");

        private static string Now() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        public static void Info(string line) => WriteLine(line);

        public static void Warn(string line) => WriteLine("WARN: " + line);

        public static void Error(string where, Exception ex)
        {
            WriteLine($"ERROR @ {where}: {ex.Message}");
        }

        private static void WriteLine(string line)
        {
            try
            {
                var dir = Path.GetDirectoryName(LogPath);
                if (!string.IsNullOrWhiteSpace(dir))
                    Directory.CreateDirectory(dir);

                lock (_lock)
                {
                    File.AppendAllText(LogPath, $"[{Now()}] {line}{Environment.NewLine}");
                }
            }
            catch
            {
                
            }
        }
    }
}
