using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
using Xunit;

namespace SmartGoldbergEmu.Tests.Helpers
{
    public sealed class GoldbergSavePathHelperSteamUserdataTests
    {
        [Fact]
        public void FormatSteamUserdataDisplayPath_uses_steam3_not_steam64()
        {
            const ulong steam64 = 76561198012345678UL;
            string steam3 = (steam64 - ApplicationConstants.SteamId64Base).ToString();

            string display = GoldbergSavePathHelper.FormatSteamUserdataDisplayPath(steam64.ToString(), 570UL);

            Assert.Contains(steam3, display);
            Assert.DoesNotContain(steam64.ToString(), display);
            Assert.Contains("570", display);
        }

        [Fact]
        public void UsesSteamUserdataLayout_matches_resolved_account_directory()
        {
            const ulong steam64 = 76561198012345678UL;
            string steam64Text = steam64.ToString();
            if (!GoldbergSavePathHelper.TryResolveSteamUserdataAccountDirectory(steam64Text, out string accountPath))
                return;

            Assert.True(GoldbergSavePathHelper.UsesSteamUserdataLayout(accountPath, steam64Text));
        }
    }
}
