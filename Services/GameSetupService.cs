using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using SmartGoldbergEmu;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Forms;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Validation;
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
        private readonly AppDataKitBridgeService _appDataKitBridge;
        private readonly ITaskReportService _taskReportService;

        public GameSetupService()
            : this(ServiceLocator.GameDataService, ServiceLocator.AppDataKitBridgeService, null)
        {
        }

        public GameSetupService(
            GameDataService gameDataService,
            AppDataKitBridgeService appDataKitBridge = null,
            ITaskReportService feedbackService = null)
        {
            _gameDataService = gameDataService ?? throw new ArgumentNullException(nameof(gameDataService));
            _appDataKitBridge = appDataKitBridge ?? ServiceLocator.AppDataKitBridgeService;
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

        public async Task<(OnlineAppData Metadata, KeyValue PicsRoot)> FetchPicsMetadataWithRootAsync(
            string appId,
            KeyValue existingPicsRoot = null,
            ITaskReportService feedback = null)
        {
            if (!ulong.TryParse(appId, out ulong appIdNum) || appIdNum == 0)
                return (null, existingPicsRoot);

            AppDataKitMetadataResult result = await _appDataKitBridge
                .FetchMetadataAsync(appIdNum, existingPicsRoot, feedback)
                .ConfigureAwait(false);
            if (result == null || result.Failure != AppMetadataFetchFailure.None || result.Metadata == null)
                return (null, result?.AppRoot ?? existingPicsRoot);

            return (result.Metadata, result.AppRoot);
        }

        public async Task<OnlineAppData> FetchMetadataAsync(ulong appId, IWin32Window owner = null, ITaskReportService feedback = null)
        {
            if (appId == 0)
                return null;

            ITaskReportService fb = feedback ?? _taskReportService;
            AppDataKitMetadataResult result = await _appDataKitBridge
                .FetchMetadataAsync(appId, null, fb)
                .ConfigureAwait(false);
            return result?.Metadata;
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
            Dictionary<long, string> prefetchedDlc = null;
            if (appId > 0)
            {
                feedbackService?.SetMessage(AddGameStatusMessages.LookingUpData(appId));
                feedbackService?.SetProgress(0, 2);
                // Add-collect: suppress intermediate chatter; still resolve DLC names via AppDataKit (steamcmd).
                ITaskReportService metadataFeedback = restrictStatusToAddGameCollect ? null : feedbackService;
                AppDataKitMetadataResult fetch = await _appDataKitBridge
                    .FetchMetadataAsync(appId, null, metadataFeedback, resolveDlcNames: true)
                    .ConfigureAwait(false);
                metadata = fetch?.Metadata;
                picsRoot = fetch?.AppRoot;
                prefetchedDlc = fetch?.DlcData;
                AppMetadataFetchFailure failure = fetch?.Failure ?? AppMetadataFetchFailure.Unavailable;
                if (metadata == null)
                {
                    Program.LogService?.LogWarning(
                        $"Could not fetch Steam metadata for App ID {appId} ({failure}).");
                    if (restrictStatusToAddGameCollect)
                    {
                        feedbackService?.SetMessage(
                            failure == AppMetadataFetchFailure.TimedOut
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
                PreFetchedDlcData = prefetchedDlc,
                Cancelled = false
            };
        }

        public async Task<OnlineAppData> EnrichForImportAsync(GameConfig game, ITaskReportService feedbackService = null)
        {
            if (game == null || game.AppId == 0)
                return null;

            ITaskReportService fb = feedbackService ?? _taskReportService;
            AppDataKitMetadataResult fetch = await _appDataKitBridge
                .FetchMetadataAsync(game.AppId, game.AppPicsKeyValue, fb)
                .ConfigureAwait(false);
            OnlineAppData metadata = fetch?.Metadata;
            KeyValue picsRoot = fetch?.AppRoot;
            game.AppPicsKeyValue = picsRoot;

            if (metadata != null && !string.IsNullOrEmpty(metadata.Name))
                game.AppName = metadata.Name;

            if (game.AppId != 0)
            {
                game.PreFetchedDlcData = fetch?.DlcData ?? new Dictionary<long, string>();
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

            // No steam_api in the install tree → Steam.dll beside exe (common for older titles).
            if (!SteamApiValidator.HasPrimarySteamApiDll(startFolder))
                gameConfig.LaunchMode = GoldbergLaunchMode.SteamDllBesideExe;

            if (fetchDlc && setupResult.AppId != 0)
            {
                if (setupResult.PreFetchedDlcData != null)
                {
                    gameConfig.PreFetchedDlcData = setupResult.PreFetchedDlcData;
                }
                else
                {
                    gameConfig.PreFetchedDlcData = await _appDataKitBridge
                        .FetchDlcAsync(setupResult.AppId, setupResult.AppPicsKeyValue)
                        .ConfigureAwait(false);
                }
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
