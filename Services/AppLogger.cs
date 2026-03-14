using System;
using System.Diagnostics;
using System.IO;

namespace Woobly.Services
{
    public interface IAppLogger
    {
        void Info(string message);
        void Warn(string message);
        void Error(string message, Exception? ex = null);
    }

    public sealed class FileAppLogger : IAppLogger
    {
        private readonly string _logFilePath;

        public FileAppLogger(string? logFilePath = null)
        {
            var dataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Woobly"
            );
            Directory.CreateDirectory(dataFolder);
            _logFilePath = logFilePath ?? Path.Combine(dataFolder, "woobly.log");
        }

        public void Info(string message) => Write("INFO", message);

        public void Warn(string message) => Write("WARN", message);

        public void Error(string message, Exception? ex = null)
        {
            var full = ex == null ? message : $"{message} | {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}";
            Write("ERROR", full);
        }

        private void Write(string level, string message)
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}{Environment.NewLine}";
            try
            {
                File.AppendAllText(_logFilePath, line);
            }
            catch
            {
                // Logging must never crash the app.
            }

            try
            {
                Trace.WriteLine(line);
            }
            catch
            {
                // Ignore trace failures.
            }
        }
    }

    public static class AppLog
    {
        private static IAppLogger _logger = new FileAppLogger();

        public static IAppLogger Logger
        {
            get => _logger;
            set => _logger = value ?? new FileAppLogger();
        }

        public static void Info(string message) => _logger.Info(message);
        public static void Warn(string message) => _logger.Warn(message);
        public static void Error(string message, Exception? ex = null) => _logger.Error(message, ex);
    }
}
