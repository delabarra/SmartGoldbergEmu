using SmartGoldbergEmu.Helpers;
using Xunit;

namespace SmartGoldbergEmu.Tests.Helpers
{
    public sealed class VersionComparisonHelperTests
    {
        [Theory]
        [InlineData("3.0.0.0", "v3.0.0.1", true)]
        [InlineData("v3.0.0.0", "v3.0.0.1", true)]
        [InlineData("3.0.0.1", "3.0.0.0", false)]
        [InlineData("3.0.0.0", "3.0.0.0", false)]
        [InlineData("1.2.9", "v1.2.10", true)]
        [InlineData("1.2.10", "1.2.9", false)]
        [InlineData("pre-existent", "3.0.0.1", true)]
        [InlineData("2.4.0+build.42", "v2.5.0", true)]
        [InlineData("v2.4.0+build.42", "2.4.0", false)]
        public void IsNewerVersion_compares_all_numeric_segments(string current, string latest, bool expected)
        {
            Assert.Equal(expected, VersionComparisonHelper.IsNewerVersion(current, latest));
        }
    }
}
