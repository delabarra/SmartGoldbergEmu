using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;
using Xunit;

namespace SmartGoldbergEmu.Tests.Services
{
    public sealed class LogServiceTimestampTests
    {
        private static readonly Regex LinePrefix = new Regex(
            @"^\[(?<stamp>[^\]]+)\] \[INFO\] ",
            RegexOptions.Compiled);

        [Fact]
        public void Same_day_lines_use_time_only_after_session_banner()
        {
            string path = Path.Combine(Path.GetTempPath(), "sge-log-ts-" + Guid.NewGuid().ToString("N") + ".log");
            try
            {
                var day = new DateTime(2026, 8, 10, 7, 45, 36);
                var clock = day;
                var log = CreateFileLogger(path);
                log.SetClockForTests(() => clock);

                log.LogMessage("first");
                clock = day.AddSeconds(1);
                log.LogMessage("second");

                string[] lines = ReadInfoLines(path);
                Assert.Equal(2, lines.Length);
                Assert.Equal("07:45:36", ExtractStamp(lines[0]));
                Assert.Equal("07:45:37", ExtractStamp(lines[1]));
            }
            finally
            {
                TryDelete(path);
                TryDelete(path + ".old");
            }
        }

        [Fact]
        public void First_line_after_midnight_includes_date()
        {
            string path = Path.Combine(Path.GetTempPath(), "sge-log-ts-" + Guid.NewGuid().ToString("N") + ".log");
            try
            {
                var day1 = new DateTime(2026, 8, 10, 23, 59, 50);
                var clock = day1;
                var log = CreateFileLogger(path);
                log.SetClockForTests(() => clock);

                log.LogMessage("before midnight");
                clock = new DateTime(2026, 8, 11, 0, 0, 5);
                log.LogMessage("after midnight");

                string[] lines = ReadInfoLines(path);
                Assert.Equal(2, lines.Length);
                Assert.Equal("23:59:50", ExtractStamp(lines[0]));
                Assert.Equal("2026-08-11 00:00:05", ExtractStamp(lines[1]));
            }
            finally
            {
                TryDelete(path);
                TryDelete(path + ".old");
            }
        }

        [Fact]
        public void First_line_of_day_includes_date_when_day_marker_cleared()
        {
            string path = Path.Combine(Path.GetTempPath(), "sge-log-ts-" + Guid.NewGuid().ToString("N") + ".log");
            try
            {
                var day = new DateTime(2026, 8, 10, 8, 0, 0);
                var clock = day;
                var log = CreateFileLogger(path);
                log.SetClockForTests(() => clock);
                log.ClearLastLoggedDateForTests();

                log.LogMessage("first of day");
                clock = day.AddMinutes(1);
                log.LogMessage("same day");

                string[] lines = ReadInfoLines(path);
                Assert.Equal(2, lines.Length);
                Assert.Equal("2026-08-10 08:00:00", ExtractStamp(lines[0]));
                Assert.Equal("08:01:00", ExtractStamp(lines[1]));
            }
            finally
            {
                TryDelete(path);
                TryDelete(path + ".old");
            }
        }

        private static LogService CreateFileLogger(string path)
        {
            return new LogService(new LoggingConfiguration
            {
                LogFilePath = path,
                EnableFileLogging = true,
                EnableConsoleLogging = false,
                MinimumLogLevel = LogLevel.Info
            });
        }

        private static string[] ReadInfoLines(string path)
        {
            return File.ReadAllLines(path)
                .Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith("----------", StringComparison.Ordinal))
                .ToArray();
        }

        private static string ExtractStamp(string line)
        {
            Match m = LinePrefix.Match(line);
            Assert.True(m.Success, "Unexpected log line: " + line);
            return m.Groups["stamp"].Value;
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
            }
        }
    }
}
