using System;
using System.IO;
using System.Threading.Tasks;
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

                assetsDownloaded = await DownloadAddModeLibraryAssetsAsync(request).ConfigureAwait(false);
                if (assetsDownloaded)
                    TryRunSuccessfulSaveCallback(request.OnAssetsDownloaded);

                await RunAddModeGoldbergWorkAsync(request, skipLibraryAssets: true).ConfigureAwait(false);

                await RunAddGameAchievementsGenerationAsync(request.GameConfig, taskReport).ConfigureAwait(false);

                taskReport?.SetProgress(0, 0);
                taskReport?.SetMessageWithAutoClear(
                    AddGameStatusMessages.AddedToLibrary(displayName),
                    delayMs: AddGameStatusMessages.StatusAutoClearDelayMs);
            }
            catch (Exception ex)
            {
                LogErrorWithExceptionMessage("Error creating game files", ex);
                taskReport?.SetMessage(ErrorDisplayHelper.SanitizeForUser("Creating game files", ex), TaskReportKind.Error);
            }

            if (request.GameConfig.AppId == 0)
                return GameSettingsSaveResult.Success();

            GameSettingsSaveResult settingsResult = await SaveEmulatorSettingsAsync(request).ConfigureAwait(false);
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

        public async Task<bool> DownloadAddModeLibraryAssetsAsync(GameSettingsSaveRequest request)
        {
            if (request?.GameConfig == null || request.GameConfig.AppId == 0)
                return false;

            string displayName = GetLibraryGameDisplayName(request.GameConfig);
            try
            {
                return await DownloadTileAndGameImagesAsync(
                    request.GameConfig.AppId,
                    request.Metadata,
                    request.TaskReportService,
                    request.GameConfig.AppPicsKeyValue,
                    displayName).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogWarningWithExceptionMessage("Failed to download game images", ex);
                return false;
            }
        }

        public async Task RunAddModeGoldbergWorkAsync(GameSettingsSaveRequest request, bool skipLibraryAssets)
        {
            if (request?.GameConfig == null || request.GameConfig.AppId == 0)
                return;

            ITaskReportService taskReport = request.SuppressStatusMessages ? null : request.TaskReportService;
            try
            {
                if (!request.SuppressStatusMessages)
                    taskReport?.SetProgress(0, 0);

                if (!skipLibraryAssets)
                    await TryExportSteamProductInfoVdfAsync(request.GameConfig, taskReport).ConfigureAwait(false);

                await RunAddGameConfigGenerationAsync(request).ConfigureAwait(false);
                await RunAddGameItemsGenerationAsync(request).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogErrorWithExceptionMessage("Error creating game files", ex);
                if (!request.SuppressStatusMessages)
                    request.TaskReportService?.SetMessage(ErrorDisplayHelper.SanitizeForUser("Creating game files", ex), TaskReportKind.Error);
            }
        }

        public async Task<GameSettingsSaveResult> SaveEmulatorSettingsFromRequestAsync(GameSettingsSaveRequest request)
        {
            return await SaveEmulatorSettingsAsync(request).ConfigureAwait(false);
        }

        private async Task RunAddGameConfigGenerationAsync(GameSettingsSaveRequest request)
        {
            try
            {
                ITaskReportService taskReport = request.SuppressStatusMessages ? null : request.TaskReportService;
                _emulatorConfigService.CreateDefaultConfigFiles(request.GameConfig.AppId);
                await TryExportSteamProductInfoVdfAsync(request.GameConfig, taskReport).ConfigureAwait(false);

                await Task.Yield();
                await _emulatorConfigService.GenerateMetadataFilesAsync(request.GameConfig, request.Metadata).ConfigureAwait(false);
                TryEnsureSteamAppIdBesideExecutable(request.GameConfig);
            }
            catch (Exception ex)
            {
                LogWarningWithExceptionMessage("Failed to generate metadata files", ex);
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
                ITaskReportService itemReport = request.SuppressStatusMessages ? null : request.TaskReportService;
                ItemGeneratorResult itemGenResult = await ServiceLocator.GoldbergArtifactService
                    .GenerateItemsForAddSaveAsync(
                        request.GameConfig,
                        itemReport,
                        showProgress: !request.SuppressStatusMessages,
                        friendlyProgressMessages: true)
                    .ConfigureAwait(false);
                if (!itemGenResult.Success && itemGenResult.ErrorMessage != "Skipped.")
                    Program.LogService?.LogMessage("Item definitions not created for new game: " + itemGenResult.ErrorMessage);
            }
            catch (Exception ex)
            {
                LogWarningWithExceptionMessage("Item generation on add game failed", ex);
            }
        }

        private async Task<bool> DownloadTileAndGameImagesAsync(
            ulong appId,
            OnlineAppData metadata,
            ITaskReportService taskReport,
            SteamKit.KeyValue appPicsData = null,
            string gameDisplayName = null,
            bool reportFeedback = false)
        {
            try
            {
                return await _gameImageService.DownloadGameImagesAsync(
                    appId,
                    metadata,
                    reportFeedback: reportFeedback,
                    steamAppIdForRemoteAssets: null,
                    appPicsData: appPicsData,
                    gameDisplayName: gameDisplayName).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogWarningWithExceptionMessage("Failed to download game images", ex);
                return false;
            }
        }

        private async Task<GameSettingsSaveResult> SaveEmulatorSettingsAsync(GameSettingsSaveRequest request)
        {
            ulong appId = request.GameConfig.AppId;
            try
            {
                GameSettingsSnapshot snapshot = request.BuildSnapshot();
                request.ResolveAchievementLanguage(snapshot);

                if (!request.IsEditMode)
                    _emulatorConfigService.SaveAllGameSettings(appId, snapshot);
                else
                    _emulatorConfigService.SaveModifiedGameSettings(appId, snapshot);

                request.SaveDlcAndPaths?.Invoke();

                if (request.IsEditMode || !string.IsNullOrWhiteSpace(request.CustomStatsRawJson))
                {
                    SaveResult saveResult = _goldbergFilesService.SaveStats(appId, request.CustomStatsRawJson ?? string.Empty);
                    if (!saveResult.IsSuccess)
                    {
                        LogSaveResultWarning(saveResult, $"Failed to save {PathConstants.GoldbergStatsJsonFileName}");
                        return GameSettingsSaveResult.InvalidCustomStatsJson();
                    }
                }

                request.SaveAdditionalGoldbergFiles?.Invoke();
            }
            catch (Exception ex)
            {
                LogWarningWithExceptionMessage("Failed to save emulator settings", ex);
            }

            return GameSettingsSaveResult.Success();
        }

        private async Task TryExportSteamProductInfoVdfAsync(GameConfig gameConfig, ITaskReportService taskReport = null)
        {
            if (gameConfig == null || gameConfig.AppId == 0)
                return;

            try
            {
                string appIdText = gameConfig.AppId.ToString();
                taskReport?.SetMessage("Fetching game assets...");
                var picsData = await _steamProductInfoService.WarmGameConfigAppPicsRootAsync(gameConfig).ConfigureAwait(false);
                if (picsData == null)
                {
                    taskReport?.SetMessage("Game assets unavailable.", TaskReportKind.Warning);
                    return;
                }

                taskReport?.SetMessage("Exporting game assets...");
                bool exported = _steamProductInfoService.ExportAppPicsToValveTextFile(appIdText, picsData);
                if (exported)
                    taskReport?.SetMessage("Game assets exported.");
                else
                {
                    taskReport?.SetMessage("Game assets export skipped.", TaskReportKind.Warning);
                    Program.LogService?.LogWarning($"Game assets export skipped for app {gameConfig.AppId}.");
                }
            }
            catch (Exception ex)
            {
                taskReport?.SetMessage("Game assets export failed.", TaskReportKind.Warning);
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
