using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using AppDataKit;
using SmartGoldbergEmu;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Forms;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Validation;

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

        // AppDataKit-first (steamcmd → PICS); returns metadata plus the catalog AppInfoKeyValue root.
        public async Task<(OnlineAppData Metadata, AppInfoKeyValue AppInfo)> FetchMetadataWithRootAsync(
            string appId,
            AppInfoKeyValue existingAppInfo = null,
            ITaskReportService feedback = null)
        {
            if (!ulong.TryParse(appId, out ulong appIdNum) || appIdNum == 0)
                return (null, existingAppInfo);

            AppCatalogSnapshot snapshot = await _appDataKitBridge
                .FetchMetadataSnapshotAsync(appIdNum, existingAppInfo, feedback)
                .ConfigureAwait(false);
            if (snapshot == null || snapshot.Failure != AppMetadataFetchFailure.None || snapshot.Online == null)
                return (null, snapshot?.AppInfo ?? existingAppInfo);

            return (snapshot.Online, snapshot.AppInfo);
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
            AppInfoKeyValue appInfo = null;
            Dictionary<long, string> prefetchedDlc = null;
            AppCatalogSnapshot catalog = null;
            if (appId > 0)
            {
                feedbackService?.SetMessage(AddGameStatusMessages.FetchingMetadata(appId, gameName));
                // Add-collect: suppress intermediate chatter; full catalog captures metadata, DLC, assets, achievements, stats, and items in one pass.
                ITaskReportService metadataFeedback = restrictStatusToAddGameCollect ? null : feedbackService;
                catalog = await _appDataKitBridge
                    .FetchFullSnapshotAsync(appId, metadataFeedback)
                    .ConfigureAwait(false);
                metadata = catalog?.Online;
                appInfo = catalog?.AppInfo;
                prefetchedDlc = catalog?.ToDlcDictionary();
                AppMetadataFetchFailure failure = catalog?.Failure ?? AppMetadataFetchFailure.Unavailable;
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

                // Drop the busy "Fetching metadata…" text as soon as the catalog is in hand.
                feedbackService?.SetMessage(string.Empty);

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
                AppInfo = appInfo,
                PreFetchedDlcData = prefetchedDlc,
                Catalog = catalog,
                Cancelled = false
            };
        }

        public async Task<OnlineAppData> EnrichForImportAsync(GameConfig game, ITaskReportService feedbackService = null)
        {
            if (game == null || game.AppId == 0)
                return null;

            ITaskReportService fb = feedbackService ?? _taskReportService;
            AppCatalogSnapshot snapshot = await _appDataKitBridge
                .FetchMetadataSnapshotAsync(game.AppId, game.AppInfo, fb)
                .ConfigureAwait(false);
            OnlineAppData metadata = snapshot?.Online;
            AppInfoKeyValue appInfo = snapshot?.AppInfo ?? game.AppInfo;
            game.AppInfo = appInfo;
            game.Catalog = snapshot;

            if (metadata != null && !string.IsNullOrEmpty(metadata.Name))
                game.AppName = metadata.Name;

            if (game.AppId != 0)
            {
                game.PreFetchedDlcData = snapshot?.ToDlcDictionary() ?? new Dictionary<long, string>();
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
                AppInfo = setupResult.AppInfo,
                Catalog = setupResult.Catalog
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
                        .FetchDlcAsync(setupResult.AppId)
                        .ConfigureAwait(false);
                }
                gameConfig.DlcCheckPerformed = true;
            }

            if (!string.IsNullOrEmpty(setupResult.Metadata?.SupportedLanguages))
                gameConfig.SupportedLanguages = ConvertSteamLanguageStringToCodes(setupResult.Metadata.SupportedLanguages);

            return gameConfig;
        }

        // Public: reused by GoldbergArtifactService when refreshing catalog-derived supported languages.
        public static List<string> ConvertSteamLanguageStringToCodes(string languageString)
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
