using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
using Xunit;

namespace SmartGoldbergEmu.Tests.Helpers
{
    public sealed class SteamIdHelperTests
    {
        [Fact]
        public void TryGetSteam3AccountId_converts_steam64_to_userdata_folder_name()
        {
            const ulong steam64 = 76561198012345678UL;
            string expected = (steam64 - ApplicationConstants.SteamId64Base).ToString();

            Assert.True(SteamIdHelper.TryGetSteam3AccountId(steam64.ToString(), out string steam3));
            Assert.Equal(expected, steam3);
        }

        [Fact]
        public void TryGetSteam3AccountId_rejects_invalid_steam64()
        {
            Assert.False(SteamIdHelper.TryGetSteam3AccountId(string.Empty, out _));
            Assert.False(SteamIdHelper.TryGetSteam3AccountId("not-a-number", out _));
            Assert.False(SteamIdHelper.TryGetSteam3AccountId("1", out _));
        }
    }
}
