using System;
using System.Threading.Tasks;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Services
{
    public class GameSaveWriter
    {
        private readonly GameDataService _gameDataService;
        private readonly GameSettingsSaveService _gameSettingsSaveService;
        private readonly GameImageService _gameImageService;
        private readonly EmulatorConfigService _emulatorConfigService;

        public GameSaveWriter()
            : this(
                ServiceLocator.GameDataService,
                ServiceLocator.GameSettingsSaveService,
                ServiceLocator.GameImageService,
                ServiceLocator.EmulatorConfigService)
        {
        }

        public GameSaveWriter(
            GameDataService gameDataService,
            GameSettingsSaveService gameSettingsSaveService,
            GameImageService gameImageService,
            EmulatorConfigService emulatorConfigService)
        {
            _gameDataService = gameDataService ?? throw new ArgumentNullException(nameof(gameDataService));
            _gameSettingsSaveService = gameSettingsSaveService ?? throw new ArgumentNullException(nameof(gameSettingsSaveService));
            _gameImageService = gameImageService ?? throw new ArgumentNullException(nameof(gameImageService));
            _emulatorConfigService = emulatorConfigService ?? throw new ArgumentNullException(nameof(emulatorConfigService));
        }

        public async Task<GameSettingsSaveResult> SaveAddAsync(GameSaveAddRequest request)
        {
            if (request?.GameConfig == null)
                return GameSettingsSaveResult.Failure("No game configuration loaded.");
            if (request.FormSaveRequest == null)
                return GameSettingsSaveResult.Failure("Missing game settings save delegates.");

            GameConfig gameConfig = request.GameConfig;
            GameSettingsSaveRequest formRequest = request.FormSaveRequest;
            formRequest.GameConfig = gameConfig;
            formRequest.IsEditMode = false;
            formRequest.Metadata = request.Metadata;

            ITaskReportService stripReport = request.TaskReportService ?? ServiceLocator.TaskReportService;
            AddSaveTaskReport taskReport = stripReport != null ? new AddSaveTaskReport(stripReport) : null;
            formRequest.TaskReportService = taskReport;
            string displayName = GetLibraryGameDisplayName(gameConfig);

            // Immediate strip feedback before the library write.
            taskReport?.SetMessage(
                request.IsUpdateOfExisting
                    ? AddGameStatusMessages.UpdatingInLibrary(displayName)
                    : AddGameStatusMessages.AddingToLibrary(displayName));
            taskReport?.SetProgress(0, 0);

            // Callers await on the UI thread; everything up to the first real await blocks painting the strip message above.
            bool isUpdate = request.IsUpdateOfExisting;
            ValidationResult libraryResult = await Task.Run(() => isUpdate
                ? _gameDataService.UpdateGame(gameConfig)
                : _gameDataService.AddGame(gameConfig)).ConfigureAwait(false);
            if (!libraryResult.IsValid)
                return GameSettingsSaveResult.Failure(libraryResult.ErrorMessage);

            // Promote the in-memory list row as soon as games.ini is committed (MainForm marshals to the UI thread).
            TryRunCallback(request.OnSuccessfulSaveCompleted);

            _gameSettingsSaveService.SaveAddModeCatalogSnapshot(formRequest);

            // Library images only feed the list/mosaic; Goldberg files do not wait for them.
            Task<bool> imagesTask = Task.Run(() => DownloadLibraryImagesAsync(request, formRequest, taskReport));
            bool assetsDownloaded;
            GameSettingsSaveResult filesResult;
            try
            {
                filesResult = await GenerateAddModeFilesAsync(request, formRequest, taskReport, displayName).ConfigureAwait(false);
            }
            finally
            {
                // Never return with the download still running: callers release the draft graphs it reads.
                if (!imagesTask.IsCompleted)
                    taskReport?.SetMessage(AddGameStatusMessages.WaitingForLibraryImages(displayName));
                assetsDownloaded = await imagesTask.ConfigureAwait(false);
            }

            // A new add is already in games.ini, so swap its waiting tile for library art even when file generation failed.
            // Failed updates restore their draft instead and never show the waiting tile.
            if (!filesResult.IsSuccess)
            {
                if (!assetsDownloaded && !request.IsUpdateOfExisting)
                    TryRunCallback(request.OnAssetsDownloaded);
                return filesResult;
            }

            taskReport?.SetMessageWithAutoClear(
                request.IsUpdateOfExisting
                    ? AddGameStatusMessages.UpdatedInLibrary(displayName)
                    : AddGameStatusMessages.AddedToLibrary(displayName));

            if (!assetsDownloaded)
                TryRunCallback(request.OnAssetsDownloaded);

            // Catalog/AppInfo are on disk (and list Tag is thin); drop the draft graphs so add/remove cycles do not retain them.
            gameConfig.ReleaseHeavyRuntimeData();
            formRequest.Metadata = null;
            formRequest.PrefetchedSchemas = null;

            return GameSettingsSaveResult.Success();
        }

        private async Task<GameSettingsSaveResult> GenerateAddModeFilesAsync(
            GameSaveAddRequest request,
            GameSettingsSaveRequest formRequest,
            ITaskReportService taskReport,
            string displayName)
        {
            GameConfig gameConfig = request.GameConfig;

            taskReport?.SetMessage(AddGameStatusMessages.GeneratingGoldbergFiles(displayName));
            taskReport?.SetProgress(0, 0);

            await _gameSettingsSaveService.RunAddModeGoldbergWorkAsync(formRequest).ConfigureAwait(false);

            GameSettingsSaveResult settingsResult = await _gameSettingsSaveService.SaveEmulatorSettingsFromRequestAsync(formRequest).ConfigureAwait(false);
            if (!settingsResult.IsSuccess)
                return settingsResult;

            taskReport?.SetMessage(AddGameStatusMessages.GeneratingAchievements(displayName));
            try
            {
                await RunAddAchievementSaveAsync(gameConfig, request.AchievementPreview, formRequest).ConfigureAwait(false);
            }
            finally
            {
                taskReport?.SetProgress(0, 0);
            }

            if (request.CredentialsTouched)
            {
                taskReport?.SetMessage(AddGameStatusMessages.SavingCredentials(displayName));
                PersistCredentialsFromForm(formRequest, gameConfig.AppId);
            }

            return GameSettingsSaveResult.Success();
        }

        // Never throws: the image download and the callback both swallow and log their failures.
        private async Task<bool> DownloadLibraryImagesAsync(
            GameSaveAddRequest request,
            GameSettingsSaveRequest formRequest,
            AddSaveTaskReport taskReport)
        {
            Action<int, int> onImageProgress = null;
            if (taskReport != null)
                onImageProgress = taskReport.ReportImageProgress;

            bool downloaded = await _gameSettingsSaveService
                .DownloadAddModeLibraryImagesAsync(formRequest, onImageProgress)
                .ConfigureAwait(false);
            taskReport?.EndImageProgress();
            if (downloaded)
                TryRunCallback(request.OnAssetsDownloaded);
            return downloaded;
        }

        public Task<GameSettingsSaveResult> SaveEditAsync(GameSaveEditRequest request)
        {
            if (request?.GameConfig == null)
                return Task.FromResult(GameSettingsSaveResult.Failure("No game configuration loaded."));
            if (request.FormSaveRequest == null)
                return Task.FromResult(GameSettingsSaveResult.Failure("Missing game settings save delegates."));

            GameConfig gameConfig = request.GameConfig;
            GameSettingsSaveRequest formRequest = request.FormSaveRequest;
            formRequest.GameConfig = gameConfig;
            formRequest.IsEditMode = true;
            formRequest.TaskReportService = request.FormSaveRequest.TaskReportService;

            if (LibraryIdentityChanged(request.InitialGameConfig, gameConfig))
            {
                ValidationResult updateResult = _gameDataService.UpdateGame(gameConfig);
                if (!updateResult.IsValid)
                    return Task.FromResult(GameSettingsSaveResult.Failure(updateResult.ErrorMessage));
            }

            return SaveEditCoreAsync(request, gameConfig, formRequest);
        }

        private async Task<GameSettingsSaveResult> SaveEditCoreAsync(
            GameSaveEditRequest request,
            GameConfig gameConfig,
            GameSettingsSaveRequest formRequest)
        {
            GameSettingsSaveResult settingsResult = await _gameSettingsSaveService.SaveEmulatorSettingsFromRequestAsync(formRequest).ConfigureAwait(false);
            if (!settingsResult.IsSuccess)
                return settingsResult;

            if (request.CredentialsTouched)
                PersistCredentialsFromForm(formRequest, gameConfig.AppId);

            if (LibraryIdentityChanged(request.InitialGameConfig, gameConfig))
                TryRunCallback(request.OnSuccessfulSaveCompleted);

            return GameSettingsSaveResult.Success();
        }

        private void PersistCredentialsFromForm(GameSettingsSaveRequest formRequest, ulong appId)
        {
            if (appId == 0 || formRequest?.BuildSnapshot == null)
                return;

            GameSettingsSnapshot snapshot = formRequest.BuildSnapshot();
            GameCredentialPersistenceService.PersistTicketAndAltSteamId(
                appId,
                snapshot.User?.Ticket,
                snapshot.User?.AltSteamId,
                ServiceLocator.RegistryService,
                _emulatorConfigService);
        }

        private static bool LibraryIdentityChanged(GameConfig initial, GameConfig current)
        {
            if (initial == null || current == null)
                return false;

            return !string.Equals(initial.AppName ?? string.Empty, current.AppName ?? string.Empty, StringComparison.Ordinal)
                || !string.Equals(initial.StartFolder ?? string.Empty, current.StartFolder ?? string.Empty, StringComparison.Ordinal)
                || !string.Equals(initial.Path ?? string.Empty, current.Path ?? string.Empty, StringComparison.Ordinal)
                || !string.Equals(initial.Parameters ?? string.Empty, current.Parameters ?? string.Empty, StringComparison.Ordinal)
                || !string.Equals(initial.WorkingDirectory ?? string.Empty, current.WorkingDirectory ?? string.Empty, StringComparison.Ordinal)
                || !string.Equals((initial.CustomIcon ?? string.Empty).Trim(), (current.CustomIcon ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase)
                || initial.LaunchMode != current.LaunchMode;
        }

        private async Task RunAddAchievementSaveAsync(
            GameConfig gameConfig,
            AchievementPreviewKind previewKind,
            GameSettingsSaveRequest formRequest)
        {
            if (gameConfig == null || gameConfig.AppId == 0)
                return;

            try
            {
                await ServiceLocator.GoldbergArtifactService.GenerateAchievementsForAddSaveAsync(
                    gameConfig,
                    previewKind,
                    formRequest.PrefetchedSchemas,
                    formRequest.TaskReportService,
                    showProgress: true,
                    progressOnlyNoMessages: true).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning("Failed to save achievements for new game: " + ex.Message);
            }
        }

        private static string GetLibraryGameDisplayName(GameConfig gameConfig)
        {
            if (gameConfig == null || string.IsNullOrWhiteSpace(gameConfig.AppName))
                return "Game";
            return gameConfig.AppName.Trim();
        }

        private static void TryRunCallback(Action callback)
        {
            if (callback == null)
                return;

            try
            {
                callback();
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning("Post-save callback failed: " + ex.Message);
            }
        }
    }
}
