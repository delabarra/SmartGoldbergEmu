using System;
using System.Collections.Generic;
using System.IO;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;
using SmartGoldbergEmu.Tests.Fakes;
using SmartGoldbergEmu.Tests.TestSupport;
using Xunit;

namespace SmartGoldbergEmu.Tests.Services
{
    [Collection("GameLaunch")]
    public sealed class GameLaunchServicePrerequisiteTests
    {
        private readonly List<string> _stagedPaths = new List<string>();

        [Fact]
        public void ValidateEmulatorFilesPrerequisite_succeeds_for_steam_client_mode_when_steamclient_files_exist()
        {
            StageSteamClientFiles();
            try
            {
                var service = CreateService();
                var game = new GameConfig
                {
                    AppId = 480,
                    LaunchMode = GoldbergLaunchMode.SteamClient,
                };

                var result = service.ValidateEmulatorFilesPrerequisite(game, requireLaunchModeBinaries: true);
                Assert.True(result.IsValid);
            }
            finally
            {
                CleanupStaged();
            }
        }

        [Fact]
        public void ValidateEmulatorFilesPrerequisite_fails_for_regular_mode_when_experimental_files_missing()
        {
            StageSteamClientFiles();
            try
            {
                var service = CreateService();
                var game = new GameConfig
                {
                    AppId = 480,
                    LaunchMode = GoldbergLaunchMode.StandardSteamApi,
                };

                var result = service.ValidateEmulatorFilesPrerequisite(game, requireLaunchModeBinaries: true);
                Assert.False(result.IsValid);
                Assert.Contains("experimental", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            }
            finally
            {
                CleanupStaged();
            }
        }

        [Fact]
        public void ValidateEmulatorFilesPrerequisite_without_emulator_only_checks_steam_client_files()
        {
            StageSteamClientFiles();
            try
            {
                var service = CreateService();
                var game = new GameConfig
                {
                    AppId = 480,
                    LaunchMode = GoldbergLaunchMode.StandardSteamApi,
                };

                var result = service.ValidateEmulatorFilesPrerequisite(game, requireLaunchModeBinaries: false);
                Assert.True(result.IsValid);
            }
            finally
            {
                CleanupStaged();
            }
        }

        [Fact]
        public void ResolveAvailableLaunchMode_returns_first_available_when_preferred_is_missing()
        {
            StageSteamDllOnly();
            try
            {
                var service = CreateService();
                var game = new GameConfig
                {
                    AppId = 480,
                    LaunchMode = GoldbergLaunchMode.StandardSteamApi,
                };

                Assert.Equal(GoldbergLaunchMode.SteamDllBesideExe, service.ResolveAvailableLaunchMode(game));
            }
            finally
            {
                CleanupStaged();
            }
        }

        private static GameLaunchService CreateService()
        {
            string gamesRoot = TestFileHelper.CreateTempDirectory("sge-games-");
            return new GameLaunchService(
                new NullLogService(),
                new EmulatorConfigService(gamesRoot, Path.Combine(gamesRoot, "global.cfg")));
        }

        private void StageSteamClientFiles()
        {
            GoldbergTestLayoutHelper.StageSteamClientGoldbergFiles();
            Track(
                PathConstants.CombineGoldbergSteamClientDllPath(false),
                PathConstants.CombineGoldbergSteamClientDllPath(true),
                PathConstants.CombineGoldbergGameOverlayRendererPath(false),
                PathConstants.CombineGoldbergGameOverlayRendererPath(true));
        }

        private void StageSteamDllOnly()
        {
            GoldbergTestLayoutHelper.StageSteamDllInGoldbergFolder();
            Track(PathConstants.CombineGoldbergSteamDllPath());
        }

        private void Track(params string[] paths)
        {
            foreach (string path in paths)
                _stagedPaths.Add(path);
        }

        private void CleanupStaged()
        {
            GoldbergTestLayoutHelper.CleanupStagedFiles(_stagedPaths);
            _stagedPaths.Clear();
        }
    }
}
