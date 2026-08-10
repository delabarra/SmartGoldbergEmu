using System;
using System.IO;
using System.Text;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Services
{
    // File log (ApplicationConstants.ApplicationLogFileName): diagnostics and full errors; redacted via LogRedactionHelper.
    // User-facing status/progress uses ITaskReportService (status strip), not this file.
    // Line prefix: time always; yyyy-MM-dd only on the first log line of each local calendar day.
    public class LogService : ILogService
    {
        private readonly object _lockObject = new object();
        private readonly LoggingConfiguration _configuration;
        private readonly string _logFilePath;
        private DateTime _lastLoggedLocalDate = DateTime.MinValue.Date;
        private Func<DateTime> _clock;

        public LogService(string logFilePath = null, bool enableConsoleLogging = false, bool enableFileLogging = true)
            : this(new LoggingConfiguration
            {
                LogFilePath = logFilePath,
                EnableConsoleLogging = enableConsoleLogging,
                EnableFileLogging = enableFileLogging
            })
        {
        }

        public LogService(LoggingConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            if (_configuration.EnableFileLogging)
            {
                _logFilePath = _configuration.LogFilePath ?? Path.Combine(
                    PathConstants.LauncherInstallDirectory,
                    ApplicationConstants.ApplicationLogFileName);

                EnsureLogDirectoryExists();
                InitializeLogFile();
            }
        }

        // Test seam: override local clock for date-rollover assertions.
        // Re-seeds the day marker so post-construct clock injection matches session-banner behavior.
        internal void SetClockForTests(Func<DateTime> clock)
        {
            lock (_lockObject)
            {
                _clock = clock;
                if (_configuration.EnableFileLogging && _clock != null)
                    _lastLoggedLocalDate = GetNow().Date;
            }
        }

        // Test seam: pretend no line has been written today (full date on next write).
        internal void ClearLastLoggedDateForTests()
        {
            lock (_lockObject)
                _lastLoggedLocalDate = DateTime.MinValue.Date;
        }

        public void LogDebug(string message)
        {
            WriteLog(LogLevel.Debug, "DEBUG", message);
        }

        public void LogMessage(string message)
        {
            WriteLog(LogLevel.Info, "INFO", message);
        }

        public void LogWarning(string message)
        {
            WriteLog(LogLevel.Warning, "WARNING", message);
        }

        public void LogError(string message)
        {
            WriteLog(LogLevel.Error, "ERROR", message);
        }

        public void LogError(string message, Exception exception)
        {
            string fullMessage = message;
            string formatted = FormatException(exception);
            if (!string.IsNullOrEmpty(formatted))
                fullMessage = string.IsNullOrEmpty(message) ? formatted : message + Environment.NewLine + formatted;
            WriteLog(LogLevel.Error, "ERROR", fullMessage);
        }

        private DateTime GetNow()
        {
            return _clock != null ? _clock() : DateTime.Now;
        }

        private void WriteLog(LogLevel logLevel, string level, string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            if (logLevel < _configuration.MinimumLogLevel)
                return;

            message = message.TrimEnd('\r', '\n');
            if (string.IsNullOrEmpty(message))
                return;

            message = LogRedactionHelper.RedactForLog(message);

            lock (_lockObject)
            {
                DateTime now = GetNow();
                string timestamp = FormatTimestamp(now);
                string logEntry = $"[{timestamp}] [{level}] {message}";

                if (_configuration.EnableConsoleLogging)
                    WriteToConsole(level, logEntry);

                if (_configuration.EnableFileLogging)
                    WriteToFile(logEntry);
            }
        }

        // Caller must hold _lockObject.
        private string FormatTimestamp(DateTime now)
        {
            DateTime today = now.Date;
            if (today != _lastLoggedLocalDate)
            {
                _lastLoggedLocalDate = today;
                return now.ToString("yyyy-MM-dd HH:mm:ss");
            }

            return now.ToString("HH:mm:ss");
        }

        private static string FormatException(Exception exception)
        {
            if (exception == null)
                return string.Empty;

            var sb = new StringBuilder();
            for (Exception ex = exception; ex != null; ex = ex.InnerException)
            {
                if (sb.Length > 0)
                    sb.AppendLine("--- Inner exception ---");
                sb.AppendLine(ex.GetType().FullName + ": " + ex.Message);
                if (!string.IsNullOrEmpty(ex.StackTrace))
                    sb.AppendLine(ex.StackTrace);
            }
            return sb.ToString();
        }

        private static void WriteToConsole(string level, string logEntry)
        {
            ConsoleColor originalColor = Console.ForegroundColor;

            try
            {
                Console.ForegroundColor = GetConsoleColorForLevel(level);
                Console.WriteLine(logEntry);
            }
            finally
            {
                Console.ForegroundColor = originalColor;
            }
        }

        private static ConsoleColor GetConsoleColorForLevel(string level)
        {
            switch (level)
            {
                case "WARNING":
                    return ConsoleColor.Yellow;
                case "ERROR":
                    return ConsoleColor.Red;
                case "DEBUG":
                    return ConsoleColor.DarkGray;
                default:
                    return ConsoleColor.White;
            }
        }

        private void WriteToFile(string logEntry)
        {
            try
            {
                if (_configuration.MaxLogFileSize > 0 && File.Exists(_logFilePath))
                {
                    var fileInfo = new FileInfo(_logFilePath);
                    if (fileInfo.Length >= _configuration.MaxLogFileSize)
                        RotateLogFile();
                }

                File.AppendAllText(_logFilePath, logEntry + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to write to log file: {ex.Message}");
                Console.WriteLine($"Log entry: {logEntry}");
            }
        }

        private void RotateLogFile()
        {
            try
            {
                string backupPath = _logFilePath + ".old";
                if (File.Exists(backupPath))
                    File.Delete(backupPath);
                if (File.Exists(_logFilePath))
                    File.Move(_logFilePath, backupPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to rotate log file: {ex.Message}");
            }
        }

        private void EnsureLogDirectoryExists()
        {
            try
            {
                string directory = Path.GetDirectoryName(_logFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to create log directory: {ex.Message}");
            }
        }

        private void InitializeLogFile()
        {
            try
            {
                string rotatedPath = _logFilePath + ".old";
                if (File.Exists(rotatedPath))
                    File.Delete(rotatedPath);

                DateTime now = GetNow();
                string header = "---------- session " + now.ToString("yyyy-MM-dd HH:mm:ss") + " ----------" + Environment.NewLine;
                File.WriteAllText(_logFilePath, header);
                // Session banner already carries today's date; first INFO uses time-only until midnight.
                _lastLoggedLocalDate = now.Date;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize log file: {ex.Message}");
            }
        }
    }
}
