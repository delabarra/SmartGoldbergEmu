using System.IO;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Tests.TestSupport;
using Xunit;

namespace SmartGoldbergEmu.Tests.Services
{
    [Collection("GameLaunch")]
    public sealed class GameLaunchServiceDeployCleanupTests
    {
        [Fact]
        public void LaunchGame_steam_dll_mode_restores_original_steam_dll_after_exit()
        {
            using (var harness = new LaunchDeployTestHarness())
            {
                string gameFolder = TestFileHelper.CreateTempDirectory("sge-game-steamdll-");
                try
                {
                    harness.CreateGameInstall(gameFolder, out string executablePath);
                    string steamDllPath = Path.Combine(gameFolder, PathConstants.GoldbergSteamDllFileName);
                    string backupPath = steamDllPath + PathConstants.SteamApiBackupSidecarExtension;
                    harness.WriteMarkerFile(steamDllPath, LaunchDeployTestHarness.OriginalFileMarker);
                    harness.StageGoldbergSteamDll();

                    string goldbergSource = PathConstants.CombineGoldbergSteamDllPath();
                    Assert.True(File.Exists(goldbergSource));
                    Assert.Equal(LaunchDeployTestHarness.GoldbergFileMarker, harness.ReadMarkerFile(goldbergSource));

                    var game = harness.CreateGameConfig(
                        LaunchDeployTestHarness.DefaultTestAppId,
                        gameFolder,
                        executablePath,
                        GoldbergLaunchMode.SteamDllBesideExe);

                    var result = harness.LaunchService.LaunchGame(game, useEmulator: true);
                    Assert.True(result.IsValid, result.ErrorMessage);

                    bool restored = LaunchDeployTestHarness.WaitUntil(
                        () => File.Exists(steamDllPath)
                            && !File.Exists(backupPath)
                            && harness.ReadMarkerFile(steamDllPath) == LaunchDeployTestHarness.OriginalFileMarker,
                        timeoutMs: 25000);

                    Assert.True(restored, "Expected Steam.dll to be restored from .sge backup after the game process exited.");
                }
                finally
                {
                    try { Directory.Delete(gameFolder, recursive: true); } catch { }
                }
            }
        }

        [Fact]
        public void LaunchGame_standard_mode_restores_original_steam_api_after_exit()
        {
            using (var harness = new LaunchDeployTestHarness())
            {
                string gameFolder = TestFileHelper.CreateTempDirectory("sge-game-standard-");
                try
                {
                    harness.CreateGameInstall(gameFolder, out string executablePath);
                    string steamApiPath = Path.Combine(gameFolder, PathConstants.GoldbergStandardSteamApiDll64);
                    string backupPath = steamApiPath + PathConstants.SteamApiBackupSidecarExtension;
                    harness.WriteMarkerFile(steamApiPath, LaunchDeployTestHarness.OriginalFileMarker);
                    harness.StageGoldbergExperimental(useX64: true);

                    var game = harness.CreateGameConfig(
                        LaunchDeployTestHarness.DefaultTestAppId,
                        gameFolder,
                        executablePath,
                        GoldbergLaunchMode.StandardSteamApi);

                    var result = harness.LaunchService.LaunchGame(game, useEmulator: true);
                    Assert.True(result.IsValid, result.ErrorMessage);

                    bool restored = LaunchDeployTestHarness.WaitUntil(
                        () => File.Exists(steamApiPath)
                            && !File.Exists(backupPath)
                            && harness.ReadMarkerFile(steamApiPath) == LaunchDeployTestHarness.OriginalFileMarker
                            && !File.Exists(Path.Combine(gameFolder, PathConstants.GoldbergSteamClientDll64)),
                        timeoutMs: 25000);

                    Assert.True(restored, "Expected experimental deploy files to be restored or removed after the game process exited.");
                }
                finally
                {
                    try { Directory.Delete(gameFolder, recursive: true); } catch { }
                }
            }
        }

        [Fact]
        public void LaunchGame_standard_mode_succeeds_when_process_exits_immediately()
        {
            using (var harness = new LaunchDeployTestHarness())
            {
                string gameFolder = TestFileHelper.CreateTempDirectory("sge-game-fast-exit-");
                try
                {
                    harness.CreateGameInstall(gameFolder, out string executablePath);
                    harness.StageGoldbergExperimental(useX64: true);

                    var game = harness.CreateGameConfig(
                        LaunchDeployTestHarness.DefaultTestAppId,
                        gameFolder,
                        executablePath,
                        GoldbergLaunchMode.StandardSteamApi,
                        parameters: "/c exit");

                    var result = harness.LaunchService.LaunchGame(game, useEmulator: true);
                    Assert.True(result.IsValid, result.ErrorMessage);

                    string steamApiPath = Path.Combine(gameFolder, PathConstants.GoldbergStandardSteamApiDll64);
                    string steamClientPath = Path.Combine(gameFolder, PathConstants.GoldbergSteamClientDll64);
                    bool cleaned = LaunchDeployTestHarness.WaitUntil(
                        () => !File.Exists(steamApiPath) && !File.Exists(steamClientPath),
                        timeoutMs: 15000);

                    Assert.True(cleaned, "Expected immediate-exit launch to clean experimental deploy files.");
                }
                finally
                {
                    try { Directory.Delete(gameFolder, recursive: true); } catch { }
                }
            }
        }

        [Fact]
        public void LaunchGame_stages_load_dlls_and_removes_folder_after_exit()
        {
            using (var harness = new LaunchDeployTestHarness())
            {
                string gameFolder = TestFileHelper.CreateTempDirectory("sge-game-loaddlls-");
                try
                {
                    harness.CreateGameInstall(gameFolder, out string executablePath);
                    harness.StageGoldbergSteamDll();
                    harness.StageGoldbergExtraDll(useX64: true);

                    string loadDllsFolder = PathConstants.CombineGameSteamSettingsLoadDllsDirectory(LaunchDeployTestHarness.DefaultTestAppId);
                    string stagedExtraDll = Path.Combine(loadDllsFolder, "plugin_x64.dll");

                    var game = harness.CreateGameConfig(
                        LaunchDeployTestHarness.DefaultTestAppId,
                        gameFolder,
                        executablePath,
                        GoldbergLaunchMode.SteamDllBesideExe);

                    var result = harness.LaunchService.LaunchGame(game, useEmulator: true);
                    Assert.True(result.IsValid, result.ErrorMessage);

                    Assert.True(File.Exists(stagedExtraDll));
                    Assert.Equal(LaunchDeployTestHarness.StagedExtraDllMarker, harness.ReadMarkerFile(stagedExtraDll));

                    bool cleaned = LaunchDeployTestHarness.WaitUntil(
                        () => !Directory.Exists(loadDllsFolder),
                        timeoutMs: 25000);

                    Assert.True(cleaned, "Expected load_dlls folder to be removed after the game process exited.");
                }
                finally
                {
                    try { Directory.Delete(gameFolder, recursive: true); } catch { }
                }
            }
        }
    }
}
