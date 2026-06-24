using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Forms;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;
using SteamKit;

namespace SmartGoldbergEmu.Services
{
    public class LaunchOptionService
    {
        private readonly SteamProductInfoService _steamProductInfo;
        private readonly ThemeService _themeService;

        private const string UserLaunchOptionsSection = "user::launch_options";
        private const string UserLaunchOptionKeyPrefix = "option_";
        private const string UserLaunchOptionNameSuffix = "_name";
        private const string UserLaunchOptionExecutableSuffix = "_executable";
        private const string UserLaunchOptionParametersSuffix = "_parameters";
        private const string UserLaunchOptionWorkingDirSuffix = "_workingdir";

        private string GetUserLaunchOptionsIniPath(ulong appId)
        {
            // INI path: parent of EmulatorConfigService.GetGameSteamSettingsPath (same as games\{appId}\), file name PathConstants.LauncherUserLaunchOptionsIniFileName beside steam_settings.
            var steamSettingsPath = ServiceLocator.EmulatorConfigService.GetGameSteamSettingsPath(appId);
            var gameDir = Path.GetDirectoryName(steamSettingsPath);
            if (string.IsNullOrEmpty(gameDir))
                gameDir = steamSettingsPath;
            return Path.Combine(gameDir, PathConstants.LauncherUserLaunchOptionsIniFileName);
        }

        public LaunchOptionService() : this(ServiceLocator.SteamProductInfoService, ServiceLocator.ThemeService)
        {
        }

        public LaunchOptionService(SteamProductInfoService steamProductInfo, ThemeService themeService)
        {
            _steamProductInfo = steamProductInfo ?? throw new ArgumentNullException(nameof(steamProductInfo));
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        }

        public async Task<LaunchOptionResult> ShowLaunchOptionsAsync(GameConfig game, IWin32Window parent = null, CancellationToken cancellationToken = default)
        {
            if (game == null)
                throw new ArgumentNullException(nameof(game));

            var launchOptions = await ExtractLaunchOptionsIncludingUserIniAsync(game, cancellationToken).ConfigureAwait(true);
            ServiceLocator.LogService.LogDebug($"Extracted {launchOptions?.Count ?? 0} launch options for app {game.AppId}");
            
            if (launchOptions == null || launchOptions.Count == 0)
            {
                ServiceLocator.LogService.LogDebug($"No launch options found for app {game.AppId}, launching with default executable");
                return new LaunchOptionResult { LaunchOption = null, SkipLauncher = false, Cancelled = false };
            }

            // Same filter as launch dialog initial list (saved FullLaunchOptions); form may re-filter if user toggles Show Extra.
            var filteredOptions = FilterLaunchOptionsForCurrentSettings(launchOptions);

            if (filteredOptions.Count <= 1)
            {
                ServiceLocator.LogService.LogDebug($"Found {filteredOptions.Count} launch option(s) after filtering for app {game.AppId}, auto-selecting without showing form");
                var selected = filteredOptions.Count == 1 ? filteredOptions[0] : null;
                return new LaunchOptionResult { LaunchOption = selected, SkipLauncher = false, Cancelled = false };
            }

            ServiceLocator.LogService.LogDebug($"Found {filteredOptions.Count} launch option(s) after filtering for app {game.AppId}, showing launch options form");
            try
            {
                using (var form = new LaunchOptionsForm(game, launchOptions))
                {
                    var dialogResult = form.ShowDialog(parent);
                    
                    ServiceLocator.LogService.LogDebug($"Launch options form returned: DialogResult={dialogResult}, SkipLauncher={form.SkipLauncher}, SelectedOption={form.SelectedOption?.Description ?? "null"}");
                    
                    if (dialogResult == DialogResult.Cancel)
                    {
                        return new LaunchOptionResult { LaunchOption = null, SkipLauncher = false, Cancelled = true };
                    }
                    
                    if (form.SkipLauncher)
                    {
                        return new LaunchOptionResult { LaunchOption = null, SkipLauncher = true, Cancelled = false };
                    }
                    
                    return new LaunchOptionResult { LaunchOption = form.SelectedOption, SkipLauncher = false, Cancelled = false };
                }
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService.LogError($"Error showing launch options form: {ex.Message}", ex);
                return new LaunchOptionResult { LaunchOption = null, SkipLauncher = false, Cancelled = false };
            }
        }

