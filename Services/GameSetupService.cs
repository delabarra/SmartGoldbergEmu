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

        // Upper bound for Steam PICS on add-game collect (session lock, connect, product info).
        private static readonly TimeSpan PicsMetadataTimeout = TimeSpan.FromSeconds(5);

        private static readonly TimeSpan PicsSettingsLookupTimeout = TimeSpan.FromSeconds(30);

        public async Task<(OnlineAppData Metadata, KeyValue PicsRoot)> FetchPicsMetadataWithRootAsync(
            string appId,
            KeyValue existingPicsRoot = null,
            ITaskReportService feedback = null)
        {
            if (!ulong.TryParse(appId, out ulong appIdNum) || appIdNum == 0)
                return (null, existingPicsRoot);

            return await FetchPicsMetadataCoreAsync(appIdNum, existingPicsRoot, PicsSettingsLookupTimeout, feedback).ConfigureAwait(false);
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
            return await FetchPicsMetadataCoreAsync(appId, null, PicsMetadataTimeout, fb).ConfigureAwait(false);
        }

        private async Task<(OnlineAppData Metadata, KeyValue PicsRoot)> FetchPicsMetadataCoreAsync(
            ulong appId,
            KeyValue existingPicsRoot,
            TimeSpan timeout,
            ITaskReportService fb)
        {
            try
            {
                using (var cts = new CancellationTokenSource())
                {
                    cts.CancelAfter(timeout);
                    fb?.SetMessage("Fetching game assets...");

                    KeyValue picsRoot = existingPicsRoot;
                    if (picsRoot == null)
                    {
                        var picsHolder = new GameConfig { AppId = appId };
                        picsRoot = await _steamProductInfo.WarmGameConfigAppPicsRootAsync(picsHolder, cts.Token).ConfigureAwait(false);
                    }

                    if (picsRoot == null)
                        return (null, null);

                    var metadata = new OnlineAppData
                    {
                        AppId = appId.ToString(),
                        DataSources = "Steam (game assets)"
                    };
                    SteamPicsKeyValueHelper.PopulateMetadataFromAppRoot(picsRoot, metadata);
                    return (metadata, picsRoot);
                }
            }
            catch (OperationCanceledException)
            {
                Program.LogService?.LogWarning(
                    $"Steam game assets timed out after {(int)timeout.TotalSeconds}s (busy or unreachable) for app {appId}.");
                fb?.SetMessage("Steam game assets request timed out.", TaskReportKind.Error);
                return (null, null);
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError($"Error fetching game assets metadata: {ex.Message}", ex);
                fb?.SetMessage("Could not fetch app metadata from Steam.", TaskReportKind.Error);
                return (null, null);
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
                ITaskReportService metadataFeedback = restrictStatusToAddGameCollect ? null : feedbackService;
                (metadata, picsRoot) = await FetchMetadataAndPicsAsync(appId, owner, metadataFeedback).ConfigureAwait(false);
                if (metadata == null)
                    return new GameSetupResult { Cancelled = true, MetadataFetchFailed = true };

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
