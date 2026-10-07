using AppDataKit;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Tests.TestSupport;
using Xunit;

namespace SmartGoldbergEmu.Tests.Helpers
{
    public sealed class GamesInfosDatasHelperTests
    {
        [Fact]
        public void ParseAchievementsDb_resolves_language_and_hidden()
        {
            string json = TestFileHelper.ReadTestData("achievements_db_sample.json");
            AchievementsSection section = GamesInfosDatasHelper.ParseAchievementsDb(json, "french");

            Assert.Equal(SnapshotSectionStatus.Ok, section.Status);
            Assert.Equal(2, section.Items.Count);
            Assert.Equal("ACHIEVEMENT_PIECE_OF_CAKE", section.Items[0].Name);
            Assert.Equal("Du gateau", section.Items[0].DisplayName);
            Assert.Equal("Complete the first mission", section.Items[0].Description);
            Assert.False(section.Items[0].Hidden);
            Assert.True(section.Items[1].Hidden);
        }

        [Fact]
        public void ParseAchievementsDb_falls_back_to_english()
        {
            string json = TestFileHelper.ReadTestData("achievements_db_sample.json");
            AchievementsSection section = GamesInfosDatasHelper.ParseAchievementsDb(json, "koreana");

            Assert.Equal(SnapshotSectionStatus.Ok, section.Status);
            Assert.Equal("Piece of Cake", section.Items[0].DisplayName);
        }

        [Fact]
        public void ParseInventoryDb_requires_itemdefid_entries()
        {
            const string json = "[{\"itemdefid\":\"1\",\"name\":\"Coin\",\"type\":\"item\"},{\"name\":\"skip-me\"}]";
            ItemsSection section = GamesInfosDatasHelper.ParseInventoryDb(json);

            Assert.Equal(SnapshotSectionStatus.Ok, section.Status);
            Assert.True(section.Supported);
            Assert.False(string.IsNullOrWhiteSpace(section.ArchiveJson));
            Assert.Single(section.Items);
            Assert.Equal("1", section.Items[0].ItemDefId);
        }

        [Fact]
        public void ParseInventoryDb_empty_array_is_unavailable()
        {
            ItemsSection section = GamesInfosDatasHelper.ParseInventoryDb("[]");
            Assert.Equal(SnapshotSectionStatus.Unavailable, section.Status);
            Assert.False(section.Supported);
        }

        [Fact]
        public void ParseStatsDb_maps_default_aliases()
        {
            const string json = "[{\"name\":\"STAT_A\",\"type\":\"int\",\"default\":3},{\"name\":\"STAT_B\",\"defaultvalue\":\"7\"}]";
            StatsSection section = GamesInfosDatasHelper.ParseStatsDb(json);

            Assert.Equal(SnapshotSectionStatus.Ok, section.Status);
            Assert.Equal(2, section.Items.Count);
            Assert.Equal("3", section.Items[0].DefaultValue);
            Assert.Equal("7", section.Items[1].DefaultValue);
            Assert.Equal("int", section.Items[1].Type);
            Assert.Equal(json, section.StatsDbJson);
        }

        [Fact]
        public void ParseStatsDb_keeps_no_raw_body_when_empty()
        {
            StatsSection section = GamesInfosDatasHelper.ParseStatsDb("[]");

            Assert.Equal(SnapshotSectionStatus.Unavailable, section.Status);
            Assert.Null(section.StatsDbJson);
        }
    }
}
