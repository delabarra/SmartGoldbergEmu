using System;
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
    public sealed class GoldbergArtifactServiceTests
    {
        private const string ValidKey = "0123456789ABCDEF0123456789ABCDEF";
        private const ulong TestAppId = 480;

        [Fact]
        public async Task BuildAddModeAchievementPreviewAsync_returns_no_api_key_when_app_id_zero()
        {
            var service = CreateService(out _);
            (AchievementPreviewKind kind, string previewJson) = await service.BuildAddModeAchievementPreviewAsync(
                new GameConfig { AppId = 0 });

            Assert.Equal(AchievementPreviewKind.NoApiKey, kind);
            Assert.False(string.IsNullOrEmpty(previewJson));
        }

        [Fact]
        public async Task GenerateItemsForAddSaveAsync_skips_when_api_key_invalid()
        {
            var service = CreateService(out _, apiKey: string.Empty);
            var game = new GameConfig { AppId = TestAppId, AppName = "Spacewar" };

            ItemGeneratorResult result = await service.GenerateItemsForAddSaveAsync(
                game,
                report: null,
                showProgress: false);

            Assert.False(result.Success);
            Assert.Equal("Skipped.", result.ErrorMessage);
        }

        [Fact]
        public async Task GenerateItemsForAddSaveAsync_skips_when_items_json_already_populated()
        {
            string gamesRoot = TestFileHelper.CreateTempDirectory("sge-artifact-games-");
            try
            {
                string steamSettings = PathConstants.CombineGameSteamSettingsDirectory(gamesRoot, TestAppId.ToString());
                Directory.CreateDirectory(steamSettings);
                File.WriteAllText(
                    Path.Combine(steamSettings, PathConstants.GoldbergItemsJsonFileName),
                    "{\"1\":{\"itemdefid\":1}}");

                var service = CreateService(out _, gamesRoot: gamesRoot, apiKey: ValidKey);
                var game = new GameConfig { AppId = TestAppId, AppName = "Spacewar" };

                ItemGeneratorResult result = await service.GenerateItemsForAddSaveAsync(
                    game,
                    report: null,
                    showProgress: false);

                Assert.False(result.Success);
                Assert.Equal("Skipped.", result.ErrorMessage);
            }
            finally
            {
                try { Directory.Delete(gamesRoot, recursive: true); } catch { }
            }
        }

        [Fact]
        public async Task GenerateAchievementsFromMenuAsync_noops_when_game_null()
        {
            var service = CreateService(out _, apiKey: ValidKey);
            await service.GenerateAchievementsFromMenuAsync(null, report: null);
        }

        private static GoldbergArtifactService CreateService(
            out FakeRegistryService registry,
            string gamesRoot = null,
            string apiKey = ValidKey)
        {
            registry = new FakeRegistryService();
            if (!string.IsNullOrEmpty(apiKey))
                registry.SetSteamApiKey(apiKey);

            var apiKeyService = new SteamApiKeyService(registry);
            var filesService = new GoldbergFilesService(gamesRoot);
            var emulatorConfig = new EmulatorConfigService();
            return new GoldbergArtifactService(apiKeyService, emulatorConfig, filesService);
        }
    }
}
