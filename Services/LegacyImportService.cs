using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Serialization;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Validation;

namespace SmartGoldbergEmu.Services
{
    // Reads 2.x SmartGoldbergEmu.cfg (XML SavedConf), ensures each AppId is in games.ini, then runs new-game file generation per entry.
    public class LegacyImportService
    {
        private const string LegacyImportedConfigFileName = PathConstants.LegacyImportedConfigFileName;

        private readonly GameDataService _gameDataService;
        private readonly GameSetupService _gameSetupService;
        private readonly GameSettingsSaveService _gameSettingsSaveService;
        private readonly SteamApiKeyService _steamApiKeyService;
        private readonly HashSet<ulong> _pendingImportAppIds = new HashSet<ulong>();
        private readonly object _pendingImportLock = new object();

        public LegacyImportService()
            : this(
                ServiceLocator.GameDataService,
                ServiceLocator.GameSetupService,
                ServiceLocator.GameSettingsSaveService,
                ServiceLocator.SteamApiKeyService)
        {
        }

        public LegacyImportService(
            GameDataService gameDataService,
            GameSetupService gameSetupService,
            GameSettingsSaveService gameSettingsSaveService,
            SteamApiKeyService steamApiKeyService)
        {
            _gameDataService = gameDataService ?? throw new ArgumentNullException(nameof(gameDataService));
            _gameSetupService = gameSetupService ?? throw new ArgumentNullException(nameof(gameSetupService));
            _gameSettingsSaveService = gameSettingsSaveService ?? throw new ArgumentNullException(nameof(gameSettingsSaveService));
            _steamApiKeyService = steamApiKeyService ?? throw new ArgumentNullException(nameof(steamApiKeyService));
        }

        public bool IsImportPending(ulong appId)
        {
            if (appId == 0)
                return false;
            lock (_pendingImportLock)
                return _pendingImportAppIds.Contains(appId);
        }

        // One-time migration before the rest of the app reads config or games.ini.
        public void RunSynchronousStartupMigration()
        {
            TryMigrateApplicationConfigFromLocalAppData();
            TryMigrateApplicationConfigFromLegacyExeCfg();
            ServiceLocator.AppDataService.TryMigrateUiSettingsFromConfig();
            TryMigrateApplicationConfigViewMode();
            TryImportLegacyApiKey(null, null);
            TryDeleteLegacyApiKeyFileIfRegistryHasKey();
            TryMigrateGamesIniLibrary();
            TryCleanupLocalAppDataPerUserFolder();
        }

        // Async 2.x library import and Goldberg file provisioning after synchronous migration.
        public async Task RunAsyncStartupMigrationAsync(ITaskReportService report = null, Action onLibraryChanged = null)
        {
            await TryImportAsync(report, onLibraryChanged).ConfigureAwait(false);
            await TryProvisionMissingFilesAsync(report, onLibraryChanged).ConfigureAwait(false);
            TryMigrateApplicationConfigFromLegacyExeCfg();
        }

        public async Task<int> TryProvisionMissingFilesAsync(ITaskReportService report = null, Action onLibraryChanged = null)
        {
            var games = _gameDataService.GetAllGames();
            if (games == null || games.Count == 0)
                return 0;

            int pendingCount = MarkPendingForUnprovisionedGames(games);
            if (pendingCount > 0)
            {
                report?.SetMessage($"Finishing setup for {pendingCount} game(s)…");
                onLibraryChanged?.Invoke();
            }

            int generated = 0;
            foreach (GameConfig game in games)
            {
                if (game == null || game.AppId == 0 || IsGameDataProvisioned(game.AppId))
                    continue;

                report?.SetMessage($"Finishing setup for AppId {game.AppId}...");
                if (await GenerateFilesForAppIdAsync(game, report).ConfigureAwait(false))
                {
                    ClearImportPending(game.AppId);
                    generated++;
                    onLibraryChanged?.Invoke();
                }
                else
                    ClearImportPending(game.AppId);
            }

            if (generated > 0)
                Program.LogService?.LogMessage($"Import: generated files for {generated} library game(s) that were missing data.");
            return generated;
        }

