using System.Collections.Generic;
using AppDataKit;
using Xunit;

namespace SmartGoldbergEmu.Tests.Services
{
    public sealed class SteamWebApiClientPercentagesTests
    {
        [Fact]
        public void ParseGlobalAchievementPercentages_reads_string_and_numeric_percents()
        {
            const string json = "{\"achievementpercentages\":{\"achievements\":["
                + "{\"name\":\"ACH_A\",\"percent\":\"49.6\"},"
                + "{\"name\":\"ACH_B\",\"percent\":3.25},"
                + "{\"name\":\"ACH_BAD\",\"percent\":\"n/a\"}]}}";

            Dictionary<string, double> result = SteamWebApiClient.ParseGlobalAchievementPercentages(json);

            Assert.Equal(2, result.Count);
            Assert.Equal(49.6, result["ACH_A"], 3);
            Assert.Equal(3.25, result["ACH_B"], 3);
        }

        [Fact]
        public void ParseGlobalAchievementPercentages_returns_empty_for_unexpected_payload()
        {
            Assert.Empty(SteamWebApiClient.ParseGlobalAchievementPercentages("{}"));
            Assert.Empty(SteamWebApiClient.ParseGlobalAchievementPercentages("not json"));
        }
    }
}
