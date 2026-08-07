using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Extensions;
using SmartGoldbergEmu.Generators;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Services
{
    // Single entry point for on-demand achievements/items generation (menu, add-save, metadata) and catalog refresh.
    public class GoldbergArtifactService
    {
        private static readonly string[] RefreshableResourceImageExtensions =
        {
            ".jpg", ".jpeg", ".png", ".ico", ".gif", ".webp"
        };

        private readonly SteamApiKeyService _steamApiKeyService;
        private readonly EmulatorConfigService _emulatorConfigService;
        private readonly GoldbergFilesService _goldbergFilesService;
        private readonly AppDataKitBridgeService _appDataKitBridgeService;
        private readonly SteamProductInfoService _steamProductInfoService;
        private readonly GameImageService _gameImageService;
        private readonly GameDataService _gameDataService;

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
            : this(
                steamApiKeyService,
                emulatorConfigService,
                goldbergFilesService,
                ServiceLocator.AppDataKitBridgeService,
                ServiceLocator.SteamProductInfoService,
                ServiceLocator.GameImageService,
                ServiceLocator.GameDataService)
        {
        }

        public GoldbergArtifactService(
            SteamApiKeyService steamApiKeyService,
            EmulatorConfigService emulatorConfigService,
            GoldbergFilesService goldbergFilesService,
            AppDataKitBridgeService appDataKitBridgeService,
            SteamProductInfoService steamProductInfoService,
            GameImageService gameImageService,
            GameDataService gameDataService)
        {
            _steamApiKeyService = steamApiKeyService ?? throw new ArgumentNullException(nameof(steamApiKeyService));
            _emulatorConfigService = emulatorConfigService ?? throw new ArgumentNullException(nameof(emulatorConfigService));
            _goldbergFilesService = goldbergFilesService ?? throw new ArgumentNullException(nameof(goldbergFilesService));
            _appDataKitBridgeService = appDataKitBridgeService ?? throw new ArgumentNullException(nameof(appDataKitBridgeService));
            _steamProductInfoService = steamProductInfoService ?? throw new ArgumentNullException(nameof(steamProductInfoService));
            _gameImageService = gameImageService ?? throw new ArgumentNullException(nameof(gameImageService));
            _gameDataService = gameDataService ?? throw new ArgumentNullException(nameof(gameDataService));
        }

        public async Task GenerateAchievementsFromMenuAsync(GameConfig game, ITaskReportService report)
        {
            if (game == null || game.AppId == 0)
                return;

            var achievementService = CreateAchievementService(report, game.AppId);
            await achievementService.GenerateAchievementsAsync(game, showProgress: report != null).ConfigureAwait(false);
            await TryPatchCatalogAchievementsAsync(game).ConfigureAwait(false);
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
            ItemGeneratorResult result = await generator.GenerateAndSaveAsync(game, showProgress: report != null).ConfigureAwait(false);
            if (result.Success)
                await TryPatchCatalogItemsAsync(game).ConfigureAwait(false);
            return result;
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

        // Goldberg → Refresh game data and assets: live catalog re-fetch, kit sidecars, and library art.
        // Never touches Path/Parameters/WorkingDirectory/CustomIcon, user.launch.options.ini, or on-disk DLC user list.
        public async Task RefreshGameCatalogAndAssetsAsync(GameConfig game, ITaskReportService report)
        {
            if (game == null || game.AppId == 0)
                return;

            report?.SetMessage("Refreshing game data and assets...");

            AppCatalogSnapshot snapshot = await _appDataKitBridgeService
                .FetchFullSnapshotAsync(game.AppId, report)
                .ConfigureAwait(false);

            if (snapshot == null || !snapshot.IsUsable)
            {
                report?.SetMessage("Could not refresh game data.", TaskReportKind.Error);
                return;
            }

            string previousName = game.AppName;

            if (!string.IsNullOrWhiteSpace(snapshot.Online?.Name))
                game.AppName = snapshot.Online.Name.Trim();

            // Preserve a user-set language list; only fill when the game has none yet.
            if ((game.SupportedLanguages == null || game.SupportedLanguages.Count == 0)
                && !string.IsNullOrWhiteSpace(snapshot.Online?.SupportedLanguages))
            {
                game.SupportedLanguages = GameSetupService.ConvertSteamLanguageStringToCodes(snapshot.Online.SupportedLanguages);
            }

            game.Catalog = snapshot;
            game.AppInfo = snapshot.AppInfo;
            game.PreFetchedDlcData = snapshot.ToDlcDictionary();
            game.DlcCheckPerformed = true;

            AppCatalogSnapshotStore.TrySave(snapshot);

            if (snapshot.AppInfo != null)
                _steamProductInfoService.ExportAppPicsToValveTextFile(game.AppId.ToString(), snapshot.AppInfo);

            try
            {
                await _emulatorConfigService.GenerateMetadataFilesAsync(game, snapshot.Online).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService?.LogWarning($"Failed to regenerate metadata files for app {game.AppId}: {ex.Message}");
            }

            await GenerateAchievementsFromMenuAsync(game, report).ConfigureAwait(false);
            await GenerateItemsFromMenuAsync(game, report).ConfigureAwait(false);

            report?.SetMessage("Refreshing game images...");
            DeleteExistingResourceImages(game.AppId);
            await _gameImageService.DownloadGameImagesAsync(
                game.AppId,
                snapshot.Online,
                reportFeedback: report != null,
                appPicsData: snapshot.AppInfo,
                gameDisplayName: game.AppName).ConfigureAwait(false);

            if (!string.Equals(previousName, game.AppName, StringComparison.Ordinal) && game.GameGuid != Guid.Empty)
            {
                var updateResult = _gameDataService.UpdateGame(game);
                if (!updateResult.IsValid)
                    ServiceLocator.LogService?.LogWarning($"Failed to persist refreshed name for app {game.AppId}: {updateResult.ErrorMessage}");
            }

            report?.SetMessageWithAutoClear("Game data and assets refreshed.");
        }

        // Menu-driven achievement/item generation already wrote the Goldberg sidecar; patch the catalog JSON section too so it stays current.
        private async Task TryPatchCatalogAchievementsAsync(GameConfig game)
        {
            if (game == null || game.AppId == 0)
                return;

            try
            {
                AppCatalogSnapshot catalog = game.Catalog;
                if (catalog == null)
                    AppCatalogSnapshotStore.TryLoad(game.AppId, out catalog);
                if (catalog == null)
                    return;

                catalog.Achievements = await _appDataKitBridgeService
                    .FetchAchievementsAsync(game.AppId, GetAchievementLanguage(game.AppId))
                    .ConfigureAwait(false);
                game.Catalog = catalog;
                AppCatalogSnapshotStore.TrySave(catalog);
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService?.LogWarning($"Failed to patch catalog achievements for app {game.AppId}: {ex.Message}");
            }
        }

        private async Task TryPatchCatalogItemsAsync(GameConfig game)
        {
            if (game == null || game.AppId == 0)
                return;

            try
            {
                AppCatalogSnapshot catalog = game.Catalog;
                if (catalog == null)
                    AppCatalogSnapshotStore.TryLoad(game.AppId, out catalog);
                if (catalog == null)
                    return;

                catalog.Items = await _appDataKitBridgeService.FetchItemsAsync(game.AppId).ConfigureAwait(false);
                game.Catalog = catalog;
                AppCatalogSnapshotStore.TrySave(catalog);
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService?.LogWarning($"Failed to patch catalog items for app {game.AppId}: {ex.Message}");
            }
        }

        // DownloadGameImagesAsync skips files that already exist; clear stale art first so a refresh actually re-downloads it.
        private static void DeleteExistingResourceImages(ulong appId)
        {
            string resourcesDirectory = PathConstants.CombineGamesPerAppResourcesDirectory(PathConstants.GamesDirectory, appId.ToString());
            if (!Directory.Exists(resourcesDirectory))
                return;

            try
            {
                foreach (string file in Directory.GetFiles(resourcesDirectory))
                {
                    string extension = Path.GetExtension(file);
                    if (!RefreshableResourceImageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                        continue;

                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception ex)
                    {
                        ServiceLocator.LogService?.LogWarning($"Failed to delete stale resource image '{file}': {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService?.LogWarning($"Failed to enumerate resource images for app {appId}: {ex.Message}");
            }
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