        public async Task<bool> TryImportAsync(ITaskReportService report = null, Action onLibraryChanged = null)
        {
            if (!TryResolveLegacyConfigPath(out string legacyPath))
                return false;

            LegacyConfig legacy;
            try
            {
                legacy = LoadLegacyConfig(legacyPath);
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError("Import: could not read SmartGoldbergEmu.cfg (2.x XML).", ex);
                report?.SetMessage("Import failed: invalid config file.", TaskReportKind.Error);
                return false;
            }

            bool apiKeyImported = TryImportLegacyApiKey(legacy.WebApiKey, report);

            if (legacy?.Apps == null || legacy.Apps.Count == 0)
            {
                FinalizeLegacyConfigFile(legacyPath);
                if (apiKeyImported)
                    report?.SetMessageWithAutoClear("Steam Web API key imported from previous configuration.");
                return true;
            }

            int generated = 0;
            int failed = 0;
            int total = legacy.Apps.Count;
            int placeholderCount = RegisterImportPlaceholders(legacy.Apps, onLibraryChanged);

            if (placeholderCount > 0)
            {
                report?.SetMessage($"Found {placeholderCount} game(s) in previous configuration — importing…");
                Program.LogService?.LogMessage(
                    $"Import: registered {placeholderCount} placeholder library entries before file generation.");
            }
            else
                report?.SetMessage($"Import: checking {total} game(s) in previous configuration…");

            report?.SetProgress(0, total);

            for (int i = 0; i < legacy.Apps.Count; i++)
            {
                LegacyGameEntry entry = legacy.Apps[i];
                ulong appId = entry?.AppId ?? 0;
                string label = !string.IsNullOrWhiteSpace(entry?.AppName)
                    ? entry.AppName.Trim()
                    : (appId > 0 ? $"App {appId}" : "game");
                report?.SetMessage($"Import ({i + 1}/{total}): {label} (AppId {appId})...");

                if (entry == null || entry.AppId == 0)
                {
                    Program.LogService?.LogWarning("Import: skipped entry with no App ID.");
                    continue;
                }

                if (await ProcessEntryAsync(entry, report, onLibraryChanged).ConfigureAwait(false))
                    generated++;
                else
                    failed++;

                report?.SetProgress(i + 1, total);
            }

            if (failed == 0)
                FinalizeLegacyConfigFile(legacyPath);

            string summary = $"Import finished: {generated} game(s) generated, {failed} failed.";
            Program.LogService?.LogMessage(summary);
            report?.SetMessageWithAutoClear(summary);
            return failed == 0;
        }

        public static bool TryResolveLegacyConfigPath(out string legacyConfigPath)
        {
            legacyConfigPath = null;
            if (TryLegacyConfigAt(PathConstants.LegacyExeConfigFilePath, out legacyConfigPath))
                return true;
            if (TryLegacyConfigAt(PathConstants.LegacyLocalAppDataConfigFilePath, out legacyConfigPath))
                return true;
            return false;
        }

        public static bool IsLegacyXmlConfig(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return false;

            try
            {
                using (var reader = new StreamReader(path))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        line = line.Trim();
                        if (line.Length == 0)
                            continue;

                        if (!line.StartsWith("<", StringComparison.Ordinal))
                            return false;

                        return line.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase)
                            || line.IndexOf("SavedConf", StringComparison.OrdinalIgnoreCase) >= 0
                            || line.IndexOf("<apps", StringComparison.OrdinalIgnoreCase) >= 0;
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        private static bool TryLegacyConfigAt(string path, out string resolvedPath)
        {
            resolvedPath = null;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) || !IsLegacyXmlConfig(path))
                return false;

            resolvedPath = path;
            return true;
        }

        private static LegacyConfig LoadLegacyConfig(string path)
        {
            string xml = ExtractLegacyXmlFromConfigFile(path);
            using (var reader = new StringReader(xml))
            {
                var serializer = new XmlSerializer(typeof(LegacyConfig));
                return (LegacyConfig)serializer.Deserialize(reader);
            }
        }

        // 2.x portable cfg can be XML with new INI settings appended after </SavedConf> (upgrade overwrite).
        private static string ExtractLegacyXmlFromConfigFile(string path)
        {
            var xmlLines = new List<string>();
            bool inXml = false;
            foreach (string line in File.ReadAllLines(path))
            {
                string trimmed = line.Trim();
                if (!inXml
                    && (trimmed.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase)
                        || trimmed.StartsWith("<SavedConf", StringComparison.OrdinalIgnoreCase)))
                {
                    inXml = true;
                }

                if (!inXml)
                    continue;

                xmlLines.Add(line);
                if (trimmed.Equals("</SavedConf>", StringComparison.OrdinalIgnoreCase))
                    break;
            }

