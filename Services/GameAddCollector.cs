using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using SmartGoldbergEmu.Abstractions;
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

        public ulong ResolveAppIdForCollect(string executablePath)
        {
            if (string.IsNullOrEmpty(executablePath) || !File.Exists(executablePath))
                return 0;

            ulong? detected = _gameSetupService.DetectAppIdFromExecutable(executablePath);
            if (detected.HasValue)
                return detected.Value;

            ulong? prompted = _gameSetupService.PromptForAppId();
            return prompted ?? 0;
        }

        public async Task<GameAddCollectResult> CollectFromExecutableAsync(
            string executablePath,
            IWin32Window owner,
            ITaskReportService taskReport,
            ulong resolvedAppId,
            GameConfig updateExisting = null)
        {
            if (string.IsNullOrEmpty(executablePath) || !File.Exists(executablePath))
                return new GameAddCollectResult { Cancelled = true };

            if (resolvedAppId == 0)
                return new GameAddCollectResult { Cancelled = true };

            GameSetupResult setupResult = await _gameSetupService
                .SetupGameFromExecutable(executablePath, resolvedAppId, owner, taskReport, restrictStatusToAddGameCollect: true)
                .ConfigureAwait(false);
            if (setupResult.Cancelled)
            {
                return new GameAddCollectResult
                {
                    Cancelled = true,
                    MetadataFetchFailed = setupResult.MetadataFetchFailed
                };
            }

            GameConfig game = await _gameSetupService
                .CreateGameConfigAsync(executablePath, setupResult, feedbackService: null, fetchDlc: false)
                .ConfigureAwait(false);

            bool isUpdate = updateExisting != null && updateExisting.GameGuid != Guid.Empty;
            if (isUpdate)
                ApplyExistingIdentityForUpdate(game, updateExisting);

            var bundle = new GameAddBundle
            {
                Game = game,
                Metadata = setupResult.Metadata,
                Catalog = setupResult.Catalog,
                IsUpdateOfExisting = isUpdate,
                FormDefaults = _emulatorConfigService.LoadGameSettingsSnapshot(
                    game.AppId,
                    mergePerGameSteamSettings: isUpdate)
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

            taskReport?.SetProgress(0, 0);

            return new GameAddCollectResult { Bundle = bundle };
        }

        // Keep library GUID and player launch identity; refreshed path/name/AppId come from collect.
        private static void ApplyExistingIdentityForUpdate(GameConfig collected, GameConfig existing)
        {
            if (collected == null || existing == null)
                return;

            collected.GameGuid = existing.GameGuid;
            collected.Parameters = existing.Parameters ?? string.Empty;
            collected.WorkingDirectory = existing.WorkingDirectory ?? string.Empty;
            collected.CustomIcon = existing.CustomIcon ?? string.Empty;
            collected.LaunchMode = existing.LaunchMode;
        }
    }
}
