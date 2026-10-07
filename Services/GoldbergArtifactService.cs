using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AppDataKit;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Generators;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Services
{
    // Single entry point for on-demand achievements/items generation (menu, add-save, import) and catalog refresh.
    public class GoldbergArtifactService
    {
        private static readonly string[] RefreshableResourceImageExtensions =
        {
            ".jpg", ".jpeg", ".png", ".ico", ".gif", ".webp", ".bmp", ".tga", ".icns"
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

            await GenerateAchievementsAndPatchCatalogAsync(game, report, catalogAchievements: null).ConfigureAwait(false);
            game.ReleaseHeavyRuntimeData();
        }

        public async Task GenerateAchievementsForAddSaveAsync(
            GameConfig game,
            AchievementPreviewKind previewKind,
            AddGamePrefetchedSchemas prefetchedSchemas,
            ITaskReportService report,
            bool showProgress = true,
            bool progressOnlyNoMessages = false)
        {
            if (game == null || game.AppId == 0)
                return;

            // Achievements are generated after the form settings are saved, so this is the language the player picked.
            string language = GetAchievementLanguage(game.AppId);
            var achievementService = new AchievementService(report, language);
            await achievementService.GenerateAchievementsForAddSaveAsync(
                game,
                previewKind,
                UnlessErrored(prefetchedSchemas?.GetAchievementsFor(game.AppId, language)),
                showProgress,
                progressOnlyNoMessages).ConfigureAwait(false);
        }

        // collectCatalog: full snapshot from add-game collect; its achievement section is reused when the language allows.
        public async Task<AchievementAddModePreview> BuildAddModeAchievementPreviewAsync(GameConfig game, AppCatalogSnapshot collectCatalog)
        {
            ulong appId = game?.AppId ?? 0;
            string language = GetAchievementLanguage(appId);
            AchievementsSection catalogAchievements = collectCatalog != null && collectCatalog.AppId == appId
                ? collectCatalog.Achievements
                : null;

            var achievementService = new AchievementService(null, language);
            return await achievementService
                .BuildAddModePreviewAsync(game, GetReusableCatalogAchievements(catalogAchievements, language))
                .ConfigureAwait(false);
        }

        public async Task<ItemGeneratorResult> GenerateItemsFromMenuAsync(GameConfig game, ITaskReportService report)
        {
            if (game == null || game.AppId == 0)
                return ItemGeneratorResult.Fail("Invalid game or App ID.");

            ItemGeneratorResult result = await GenerateItemsAndPatchCatalogAsync(game, report, catalogItems: null).ConfigureAwait(false);
            game.ReleaseHeavyRuntimeData();
            return result;
        }

        public async Task<ItemGeneratorResult> GenerateItemsForAddSaveAsync(
            GameConfig game,
            AddGamePrefetchedSchemas prefetchedSchemas,
            ITaskReportService report,
            bool showProgress,
            bool friendlyProgressMessages = true)
        {
            if (game == null || game.AppId == 0)
                return ItemGeneratorResult.Fail("Invalid game or App ID.");

            if (!_goldbergFilesService.ShouldAutoGenerateItems(game.AppId))
                return ItemGeneratorResult.Fail("Skipped.");

            var generator = CreateItemGenerator(report);
            return await generator.GenerateAndSaveAsync(
                game,
                UnlessErrored(prefetchedSchemas?.GetItemsFor(game.AppId)),
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

            await GenerateAchievementsAndPatchCatalogAsync(game, report, snapshot.Achievements).ConfigureAwait(false);
            await GenerateItemsAndPatchCatalogAsync(game, report, snapshot.Items).ConfigureAwait(false);

            report?.SetMessage("Refreshing game images...");
            DeleteExistingResourceImages(game.AppId);
            await _gameImageService.DownloadGameImagesAsync(
                game.AppId,
                snapshot.Online,
                reportFeedback: report != null,
                appPicsData: snapshot.AppInfo,
                gameDisplayName: game.AppName,
                catalogAssets: snapshot.Assets).ConfigureAwait(false);

            if (!string.Equals(previousName, game.AppName, StringComparison.Ordinal) && game.GameGuid != Guid.Empty)
            {
                var updateResult = _gameDataService.UpdateGame(game);
                if (!updateResult.IsValid)
                    ServiceLocator.LogService?.LogWarning($"Failed to persist refreshed name for app {game.AppId}: {updateResult.ErrorMessage}");
            }

            report?.SetMessageWithAutoClear("Game data and assets refreshed.");

            // ListView Tag must not keep the full catalog/AppInfo tree after disk write.
            game.ReleaseHeavyRuntimeData();
        }

        // catalogAchievements: section from a catalog already fetched in this operation (null fetches live).
        // When it is reused the catalog already holds it; otherwise the freshly fetched section is patched into the catalog JSON.
        private async Task GenerateAchievementsAndPatchCatalogAsync(
            GameConfig game,
            ITaskReportService report,
            AchievementsSection catalogAchievements)
        {
            string language = GetAchievementLanguage(game.AppId);
            AchievementsSection reusable = GetReusableCatalogAchievements(catalogAchievements, language);

            var achievementService = new AchievementService(report, language);
            AchievementsSection used = await achievementService
                .GenerateAchievementsAsync(game, showProgress: report != null, prefetchedSchema: reusable)
                .ConfigureAwait(false);

            if (reusable == null)
                TryPatchCatalog(game, catalog => catalog.Achievements = used, "achievements");
        }

        private async Task<ItemGeneratorResult> GenerateItemsAndPatchCatalogAsync(
            GameConfig game,
            ITaskReportService report,
            ItemsSection catalogItems)
        {
            ItemsSection reusable = UnlessErrored(catalogItems);

            var generator = CreateItemGenerator(report);
            ItemGeneratorResult result = await generator
                .GenerateAndSaveAsync(game, reusable, showProgress: report != null)
                .ConfigureAwait(false);

            if (result.Success && reusable == null)
                TryPatchCatalog(game, catalog => catalog.Items = result.Section, "items");
            return result;
        }

        // Generation already wrote the Goldberg sidecar; keep the catalog JSON section in step with it.
        private static void TryPatchCatalog(GameConfig game, Action<AppCatalogSnapshot> patch, string sectionName)
        {
            try
            {
                AppCatalogSnapshot catalog = game.Catalog;
                if (catalog == null)
                    AppCatalogSnapshotStore.TryLoad(game.AppId, out catalog);
                if (catalog == null)
                    return;

                patch(catalog);
                game.Catalog = catalog;
                AppCatalogSnapshotStore.TrySave(catalog);
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService?.LogWarning($"Failed to patch catalog {sectionName} for app {game.AppId}: {ex.Message}");
            }
        }

        // Catalog snapshots fetch the achievement schema in the kit default language; names and descriptions are localized.
        private static AchievementsSection GetReusableCatalogAchievements(AchievementsSection catalogAchievements, string language)
        {
            if (!string.Equals(language, ApplicationConstants.DefaultLanguage, StringComparison.OrdinalIgnoreCase))
                return null;
            return UnlessErrored(catalogAchievements);
        }

        // An Error section may be transient (timeout, HTTP failure), so fetch again instead of writing from it.
        private static T UnlessErrored<T>(T section) where T : SnapshotSection
        {
            return section == null || section.Status == SnapshotSectionStatus.Error ? null : section;
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

        private ItemGenerator CreateItemGenerator(ITaskReportService report)
        {
            return new ItemGenerator(report);
        }

        private string GetAchievementLanguage(ulong appId)
        {
            return _emulatorConfigService?.GetLanguageForAchievements(appId) ?? ApplicationConstants.DefaultLanguage;
        }
    }
}
