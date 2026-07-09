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
                }
            });

            var urls = service.GetAchievementIconCandidateUrls(
                "https://cdn.steamstatic.com/steamcommunity/public/images/apps/730/abc.jpg");

            Assert.Equal(3, urls.Count);
            Assert.Equal(
                "https://cdn.steamstatic.com/steamcommunity/public/images/apps/730/abc.jpg",
                urls[0]);
            Assert.Contains(
                "https://cdn.akamai.steamstatic.com/steamcommunity/public/images/apps/730/abc.jpg",
                urls);
            Assert.Contains(
                "https://cdn.cloudflare.steamstatic.com/steamcommunity/public/images/apps/730/abc.jpg",
                urls);
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
