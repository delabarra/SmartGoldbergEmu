using System;

namespace SmartGoldbergEmu.Models
{
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warning = 2,
        Error = 3
    }

    public class LoggingConfiguration
    {
        public bool EnableConsoleLogging { get; set; } = false;
        public bool EnableFileLogging { get; set; } = true;
        public string LogFilePath { get; set; } = null;
        public LogLevel MinimumLogLevel { get; set; } = LogLevel.Info;
        public long MaxLogFileSize { get; set; } = 5 * 1024 * 1024;

        public static LoggingConfiguration CreateDefault()
        {
            return new LoggingConfiguration
            {
                MinimumLogLevel = ResolveMinimumLogLevelFromEnvironment()
            };
        }

        // Optional override: SGE_LOG_LEVEL = debug | info | warning | error
        public static LogLevel ResolveMinimumLogLevelFromEnvironment()
        {
            string env = Environment.GetEnvironmentVariable("SGE_LOG_LEVEL");
            if (string.IsNullOrWhiteSpace(env))
                return LogLevel.Info;

            switch (env.Trim().ToLowerInvariant())
            {
                case "debug":
                    return LogLevel.Debug;
                case "info":
                    return LogLevel.Info;
                case "warning":
                case "warn":
                    return LogLevel.Warning;
                case "error":
                    return LogLevel.Error;
                default:
                    return LogLevel.Info;
            }
        }
    }
}
