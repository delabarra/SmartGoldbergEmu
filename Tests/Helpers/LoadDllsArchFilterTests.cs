using SmartGoldbergEmu.Helpers;
using Xunit;

namespace SmartGoldbergEmu.Tests.Helpers
{
    public sealed class LoadDllsArchFilterTests
    {
        [Theory]
        [InlineData("extra_x64.dll", true, true)]
        [InlineData("extra_x64.dll", false, false)]
        [InlineData("extra_x32.dll", true, false)]
        [InlineData("extra_x32.dll", false, true)]
        [InlineData("extra.dll", true, true)]
        [InlineData("extra.dll", false, true)]
        [InlineData("both_x64_and_x32.dll", true, true)]
        public void MatchesProcessArchitecture_filters_by_filename_markers(string fileName, bool useX64, bool expected)
        {
            Assert.Equal(expected, LoadDllsArchFilter.MatchesProcessArchitecture(fileName, useX64));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void MatchesProcessArchitecture_rejects_empty_name(string fileName)
        {
            Assert.False(LoadDllsArchFilter.MatchesProcessArchitecture(fileName, true));
        }
    }
}
