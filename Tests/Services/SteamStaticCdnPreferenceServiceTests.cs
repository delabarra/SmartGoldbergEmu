using System.Collections.Generic;
using System.Linq;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;
using Xunit;

namespace SmartGoldbergEmu.Tests.Services
{
    public sealed class SteamStaticCdnPreferenceServiceTests
    {
        [Fact]
        public void BuildStoreAssetCandidateUrls_includes_store_item_assets_and_bare_filename_fallbacks()
        {
            var preferences = new SteamStaticCdnPreferences
            {
                StoreItemAssetsHosts = new List<string> { "shared.fastly.steamstatic.com" },
                SteamAppsBareHosts = new List<string>
                {
                    "cdn.akamai.steamstatic.com",
                    "steamcdn-a.akamaihd.net"
                }
            };

            var urls = SteamStaticCdnPreferenceService.BuildStoreAssetCandidateUrls(
                2218750,
                "35022e01a7288521bc746f0d611cf25e717dadcf/header.jpg",
                preferences);

            Assert.Contains(
                "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/2218750/35022e01a7288521bc746f0d611cf25e717dadcf/header.jpg",
                urls);
            Assert.Contains(
                "https://cdn.akamai.steamstatic.com/steam/apps/2218750/header.jpg",
                urls);
            Assert.Contains(
                "https://steamcdn-a.akamaihd.net/steam/apps/2218750/header.jpg",
                urls);
        }

        [Fact]
        public void GetAchievementIconCandidateUrls_rewrites_known_steam_cdn_hosts()
        {
            var service = new SteamStaticCdnPreferenceService();
            service.SetPreferencesForTests(new SteamStaticCdnPreferences
            {
                GeneralCdnHosts = new List<string>
                {
                    "cdn.akamai.steamstatic.com",
                    "cdn.cloudflare.steamstatic.com"
                },
                SharedFastlyHosts = new List<string>
                {
                    "shared.fastly.steamstatic.com"
                }
            });

            var urls = service.GetAchievementIconCandidateUrls(
                "https://cdn.steamstatic.com/steamcommunity/public/images/apps/730/abc.jpg");

            Assert.Equal(4, urls.Count);
            Assert.Equal(
                "https://cdn.steamstatic.com/steamcommunity/public/images/apps/730/abc.jpg",
                urls[0]);
            Assert.Contains(
                "https://cdn.akamai.steamstatic.com/steamcommunity/public/images/apps/730/abc.jpg",
                urls);
            Assert.Contains(
                "https://cdn.cloudflare.steamstatic.com/steamcommunity/public/images/apps/730/abc.jpg",
                urls);
            Assert.Contains(
                "https://shared.fastly.steamstatic.com/community_assets/images/apps/730/abc.jpg",
                urls);
        }

        [Fact]
        public void GetAchievementIconCandidateUrls_adds_community_assets_fallback_for_schema_path()
        {
            var service = new SteamStaticCdnPreferenceService();
            service.SetPreferencesForTests(new SteamStaticCdnPreferences
            {
                GeneralCdnHosts = new List<string> { "steamcdn-a.akamaihd.net" },
                SharedFastlyHosts = new List<string>
                {
                    "shared.fastly.steamstatic.com",
                    "shared.akamai.steamstatic.com"
                }
            });

            var urls = service.GetAchievementIconCandidateUrls(
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
        }

        [Fact]
        public void TryParseSteamCommunityAppImagePath_parses_appid_and_filename()
        {
            Assert.True(
                SteamStaticCdnPreferenceService.TryParseSteamCommunityAppImagePath(
                    "/steamcommunity/public/images/apps/219990/abc123.jpg",
                    out ulong appId,
                    out string fileName));
            Assert.Equal(219990UL, appId);
            Assert.Equal("abc123.jpg", fileName);
            Assert.False(
                SteamStaticCdnPreferenceService.TryParseSteamCommunityAppImagePath(
                    "/community_assets/images/apps/219990/abc123.jpg",
                    out _,
                    out _));
        }

        [Fact]
        public void GetStoreAssetCandidateUrls_uses_default_mirrors_when_preferences_not_warmed()
        {
            var service = new SteamStaticCdnPreferenceService();
            var urls = service.GetStoreAssetCandidateUrls(570, "header.jpg").ToList();

            Assert.Contains(
                "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/570/header.jpg",
                urls);
            Assert.Contains(
                "https://cdn.fastly.steamstatic.com/steam/apps/570/header.jpg",
                urls);
            Assert.DoesNotContain(
                "https://shared.fastly.steamstatic.com/steam/apps/570/header.jpg",
                urls);
        }
    }
}
