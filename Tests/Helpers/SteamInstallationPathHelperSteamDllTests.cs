using System.IO;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Tests.TestSupport;
using Xunit;

namespace SmartGoldbergEmu.Tests.Helpers
{
    public sealed class SteamInstallationPathHelperSteamDllTests
    {
        [Fact]
        public void TryPromoteSteamOFallbackToSteamDll_copies_steam_o_when_Steam_dll_missing()
        {
            string folder = TestFileHelper.CreateTempDirectory("sge-steamdll-promote-");
            try
            {
                string steamO = Path.Combine(folder, PathConstants.GoldbergSteamOriginalDllFileName);
                string steamDll = Path.Combine(folder, PathConstants.GoldbergSteamDllFileName);
                File.WriteAllBytes(steamO, new byte[] { 0x51 });

                Assert.True(SteamInstallationPathHelper.TryPromoteSteamOFallbackToSteamDll(folder, out string error), error);
                Assert.True(File.Exists(steamDll));
                Assert.Equal(0x51, File.ReadAllBytes(steamDll)[0]);
            }
            finally
            {
                try { Directory.Delete(folder, recursive: true); } catch { }
            }
        }

        [Fact]
        public void TryPromoteSteamOFallbackToSteamDll_leaves_existing_Steam_dll_untouched()
        {
            string folder = TestFileHelper.CreateTempDirectory("sge-steamdll-keep-");
            try
            {
                string steamO = Path.Combine(folder, PathConstants.GoldbergSteamOriginalDllFileName);
                string steamDll = Path.Combine(folder, PathConstants.GoldbergSteamDllFileName);
                File.WriteAllBytes(steamDll, new byte[] { 0x11 });
                File.WriteAllBytes(steamO, new byte[] { 0x22 });

                Assert.True(SteamInstallationPathHelper.TryPromoteSteamOFallbackToSteamDll(folder, out string error), error);
                Assert.Equal(0x11, File.ReadAllBytes(steamDll)[0]);
            }
            finally
            {
                try { Directory.Delete(folder, recursive: true); } catch { }
            }
        }

        [Fact]
        public void TrySyncSteamDllToDirectory_prefers_existing_Steam_dll_over_steam_o()
        {
            string folder = TestFileHelper.CreateTempDirectory("sge-steamdll-sync-");
            try
            {
                string steamO = Path.Combine(folder, PathConstants.GoldbergSteamOriginalDllFileName);
                string steamDll = Path.Combine(folder, PathConstants.GoldbergSteamDllFileName);
                File.WriteAllBytes(steamDll, new byte[] { 0x31 });
                File.WriteAllBytes(steamO, new byte[] { 0x32 });

                Assert.True(SteamInstallationPathHelper.TrySyncSteamDllToDirectory(folder, out string error), error);
                Assert.Equal(0x31, File.ReadAllBytes(steamDll)[0]);
            }
            finally
            {
                try { Directory.Delete(folder, recursive: true); } catch { }
            }
        }
    }
}