        public List<LaunchOption> LoadUserLaunchOptions(ulong appId)
        {
            var results = new List<LaunchOption>();
            if (appId == 0)
                return results;

            var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                void TryLoadFrom(string iniPath)
                {
                    if (string.IsNullOrEmpty(iniPath) || !File.Exists(iniPath))
                        return;

                    var iniFile = ServiceLocator.IniFileService.ParseFile(iniPath);
                    var byIndex = new Dictionary<int, UserLaunchOptionParts>();

                    for (int i = 0; i < iniFile.Lines.Count; i++)
                    {
                        var line = iniFile.Lines[i];
                        if (line == null)
                            continue;

                        if (line.Type != IniLineType.KeyValue)
                            continue;

                        if (!string.Equals(line.Section, UserLaunchOptionsSection, StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (string.IsNullOrEmpty(line.Key))
                            continue;

                        if (TryParseUserLaunchOptionKey(line.Key, out int idx, out UserLaunchOptionField field))
                        {
                            ApplyUserLaunchOptionField(GetOrCreateUserLaunchOptionParts(byIndex, idx), field, line.Value);
                            continue;
                        }
                    }

                    foreach (var kvp in byIndex)
                    {
                        var parts = kvp.Value;
                        if (parts == null)
                            continue;

                        var name = parts.Name != null ? parts.Name.Trim() : string.Empty;
                        if (string.IsNullOrWhiteSpace(name))
                            continue;

                        if (!seenNames.Add(name))
                            continue;

                        results.Add(new LaunchOption
                        {
                            Type = "user",
                            Description = name,
                            Executable = parts.Executable ?? string.Empty,
                            Parameters = parts.Parameters ?? string.Empty,
                            WorkingDir = parts.WorkingDir ?? string.Empty
                        });
                    }
                }

                // New storage location
                TryLoadFrom(GetUserLaunchOptionsIniPath(appId));

                // Legacy fallback
                var steamSettingsPath = ServiceLocator.EmulatorConfigService.GetGameSteamSettingsPath(appId);
                var legacyIniPath = Path.Combine(steamSettingsPath, PathConstants.GoldbergUserIniFileName);
                TryLoadFrom(legacyIniPath);
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService.LogWarning($"Failed to load user launch options for app {appId}: {ex.Message}");
            }

            return results;
        }

        public void SaveUserLaunchOption(ulong appId, string customName, string executable, string parameters, string workingDir)
        {
            if (appId == 0)
                return;
            if (string.IsNullOrWhiteSpace(customName))
                return;

            customName = customName.Trim();
            executable = executable ?? string.Empty;
            parameters = parameters ?? string.Empty;
            workingDir = workingDir ?? string.Empty;

            try
            {
                var iniPath = GetUserLaunchOptionsIniPath(appId);
                var gameDir = Path.GetDirectoryName(iniPath);
                if (!string.IsNullOrEmpty(gameDir))
                    Directory.CreateDirectory(gameDir);

                var iniFile = ServiceLocator.IniFileService.ParseFile(iniPath);

                // Prevent duplicate entries for the same name: if multiple indices exist,
                // keep the first and remove the rest.
                var existingIndices = FindUserLaunchOptionIndicesByName(iniFile, customName);
                int index;
                if (existingIndices.Count > 0)
                {
                    index = existingIndices[0];
                    for (int i = 1; i < existingIndices.Count; i++)
                        RemoveUserLaunchOptionIndex(iniFile, existingIndices[i]);
                }
                else
                {
                    index = FindNextUserLaunchOptionIndex(iniFile);
                }

                SetUserLaunchOptionFields(iniFile, index, customName, executable, parameters, workingDir);
                ServiceLocator.IniFileService.WriteFile(iniFile, iniPath);
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService.LogWarning($"Failed to save user launch option '{customName}' for app {appId}: {ex.Message}");
            }
        }

        public bool RemoveUserLaunchOption(ulong appId, string customName)
        {
            if (appId == 0)
                return false;
            if (string.IsNullOrWhiteSpace(customName))
                return false;

            customName = customName.Trim();

            try
            {
                bool removed = TryRemoveFromIni(GetUserLaunchOptionsIniPath(appId), customName);
                if (removed)
                    return true;

                // Legacy fallback
                var steamSettingsPath = ServiceLocator.EmulatorConfigService.GetGameSteamSettingsPath(appId);
                var legacyIniPath = Path.Combine(steamSettingsPath, PathConstants.GoldbergUserIniFileName);
                return TryRemoveFromIni(legacyIniPath, customName);
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService.LogWarning($"Failed to remove user launch option '{customName}' for app {appId}: {ex.Message}");
                return false;
            }
        }

        private bool TryRemoveFromIni(string iniPath, string customName)
        {
            if (string.IsNullOrEmpty(iniPath) || !File.Exists(iniPath))
                return false;

            var iniFile = ServiceLocator.IniFileService.ParseFile(iniPath);
            var indices = FindUserLaunchOptionIndicesByName(iniFile, customName);
            if (indices.Count == 0)
                return false;

            for (int i = 0; i < indices.Count; i++)
                RemoveUserLaunchOptionIndex(iniFile, indices[i]);

            ServiceLocator.IniFileService.WriteFile(iniFile, iniPath);
            return true;
        }

        private enum UserLaunchOptionField
        {
            Name,
            Executable,
            Parameters,
            WorkingDir
        }

        private class UserLaunchOptionParts
        {
            public string Name;
            public string Executable;
            public string Parameters;
            public string WorkingDir;
        }

        private static UserLaunchOptionParts GetOrCreateUserLaunchOptionParts(Dictionary<int, UserLaunchOptionParts> byIndex, int index)
        {
            if (!byIndex.TryGetValue(index, out UserLaunchOptionParts parts))
                byIndex[index] = parts = new UserLaunchOptionParts();
            return parts;
        }

        private static void ApplyUserLaunchOptionField(UserLaunchOptionParts parts, UserLaunchOptionField field, string value)
        {
            switch (field)
            {
                case UserLaunchOptionField.Name:
                    parts.Name = value;
                    break;
                case UserLaunchOptionField.Executable:
                    parts.Executable = value;
                    break;
                case UserLaunchOptionField.Parameters:
                    parts.Parameters = value;
                    break;
                case UserLaunchOptionField.WorkingDir:
                    parts.WorkingDir = value;
                    break;
            }
        }

        private static bool TryParseUserLaunchOptionKey(string key, out int index, out UserLaunchOptionField field)
        {
            index = -1;
            field = UserLaunchOptionField.Name;
            if (TryParseUserLaunchOptionIndex(key, UserLaunchOptionNameSuffix, out index))
            {
                field = UserLaunchOptionField.Name;
                return true;
            }
            if (TryParseUserLaunchOptionIndex(key, UserLaunchOptionExecutableSuffix, out index))
            {
                field = UserLaunchOptionField.Executable;
                return true;
            }
            if (TryParseUserLaunchOptionIndex(key, UserLaunchOptionParametersSuffix, out index))
            {
                field = UserLaunchOptionField.Parameters;
                return true;
            }
            if (TryParseUserLaunchOptionIndex(key, UserLaunchOptionWorkingDirSuffix, out index))
            {
                field = UserLaunchOptionField.WorkingDir;
                return true;
            }
            return false;
        }

        private static bool TryParseUserLaunchOptionIndex(string key, string suffix, out int index)
        {
            index = -1;
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(suffix))
                return false;

            if (!key.StartsWith(UserLaunchOptionKeyPrefix, StringComparison.OrdinalIgnoreCase))
                return false;
            if (!key.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return false;

            // key format: option_<index>_<suffix>
            var mid = key.Substring(UserLaunchOptionKeyPrefix.Length, key.Length - UserLaunchOptionKeyPrefix.Length - suffix.Length);
            int parsed;
            if (!int.TryParse(mid, out parsed))
                return false;

            index = parsed;
            return true;
        }

        private static int? FindUserLaunchOptionIndexByName(IniFile iniFile, string customName)
        {
            int? foundIndex = null;
            var nameToFind = customName.Trim();

            for (int i = 0; i < iniFile.Lines.Count; i++)
            {
                var line = iniFile.Lines[i];
                if (line == null)
                    continue;

                if (line.Type != IniLineType.KeyValue)
                    continue;
                if (!string.Equals(line.Section, UserLaunchOptionsSection, StringComparison.OrdinalIgnoreCase))
                    continue;

                int idx;
                if (!TryParseUserLaunchOptionIndex(line.Key, UserLaunchOptionNameSuffix, out idx))
                    continue;

                if (!string.IsNullOrEmpty(line.Value) &&
                    string.Equals(line.Value.Trim(), nameToFind, StringComparison.OrdinalIgnoreCase))
                {
                    foundIndex = idx;
                    break;
                }
            }

            return foundIndex;
        }

        private static int FindNextUserLaunchOptionIndex(IniFile iniFile)
        {
            int maxIndex = 0;
            for (int i = 0; i < iniFile.Lines.Count; i++)
            {
                var line = iniFile.Lines[i];
                if (line == null)
                    continue;

                if (line.Type != IniLineType.KeyValue)
                    continue;
                if (!string.Equals(line.Section, UserLaunchOptionsSection, StringComparison.OrdinalIgnoreCase))
                    continue;

                int idx;
                if (TryParseUserLaunchOptionIndex(line.Key, UserLaunchOptionNameSuffix, out idx))
                {
                    if (idx > maxIndex)
                        maxIndex = idx;
                }
            }

            return maxIndex + 1;
        }

        private static List<int> FindUserLaunchOptionIndicesByName(IniFile iniFile, string customName)
        {
            var indices = new List<int>();
            var nameToFind = customName.Trim();

            for (int i = 0; i < iniFile.Lines.Count; i++)
            {
                var line = iniFile.Lines[i];
                if (line == null)
                    continue;

                if (line.Type != IniLineType.KeyValue)
                    continue;
                if (!string.Equals(line.Section, UserLaunchOptionsSection, StringComparison.OrdinalIgnoreCase))
                    continue;

                int idx;
                if (!TryParseUserLaunchOptionIndex(line.Key, UserLaunchOptionNameSuffix, out idx))
                    continue;

                if (!string.IsNullOrEmpty(line.Value) &&
                    string.Equals(line.Value.Trim(), nameToFind, StringComparison.OrdinalIgnoreCase))
                {
                    indices.Add(idx);
                }
            }

            return indices;
        }

        private static void SetUserLaunchOptionFields(IniFile iniFile, int index, string customName, string executable, string parameters, string workingDir)
        {
            string nameKey = UserLaunchOptionKeyPrefix + index + UserLaunchOptionNameSuffix;
            string exeKey = UserLaunchOptionKeyPrefix + index + UserLaunchOptionExecutableSuffix;
            string prmKey = UserLaunchOptionKeyPrefix + index + UserLaunchOptionParametersSuffix;
            string wdKey = UserLaunchOptionKeyPrefix + index + UserLaunchOptionWorkingDirSuffix;

            ServiceLocator.IniFileService.SetValue(iniFile, UserLaunchOptionsSection, nameKey, customName);
            ServiceLocator.IniFileService.SetValue(iniFile, UserLaunchOptionsSection, exeKey, executable);
            ServiceLocator.IniFileService.SetValue(iniFile, UserLaunchOptionsSection, prmKey, parameters);
            ServiceLocator.IniFileService.SetValue(iniFile, UserLaunchOptionsSection, wdKey, workingDir);
        }

        private static void RemoveUserLaunchOptionIndex(IniFile iniFile, int index)
        {
            string nameKey = UserLaunchOptionKeyPrefix + index + UserLaunchOptionNameSuffix;
            string exeKey = UserLaunchOptionKeyPrefix + index + UserLaunchOptionExecutableSuffix;
            string prmKey = UserLaunchOptionKeyPrefix + index + UserLaunchOptionParametersSuffix;
            string wdKey = UserLaunchOptionKeyPrefix + index + UserLaunchOptionWorkingDirSuffix;

            ServiceLocator.IniFileService.SetValue(iniFile, UserLaunchOptionsSection, nameKey, null, true);
            ServiceLocator.IniFileService.SetValue(iniFile, UserLaunchOptionsSection, exeKey, null, true);
            ServiceLocator.IniFileService.SetValue(iniFile, UserLaunchOptionsSection, prmKey, null, true);
            ServiceLocator.IniFileService.SetValue(iniFile, UserLaunchOptionsSection, wdKey, null, true);
        }

        // Async PICS fetch so callers can await without blocking the WinForms message pump.
        public async Task<List<LaunchOption>> ExtractLaunchOptionsAsync(GameConfig game, CancellationToken cancellationToken = default)
        {
            if (TryGetEmptyListIfInvalidGame(game, out List<LaunchOption> early))
                return early;

            try
            {
                KeyValue kv = await _steamProductInfo.WarmGameConfigAppPicsRootAsync(game, cancellationToken).ConfigureAwait(false);
                return FinishExtractLaunchOptions(game, kv);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService.LogError($"Failed to extract launch options for app {game.AppId}: {ex.Message}", ex);
                return new List<LaunchOption>();
            }
        }

        // Same Steam/PICS extraction plus user.launch.options.ini merge as used before LaunchOptionsForm.
        public async Task<List<LaunchOption>> ExtractLaunchOptionsIncludingUserIniAsync(GameConfig game, CancellationToken cancellationToken = default)
        {
            List<LaunchOption> all = await ExtractLaunchOptionsAsync(game, cancellationToken).ConfigureAwait(false);
            if (all == null)
                all = new List<LaunchOption>();

            if (game != null && game.AppId != 0)
            {
                List<LaunchOption> userOptions = LoadUserLaunchOptions(game.AppId);
                if (userOptions != null && userOptions.Count > 0)
                    all.AddRange(userOptions);
            }

            return all;
        }

        private static bool TryGetEmptyListIfInvalidGame(GameConfig game, out List<LaunchOption> empty)
        {
            if (game == null)
            {
                ServiceLocator.LogService.LogWarning("ExtractLaunchOptions: game is null");
                empty = new List<LaunchOption>();
                return true;
            }
            if (game.AppId == 0)
            {
                empty = new List<LaunchOption>();
                return true;
            }
            empty = null;
            return false;
        }

        private List<LaunchOption> FinishExtractLaunchOptions(GameConfig game, KeyValue kv)
        {
            if (kv == null)
            {
                ServiceLocator.LogService.LogWarning($"ExtractLaunchOptions: game assets KeyValue root is null for app {game.AppId}");
                return new List<LaunchOption>();
            }

            var options = ExtractLaunchOptionsFromKeyValue(kv);
            ServiceLocator.LogService.LogMessage(
                $"ExtractLaunchOptions: app {game.AppId} ({game.AppName}) -> {options.Count} Windows launch option(s)");
            return options;
        }

        private List<LaunchOption> ExtractLaunchOptionsFromKeyValue(KeyValue root)
        {
            var launchOptions = new List<LaunchOption>();
            if (!HasKvChildren(root))
                return launchOptions;

            KeyValue appInfo = FindKvChild(root, SteamPicsKeyNames.AppInfo);
            KeyValue targetNode = HasKvChildren(appInfo) ? appInfo : root;

            if (!HasKvChildren(targetNode))
                return launchOptions;

            ResolveLaunchNode(targetNode, out KeyValue launchNode, out string launchPath);
            if (launchNode == null || !HasKvChildren(launchNode))
                return launchOptions;

            foreach (KeyValue entry in launchNode.Children)
                TryAddLaunchOptionFromEntry(entry, launchPath, launchOptions);

            return launchOptions;
        }

        private void ResolveLaunchNode(KeyValue targetNode, out KeyValue launchNode, out string launchPath)
        {
            launchNode = null;
            launchPath = string.Empty;

            KeyValue configNode = FindKvChild(targetNode, SteamPicsKeyNames.Config);
            if (HasKvChildren(configNode))
            {
                launchNode = FindKvChild(configNode, SteamPicsKeyNames.LaunchOverride);
                if (launchNode != null)
                {
                    launchPath = "config/launch_override";
                    return;
                }
                launchNode = FindKvChild(configNode, SteamPicsKeyNames.Launch);
                if (launchNode != null)
                {
                    launchPath = "config/launch";
                    return;
                }
            }

            KeyValue commonNode = FindKvChild(targetNode, PathConstants.SteamAppsCommonDirectoryName);
            if (HasKvChildren(commonNode))
            {
                launchNode = FindKvChild(commonNode, SteamPicsKeyNames.Launch);
                if (launchNode != null)
                {
                    launchPath = "common/launch";
                    return;
                }
            }

            launchNode = FindKvChild(targetNode, SteamPicsKeyNames.Launch);
            if (launchNode != null)
                launchPath = "root/launch";
        }

        private void TryAddLaunchOptionFromEntry(KeyValue launchOptionNode, string launchPath, List<LaunchOption> launchOptions)
        {
            if (!HasKvChildren(launchOptionNode))
                return;

            string entryKey = launchOptionNode.Name ?? string.Empty;
            var opt = new LaunchOption
            {
                Description = FindKvChild(launchOptionNode, SteamPicsKeyNames.Description)?.Value,
                Executable = KvValueOrAlternateKey(launchOptionNode, SteamPicsKeyNames.Executable, SteamPicsKeyNames.Launch),
                Type = FindKvChild(launchOptionNode, SteamPicsKeyNames.Type)?.Value,
                Parameters = KvValueOrAlternateKey(launchOptionNode, SteamPicsKeyNames.Arguments, SteamPicsKeyNames.CommandLine),
                WorkingDir = FindKvChild(launchOptionNode, SteamPicsKeyNames.WorkingDir)?.Value
            };

            bool isWindows = true;
            KeyValue cfg = FindKvChild(launchOptionNode, SteamPicsKeyNames.Config);
            if (HasKvChildren(cfg))
            {
                KeyValue oslistNode = FindKvChild(cfg, SteamPicsKeyNames.OsList);
                if (oslistNode != null)
                {
                    string oslist = oslistNode.Value ?? string.Empty;
                    isWindows = oslist.ToLowerInvariant().Contains("windows");
                }

                string betaKey = GetKvChildStringCaseInsensitive(cfg, SteamPicsKeyNames.BetaKey);
                if (!string.IsNullOrEmpty(betaKey))
                    opt.BetaKey = betaKey;

                string osArchCfg = GetKvChildStringCaseInsensitive(cfg, SteamPicsKeyNames.OsArch);
                if (!string.IsNullOrEmpty(osArchCfg))
                    opt.OsArch = osArchCfg;
            }

            if (string.IsNullOrEmpty(opt.OsArch))
            {
                string rootOsArch = GetKvChildStringCaseInsensitive(launchOptionNode, SteamPicsKeyNames.OsArch);
                if (!string.IsNullOrEmpty(rootOsArch))
                    opt.OsArch = rootOsArch;
            }

            bool osArchOk = HostMatchesLaunchOsArch(opt.OsArch);
            if (isWindows && osArchOk && (!string.IsNullOrEmpty(opt.Description) || !string.IsNullOrEmpty(opt.Executable)))
                launchOptions.Add(opt);
            else
            {
                string skipReason = !isWindows ? "not Windows" : (!osArchOk ? "osarch mismatch" : "missing description/executable");
                ServiceLocator.LogService.LogDebug(
                    $"Launch option skipped ({skipReason}) entry={entryKey} section={launchPath}");
            }
        }

        private static string KvValueOrAlternateKey(KeyValue parent, string primaryKey, string fallbackKey)
        {
            KeyValue primary = FindKvChild(parent, primaryKey);
            if (primary != null)
                return primary.Value;
            return FindKvChild(parent, fallbackKey)?.Value;
        }

        private static bool HasKvChildren(KeyValue k) => k?.Children != null && k.Children.Count > 0;

        private static KeyValue FindKvChild(KeyValue parent, string name)
        {
            if (parent?.Children == null || string.IsNullOrEmpty(name))
                return null;
            foreach (KeyValue c in parent.Children)
            {
                if (c != null && string.Equals(c.Name, name, StringComparison.Ordinal))
                    return c;
            }
            return null;
        }

        private static string GetKvChildStringCaseInsensitive(KeyValue parent, string keyName)
        {
            if (parent?.Children == null || string.IsNullOrEmpty(keyName))
                return null;
            foreach (KeyValue c in parent.Children)
            {
                if (c != null && c.Name.Equals(keyName, StringComparison.OrdinalIgnoreCase))
                {
                    string s = c.Value;
                    return string.IsNullOrEmpty(s) ? null : s.Trim();
                }
            }
            return null;
        }

        public bool ShouldExcludeRestrictedLaunchTypes(AppDataService appDataService = null)
        {
            var appData = appDataService ?? ServiceLocator.AppDataService;
            return !appData.LoadApplicationSettings().FullLaunchOptions;
        }

        public List<LaunchOption> FilterLaunchOptionsForCurrentSettings(List<LaunchOption> allOptions, AppDataService appDataService = null)
        {
            return FilterLaunchOptionsForUi(allOptions, ShouldExcludeRestrictedLaunchTypes(appDataService));
        }

        public List<LaunchOption> FilterLaunchOptionsForUi(List<LaunchOption> options, bool excludeRestrictedTypes)
        {
            if (options == null)
                return new List<LaunchOption>();

            var filtered = FilterOptions(options, excludeRestrictedTypes);
            return filtered
                .OrderBy(opt => string.IsNullOrEmpty(opt.Type) || opt.Type.Equals("default", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ToList();
        }

        public static int FindDefaultLaunchOptionIndex(IList<LaunchOption> options)
        {
            if (options == null || options.Count == 0)
                return 0;
            for (int i = 0; i < options.Count; i++)
            {
                string t = options[i].Type;
                if (!string.IsNullOrEmpty(t) && t.Equals("default", StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return 0;
        }

        public static bool LaunchOptionMatchesFields(LaunchOption option, string executable, string parameters, string workingDir)
        {
            if (option == null)
                return false;
            return string.Equals(NormalizeLaunchToken(option.Executable), NormalizeLaunchToken(executable), StringComparison.OrdinalIgnoreCase)
                && string.Equals(NormalizeLaunchToken(option.Parameters), NormalizeLaunchToken(parameters), StringComparison.OrdinalIgnoreCase)
                && string.Equals(NormalizeLaunchToken(option.WorkingDir), NormalizeLaunchToken(workingDir), StringComparison.OrdinalIgnoreCase);
        }

        public static string NormalizeLaunchToken(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            return value.Trim().Replace('/', Path.DirectorySeparatorChar);
        }

        public static string ToDisplayPathForGameFolder(string launchPath, string gameFolder)
        {
            string normalized = NormalizeLaunchToken(launchPath);
            if (string.IsNullOrEmpty(normalized))
                return string.Empty;

            if (!string.IsNullOrEmpty(gameFolder) &&
                Directory.Exists(gameFolder) &&
                Path.IsPathRooted(normalized))
            {
                return PathValidationHelper.ToDisplayPathRelativeToGameFolder(normalized, gameFolder);
            }

            return normalized;
        }

        public static string FormatLaunchOptionComboLabel(LaunchOption option, string gameDisplayName)
        {
            if (option == null)
                return string.Empty;

            string baseLabel;
            if (!string.IsNullOrEmpty(option.Description))
                baseLabel = option.Description.Trim();
            else if (!string.IsNullOrWhiteSpace(gameDisplayName))
                baseLabel = gameDisplayName.Trim();
            else if (!string.IsNullOrEmpty(option.Executable))
                baseLabel = option.Executable.Trim();
            else
                baseLabel = "Launch option";

            if (!option.IsHiddenWhenFullLaunchOptionsOff())
                return baseLabel;

            var tags = new List<string>();

            if (!string.IsNullOrEmpty(option.BetaKey))
                tags.Add("beta");

            if (!string.IsNullOrEmpty(option.Type))
            {
                string t = option.Type.Trim();
                if (!t.Equals(SteamPicsKeyNames.Default, StringComparison.OrdinalIgnoreCase))
                {
                    if (t.Equals(SteamPicsKeyNames.LaunchOptionTypeBetaKey, StringComparison.OrdinalIgnoreCase) ||
                        t.Equals(SteamPicsKeyNames.LaunchOptionTypeBeta, StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrEmpty(option.BetaKey))
                            tags.Add("beta");
                    }
                    else if (t.Equals(SteamPicsKeyNames.LaunchOptionTypeDeveloper, StringComparison.OrdinalIgnoreCase))
                    {
                        tags.Add("dev");
                    }
                    else
                    {
                        tags.Add(t.ToLowerInvariant());
                    }
                }
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var unique = new List<string>();
            foreach (var tag in tags)
            {
                if (seen.Add(tag))
                    unique.Add(tag);
            }

            if (unique.Count == 0)
                return baseLabel;

            return string.Join(" ", unique.Select(x => "[" + x + "]")) + " - " + baseLabel;
        }

        public static bool LaunchFieldsMatchBaseline(string executable, string parameters, string workingDir, GameConfig baseline)
        {
            if (baseline == null)
                return true;

            var baselineOption = new LaunchOption
            {
                Executable = baseline.Path,
                Parameters = baseline.Parameters,
                WorkingDir = baseline.WorkingDirectory
            };

            return LaunchOptionMatchesFields(
                baselineOption,
                executable ?? string.Empty,
                parameters ?? string.Empty,
                workingDir ?? string.Empty);
        }

        public static int FindBestMatchingLaunchOptionIndex(IList<LaunchOption> options, string executable, string parameters, string workingDir)
        {
            if (options == null || options.Count == 0)
                return -1;

            string exe = executable != null ? executable.Trim() : string.Empty;
            string prm = parameters != null ? parameters.Trim() : string.Empty;
            string wd = workingDir != null ? workingDir.Trim() : string.Empty;

            for (int i = 0; i < options.Count; i++)
            {
                if (LaunchOptionMatchesFields(options[i], exe, prm, wd))
                    return i;
            }

            return -1;
        }

        public static string ResolveGameDisplayName(string formGameName, string configGameName)
        {
            if (!string.IsNullOrWhiteSpace(formGameName))
                return formGameName.Trim();
            if (!string.IsNullOrWhiteSpace(configGameName))
                return configGameName.Trim();
            return string.Empty;
        }

        public static bool IsUserLaunchOption(LaunchOption option)
        {
            return option != null &&
                   !string.IsNullOrEmpty(option.Type) &&
                   option.Type.Equals("user", StringComparison.OrdinalIgnoreCase);
        }

        public static ulong ResolveLaunchOptionAppId(GameConfig game, string appIdText)
        {
            ulong appId = game != null ? game.AppId : 0;
            if (appId == 0 && !string.IsNullOrWhiteSpace(appIdText) && ulong.TryParse(appIdText.Trim(), out ulong parsed))
                appId = parsed;
            return appId;
        }

        public static bool IsMatchingUserLaunchOption(LaunchOption option, string customName)
        {
            return IsUserLaunchOption(option) &&
                   string.Equals(option.Description ?? string.Empty, customName ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        public static int FindUserLaunchOptionIndexByName(IList<LaunchOption> options, string customName)
        {
            if (options == null || options.Count == 0 || string.IsNullOrWhiteSpace(customName))
                return -1;

            for (int i = 0; i < options.Count; i++)
            {
                if (IsMatchingUserLaunchOption(options[i], customName))
                    return i;
            }

            return -1;
        }

        public static string NormalizeCustomLaunchOptionName(string customName)
        {
            return string.IsNullOrWhiteSpace(customName) ? string.Empty : customName.Trim();
        }

        private List<LaunchOption> FilterOptions(List<LaunchOption> options, bool excludeConfigType)
        {
            if (!excludeConfigType)
            {
                return options;
            }

            return options.Where(opt =>
            {
                if (opt == null)
                    return false;

                // Even though user options are marked as "hidden" for tag purposes,
                // they must never be filtered out by the "extra options" checkbox.
                if (!string.IsNullOrEmpty(opt.Type) && opt.Type.Equals("user", StringComparison.OrdinalIgnoreCase))
                    return true;

                return !opt.IsHiddenWhenFullLaunchOptionsOff();
            }).ToList();
        }

        private static bool HostCpuIsArm64()
        {
            try
            {
                return RuntimeInformation.OSArchitecture == Architecture.Arm64;
            }
            catch
            {
                return false;
            }
        }

        private static bool HostMatchesLaunchOsArch(string osArchFromVdf)
        {
            if (string.IsNullOrWhiteSpace(osArchFromVdf))
                return true;

            string s = osArchFromVdf.Trim().ToLowerInvariant();
            bool osIs64Bit = Environment.Is64BitOperatingSystem;

            if (s == "arm64" || s == "aarch64")
                return HostCpuIsArm64();

            if (s == "64" || s == "win64" || s == "x64" || s == "amd64")
                return osIs64Bit;

            if (s == "32" || s == "win32" || s == "x86" || s == "i386")
                return !osIs64Bit;

            if (s.IndexOf("arm64", StringComparison.Ordinal) >= 0 || s.IndexOf("aarch64", StringComparison.Ordinal) >= 0)
                return HostCpuIsArm64();

            bool has64 = s.IndexOf("64", StringComparison.Ordinal) >= 0;
            bool has32 = s.IndexOf("32", StringComparison.Ordinal) >= 0;
            if (has64 && !has32)
                return osIs64Bit;
            if (has32 && !has64)
                return !osIs64Bit;

            return true;
        }

    }
}

