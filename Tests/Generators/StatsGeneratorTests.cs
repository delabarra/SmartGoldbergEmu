using System;
using System.Globalization;
using System.IO;
using System.Threading;
using AppDataKit;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Generators;
using SmartGoldbergEmu.JsonKit;
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
        public void ConvertStatsDbJsonToGoldbergFormat_writes_string_default_and_global()
        {
            string input = TestFileHelper.ReadTestData("stats_db_225140_input.json");
            string result = StatsGenerator.ConvertStatsDbJsonToGoldbergFormat(input);

            var stat = Assert.IsType<JsonObject>(Assert.Single(JsonArray.Parse(result)));
            Assert.Equal(4, stat.Count);
            Assert.Equal("STAT_THE_MIGHTY_FOOT", ((JsonString)stat["name"]).Value);
            Assert.Equal("int", ((JsonString)stat["type"]).Value);
            Assert.Equal("0", ((JsonString)stat["default"]).Value);
            Assert.Equal("0", ((JsonString)stat["global"]).Value);
        }

        [Fact]
        public void ConvertStatsDbJsonToGoldbergFormat_uses_invariant_float_and_ignores_max()
        {
            const string input = "[{\"name\":\"accuracy\",\"displayName\":\"Accuracy\",\"type\":\"float\",\"default\":0.5,\"max\":100}]";
            CultureInfo previous = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                string result = StatsGenerator.ConvertStatsDbJsonToGoldbergFormat(input);

                var stat = Assert.IsType<JsonObject>(Assert.Single(JsonArray.Parse(result)));
                Assert.Equal(4, stat.Count);
                Assert.Equal("float", ((JsonString)stat["type"]).Value);
                Assert.Equal("0.5", ((JsonString)stat["default"]).Value);
                Assert.Equal("0", ((JsonString)stat["global"]).Value);
                Assert.Null(stat["max"]);
                Assert.Null(stat["displayName"]);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
            }
        }

        [Fact]
        public void TryConvertStatsDbJsonToGoldbergFormat_matches_convert()
        {
            string input = TestFileHelper.ReadTestData("stats_db_225140_input.json");

            Assert.Equal(
                StatsGenerator.ConvertStatsDbJsonToGoldbergFormat(input),
                StatsGenerator.TryConvertStatsDbJsonToGoldbergFormat(input));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not json")]
        public void TryConvertStatsDbJsonToGoldbergFormat_returns_null_for_unusable_body(string body)
        {
            Assert.Null(StatsGenerator.TryConvertStatsDbJsonToGoldbergFormat(body));
        }

        [Fact]
        public void BuildGoldbergStatsJson_maps_ok_section()
        {
            var section = new StatsSection
            {
                Status = SnapshotSectionStatus.Ok,
                Items = new[] { new StatSchemaEntry { Name = "NumGames", Type = "int", DefaultValue = "" } }
            };

            string result = StatsGenerator.BuildGoldbergStatsJson(section);

            Assert.Contains("\"NumGames\"", result);
            Assert.Contains("\"default\": \"0\"", result);
            Assert.Contains("\"global\": \"0\"", result);
        }

        [Theory]
        [InlineData(SnapshotSectionStatus.Unavailable)]
        [InlineData(SnapshotSectionStatus.Error)]
        public void BuildGoldbergStatsJson_returns_null_unless_ok(SnapshotSectionStatus status)
        {
            var section = new StatsSection
            {
                Status = status,
                Items = new[] { new StatSchemaEntry { Name = "NumGames", Type = "int" } }
            };

            Assert.Null(StatsGenerator.BuildGoldbergStatsJson(section));
        }
    }
}
