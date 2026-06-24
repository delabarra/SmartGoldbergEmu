using System;
using System.IO;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Generators;
using SmartGoldbergEmu.Tests.TestSupport;
using Xunit;

namespace SmartGoldbergEmu.Tests.Generators
{
    public sealed class StatsGeneratorTests
    {
        [Fact]
        public void TryWriteStatsJsonIfAbsent_writes_indented_stats_json()
        {
            string gamesRoot = TestFileHelper.CreateTempDirectory("sge-stats-");
            try
            {
                const ulong appId = 480;
                var generator = new StatsGenerator(gamesRoot);
                string steamSettings = generator.GetGameSteamSettingsPath(appId);
                Directory.CreateDirectory(steamSettings);

                string input = TestFileHelper.ReadTestData("stats_write_input.json");
                string expected = TestFileHelper.ReadTestData("stats_write_expected.json");

                Assert.True(generator.TryWriteStatsJsonIfAbsent(steamSettings, input, appId));

                string statsPath = Path.Combine(steamSettings, PathConstants.GoldbergStatsJsonFileName);
                Assert.True(File.Exists(statsPath));
                string written = File.ReadAllText(statsPath);
                Assert.Equal(TestFileHelper.NormalizeNewlines(expected), TestFileHelper.NormalizeNewlines(written));
            }
            finally
            {
                try { Directory.Delete(gamesRoot, recursive: true); } catch { }
            }
        }

        [Fact]
        public void TryWriteStatsJsonIfAbsent_does_not_overwrite_existing_file()
        {
            string gamesRoot = TestFileHelper.CreateTempDirectory("sge-stats-");
            try
            {
                const ulong appId = 225140;
                var generator = new StatsGenerator(gamesRoot);
                string steamSettings = generator.GetGameSteamSettingsPath(appId);
                Directory.CreateDirectory(steamSettings);
                string statsPath = Path.Combine(steamSettings, PathConstants.GoldbergStatsJsonFileName);
                const string existing = "[]";
                File.WriteAllText(statsPath, existing);

                Assert.False(generator.TryWriteStatsJsonIfAbsent(steamSettings, "[{\"name\":\"x\"}]", appId));
                Assert.Equal(existing, File.ReadAllText(statsPath));
            }
            finally
            {
                try { Directory.Delete(gamesRoot, recursive: true); } catch { }
            }
        }

        [Fact]
        public void ConvertStatsDbJsonToGoldbergFormat_adds_global_from_default()
        {
            string input = TestFileHelper.ReadTestData("stats_db_225140_input.json");
            string result = StatsGenerator.ConvertStatsDbJsonToGoldbergFormat(input);
            Assert.False(string.IsNullOrEmpty(result));
            Assert.Contains("\"global\"", result);
            Assert.Contains("STAT_THE_MIGHTY_FOOT", result);
        }
    }
}
