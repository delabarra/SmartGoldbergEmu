using AppDataKit;
using SmartGoldbergEmu.Models;
using Xunit;

namespace SmartGoldbergEmu.Tests.Models
{
    public sealed class AddGamePrefetchedSchemasTests
    {
        private const ulong CollectedAppId = 480;

        [Fact]
        public void GetItemsFor_returns_section_for_collected_app()
        {
            var items = new ItemsSection { Status = SnapshotSectionStatus.Ok };
            var schemas = new AddGamePrefetchedSchemas { AppId = CollectedAppId, Items = items };

            Assert.Same(items, schemas.GetItemsFor(CollectedAppId));
        }

        [Theory]
        [InlineData(0UL)]
        [InlineData(570UL)]
        public void GetItemsFor_returns_null_for_other_app(ulong appId)
        {
            var schemas = new AddGamePrefetchedSchemas
            {
                AppId = CollectedAppId,
                Items = new ItemsSection { Status = SnapshotSectionStatus.Ok }
            };

            Assert.Null(schemas.GetItemsFor(appId));
        }

        [Fact]
        public void GetAchievementsFor_matches_language_case_insensitively()
        {
            var achievements = new AchievementsSection { Status = SnapshotSectionStatus.Ok };
            var schemas = new AddGamePrefetchedSchemas
            {
                AppId = CollectedAppId,
                Achievements = achievements,
                AchievementsLanguage = "english"
            };

            Assert.Same(achievements, schemas.GetAchievementsFor(CollectedAppId, "English"));
        }

        [Fact]
        public void GetAchievementsFor_returns_null_when_language_differs()
        {
            var schemas = new AddGamePrefetchedSchemas
            {
                AppId = CollectedAppId,
                Achievements = new AchievementsSection { Status = SnapshotSectionStatus.Ok },
                AchievementsLanguage = "english"
            };

            Assert.Null(schemas.GetAchievementsFor(CollectedAppId, "german"));
        }

        [Fact]
        public void GetAchievementsFor_returns_null_for_other_app()
        {
            var schemas = new AddGamePrefetchedSchemas
            {
                AppId = CollectedAppId,
                Achievements = new AchievementsSection { Status = SnapshotSectionStatus.Ok },
                AchievementsLanguage = "english"
            };

            Assert.Null(schemas.GetAchievementsFor(570, "english"));
        }
    }
}
