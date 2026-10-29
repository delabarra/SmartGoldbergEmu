using System.IO;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Tests.TestSupport;
using Xunit;

namespace SmartGoldbergEmu.Tests.Helpers
{
    public sealed class LaunchSessionCleanupTrustHelperTests
    {
        [Fact]
        public void IsAllowedManifestPath_requires_launch_sessions_folder_and_appid_name()
        {
            string allowed = PathConstants.CombineLaunchSessionManifestPath(12345);
            Assert.True(LaunchSessionCleanupTrustHelper.IsAllowedManifestPath(allowed));

            string outside = Path.Combine(Path.GetTempPath(), "12345.json");
            Assert.False(LaunchSessionCleanupTrustHelper.IsAllowedManifestPath(outside));
            Assert.False(LaunchSessionCleanupTrustHelper.IsAllowedManifestPath(
                Path.Combine(PathConstants.LaunchSessionManifestDirectory, "not-an-appid.json")));
        }

        [Fact]
        public void IsAllowedDeployedFile_requires_known_dll_name_and_sibling_sge_backup()
        {
            string dir = TestFileHelper.CreateTempDirectory("sge-trust-deploy-");
            try
            {
                string target = Path.Combine(dir, PathConstants.GoldbergStandardSteamApiDll64);
                string backup = target + PathConstants.SteamApiBackupSidecarExtension;
                Assert.True(LaunchSessionCleanupTrustHelper.IsAllowedDeployedFile(new PersistedFileDeployment
                {
                    TargetPath = target,
                    BackupPath = backup,
                    HadOriginal = true,
                }));

                Assert.False(LaunchSessionCleanupTrustHelper.IsAllowedDeployedFile(new PersistedFileDeployment
                {
                    TargetPath = Path.Combine(dir, "notes.txt"),
                    BackupPath = Path.Combine(dir, "notes.txt") + PathConstants.SteamApiBackupSidecarExtension,
                    HadOriginal = true,
                }));

                Assert.False(LaunchSessionCleanupTrustHelper.IsAllowedDeployedFile(new PersistedFileDeployment
                {
                    TargetPath = target,
                    BackupPath = Path.Combine(dir, "other" + PathConstants.SteamApiBackupSidecarExtension),
                    HadOriginal = true,
                }));
            }
            finally
            {
                try { Directory.Delete(dir, recursive: true); } catch { }
            }
        }

        [Fact]
        public void IsAllowedSteamSettingsPaths_require_expected_folder_names()
        {
            string dir = TestFileHelper.CreateTempDirectory("sge-trust-settings-");
            try
            {
                Assert.True(LaunchSessionCleanupTrustHelper.IsAllowedSteamSettingsJunctionPath(
                    Path.Combine(dir, PathConstants.SteamSettingsFolderName)));
                Assert.False(LaunchSessionCleanupTrustHelper.IsAllowedSteamSettingsJunctionPath(
                    Path.Combine(dir, "other_folder")));

                Assert.True(LaunchSessionCleanupTrustHelper.IsAllowedSteamSettingsBackupPath(
                    Path.Combine(dir, PathConstants.SteamSettingsFolderName + PathConstants.SteamApiBackupSidecarExtension)));
                Assert.False(LaunchSessionCleanupTrustHelper.IsAllowedSteamSettingsBackupPath(
                    Path.Combine(dir, "steam_settings.bak")));
            }
            finally
            {
                try { Directory.Delete(dir, recursive: true); } catch { }
            }
        }

        [Fact]
        public void IsAllowedGameLibraryFolder_requires_games_appId_layout()
        {
            const ulong appId = 424242;
            string allowed = Path.Combine(Path.GetTempPath(), PathConstants.GamesDirectoryFolderName, appId.ToString());
            Assert.True(LaunchSessionCleanupTrustHelper.IsAllowedGameLibraryFolder(appId, allowed));
            Assert.False(LaunchSessionCleanupTrustHelper.IsAllowedGameLibraryFolder(appId, Path.Combine(Path.GetTempPath(), appId.ToString())));
            Assert.False(LaunchSessionCleanupTrustHelper.IsAllowedGameLibraryFolder(appId, Path.Combine(Path.GetTempPath(), PathConstants.GamesDirectoryFolderName, "999")));
            Assert.False(LaunchSessionCleanupTrustHelper.IsAllowedGameLibraryFolder(appId, Path.GetTempPath()));
        }

        [Fact]
        public void IsAllowedLoadDllsFolder_requires_steam_settings_load_dlls_under_game_library()
        {
            const ulong appId = 424242;
            string allowed = Path.Combine(
                Path.GetTempPath(),
                PathConstants.GamesDirectoryFolderName,
                appId.ToString(),
                PathConstants.SteamSettingsFolderName,
                PathConstants.GoldbergLoadDllsFolderName);
            Assert.True(LaunchSessionCleanupTrustHelper.IsAllowedLoadDllsFolder(appId, allowed));
            Assert.False(LaunchSessionCleanupTrustHelper.IsAllowedLoadDllsFolder(
                appId,
                Path.Combine(Path.GetTempPath(), PathConstants.GoldbergLoadDllsFolderName)));
            Assert.False(LaunchSessionCleanupTrustHelper.IsAllowedLoadDllsFolder(
                appId,
                Path.Combine(Path.GetTempPath(), PathConstants.GamesDirectoryFolderName, appId.ToString(), "other", PathConstants.GoldbergLoadDllsFolderName)));
        }
    }
}
