using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;
using Xunit;

namespace SmartGoldbergEmu.Tests.Helpers
{
    public sealed class GoldbergVersionHelperTests
    {
        [Theory]
        [InlineData("release-2026_05_19", "2026_05_19", true)]
        [InlineData("2026_02_16", "2026_02_16", true)]
        [InlineData("repack-2026_05_19-2026_02_16-1", "2026_05_19", true)]
        [InlineData("v1.2.3", null, false)]
        public void TryNormalizeForkVersion_extracts_fork_date(string raw, string expected, bool shouldNormalize)
        {
            bool ok = GoldbergVersionHelper.TryNormalizeForkVersion(raw, out string normalized);
            Assert.Equal(shouldNormalize, ok);
            if (shouldNormalize)
                Assert.Equal(expected, normalized);
        }

        [Theory]
        [InlineData("2026_02_16", "2026_05_19", true)]
        [InlineData("2026_05_19", "2026_05_19", false)]
        [InlineData("2026_05_19", "2026_02_16", false)]
        [InlineData("pre-existent", "2026_05_19", true)]
        public void IsNewerGoldbergVersion_compares_fork_dates(string current, string latest, bool expected)
        {
            Assert.Equal(expected, GoldbergVersionHelper.IsNewerGoldbergVersion(current, latest));
        }
    }
}
