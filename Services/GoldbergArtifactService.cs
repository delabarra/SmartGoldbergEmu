using System;
using System.Threading.Tasks;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Extensions;
using SmartGoldbergEmu.Generators;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Services
{
    // Single entry point for on-demand achievements/items generation (menu, add-save, metadata).
    public class GoldbergArtifactService
    {
        private readonly SteamApiKeyService _steamApiKeyService;
        private readonly EmulatorConfigService _emulatorConfigService;
        private readonly GoldbergFilesService _goldbergFilesService;

        public GoldbergArtifactService()
            : this(
                ServiceLocator.SteamApiKeyService,
                ServiceLocator.EmulatorConfigService,
                ServiceLocator.GoldbergFilesService)
        {
        }

        public GoldbergArtifactService(
            SteamApiKeyService steamApiKeyService,
            EmulatorConfigService emulatorConfigService,
            GoldbergFilesService goldbergFilesService)
        {
            _steamApiKeyService = steamApiKeyService ?? throw new ArgumentNullException(nameof(steamApiKeyService));
            _emulatorConfigService = emulatorConfigService ?? throw new ArgumentNullException(nameof(emulatorConfigService));
            _goldbergFilesService = goldbergFilesService ?? throw new ArgumentNullException(nameof(goldbergFilesService));
        }

        public async Task GenerateAchievementsFromMenuAsync(GameConfig game, ITaskReportService report)
        {
            if (game == null || game.AppId == 0)
                return;

            var achievementService = CreateAchievementService(report, game.AppId);
            await achievementService.GenerateAchievementsAsync(game, showProgress: report != null).ConfigureAwait(false);
        }

        public async Task GenerateAchievementsForAddSaveAsync(
            GameConfig game,
            AchievementPreviewKind previewKind,
            ITaskReportService report,
            bool showProgress = true,
            bool progressOnlyNoMessages = false)
        {
            if (game == null || game.AppId == 0)
                return;

            var achievementService = CreateAchievementService(report, game.AppId);
            await achievementService.GenerateAchievementsForAddSaveAsync(
                game,
                previewKind,
                showProgress,
                progressOnlyNoMessages).ConfigureAwait(false);
        }

        public async Task<(AchievementPreviewKind kind, string previewJson)> BuildAddModeAchievementPreviewAsync(GameConfig game)
        {
            var achievementService = CreateAchievementService(null, game?.AppId ?? 0);
            return await achievementService.BuildAddModePreviewAsync(game).ConfigureAwait(false);
        }

        public async Task<ItemGeneratorResult> GenerateItemsFromMenuAsync(GameConfig game, ITaskReportService report)
        {
            if (game == null || game.AppId == 0)
                return ItemGeneratorResult.Fail("Invalid game or App ID.");

            var generator = CreateItemGenerator(report);
            return await generator.GenerateAndSaveAsync(game, showProgress: report != null).ConfigureAwait(false);
        }

        public async Task<ItemGeneratorResult> GenerateItemsForAddSaveAsync(
            GameConfig game,
            ITaskReportService report,
            bool showProgress,
            bool friendlyProgressMessages = true)
        {
            if (game == null || game.AppId == 0)
                return ItemGeneratorResult.Fail("Invalid game or App ID.");

            if (!_steamApiKeyService.TryGetValidFormatKey(out _) || !_goldbergFilesService.ShouldAutoGenerateItems(game.AppId))
                return ItemGeneratorResult.Fail("Skipped.");

            var generator = CreateItemGenerator(report);
            return await generator.GenerateAndSaveAsync(
                game,
                showProgress,
                friendlyProgressMessages).ConfigureAwait(false);
        }

        public void TryGenerateItemsOnlineIfMissing(GameConfig gameConfig)
        {
            if (gameConfig == null || gameConfig.AppId == 0)
                return;

            if (!_goldbergFilesService.ShouldAutoGenerateItems(gameConfig.AppId))
                return;

            _ = Task.Run(async () =>
            {
                try
                {
                    await GenerateItemsFromMenuAsync(gameConfig, null).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    ServiceLocator.LogService?.LogError(
                        $"Failed to auto-generate {PathConstants.GoldbergItemsJsonFileName} for app {gameConfig.AppId}",
                        ex);
                }
            }).ForgetFaults(ServiceLocator.LogService, nameof(TryGenerateItemsOnlineIfMissing));
        }

        private AchievementService CreateAchievementService(ITaskReportService report, ulong appId)
        {
            return new AchievementService(report, _steamApiKeyService, GetAchievementLanguage(appId));
        }

        private ItemGenerator CreateItemGenerator(ITaskReportService report)
        {
            return new ItemGenerator(report, _steamApiKeyService);
        }

        private string GetAchievementLanguage(ulong appId)
        {
            return _emulatorConfigService?.GetLanguageForAchievements(appId) ?? "english";
        }
    }
}
