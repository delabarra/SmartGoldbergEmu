using System;
using System.IO;
using System.Threading.Tasks;
using AppDataKit;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Services
{
    public class GameSettingsSaveService
    {
        private readonly EmulatorConfigService _emulatorConfigService;
        private readonly IRegistryService _registryService;
        private readonly GoldbergFilesService _goldbergFilesService;
        private readonly GameImageService _gameImageService;
        private readonly SteamProductInfoService _steamProductInfoService;

        public GameSettingsSaveService()
            : this(
                ServiceLocator.EmulatorConfigService,
                ServiceLocator.RegistryService,
                ServiceLocator.GoldbergFilesService,
                ServiceLocator.GameImageService,
                ServiceLocator.SteamProductInfoService)
        {
        }

        public GameSettingsSaveService(
            EmulatorConfigService emulatorConfigService,
            IRegistryService registryService,
            GoldbergFilesService goldbergFilesService,
            GameImageService gameImageService,
            SteamProductInfoService steamProductInfoService)
        {
            _emulatorConfigService = emulatorConfigService ?? throw new ArgumentNullException(nameof(emulatorConfigService));
            _registryService = registryService ?? throw new ArgumentNullException(nameof(registryService));
            _goldbergFilesService = goldbergFilesService ?? throw new ArgumentNullException(nameof(goldbergFilesService));
            _gameImageService = gameImageService ?? throw new ArgumentNullException(nameof(gameImageService));
            _steamProductInfoService = steamProductInfoService ?? throw new ArgumentNullException(nameof(steamProductInfoService));
        }

        // Background Goldberg provisioning after 2.x import (game already in games.ini); same pipeline as add-save without the settings form.
        public async Task<GameSettingsSaveResult> GenerateNewGameFilesAsync(
            GameConfig gameConfig,
            OnlineAppData metadata = null,
            ITaskReportService report = null,
            Action onAssetsDownloaded = null,
            Action onCompleted = null)
        {
            if (gameConfig == null || gameConfig.AppId == 0)
                return GameSettingsSaveResult.Failure("App ID is required to generate game files.");

            GameSettingsSnapshot snapshot = _emulatorConfigService.LoadGameSettingsSnapshot(gameConfig.AppId, mergePerGameSteamSettings: false);

            var request = new GameSettingsSaveRequest
            {
                GameConfig = gameConfig,
                IsEditMode = false,
                Metadata = metadata,
                TaskReportService = report,
                BuildSnapshot = () => snapshot,
                ResolveAchievementLanguage = s =>
                {
                    if (!string.IsNullOrEmpty(s?.User?.Language))
                        return s.User.Language;
                    return _emulatorConfigService.GetLanguageForAchievements(gameConfig.AppId);
                },
                SaveDlcAndPaths = () =>
                {
                    _goldbergFilesService.SaveAppConfigDlcAndPaths(gameConfig.AppId, gameConfig.PreFetchedDlcData, null);
                },
                SaveAdditionalGoldbergFiles = () => { },
                OnAssetsDownloaded = onAssetsDownloaded,
                OnSuccessfulSaveCompleted = onCompleted ?? onAssetsDownloaded
            };

            return await ProvisionImportedGameAsync(request).ConfigureAwait(false);
        }

        public async Task<GameSettingsSaveResult> ProvisionImportedGameAsync(GameSettingsSaveRequest request)
        {
            if (request?.GameConfig == null)
                return GameSettingsSaveResult.Failure("No game configuration loaded.");
            if (request.BuildSnapshot == null || request.ResolveAchievementLanguage == null)
                return GameSettingsSaveResult.Failure("Missing game settings save delegates.");

            ITaskReportService taskReport = request.TaskReportService;
            string displayName = GetLibraryGameDisplayName(request.GameConfig);
            bool assetsDownloaded = false;

            try
            {
                taskReport?.SetProgress(0, 0);

                SaveAddModeCatalogSnapshot(request);
                assetsDownloaded = await DownloadAddModeLibraryImagesAsync(request).ConfigureAwait(false);
                if (assetsDownloaded)
                    TryRunSuccessfulSaveCallback(request.OnAssetsDownloaded);

                await RunAddModeGoldbergWorkAsync(request).ConfigureAwait(false);

                await RunAddGameAchievementsGenerationAsync(request.GameConfig, taskReport).ConfigureAwait(false);

                taskReport?.SetProgress(0, 0);
                taskReport?.SetMessageWithAutoClear(AddGameStatusMessages.AddedToLibrary(displayName));
            }
            catch (Exception ex)
            {
                LogErrorWithExceptionMessage("Error creating game files", ex);
                taskReport?.SetMessage(ErrorDisplayHelper.SanitizeForUser("Creating game files", ex), TaskReportKind.Error);
            }

            if (request.GameConfig.AppId == 0)
                return GameSettingsSaveResult.Success();

            // Runs after the terminal "added" message; step text here would stick on the strip.
            GameSettingsSaveResult settingsResult = await SaveEmulatorSettingsAsync(request, taskReport: null).ConfigureAwait(false);
            if (!settingsResult.IsSuccess)
                return settingsResult;

            if (!assetsDownloaded)
                TryRunSuccessfulSaveCallback(request.OnSuccessfulSaveCompleted);
            return GameSettingsSaveResult.Success();
        }

        private static string GetLibraryGameDisplayName(GameConfig gameConfig)
        {
            if (gameConfig == null || string.IsNullOrWhiteSpace(gameConfig.AppName))
                return "Game";
            return gameConfig.AppName.Trim();
        }

        // Run before the image download and RunAddModeGoldbergWorkAsync: both reload AppInfo/assets from this snapshot when memory was cleared.
        public void SaveAddModeCatalogSnapshot(GameSettingsSaveRequest request)
        {
            GameConfig gameConfig = request?.GameConfig;
            if (gameConfig == null || gameConfig.AppId == 0)
                return;

            try
            {
                if (gameConfig.AppInfo == null && gameConfig.Catalog?.AppInfo != null)
                    gameConfig.AppInfo = gameConfig.Catalog.AppInfo;

                if (gameConfig.Catalog != null)
                {
                    request.TaskReportService?.SetMessage(
                        AddGameStatusMessages.SavingCatalogSnapshot(GetLibraryGameDisplayName(gameConfig)));
                    AppCatalogSnapshotStore.TrySave(gameConfig.Catalog);
                }
            }
            catch (Exception ex)
            {
                LogWarningWithExceptionMessage("Failed to save catalog snapshot", ex);
            }
        }

        // onImageProgress (completed, total) replaces the image service's own strip messages; never throws.
        public async Task<bool> DownloadAddModeLibraryImagesAsync(
            GameSettingsSaveRequest request,
            Action<int, int> onImageProgress = null)
        {
            GameConfig gameConfig = request?.GameConfig;
            if (gameConfig == null || gameConfig.AppId == 0)
                return false;

            try
            {
                return await _gameImageService.DownloadGameImagesAsync(
                    gameConfig.AppId,
                    request.Metadata,
                    reportFeedback: onImageProgress == null && request.TaskReportService != null,
                    steamAppIdForRemoteAssets: null,
                    appPicsData: gameConfig.AppInfo,
                    gameDisplayName: GetLibraryGameDisplayName(gameConfig),
                    catalogAssets: gameConfig.Catalog?.Assets,
                    onProgress: onImageProgress).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogWarningWithExceptionMessage("Failed to download game images", ex);
                return false;
            }
        }

        // Run SaveAddModeCatalogSnapshot first: this work relies on the persisted catalog snapshot.
        public async Task RunAddModeGoldbergWorkAsync(GameSettingsSaveRequest request)
        {
            if (request?.GameConfig == null || request.GameConfig.AppId == 0)
                return;

            try
            {
                request.TaskReportService?.SetProgress(0, 0);

                await RunAddGameConfigGenerationAsync(request).ConfigureAwait(false);
                await RunAddGameItemsGenerationAsync(request).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogErrorWithExceptionMessage("Error creating game files", ex);
                request.TaskReportService?.SetMessage(ErrorDisplayHelper.SanitizeForUser("Creating game files", ex), TaskReportKind.Error);
            }
        }

        public async Task<GameSettingsSaveResult> SaveEmulatorSettingsFromRequestAsync(GameSettingsSaveRequest request)
        {
            ITaskReportService taskReport = request?.IsEditMode == false ? request.TaskReportService : null;
            return await SaveEmulatorSettingsAsync(request, taskReport).ConfigureAwait(false);
        }

        private async Task RunAddGameConfigGenerationAsync(GameSettingsSaveRequest request)
        {
            try
            {
                ITaskReportService taskReport = request.TaskReportService;
                string displayName = GetLibraryGameDisplayName(request.GameConfig);

                taskReport?.SetMessage(AddGameStatusMessages.CreatingDefaultConfigFiles(displayName));
                _emulatorConfigService.CreateDefaultConfigFiles(request.GameConfig.AppId);
                await TryExportSteamProductInfoVdfAsync(request.GameConfig, taskReport).ConfigureAwait(false);

                await Task.Yield();
                taskReport?.SetMessage(AddGameStatusMessages.GeneratingMetadataFiles(displayName));
                await _emulatorConfigService.GenerateMetadataFilesAsync(request.GameConfig, request.Metadata).ConfigureAwait(false);
                taskReport?.SetMessage(AddGameStatusMessages.WritingSteamAppIdFile(displayName));
                TryEnsureSteamAppIdBesideExecutable(request.GameConfig);
                Program.LogService?.LogMessage(
                    $"Emulator files for AppId {request.GameConfig?.AppId} generated.");
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError(
                    $"Emulator files for AppId {request.GameConfig?.AppId} failed: {ex.Message}",
                    ex);
            }
        }

        private async Task RunAddGameAchievementsGenerationAsync(GameConfig gameConfig, ITaskReportService taskReport)
        {
            if (gameConfig == null || gameConfig.AppId == 0)
                return;

            try
            {
                await ServiceLocator.GoldbergArtifactService.GenerateAchievementsForAddSaveAsync(
                    gameConfig,
                    AchievementPreviewKind.RealList,
                    prefetchedSchemas: null,
                    taskReport,
                    showProgress: true).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogWarningWithExceptionMessage("Failed to generate achievements for new game", ex);
            }
        }

        private async Task RunAddGameItemsGenerationAsync(GameSettingsSaveRequest request)
        {
            try
            {
                ItemGeneratorResult itemGenResult = await ServiceLocator.GoldbergArtifactService
                    .GenerateItemsForAddSaveAsync(
                        request.GameConfig,
                        request.PrefetchedSchemas,
                        request.TaskReportService,
                        showProgress: request.TaskReportService != null,
                        friendlyProgressMessages: true)
                    .ConfigureAwait(false);
                if (!itemGenResult.Success && itemGenResult.ErrorMessage != "Skipped.")
                {
                    // No inventory definitions is common; keep detail for real failures.
                    if (string.Equals(itemGenResult.ErrorMessage, "No items found.", StringComparison.Ordinal))
                        Program.LogService?.LogDebug("Item definitions not created for new game: No items found.");
                    else
                        Program.LogService?.LogWarning(
                            "Item definitions not created for new game: " + itemGenResult.ErrorMessage);
                }
                // Archive JSON is on disk in the catalog file / items.json; drop the in-memory copy.
                if (request.GameConfig?.Catalog?.Items != null)
                    request.GameConfig.Catalog.Items.ArchiveJson = null;
            }
            catch (Exception ex)
            {
                LogWarningWithExceptionMessage("Item generation on add game failed", ex);
            }
        }

        private async Task<GameSettingsSaveResult> SaveEmulatorSettingsAsync(GameSettingsSaveRequest request, ITaskReportService taskReport)
        {
            ulong appId = request.GameConfig.AppId;
            string displayName = GetLibraryGameDisplayName(request.GameConfig);
            // Keep saving the remaining files after a settings write failure, then report it.
            GameSettingsSaveResult deferredFailure = null;
            try
            {
                taskReport?.SetMessage(AddGameStatusMessages.SavingEmulatorSettings(displayName));
                GameSettingsSnapshot snapshot = request.BuildSnapshot();
                request.ResolveAchievementLanguage(snapshot);

                SaveResult settingsSave = !request.IsEditMode
                    ? _emulatorConfigService.SaveAllGameSettings(appId, snapshot)
                    : _emulatorConfigService.SaveModifiedGameSettings(appId, snapshot);
                if (settingsSave != null && !settingsSave.IsSuccess)
                {
                    LogSaveResultWarning(settingsSave, "Failed to save emulator settings");
                    deferredFailure = GameSettingsSaveResult.Failure(
                        string.IsNullOrWhiteSpace(settingsSave.ErrorMessage)
                            ? "Failed to save emulator settings."
                            : settingsSave.ErrorMessage);
                }

                if (request.SaveDlcAndPaths != null)
                {
                    taskReport?.SetMessage(AddGameStatusMessages.SavingDlcAndPaths(displayName));
                    request.SaveDlcAndPaths();
                }

                if (request.IsEditMode || !string.IsNullOrWhiteSpace(request.CustomStatsRawJson))
                {
                    taskReport?.SetMessage(AddGameStatusMessages.SavingCustomStats(displayName));
                    SaveResult saveResult = _goldbergFilesService.SaveStats(appId, request.CustomStatsRawJson ?? string.Empty);
                    if (!saveResult.IsSuccess)
                    {
                        LogSaveResultWarning(saveResult, $"Failed to save {PathConstants.GoldbergStatsJsonFileName}");
                        return GameSettingsSaveResult.InvalidCustomStatsJson();
                    }
                }

                if (request.SaveAdditionalGoldbergFiles != null)
                {
                    taskReport?.SetMessage(AddGameStatusMessages.SavingAdditionalFiles(displayName));
                    request.SaveAdditionalGoldbergFiles();
                }
            }
            catch (Exception ex)
            {
                LogWarningWithExceptionMessage("Failed to save emulator settings", ex);
                return GameSettingsSaveResult.Failure(ErrorDisplayHelper.SanitizeForUser("Saving emulator settings", ex));
            }

            return deferredFailure ?? GameSettingsSaveResult.Success();
        }

        private async Task TryExportSteamProductInfoVdfAsync(GameConfig gameConfig, ITaskReportService taskReport = null)
        {
            if (gameConfig == null || gameConfig.AppId == 0)
                return;

            try
            {
                string appIdText = gameConfig.AppId.ToString();

                // A root from setup/edit load (AppInfo or the catalog snapshot) makes the PICS re-warm unnecessary.
                AppInfoKeyValue picsData = gameConfig.AppInfo ?? gameConfig.Catalog?.AppInfo;
                if (picsData != null)
                {
                    gameConfig.AppInfo = picsData;
                }
                else
                {
                    taskReport?.SetMessage("Fetching app info from Steam…");
                    picsData = await _steamProductInfoService.WarmGameConfigAppInfoAsync(gameConfig).ConfigureAwait(false);
                }

                if (picsData == null)
                {
                    taskReport?.SetMessage("App info unavailable; .vdf export skipped.", TaskReportKind.Warning);
                    return;
                }

                taskReport?.SetMessage("Exporting app info (.vdf)…");
                bool exported = _steamProductInfoService.ExportAppPicsToValveTextFile(appIdText, picsData);
                if (exported)
                    taskReport?.SetMessageWithAutoClear("App info exported.");
                else
                {
                    taskReport?.SetMessage("App info export skipped.", TaskReportKind.Warning);
                    Program.LogService?.LogWarning($"Game assets export skipped for app {gameConfig.AppId}.");
                }
            }
            catch (Exception ex)
            {
                taskReport?.SetMessage("App info export failed.", TaskReportKind.Warning);
                Program.LogService?.LogWarning($"Failed to export game assets .vdf for app {gameConfig.AppId}: {ex.Message}");
            }
        }

        private static void LogWarningWithDetail(string prefix, string detail)
        {
            Program.LogService?.LogWarning(detail != null ? $"{prefix}: {detail}" : prefix);
        }

        private void TryEnsureSteamAppIdBesideExecutable(GameConfig gameConfig)
        {
            if (gameConfig == null || gameConfig.AppId == 0)
                return;

            ValidationResult result = _emulatorConfigService.TryEnsureSteamAppIdBesideExecutable(gameConfig);
            if (!result.IsValid)
                LogWarningWithDetail($"Skipped {PathConstants.SteamAppIdFileName} beside executable", result.ErrorMessage);
        }

        private static void LogWarningWithExceptionMessage(string prefix, Exception ex)
        {
            Program.LogService?.LogWarning(prefix + ": " + ex.Message);
        }

        private static void LogErrorWithExceptionMessage(string prefix, Exception ex)
        {
            Program.LogService?.LogError(prefix + ": " + ex.Message, ex);
        }

        private static void LogSaveResultWarning(SaveResult saveResult, string fallbackPrefix = null)
        {
            if (saveResult == null)
                return;

            string message = !string.IsNullOrEmpty(saveResult.ErrorMessage)
                ? saveResult.ErrorMessage
                : (fallbackPrefix ?? "Save operation warning");
            Program.LogService?.LogWarning(message);
        }

        private static void TryRunSuccessfulSaveCallback(Action callback)
        {
            if (callback == null)
                return;

            try
            {
                callback();
            }
            catch (Exception ex)
            {
                LogWarningWithExceptionMessage("Post-save callback failed", ex);
            }
        }
    }
}
