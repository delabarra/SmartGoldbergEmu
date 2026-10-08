using System.IO;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
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
        public void LaunchGame_standard_mode_keeps_existing_sge_original_over_stale_goldberg_target()
        {
            const byte staleGoldbergMarker = 0xD4;
            using (var harness = new LaunchDeployTestHarness())
            {
                string gameFolder = TestFileHelper.CreateTempDirectory("sge-game-stale-sge-");
                try
                {
                    harness.CreateGameInstall(gameFolder, out string executablePath);
                    string steamApiPath = Path.Combine(gameFolder, PathConstants.GoldbergStandardSteamApiDll64);
                    string backupPath = steamApiPath + PathConstants.SteamApiBackupSidecarExtension;
                    harness.WriteMarkerFile(backupPath, LaunchDeployTestHarness.OriginalFileMarker);
                    harness.WriteMarkerFile(steamApiPath, staleGoldbergMarker);
                    harness.StageGoldbergExperimental(useX64: true);

                    var game = harness.CreateGameConfig(
                        LaunchDeployTestHarness.DefaultTestAppId,
                        gameFolder,
                        executablePath,
                        GoldbergLaunchMode.StandardSteamApi);

                    var result = harness.LaunchService.LaunchGame(game, useEmulator: true);
                    Assert.True(result.IsValid, result.ErrorMessage);
                    Assert.Equal(LaunchDeployTestHarness.OriginalFileMarker, harness.ReadMarkerFile(backupPath));

                    bool restored = LaunchDeployTestHarness.WaitUntil(
                        () => File.Exists(steamApiPath)
                            && !File.Exists(backupPath)
                            && harness.ReadMarkerFile(steamApiPath) == LaunchDeployTestHarness.OriginalFileMarker,
                        timeoutMs: 25000);

                    Assert.True(restored, "Expected the pre-existing .sge original to be restored, not the stale Goldberg copy.");
                }
                finally
                {
                    try { Directory.Delete(gameFolder, recursive: true); } catch { }
                }
            }
        }

        [Fact]
        public void LaunchGame_steam_dll_mode_moves_game_steamclient_aside_and_restores_it()
        {
            using (var harness = new LaunchDeployTestHarness())
            {
                string gameFolder = TestFileHelper.CreateTempDirectory("sge-game-stray-client-");
                try
                {
                    harness.CreateGameInstall(gameFolder, out string executablePath);
                    string steamClientPath = Path.Combine(gameFolder, PathConstants.GoldbergSteamClientDll64);
                    string backupPath = steamClientPath + PathConstants.SteamApiBackupSidecarExtension;
                    harness.WriteMarkerFile(steamClientPath, LaunchDeployTestHarness.OriginalFileMarker);
                    harness.StageGoldbergSteamDll();

                    var game = harness.CreateGameConfig(
                        LaunchDeployTestHarness.DefaultTestAppId,
                        gameFolder,
                        executablePath,
                        GoldbergLaunchMode.SteamDllBesideExe);

                    var result = harness.LaunchService.LaunchGame(game, useEmulator: true);
                    Assert.True(result.IsValid, result.ErrorMessage);
                    Assert.False(File.Exists(steamClientPath), "Expected the game's steamclient to be moved aside during the session.");
                    Assert.True(File.Exists(backupPath));

                    bool restored = LaunchDeployTestHarness.WaitUntil(
                        () => File.Exists(steamClientPath)
                            && !File.Exists(backupPath)
                            && harness.ReadMarkerFile(steamClientPath) == LaunchDeployTestHarness.OriginalFileMarker,
                        timeoutMs: 25000);

                    Assert.True(restored, "Expected the game's steamclient to be restored after the game process exited.");
                }
                finally
                {
                    try { Directory.Delete(gameFolder, recursive: true); } catch { }
                }
            }
        }

        [Fact]
        public void LaunchGame_standard_mode_restores_game_owned_steam_settings_folder()
        {
            using (var harness = new LaunchDeployTestHarness())
            {
                string gameFolder = TestFileHelper.CreateTempDirectory("sge-game-own-settings-");
                try
                {
                    harness.CreateGameInstall(gameFolder, out string executablePath);
                    string settingsPath = Path.Combine(gameFolder, PathConstants.SteamSettingsFolderName);
                    string settingsBackupPath = settingsPath + PathConstants.SteamApiBackupSidecarExtension;
                    string customFile = Path.Combine(settingsPath, "custom.txt");
                    harness.WriteMarkerFile(customFile, LaunchDeployTestHarness.OriginalFileMarker);
                    harness.WriteMarkerFile(
                        Path.Combine(gameFolder, PathConstants.GoldbergStandardSteamApiDll64),
                        LaunchDeployTestHarness.OriginalFileMarker);
                    harness.StageGoldbergExperimental(useX64: true);

                    var game = harness.CreateGameConfig(
                        LaunchDeployTestHarness.DefaultTestAppId,
                        gameFolder,
                        executablePath,
                        GoldbergLaunchMode.StandardSteamApi);

                    var result = harness.LaunchService.LaunchGame(game, useEmulator: true);
                    Assert.True(result.IsValid, result.ErrorMessage);
                    Assert.True(File.Exists(Path.Combine(settingsBackupPath, "custom.txt")));

                    bool restored = LaunchDeployTestHarness.WaitUntil(
                        () => File.Exists(customFile)
                            && !DirectoryJunctionHelper.IsDirectoryReparsePoint(settingsPath)
                            && !Directory.Exists(settingsBackupPath),
                        timeoutMs: 25000);

                    Assert.True(restored, "Expected the game's own steam_settings folder to be restored after the game process exited.");
                    Assert.Equal(LaunchDeployTestHarness.OriginalFileMarker, harness.ReadMarkerFile(customFile));
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

        [Fact]
        public void LaunchGame_standard_mode_restores_deploy_when_process_fails_to_start()
        {
            using (var harness = new LaunchDeployTestHarness())
            {
                string gameFolder = TestFileHelper.CreateTempDirectory("sge-game-start-fail-");
                try
                {
                    string executablePath = Path.Combine(gameFolder, "testgame.exe");
                    Directory.CreateDirectory(gameFolder);
                    // Valid path for pre-launch checks, but not a runnable PE image.
                    File.WriteAllBytes(executablePath, new byte[] { 0x00, 0x01, 0x02, 0x03 });

                    string steamApiPath = Path.Combine(gameFolder, PathConstants.GoldbergStandardSteamApiDll64);
                    string backupPath = steamApiPath + PathConstants.SteamApiBackupSidecarExtension;
                    harness.WriteMarkerFile(steamApiPath, LaunchDeployTestHarness.OriginalFileMarker);
                    harness.StageGoldbergExperimental(useX64: true);
                    harness.StageGoldbergExtraDll(useX64: true);

                    string loadDllsFolder = PathConstants.CombineGameSteamSettingsLoadDllsDirectory(
                        LaunchDeployTestHarness.DefaultTestAppId);

                    var game = harness.CreateGameConfig(
                        LaunchDeployTestHarness.DefaultTestAppId,
                        gameFolder,
                        executablePath,
                        GoldbergLaunchMode.StandardSteamApi,
                        parameters: string.Empty);

                    var result = harness.LaunchService.LaunchGame(game, useEmulator: true);
                    Assert.False(result.IsValid, "Expected launch to fail for a non-runnable executable.");

                    Assert.True(
                        File.Exists(steamApiPath)
                        && !File.Exists(backupPath)
                        && harness.ReadMarkerFile(steamApiPath) == LaunchDeployTestHarness.OriginalFileMarker,
                        "Expected experimental deploy files to be restored after Process.Start failure.");
                    Assert.False(
                        File.Exists(Path.Combine(gameFolder, PathConstants.GoldbergSteamClientDll64)),
                        "Expected deployed steamclient to be removed after Process.Start failure.");
                    Assert.False(
                        Directory.Exists(loadDllsFolder),
                        "Expected load_dlls staging to be removed after Process.Start failure.");
                }
                finally
                {
                    try { Directory.Delete(gameFolder, recursive: true); } catch { }
                }
            }
        }
    }
}
