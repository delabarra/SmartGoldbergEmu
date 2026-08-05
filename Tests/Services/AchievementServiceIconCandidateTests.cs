using System.Linq;
using SmartGoldbergEmu.Services;
using Xunit;

namespace SmartGoldbergEmu.Tests.Services
{
    public sealed class AchievementServiceIconCandidateTests
    {
        [Fact]
        public void GetAchievementIconCandidateUrls_includes_community_assets_fallback()
        {
            var urls = AchievementService.GetAchievementIconCandidateUrls(
                "https://steamcdn-a.akamaihd.net/steamcommunity/public/images/apps/219990/b9c54f06adb6d6fefe983665896a90cbac9d6265.jpg");

            Assert.Equal(
                "https://steamcdn-a.akamaihd.net/steamcommunity/public/images/apps/219990/b9c54f06adb6d6fefe983665896a90cbac9d6265.jpg",
                urls[0]);
            Assert.Contains(
                "https://shared.fastly.steamstatic.com/community_assets/images/apps/219990/b9c54f06adb6d6fefe983665896a90cbac9d6265.jpg",
                urls);
            Assert.Contains(
                "https://shared.akamai.steamstatic.com/community_assets/images/apps/219990/b9c54f06adb6d6fefe983665896a90cbac9d6265.jpg",
                urls);
            Assert.True(urls.Count >= 3);
            Assert.Equal(urls.Count, urls.Distinct().Count());
        }

        [Fact]
        public void GetAchievementIconCandidateUrls_returns_empty_for_blank_url()
        {
            Assert.Empty(AchievementService.GetAchievementIconCandidateUrls(null));
            Assert.Empty(AchievementService.GetAchievementIconCandidateUrls(""));
            Assert.Empty(AchievementService.GetAchievementIconCandidateUrls("   "));
        }
    }
}