            if (xmlLines.Count == 0)
                throw new InvalidDataException("No SavedConf XML found in SmartGoldbergEmu.cfg.");

            return string.Join(Environment.NewLine, xmlLines);
        }

        private static bool IsHybridConfigFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return false;

            bool seenClose = false;
            foreach (string line in File.ReadAllLines(path))
            {
                string trimmed = line.Trim();
                if (trimmed.Equals("</SavedConf>", StringComparison.OrdinalIgnoreCase))
                {
                    seenClose = true;
                    continue;
                }

                if (seenClose
                    && trimmed.Length > 0
                    && trimmed[0] == '['
                    && trimmed[trimmed.Length - 1] == ']')
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryStripLegacyXmlFromHybridConfig(string cfgPath)
        {
            if (!IsHybridConfigFile(cfgPath))
                return false;

            var lines = File.ReadAllLines(cfgPath).ToList();
            int savedConfEnd = -1;
            int iniStart = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                string trimmed = lines[i].Trim();
                if (trimmed.Equals("</SavedConf>", StringComparison.OrdinalIgnoreCase))
                    savedConfEnd = i;
                else if (savedConfEnd >= 0
                    && i > savedConfEnd
                    && trimmed.Length > 0
                    && trimmed[0] == '['
                    && trimmed[trimmed.Length - 1] == ']')
                {
                    iniStart = i;
                    break;
                }
            }

            if (savedConfEnd < 0 || iniStart < 0)
                return false;

            File.WriteAllLines(cfgPath, lines.Skip(iniStart));
            Program.LogService?.LogMessage(
                "Import: removed 2.x XML from SmartGoldbergEmu.cfg; application settings kept.");
            return true;
        }

        private static void FinalizeLegacyConfigFile(string legacyPath)
        {
            string directory = Path.GetDirectoryName(legacyPath);
            if (TryStripLegacyXmlFromHybridConfig(legacyPath))
            {
                DeleteLegacyImportedSidecar(directory);
                return;
            }

            TryDeletePureLegacyXmlConfig(legacyPath);
            DeleteLegacyImportedSidecar(directory);

            try
            {
                ValidationResult result = ServiceLocator.AppDataService.EnsureMinimalConfigFilesExist();
                if (!result.IsValid)
                    Program.LogService?.LogWarning($"Import: could not ensure application config after removing 2.x XML: {result.ErrorMessage}");
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning($"Import: could not ensure application config after removing 2.x XML: {ex.Message}");
            }
        }

        private static void TryDeletePureLegacyXmlConfig(string legacyPath)
        {
            if (string.IsNullOrWhiteSpace(legacyPath) || !File.Exists(legacyPath))
                return;
            if (IsHybridConfigFile(legacyPath))
                return;

            try
            {
                File.Delete(legacyPath);
                Program.LogService?.LogMessage("Import: removed 2.x SmartGoldbergEmu.cfg after migration.");
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning($"Import: could not remove 2.x SmartGoldbergEmu.cfg: {ex.Message}");
            }
        }

        private static void DeleteLegacyImportedSidecar(string configDirectory)
        {
            if (string.IsNullOrWhiteSpace(configDirectory))
                return;

            string importedPath = Path.Combine(configDirectory, LegacyImportedConfigFileName);
            if (!File.Exists(importedPath))
                return;

            try
            {
                File.Delete(importedPath);
                Program.LogService?.LogMessage($"Import: removed {LegacyImportedConfigFileName}.");
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning($"Import: could not remove {LegacyImportedConfigFileName}: {ex.Message}");
            }
        }

        // XML <webapi_key> and install-root steam_apikey.txt (legacy file); only when registry has no key yet.
        private bool TryImportLegacyApiKey(string legacyXmlKey, ITaskReportService report)
        {
            if (_steamApiKeyService.HasApiKey())
                return false;

            string candidate = ResolveLegacyApiKeyCandidate(legacyXmlKey);
            if (string.IsNullOrEmpty(candidate))
                return false;

            ValidationResult result = _steamApiKeyService.SetApiKey(candidate);
            if (result.IsValid)
            {
                Program.LogService?.LogMessage("Import: migrated Steam Web API key to registry.");
                report?.SetMessage("Steam Web API key imported from previous configuration.");
                return true;
            }

            Program.LogService?.LogWarning("Import: could not migrate API key.");
            return false;
        }

        private static string ResolveLegacyApiKeyCandidate(string legacyXmlKey)
        {
            string fromXml = NormalizeLegacyApiKey(legacyXmlKey);
            if (SteamApiKeyService.IsValidApiKeyFormat(fromXml))
                return fromXml;

            return ReadLegacyApiKeyFromFile();
        }

        private static string NormalizeLegacyApiKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return string.Empty;
            return key.Replace(" ", string.Empty).Trim();
        }

        private static string ReadLegacyApiKeyFromFile()
        {
            try
            {
                string path = PathConstants.LegacyApiKeyFilePath;
                if (!File.Exists(path))
                    return string.Empty;

                string key = NormalizeLegacyApiKey(File.ReadAllText(path));
                return SteamApiKeyService.IsValidApiKeyFormat(key) ? key : string.Empty;
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning($"Import: could not read {PathConstants.LegacyApiKeyFileName}: {ex.Message}");
                return string.Empty;
            }
        }

        private async Task<bool> ProcessEntryAsync(
            LegacyGameEntry entry,
            ITaskReportService report,
            Action onLibraryChanged)
        {
            if (entry == null || entry.AppId == 0)
            {
                Program.LogService?.LogWarning("Import: skipped entry with no App ID.");
                return false;
            }

            GameConfig libraryGame = EnsureLibraryEntry(entry);
            if (libraryGame == null)
            {
                Program.LogService?.LogWarning($"Import: could not add AppId {entry.AppId} to games.ini.");
                return false;
            }

            bool needsProvision = !IsGameDataProvisioned(entry.AppId);
            if (needsProvision)
            {
                GameConfig game = ToGameConfig(entry);
                NormalizeForLibrary(game);
                report?.SetMessage($"Generating Goldberg files for {libraryGame.AppName} (AppId {entry.AppId})…");
                if (!await GenerateFilesForAppIdAsync(game, report).ConfigureAwait(false))
                {
                    ClearImportPending(entry.AppId);
                    onLibraryChanged?.Invoke();
                    return false;
                }

                ClearImportPending(entry.AppId);
            }
            else
                Program.LogService?.LogMessage($"Import: AppId {entry.AppId} already provisioned; ensuring library entry.");

            onLibraryChanged?.Invoke();
            Program.LogService?.LogMessage(
                $"Import: AppId {entry.AppId} ({libraryGame.AppName}) — files at {PathConstants.GetGameSteamSettingsPath(entry.AppId)}");
            return true;
        }

        private int RegisterImportPlaceholders(IEnumerable<LegacyGameEntry> entries, Action onLibraryChanged)
        {
            if (entries == null)
                return 0;

            int count = 0;
            foreach (LegacyGameEntry entry in entries)
            {
                if (entry == null || entry.AppId == 0)
                    continue;

                if (EnsureLibraryEntry(entry) == null)
                    continue;

                if (!IsGameDataProvisioned(entry.AppId))
                {
                    SetImportPending(entry.AppId);
                    count++;
                }
            }

            if (count > 0)
                onLibraryChanged?.Invoke();

            return count;
        }

        private int MarkPendingForUnprovisionedGames(IEnumerable<GameConfig> games)
        {
            if (games == null)
                return 0;

            int count = 0;
            foreach (GameConfig game in games)
            {
                if (game == null || game.AppId == 0 || IsGameDataProvisioned(game.AppId))
                    continue;

                SetImportPending(game.AppId);
                count++;
            }

            return count;
        }

        private void SetImportPending(ulong appId)
        {
            if (appId == 0)
                return;
            lock (_pendingImportLock)
                _pendingImportAppIds.Add(appId);
        }

        private void ClearImportPending(ulong appId)
        {
            if (appId == 0)
                return;
            lock (_pendingImportLock)
                _pendingImportAppIds.Remove(appId);
        }

        private async Task<bool> GenerateFilesForAppIdAsync(GameConfig game, ITaskReportService report)
        {
            if (game == null || game.AppId == 0)
                return false;

            OnlineAppData metadata = await _gameSetupService.EnrichForImportAsync(game, report).ConfigureAwait(false);
            if (metadata == null)
            {
                metadata = new OnlineAppData
                {
                    AppId = game.AppId.ToString(),
                    Success = true,
                    DataSources = "Import"
                };
            }

            GameSettingsSaveResult saveResult = await _gameSettingsSaveService.GenerateNewGameFilesAsync(
                game,
                metadata,
                report,
                onAssetsDownloaded: null,
                onCompleted: null).ConfigureAwait(false);

            if (!saveResult.IsSuccess)
            {
                Program.LogService?.LogWarning(
                    $"Import: file generation failed for AppId {game.AppId}: {saveResult.ErrorMessage}");
                return false;
            }

            if (!IsGameDataProvisioned(game.AppId))
            {
                Program.LogService?.LogWarning(
                    $"Import: file generation reported success but provisioning markers are missing for AppId {game.AppId} ({PathConstants.GetGameSteamSettingsPath(game.AppId)}).");
                return false;
            }

            return true;
        }

        // 2.x often left only configs.*.ini; a fully provisioned game also has steam_appid.txt and installed_app_ids.txt.
        public static bool IsGameDataProvisioned(ulong appId)
        {
            if (appId == 0)
                return false;

            string steamSettingsDir = PathConstants.GetGameSteamSettingsPath(appId);
            if (!Directory.Exists(steamSettingsDir))
                return false;

            if (!File.Exists(Path.Combine(steamSettingsDir, PathConstants.GoldbergMainIniFileName)))
                return false;

            return File.Exists(Path.Combine(steamSettingsDir, PathConstants.SteamAppIdFileName))
                && File.Exists(Path.Combine(steamSettingsDir, PathConstants.GoldbergInstalledAppIdsFileName));
        }

        private GameConfig EnsureLibraryEntry(LegacyGameEntry entry)
        {
            GameConfig existing = _gameDataService.GetGameByAppId(entry.AppId);
            if (existing != null)
                return existing;

            GameConfig game = ToGameConfig(entry);
            NormalizeForLibrary(game);

            ValidationResult validation = _gameDataService.ValidateGameConfig(game);
            if (!validation.IsValid)
            {
                Program.LogService?.LogWarning($"Import: games.ini validation for AppId {entry.AppId}: {validation.ErrorMessage}");
                return null;
            }

            ValidationResult addResult = _gameDataService.AddGame(game);
            if (!addResult.IsValid)
            {
                Program.LogService?.LogWarning($"Import: AddGame for AppId {entry.AppId}: {addResult.ErrorMessage}");
                return _gameDataService.GetGameByAppId(entry.AppId);
            }

            return game;
        }

        private static void NormalizeForLibrary(GameConfig game)
        {
            if (string.IsNullOrWhiteSpace(game.AppName))
                game.AppName = game.AppId > 0 ? $"App {game.AppId}" : "Unknown";

            if (game.GameGuid == Guid.Empty)
                game.GameGuid = Guid.NewGuid();

            if (string.IsNullOrWhiteSpace(game.Path))
                game.Path = "game.exe";
        }

        private static GameConfig ToGameConfig(LegacyGameEntry entry)
        {
            string startFolder = entry.StartFolder?.Trim() ?? string.Empty;
            string path = entry.Path?.Trim() ?? string.Empty;

            if (!string.IsNullOrEmpty(startFolder) && Directory.Exists(startFolder))
            {
                string gameBase = Path.GetFullPath(startFolder);
                if (!string.IsNullOrEmpty(path) && Path.IsPathRooted(path) && File.Exists(path))
                    path = PathValidationHelper.ToRelativePathOrFileNameOrOriginal(gameBase, path);
            }

            Guid guid = entry.GameGuid;
            if (guid == Guid.Empty)
                guid = Guid.NewGuid();

            return new GameConfig
            {
                AppName = entry.AppName?.Trim() ?? string.Empty,
                AppId = entry.AppId,
                StartFolder = startFolder,
                Path = path,
                Parameters = entry.Parameters?.Trim() ?? string.Empty,
                CustomIcon = entry.CustomIcon?.Trim() ?? string.Empty,
                GameGuid = guid,
                LaunchMode = entry.UseX64 ? GoldbergLaunchMode.SteamClient : GoldbergLaunchMode.SteamDllBesideExe
            };
        }

        private void TryMigrateApplicationConfigFromLocalAppData()
        {
            string targetCfg = PathConstants.ConfigFilePath;
            string legacyCfg = PathConstants.LegacyLocalAppDataConfigFilePath;
            if (string.Equals(targetCfg, legacyCfg, StringComparison.OrdinalIgnoreCase))
                return;

            try
            {
                if (!File.Exists(targetCfg) && File.Exists(legacyCfg))
                {
                    File.Copy(legacyCfg, targetCfg);
                    Program.LogService?.LogMessage("Migration: copied application settings from LocalAppData to the install folder.");
                }
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning($"Migration: could not copy application settings from LocalAppData: {ex.Message}");
            }
        }

        private void TryMigrateApplicationConfigFromLegacyExeCfg()
        {
            string targetPath = PathConstants.ConfigFilePath;
            string legacyExePath = PathConstants.LegacyExeConfigFilePath;
            if (string.Equals(targetPath, legacyExePath, StringComparison.OrdinalIgnoreCase))
                return;
            if (File.Exists(targetPath) || !File.Exists(legacyExePath) || IsLegacyXmlConfig(legacyExePath))
                return;

            try
            {
                File.Move(legacyExePath, targetPath);
                Program.LogService?.LogMessage(
                    "Migration: moved application settings from SmartGoldbergEmu.cfg to settings.ini.");
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning(
                    $"Migration: could not move application settings from SmartGoldbergEmu.cfg: {ex.Message}");
            }
        }

        private void TryMigrateApplicationConfigViewMode()
        {
            string path = PathConstants.ConfigFilePath;
            if (!File.Exists(path))
                return;

            try
            {
                var iniService = ServiceLocator.IniFileService;
                var iniFile = iniService.ParseFile(path);
                string raw = iniService.GetValue(
                    iniFile,
                    ApplicationConstants.SettingSectionApplication,
                    ApplicationConstants.SettingKeyViewMode);
                if (string.IsNullOrWhiteSpace(raw))
                    return;

                string normalized = ApplicationConstants.NormalizeViewMode(raw);
                if (string.Equals(raw, normalized, StringComparison.OrdinalIgnoreCase))
                    return;

                iniService.SetValue(
                    iniFile,
                    ApplicationConstants.SettingSectionApplication,
                    ApplicationConstants.SettingKeyViewMode,
                    normalized);
                iniService.WriteFile(iniFile, path);
                Program.LogService?.LogMessage("Migration: normalized application view mode in settings.ini.");
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning($"Migration: could not normalize view mode in config: {ex.Message}");
            }
        }

        private void TryDeleteLegacyApiKeyFileIfRegistryHasKey()
        {
            if (!_steamApiKeyService.HasApiKey())
                return;

            try
            {
                string path = PathConstants.LegacyApiKeyFilePath;
                if (File.Exists(path))
                {
                    File.Delete(path);
                    Program.LogService?.LogMessage($"Migration: removed {PathConstants.LegacyApiKeyFileName} (key is in registry).");
                }
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning($"Migration: could not remove {PathConstants.LegacyApiKeyFileName}: {ex.Message}");
            }
        }

        private void TryMigrateGamesIniLibrary()
        {
            string gamesIniPath = PathConstants.GamesIniPath;
            if (!File.Exists(gamesIniPath))
                return;

            try
            {
                string[] lines = File.ReadAllLines(gamesIniPath);
                if (!GamesIniContainsLegacyKeys(lines))
                    return;

                List<GameConfig> games = ParseGameLibraryFromLegacyIni(lines);
                ValidationResult save = _gameDataService.SaveGameLibrary(games);
                if (save.IsValid)
                    Program.LogService?.LogMessage("Migration: rewrote games.ini to the current launch-mode format.");
                else
                    Program.LogService?.LogWarning($"Migration: could not rewrite games.ini: {save.ErrorMessage}");
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning($"Migration: could not migrate games.ini: {ex.Message}");
            }
        }

        private static bool GamesIniContainsLegacyKeys(string[] lines)
        {
            foreach (string line in lines)
            {
                string t = line.Trim();
                if (t.Length == 0 || t[0] == ';' || t[0] == '#' || !t.Contains("="))
                    continue;

                string key = t.Split(new[] { '=' }, 2)[0].Trim();
                if (IsLegacyGamesIniKey(key))
                    return true;
            }

            return false;
        }

        private static bool IsLegacyGamesIniKey(string key)
        {
            return key.Equals("deploywin32dllsbesideexe", StringComparison.OrdinalIgnoreCase)
                || key.Equals("loadlegacyapidll", StringComparison.OrdinalIgnoreCase)
                || key.Equals("LoadLegacyApiDll", StringComparison.OrdinalIgnoreCase);
        }

        private static List<GameConfig> ParseGameLibraryFromLegacyIni(string[] lines)
        {
            var games = new List<GameConfig>();
            GameConfig current = null;

            foreach (string line in lines)
            {
                string t = line.Trim();
                if (t.Length == 0 || t[0] == ';' || t[0] == '#')
                    continue;

                if (t.StartsWith("[") && t.EndsWith("]"))
                {
                    if (current != null)
                        games.Add(current);
                    current = new GameConfig();
                    continue;
                }

                if (current == null || !t.Contains("="))
                    continue;

                string[] parts = t.Split(new[] { '=' }, 2);
                if (parts.Length != 2)
                    continue;

                string key = parts[0].Trim();
                string value = parts[1].Trim();
                string keyLower = key.ToLowerInvariant();

                if (keyLower == GamesIniKeyNames.AppName)
                    current.AppName = value;
                else if (keyLower == GamesIniKeyNames.AppId)
                {
                    if (ulong.TryParse(value, out ulong appId))
                        current.AppId = appId;
                }
                else if (keyLower == GamesIniKeyNames.StartFolder)
                    current.StartFolder = value;
                else if (keyLower == GamesIniKeyNames.Path)
                    current.Path = value;
                else if (keyLower == GamesIniKeyNames.Parameters)
                    current.Parameters = value;
                else if (keyLower == GamesIniKeyNames.WorkingDirectory)
                    current.WorkingDirectory = value;
                else if (keyLower == GamesIniKeyNames.CustomIcon)
                    current.CustomIcon = value;
                else if (keyLower == GamesIniKeyNames.GameGuid)
                {
                    if (Guid.TryParse(value, out Guid guid))
                        current.GameGuid = guid;
                }
                else if (keyLower == "deploywin32dllsbesideexe" || keyLower == "loadlegacyapidll"
                    || key.Equals("LoadLegacyApiDll", StringComparison.OrdinalIgnoreCase))
                {
                    if (ParseIniBool(value))
                        current.LaunchMode = GoldbergLaunchMode.SteamDllBesideExe;
                }
                else if (keyLower == GamesIniKeyNames.GoldbergLaunchMode)
                    current.LaunchMode = ParseLegacyGoldbergLaunchMode(value);
            }

            if (current != null)
                games.Add(current);

            return games;
        }

        private static bool ParseIniBool(string value)
        {
            return value == "1"
                || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }

        private static GoldbergLaunchMode ParseLegacyGoldbergLaunchMode(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return GoldbergLaunchMode.SteamClient;
            if (value.Equals("StandardSteamApi", StringComparison.OrdinalIgnoreCase)
                || value.Equals("steamapi", StringComparison.OrdinalIgnoreCase)
                || value.Equals("regular", StringComparison.OrdinalIgnoreCase)
                || value == "1")
            {
                return GoldbergLaunchMode.StandardSteamApi;
            }
            if (value.Equals("SteamDllBesideExe", StringComparison.OrdinalIgnoreCase)
                || value.Equals("steamdll", StringComparison.OrdinalIgnoreCase)
                || value == "2")
            {
                return GoldbergLaunchMode.SteamDllBesideExe;
            }
            if (value.Equals("NoEmulation", StringComparison.OrdinalIgnoreCase)
                || value.Equals("noemulation", StringComparison.OrdinalIgnoreCase)
                || value.Equals("no emulation", StringComparison.OrdinalIgnoreCase)
                || value == "3")
            {
                return GoldbergLaunchMode.NoEmulation;
            }
            if (Enum.TryParse(value, true, out GoldbergLaunchMode parsed))
                return parsed;
            return GoldbergLaunchMode.SteamClient;
        }

        private static void TryCleanupLocalAppDataPerUserFolder()
        {
            string dir = PathConstants.LocalAppDataPerUserDirectory;
            if (!Directory.Exists(dir))
                return;

            try
            {
                foreach (string file in Directory.GetFiles(dir))
                {
                    string fileName = Path.GetFileName(file);
                    if (fileName.Equals(PathConstants.UiSettingsIniFileName, StringComparison.OrdinalIgnoreCase))
                        continue;
                    TryDeleteFileQuiet(file);
                }

                if (Directory.GetFiles(dir).Length == 0 && Directory.GetDirectories(dir).Length == 0)
                    Directory.Delete(dir, false);
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning($"Migration: could not clean LocalAppData launcher folder: {ex.Message}");
            }
        }

        private static void TryDeleteFileQuiet(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            try
            {
                File.Delete(path);
            }
            catch
            {
            }
        }
    }
}
