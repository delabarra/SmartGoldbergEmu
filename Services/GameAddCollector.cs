using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Services
{
    // Builds in-memory GameAddBundle during add-game collect; no library or per-game folder writes.
    public class GameAddCollector
    {
        private readonly GameSetupService _gameSetupService;
        private readonly EmulatorConfigService _emulatorConfigService;
        private readonly SteamApiKeyService _steamApiKeyService;

        public GameAddCollector()
            : this(
                ServiceLocator.GameSetupService,
                ServiceLocator.EmulatorConfigService,
                ServiceLocator.SteamApiKeyService)
        {
        }

        public GameAddCollector(
            GameSetupService gameSetupService,
            EmulatorConfigService emulatorConfigService,
            SteamApiKeyService steamApiKeyService)
        {
            _gameSetupService = gameSetupService ?? throw new ArgumentNullException(nameof(gameSetupService));
            _emulatorConfigService = emulatorConfigService ?? throw new ArgumentNullException(nameof(emulatorConfigService));
            _steamApiKeyService = steamApiKeyService ?? throw new ArgumentNullException(nameof(steamApiKeyService));
        }

        public async Task<GameAddCollectResult> CollectFromExecutableAsync(
            string executablePath,
            IWin32Window owner,
            ITaskReportService taskReport)
        {
            if (string.IsNullOrEmpty(executablePath) || !File.Exists(executablePath))
                return new GameAddCollectResult { Cancelled = true };

            ulong appId = ResolveAppIdForCollect(executablePath);
            if (appId == 0)
                return new GameAddCollectResult { Cancelled = true };

            GameSetupResult setupResult = await _gameSetupService
                .SetupGameFromExecutable(executablePath, appId, owner, taskReport, restrictStatusToAddGameCollect: true)
                .ConfigureAwait(false);
            if (setupResult.Cancelled)
            {
                return new GameAddCollectResult
                {
                    Cancelled = true,
                    MetadataFetchFailed = setupResult.MetadataFetchFailed
                };
            }

            string displayName = !string.IsNullOrEmpty(setupResult.Metadata?.Name)
                ? setupResult.Metadata.Name
                : setupResult.GameName;

            GameConfig game = await _gameSetupService
                .CreateGameConfigAsync(executablePath, setupResult, feedbackService: null, fetchDlc: false)
                .ConfigureAwait(false);

            var bundle = new GameAddBundle
            {
                Game = game,
                Metadata = setupResult.Metadata,
                FormDefaults = _emulatorConfigService.LoadGameSettingsSnapshot(game.AppId, mergePerGameSteamSettings: false)
            };

            if (game.AppId > 0)
            {
                game.PreFetchedDlcData = setupResult.PreFetchedDlcData
                    ?? new Dictionary<long, string>();
                game.DlcCheckPerformed = true;

                (AchievementPreviewKind kind, string previewJson) = await ServiceLocator.GoldbergArtifactService
                    .BuildAddModeAchievementPreviewAsync(game)
                    .ConfigureAwait(false);
                bundle.AchievementPreview = kind;
                bundle.AchievementsPreviewJson = previewJson ?? string.Empty;

                if (_steamApiKeyService.TryGetValidFormatKey(out _)
                    && ServiceLocator.GoldbergFilesService.ShouldAutoGenerateItems(game.AppId))
                {
                    // Items preview JSON is populated on save; collector leaves empty object for add mode.
                    bundle.ItemsJson = "{}";
                }
            }

            taskReport?.SetMessage(AddGameStatusMessages.WaitingToPreview(displayName));
            taskReport?.SetProgress(0, 0);

            return new GameAddCollectResult { Bundle = bundle };
        }

        private ulong ResolveAppIdForCollect(string executablePath)
        {
            ulong? detected = _gameSetupService.DetectAppIdFromExecutable(executablePath);
            if (detected.HasValue)
                return detected.Value;

            ulong? prompted = _gameSetupService.PromptForAppId();
            return prompted ?? 0;
        }
    }
}
