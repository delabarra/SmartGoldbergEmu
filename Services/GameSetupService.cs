using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SmartGoldbergEmu;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Forms;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;
using SteamKit;

namespace SmartGoldbergEmu.Services
{
    public class GameSetupService
    {
        private static readonly Dictionary<string, string> SteamLanguageDisplayToCode =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "English", "english" },
                { "French", "french" },
                { "Italian", "italian" },
                { "German", "german" },
                { "Spanish", "spanish" },
                { "Spanish - Spain", "spanish" },
                { "Portuguese", "portuguese" },
                { "Portuguese - Brazil", "brazilian" },
                { "Russian", "russian" },
                { "Japanese", "japanese" },
                { "Korean", "koreana" },
                { "Simplified Chinese", "schinese" },
                { "Traditional Chinese", "tchinese" },
                { "Polish", "polish" },
                { "Dutch", "dutch" },
                { "Czech", "czech" },
                { "Hungarian", "hungarian" },
                { "Romanian", "romanian" },
                { "Turkish", "turkish" },
                { "Brazilian Portuguese", "brazilian" },
                { "Swedish", "swedish" },
                { "Norwegian", "norwegian" },
                { "Danish", "danish" },
                { "Finnish", "finnish" },
                { "Greek", "greek" },
                { "Thai", "thai" },
                { "Vietnamese", "vietnamese" },
                { "Arabic", "arabic" },
                { "Ukrainian", "ukrainian" },
                { "Latam", "latam" }
            };

        private readonly GameDataService _gameDataService;
        private readonly SteamProductInfoService _steamProductInfo;
        private readonly DlcService _dlcService;
        private readonly ITaskReportService _taskReportService;

        public GameSetupService()
            : this(ServiceLocator.GameDataService, ServiceLocator.SteamProductInfoService, ServiceLocator.DlcService, null)
        {
        }

        public GameSetupService(GameDataService gameDataService, SteamProductInfoService steamProductInfo, DlcService dlcService = null, ITaskReportService feedbackService = null)
        {
            _gameDataService = gameDataService ?? throw new ArgumentNullException(nameof(gameDataService));
            _steamProductInfo = steamProductInfo ?? throw new ArgumentNullException(nameof(steamProductInfo));
            _dlcService = dlcService ?? ServiceLocator.DlcService;
            _taskReportService = feedbackService;
        }

        public ulong? DetectAppIdFromExecutable(string executablePath)
        {
            try
            {
                string dir = Path.GetDirectoryName(executablePath);
                if (string.IsNullOrEmpty(dir))
                    return null;

                string[] files = Directory.GetFiles(dir, PathConstants.SteamAppIdFileName, SearchOption.AllDirectories);
                if (files.Length == 0)
                    return null;

                string text = File.ReadAllText(files[0]).Trim();
                return ulong.TryParse(text, out ulong appId) ? (ulong?)appId : null;
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError($"Error detecting App ID: {ex.Message}", ex);
                return null;
            }
        }

        public ulong? PromptForAppId()
        {
            using (var searchForm = new GameSearchForm())
            {
                if (searchForm.ShowDialog() == DialogResult.OK && searchForm.SelectedAppId.HasValue)
                    return searchForm.SelectedAppId.Value;
            }
            return null;
        }

        // Session establish can take up to ~25s + one ~22s retry; keep that off the product-info clock.
        private static readonly TimeSpan PicsSessionEnsureTimeout = TimeSpan.FromSeconds(50);

        // Bound for PICS product-info after a session is ready (or disk cache miss path).
        private static readonly TimeSpan PicsProductInfoTimeout = TimeSpan.FromSeconds(20);

        public async Task<(OnlineAppData Metadata, KeyValue PicsRoot)> FetchPicsMetadataWithRootAsync(
            string appId,
            KeyValue existingPicsRoot = null,
            ITaskReportService feedback = null)
        {
            if (!ulong.TryParse(appId, out ulong appIdNum) || appIdNum == 0)
                return (null, existingPicsRoot);

            var (metadata, picsRoot, _) = await FetchPicsMetadataCoreAsync(appIdNum, existingPicsRoot, feedback)
                .ConfigureAwait(false);
            return (metadata, picsRoot);
        }

        public async Task<OnlineAppData> FetchMetadataAsync(ulong appId, IWin32Window owner = null, ITaskReportService feedback = null)
        {
            var (metadata, _) = await FetchMetadataAndPicsAsync(appId, owner, feedback).ConfigureAwait(false);
            return metadata;
        }

        private async Task<(OnlineAppData Metadata, KeyValue PicsRoot)> FetchMetadataAndPicsAsync(
            ulong appId,
            IWin32Window owner = null,
            ITaskReportService feedback = null)
        {
            if (appId == 0)
                return (null, null);

            ITaskReportService fb = feedback ?? _taskReportService;
            var (metadata, picsRoot, _) = await FetchPicsMetadataCoreAsync(appId, null, fb).ConfigureAwait(false);
            return (metadata, picsRoot);
        }

        private enum PicsMetadataFailure
        {
            None,
            TimedOut,
            Unavailable
        }

        private async Task<(OnlineAppData Metadata, KeyValue PicsRoot, PicsMetadataFailure Failure)> FetchPicsMetadataCoreAsync(
            ulong appId,
            KeyValue existingPicsRoot,
            ITaskReportService fb)
        {
            try
            {
                KeyValue picsRoot = existingPicsRoot;
                if (picsRoot == null)
                {
                    // Disk cache does not need a live Steam session.
                    picsRoot = SteamPicsKeyValueHelper.TryLoadExportedAppPicsFromValveFile(
                        PathConstants.GamesDirectory,
                        appId);

                    if (picsRoot == null)
                    {
                        fb?.SetMessage(AddGameStatusMessages.ConnectingToSteam);
                        using (var sessionCts = new CancellationTokenSource())
                        {
                            sessionCts.CancelAfter(PicsSessionEnsureTimeout);
                            bool sessionReady = await _steamProductInfo.TryEnsureSessionAsync(sessionCts.Token)
                                .ConfigureAwait(false);
                            if (!sessionReady)
                            {
                                Program.LogService?.LogWarning(
                                    $"Steam session not ready for app {appId} after {(int)PicsSessionEnsureTimeout.TotalSeconds}s.");
                                fb?.SetMessage(AddGameStatusMessages.MetadataFetchTimedOut, TaskReportKind.Error);
                                return (null, null, PicsMetadataFailure.TimedOut);
                            }
                        }

                        fb?.SetMessage("Fetching game assets...");
                        using (var picsCts = new CancellationTokenSource())
                        {
                            picsCts.CancelAfter(PicsProductInfoTimeout);
                            var picsHolder = new GameConfig { AppId = appId };
                            picsRoot = await _steamProductInfo.WarmGameConfigAppPicsRootAsync(picsHolder, picsCts.Token)
                                .ConfigureAwait(false);
                        }
                    }
                }

                if (picsRoot == null)
                {
                    fb?.SetMessage(AddGameStatusMessages.MetadataFetchFailed, TaskReportKind.Error);
                    return (null, null, PicsMetadataFailure.Unavailable);
                }

                var metadata = new OnlineAppData
                {
                    AppId = appId.ToString(),
                    DataSources = "Steam (game assets)"
                };
                SteamPicsKeyValueHelper.PopulateMetadataFromAppRoot(picsRoot, metadata);
                return (metadata, picsRoot, PicsMetadataFailure.None);
            }
            catch (OperationCanceledException)
            {
                Program.LogService?.LogWarning(
                    $"Steam game assets timed out while fetching app {appId}.");
                fb?.SetMessage(AddGameStatusMessages.MetadataFetchTimedOut, TaskReportKind.Error);
                return (null, null, PicsMetadataFailure.TimedOut);
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError($"Error fetching game assets metadata: {ex.Message}", ex);
                fb?.SetMessage(AddGameStatusMessages.MetadataFetchFailed, TaskReportKind.Error);
                return (null, null, PicsMetadataFailure.Unavailable);
            }
        }

        public async Task<Dictionary<long, string>> FetchDlcNamesAsync(
            OnlineAppData metadata,
            ITaskReportService feedbackService = null,
            KeyValue picsAppRoot = null)
        {
            if (metadata == null)
                return new Dictionary<long, string>();

            try
            {
                DlcService dlcService = feedbackService != null
                    ? new DlcService(_steamProductInfo, null, feedbackService)
                    : _dlcService;

                return await dlcService.GetDlcDataAsync(metadata.AppId, picsAppRoot: picsAppRoot).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError($"Error fetching DLC names: {ex.Message}", ex);
                return new Dictionary<long, string>();
            }
        }

        public async Task<GameSetupResult> SetupGameFromExecutable(string executablePath, IWin32Window owner = null, ITaskReportService feedbackService = null)
        {
            return await SetupGameFromExecutable(executablePath, resolvedAppId: null, owner, feedbackService).ConfigureAwait(false);
        }

        public async Task<GameSetupResult> SetupGameFromExecutable(
            string executablePath,
            ulong? resolvedAppId,
            IWin32Window owner = null,
            ITaskReportService feedbackService = null,
            bool restrictStatusToAddGameCollect = false)
        {
            if (string.IsNullOrEmpty(executablePath) || !File.Exists(executablePath))
                return new GameSetupResult { Cancelled = true };

            string gameName = Path.GetFileNameWithoutExtension(executablePath);
            ulong appId;
            if (resolvedAppId.HasValue)
                appId = resolvedAppId.Value;
            else
            {
                ulong? detected = DetectAppIdFromExecutable(executablePath);
                if (detected.HasValue)
                    appId = detected.Value;
                else
                {
                    ulong? prompted = PromptForAppId();
                    if (!prompted.HasValue)
                        return new GameSetupResult { Cancelled = true };
                    appId = prompted.Value;
                }
            }

            OnlineAppData metadata = null;
            KeyValue picsRoot = null;
            if (appId > 0)
            {
                feedbackService?.SetMessage(AddGameStatusMessages.LookingUpData(appId));
                feedbackService?.SetProgress(0, 2);
                // Suppress intermediate fetch chatter on the add-collect strip; keep LookingUpData until done.
                ITaskReportService metadataFeedback = restrictStatusToAddGameCollect ? null : feedbackService;
                PicsMetadataFailure failure;
                (metadata, picsRoot, failure) = await FetchPicsMetadataCoreAsync(appId, null, metadataFeedback)
                    .ConfigureAwait(false);
                if (metadata == null)
                {
                    Program.LogService?.LogWarning(
                        $"Could not fetch Steam metadata for App ID {appId} ({failure}).");
                    if (restrictStatusToAddGameCollect)
                    {
                        feedbackService?.SetMessage(
                            failure == PicsMetadataFailure.TimedOut
                                ? AddGameStatusMessages.MetadataFetchTimedOut
                                : AddGameStatusMessages.MetadataFetchFailed,
                            TaskReportKind.Error);
                    }

                    return new GameSetupResult { Cancelled = true, MetadataFetchFailed = true };
                }

                if (!string.IsNullOrEmpty(metadata.Name))
                {
                    gameName = metadata.Name;
                    Program.LogService?.LogMessage($"Using fetched game name: {gameName}");
                }
                else
                {
                    Program.LogService?.LogWarning($"Metadata has no name for App ID {appId}, using filename: {gameName}");
                }
            }

            return new GameSetupResult
            {
                AppId = appId,
                GameName = gameName,
                Metadata = metadata,
                AppPicsKeyValue = picsRoot,
                Cancelled = false
            };
        }

        public async Task<OnlineAppData> EnrichForImportAsync(GameConfig game, ITaskReportService feedbackService = null)
        {
            if (game == null || game.AppId == 0)
                return null;

            ITaskReportService fb = feedbackService ?? _taskReportService;
            var (metadata, picsRoot) = await FetchMetadataAndPicsAsync(game.AppId, null, fb).ConfigureAwait(false);
            game.AppPicsKeyValue = picsRoot;

            if (metadata != null && !string.IsNullOrEmpty(metadata.Name))
                game.AppName = metadata.Name;

            if (game.AppId != 0)
            {
                var metadataForDlc = metadata ?? new OnlineAppData { AppId = game.AppId.ToString() };
                game.PreFetchedDlcData = await FetchDlcNamesAsync(metadataForDlc, fb, picsRoot).ConfigureAwait(false);
                game.DlcCheckPerformed = true;
            }

            if (metadata != null && !string.IsNullOrEmpty(metadata.SupportedLanguages))
                game.SupportedLanguages = ConvertSteamLanguageStringToCodes(metadata.SupportedLanguages);

            return metadata;
        }

        public async Task<GameConfig> CreateGameConfigAsync(
            string executablePath,
            GameSetupResult setupResult,
            ITaskReportService feedbackService = null,
            bool fetchDlc = true)
        {
            string gameName = !string.IsNullOrEmpty(setupResult.Metadata?.Name)
                ? setupResult.Metadata.Name
                : setupResult.GameName;

            if (!string.IsNullOrEmpty(setupResult.Metadata?.Name))
                Program.LogService?.LogMessage($"CreateGameConfig: Using metadata name: {gameName}");
            else
                Program.LogService?.LogWarning($"CreateGameConfig: No metadata name available, using: {gameName}");

            string startFolder = Path.GetDirectoryName(executablePath);
            string pathExe = executablePath;
            if (setupResult.Metadata != null && !string.IsNullOrWhiteSpace(setupResult.Metadata.InstallDir) &&
                GameFolderPathHelper.TrySplitExecutableAtSteamInstallDir(executablePath, setupResult.Metadata.InstallDir, out string gameRootFromInstall, out string relativeExe))
            {
                startFolder = gameRootFromInstall;
                pathExe = relativeExe;
            }

            var gameConfig = new GameConfig
            {
                AppName = gameName,
                AppId = setupResult.AppId,
                Path = pathExe,
                StartFolder = startFolder,
                Parameters = string.Empty,
                GameGuid = _gameDataService.GenerateGameGuid(),
                AppPicsKeyValue = setupResult.AppPicsKeyValue
            };

            if (fetchDlc && setupResult.AppId != 0)
            {
                var metadataForDlc = setupResult.Metadata ?? new OnlineAppData { AppId = setupResult.AppId.ToString() };
                gameConfig.PreFetchedDlcData = await FetchDlcNamesAsync(metadataForDlc, feedbackService, setupResult.AppPicsKeyValue).ConfigureAwait(false);
                gameConfig.DlcCheckPerformed = true;
            }

            if (!string.IsNullOrEmpty(setupResult.Metadata?.SupportedLanguages))
                gameConfig.SupportedLanguages = ConvertSteamLanguageStringToCodes(setupResult.Metadata.SupportedLanguages);

            return gameConfig;
        }

        private static List<string> ConvertSteamLanguageStringToCodes(string languageString)
        {
            if (string.IsNullOrWhiteSpace(languageString))
                return new List<string>();

            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string part in languageString.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = part.Trim();
                if (trimmed.Length == 0)
                    continue;

                string code = SteamLanguageDisplayToCode.TryGetValue(trimmed, out string mapped)
                    ? mapped
                    : trimmed.ToLowerInvariant();
                if (seen.Add(code))
                    result.Add(code);
            }

            return result;
        }
    }
}
