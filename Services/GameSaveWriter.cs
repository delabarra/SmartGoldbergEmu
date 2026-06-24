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
            formRequest.TaskReportService = request.TaskReportService ?? ServiceLocator.TaskReportService;

            ITaskReportService taskReport = formRequest.TaskReportService ?? ServiceLocator.TaskReportService;
            string displayName = GetLibraryGameDisplayName(gameConfig);

            ValidationResult addResult = _gameDataService.AddGame(gameConfig);
            if (!addResult.IsValid)
                return GameSettingsSaveResult.Failure(addResult.ErrorMessage);

            // Promote the in-memory list row as soon as games.ini is committed (UI thread via MainForm).
            TryRunCallback(request.OnSuccessfulSaveCompleted);

            bool assetsDownloaded = await _gameSettingsSaveService.DownloadAddModeLibraryAssetsAsync(formRequest).ConfigureAwait(false);
            if (assetsDownloaded)
                TryRunCallback(request.OnAssetsDownloaded);

            taskReport?.SetMessage(AddGameStatusMessages.GeneratingGoldbergFiles(displayName));
            taskReport?.SetProgress(0, 0);

            await _gameSettingsSaveService.RunAddModeGoldbergWorkAsync(formRequest, skipLibraryAssets: true).ConfigureAwait(false);

            GameSettingsSaveResult settingsResult = await _gameSettingsSaveService.SaveEmulatorSettingsFromRequestAsync(formRequest).ConfigureAwait(false);
            if (!settingsResult.IsSuccess)
                return settingsResult;

            taskReport?.SetMessage(AddGameStatusMessages.DownloadingAchievementIcons(displayName, 0, 0));
            try
            {
                await RunAddAchievementSaveAsync(gameConfig, request.AchievementPreview, formRequest).ConfigureAwait(false);
            }
            finally
            {
                taskReport?.SetProgress(0, 0);
            }

            taskReport?.SetMessageWithAutoClear(
                AddGameStatusMessages.AddedToLibrary(displayName),
                delayMs: AddGameStatusMessages.StatusAutoClearDelayMs);

            if (request.CredentialsTouched)
                PersistCredentialsFromForm(formRequest, gameConfig.AppId);

            if (!assetsDownloaded)
                TryRunCallback(request.OnAssetsDownloaded);

            return GameSettingsSaveResult.Success();
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
