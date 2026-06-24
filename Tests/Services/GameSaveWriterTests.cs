using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;
using SmartGoldbergEmu.Tests.Fakes;
using SmartGoldbergEmu.Tests.TestSupport;
using Xunit;

namespace SmartGoldbergEmu.Tests.Services
{
    public sealed class GameSaveWriterTests
    {
        private const ulong TestAppId = 480;

        [Fact]
        public async Task SaveEditAsync_returns_failure_when_form_request_missing()
        {
            var writer = CreateWriter(out _);
            GameSettingsSaveResult result = await writer.SaveEditAsync(new GameSaveEditRequest
            {
                GameConfig = CreateSampleGame(),
                FormSaveRequest = null
            });

            Assert.False(result.IsSuccess);
            Assert.Contains("Missing", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SaveEditAsync_returns_invalid_custom_stats_error_from_save_service()
        {
            var writer = CreateWriter(out SaveTestContext ctx);
            var snapshot = ctx.EmulatorConfig.LoadGameSettingsSnapshot(TestAppId);
            var formRequest = BuildFormRequest(ctx, snapshot, customStatsRawJson: "{ bad");

            GameSettingsSaveResult result = await writer.SaveEditAsync(new GameSaveEditRequest
            {
                GameConfig = CreateSampleGame(),
                InitialGameConfig = CreateSampleGame(),
                FormSaveRequest = formRequest
            });

            Assert.False(result.IsSuccess);
            Assert.True(result.HasCustomStatsJsonError);
        }

        [Fact]
        public async Task SaveEditAsync_succeeds_and_writes_emulator_settings()
        {
            var writer = CreateWriter(out SaveTestContext ctx);
            var snapshot = ctx.EmulatorConfig.LoadGameSettingsSnapshot(TestAppId);
            snapshot.Main.Offline = true;
            var formRequest = BuildFormRequest(ctx, snapshot, customStatsRawJson: string.Empty);

            GameSettingsSaveResult result = await writer.SaveEditAsync(new GameSaveEditRequest
            {
                GameConfig = CreateSampleGame(),
                InitialGameConfig = CreateSampleGame(),
                FormSaveRequest = formRequest
            });

            Assert.True(result.IsSuccess, result.ErrorMessage);
            string mainPath = Path.Combine(ctx.SteamSettingsPath, PathConstants.GoldbergMainIniFileName);
            Assert.True(File.Exists(mainPath));
            Assert.Contains("offline=1", File.ReadAllText(mainPath), StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SaveEditAsync_updates_library_when_identity_changed()
        {
            var writer = CreateWriter(out SaveTestContext ctx);
            var game = CreateSampleGame();
            Assert.True(ctx.GameData.AddGame(game).IsValid);

            var snapshot = ctx.EmulatorConfig.LoadGameSettingsSnapshot(TestAppId);
            var formRequest = BuildFormRequest(ctx, snapshot, customStatsRawJson: string.Empty);
            game.AppName = "Renamed Game";

            GameSettingsSaveResult result = await writer.SaveEditAsync(new GameSaveEditRequest
            {
                GameConfig = game,
                InitialGameConfig = CreateSampleGame(),
                FormSaveRequest = formRequest
            });

            Assert.True(result.IsSuccess, result.ErrorMessage);
            List<GameConfig> library = ctx.GameData.LoadGameLibrary();
            Assert.Single(library);
            Assert.Equal("Renamed Game", library[0].AppName);
        }

        private static GameSaveWriter CreateWriter(out SaveTestContext ctx)
        {
            ctx = CreateContext();
            return new GameSaveWriter(
                ctx.GameData,
                ctx.SaveService,
                new GameImageService(),
                ctx.EmulatorConfig);
        }

        private static SaveTestContext CreateContext()
        {
            string gamesRoot = TestFileHelper.CreateTempDirectory("sge-save-writer-games-");
            string globalRoot = TestFileHelper.CreateTempDirectory("sge-save-writer-global-");
            var cfgService = new GoldbergCfgService(globalRoot);
            var emulatorConfig = new EmulatorConfigService(gamesRoot, globalRoot, cfgService);
            var goldbergFiles = new GoldbergFilesService(gamesRoot);
            var saveService = new GameSettingsSaveService(
                emulatorConfig,
                new FakeRegistryService(),
                goldbergFiles,
                new GameImageService(),
                new SteamProductInfoService());

            return new SaveTestContext(gamesRoot, globalRoot, new GameDataService(gamesRoot), emulatorConfig, saveService);
        }

        private static GameSettingsSaveRequest BuildFormRequest(
            SaveTestContext ctx,
            GameSettingsSnapshot snapshot,
            string customStatsRawJson)
        {
            return new GameSettingsSaveRequest
            {
                GameConfig = CreateSampleGame(),
                IsEditMode = true,
                CustomStatsRawJson = customStatsRawJson,
                BuildSnapshot = () => snapshot,
                ResolveAchievementLanguage = s => s?.User?.Language ?? ApplicationConstants.DefaultLanguage,
                SaveDlcAndPaths = () => { },
                SaveAdditionalGoldbergFiles = () => { },
            };
        }

        private static GameConfig CreateSampleGame()
        {
            return new GameConfig
            {
                AppId = TestAppId,
                AppName = "Spacewar",
                Path = @"C:\Games\Spacewar\spacewar.exe",
                StartFolder = @"C:\Games\Spacewar",
                Parameters = string.Empty,
                WorkingDirectory = @"C:\Games\Spacewar",
                GameGuid = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                LaunchMode = GoldbergLaunchMode.SteamClient
            };
        }

        private sealed class SaveTestContext : IDisposable
        {
            public string GamesRoot { get; }
            public GameDataService GameData { get; }
            public EmulatorConfigService EmulatorConfig { get; }
            public GameSettingsSaveService SaveService { get; }
            public string SteamSettingsPath { get; }
            private readonly string _globalRoot;

            public SaveTestContext(
                string gamesRoot,
                string globalRoot,
                GameDataService gameData,
                EmulatorConfigService emulatorConfig,
                GameSettingsSaveService saveService)
            {
                GamesRoot = gamesRoot;
                _globalRoot = globalRoot;
                GameData = gameData;
                EmulatorConfig = emulatorConfig;
                SaveService = saveService;
                SteamSettingsPath = Path.Combine(gamesRoot, TestAppId.ToString(), PathConstants.SteamSettingsFolderName);
            }

            public void Dispose()
            {
                TryDeleteDirectory(GamesRoot);
                TryDeleteDirectory(_globalRoot);
            }

            private static void TryDeleteDirectory(string path)
            {
                try
                {
                    if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                        Directory.Delete(path, recursive: true);
                }
                catch
                {
                }
            }
        }
    }
}
