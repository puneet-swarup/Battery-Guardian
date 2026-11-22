using System;
using System.IO;

namespace BatteryGuardian
{
    public static class DiagnosticLog
    {
        public static bool Enabled { get; set; } = false;

        private static readonly object _lock = new();
        private static readonly string _logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BatteryGuardian",
            "diagnostic.log");

        public static void Write(string message)
        {
            if (!Enabled) return;
            try
            {
                lock (_lock)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
                    File.AppendAllText(_logPath,
                        $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
                }
            }
            catch { }
        }

        /// <summary>
        /// Writes regardless of the Enabled setting. Used by paths that are
        /// otherwise invisible - a handful of lines per sleep or shutdown - so a
        /// failure can be diagnosed without asking the user to turn logging on
        /// first and reproduce it.
        /// </summary>
        public static void WriteAlways(string message)
        {
            try
            {
                lock (_lock)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
                    File.AppendAllText(_logPath,
                        $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
                }
            }
            catch { }
        }

        public static void WriteException(string context, Exception ex)
        {
            Write($"EXCEPTION in {context}: {ex.GetType().Name}: {ex.Message}");
        }

        public static void Clear()
        {
            try { if (File.Exists(_logPath)) File.Delete(_logPath); } catch { }
        }

        public static string GetLogPath() => _logPath;
    }
}