using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Services;
using SteamKit;
using Xunit;

namespace SmartGoldbergEmu.Tests.Services
{
    public sealed class SteamProductInfoServiceStatsTests
    {
        [Fact]
        public void ExtractAppDataFromAppRoot_maps_pics_stats_to_goldberg_json()
        {
            var root = new KeyValue(SteamPicsKeyNames.AppInfo);
            var stats = new KeyValue(SteamPicsKeyNames.Stats);
            var numGames = new KeyValue("NumGames");
            numGames.Children.Add(new KeyValue(SteamPicsKeyNames.Type, "int"));
            numGames.Children.Add(new KeyValue(SteamPicsKeyNames.Default, "0"));
            numGames.Children.Add(new KeyValue(SteamPicsKeyNames.Global, "100"));
            stats.Children.Add(numGames);
            root.Children.Add(stats);

            using (var service = new SteamProductInfoService())
            {
                var result = service.ExtractAppDataFromAppRoot(root, "480");
                Assert.False(string.IsNullOrEmpty(result.Stats));
                Assert.Contains("\"name\": \"NumGames\"", result.Stats);
                Assert.Contains("\"type\": \"int\"", result.Stats);
                Assert.Contains("\"default\": \"0\"", result.Stats);
                Assert.Contains("\"global\": \"100\"", result.Stats);
            }
        }
    }
}
