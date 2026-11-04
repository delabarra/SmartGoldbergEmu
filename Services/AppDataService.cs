using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Windows.Forms;
using SmartGoldbergEmu.JsonKit;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Services
{
    public class AppDataService
    {
        private readonly string _configFilePath;
        private readonly string _uiSettingsFilePath;
        private readonly string _globalSettingsPath;
        private readonly IniFileService _iniService;
        private readonly ITaskReportService _taskReportService;

        private ITaskReportService Feedback => _taskReportService ?? ServiceLocator.TaskReportService;

        private void EnsureConfigDirectoryExists()
        {
            var configDirectory = Path.GetDirectoryName(_configFilePath);
            if (!string.IsNullOrEmpty(configDirectory))
                Directory.CreateDirectory(configDirectory);
        }

        private void EnsureUiSettingsDirectoryExists()
        {
            var uiSettingsDirectory = Path.GetDirectoryName(_uiSettingsFilePath);
            if (!string.IsNullOrEmpty(uiSettingsDirectory))
                Directory.CreateDirectory(uiSettingsDirectory);
        }

        private T GetAppSetting<T>(Func<ApplicationSettings, T> selector)
        {
            return selector(LoadApplicationSettings());
        }

        private string GetAppStringSetting(Func<ApplicationSettings, string> selector, string defaultValue)
        {
            var settings = LoadApplicationSettings();
            return selector(settings) ?? defaultValue;
        }

        private ValidationResult SetAppSetting(Action<ApplicationSettings> mutate, string failureMessagePrefix)
        {
            try
            {
                var settings = LoadApplicationSettings();
                mutate(settings);
                return SaveApplicationSettings(settings);
            }
            catch (Exception ex)
            {
                return ValidationResult.Failure($"{failureMessagePrefix}: {ex.Message}");
            }
        }

        private ValidationResult SetAppStringSetting(string value, string emptyError, Action<ApplicationSettings, string> assign, string failureMessagePrefix)
        {
            try
            {
                if (string.IsNullOrEmpty(value))
                    return ValidationResult.Failure(emptyError);
                var settings = LoadApplicationSettings();
                assign(settings, value);
                return SaveApplicationSettings(settings);
            }
            catch (Exception ex)
            {
                return ValidationResult.Failure($"{failureMessagePrefix}: {ex.Message}");
            }
        }

        public AppDataService() : this(ServiceLocator.IniFileService, null)
        {
        }

        public AppDataService(IniFileService iniService, ITaskReportService feedbackService = null)
        {
            _configFilePath = PathConstants.ConfigFilePath;
            _uiSettingsFilePath = PathConstants.UiSettingsFilePath;
            _globalSettingsPath = PathConstants.GlobalSettingsPath;
            _iniService = iniService ?? throw new ArgumentNullException(nameof(iniService));
            _taskReportService = feedbackService;
        }

        public AppDataService(
            string configFilePath,
            string globalSettingsPath,
            string uiSettingsFilePath = null,
            IniFileService iniService = null,
            ITaskReportService feedbackService = null)
        {
            _configFilePath = configFilePath;
            _uiSettingsFilePath = uiSettingsFilePath ?? PathConstants.UiSettingsFilePath;
            _globalSettingsPath = globalSettingsPath;
            _iniService = iniService ?? ServiceLocator.IniFileService;
            _taskReportService = feedbackService;
        }

        public ApplicationSettings LoadApplicationSettings()
        {
            try
            {
                var settings = new ApplicationSettings();

                if (File.Exists(_configFilePath))
                {
                    var iniFile = _iniService.ParseFile(_configFilePath);
                    LoadApplicationSettingsFromConfig(iniFile, settings);
                }

                ApplyUiSettings(settings);
                return settings;
            }
            catch (Exception ex)
            {
                LogRedactionHelper.WriteDebug($"Failed to load application settings: {ex.Message}");
                return new ApplicationSettings();
            }
        }

        // One-time migration: move theme and main-window settings from settings.ini into ui_settings.ini.
        public void TryMigrateUiSettingsFromConfig()
        {
            try
            {
                var uiIni = File.Exists(_uiSettingsFilePath)
                    ? _iniService.ParseFile(_uiSettingsFilePath)
                    : new IniFile();
                bool migrated = false;

                foreach (string configPath in GetUiSettingsMigrationConfigPaths())
                {
                    if (!File.Exists(configPath))
                        continue;

                    var configIni = _iniService.ParseFile(configPath);
                    bool configChanged = false;

                    configChanged |= MigrateUiSetting(configIni, uiIni, ApplicationConstants.SettingSectionApplication, ApplicationConstants.SettingKeyThemeMode);
                    configChanged |= MigrateUiSetting(configIni, uiIni, ApplicationConstants.SettingSectionWindow, ApplicationConstants.SettingKeyWindowSize);
                    configChanged |= MigrateUiSetting(configIni, uiIni, ApplicationConstants.SettingSectionWindow, ApplicationConstants.SettingKeyWindowLocation);
                    configChanged |= MigrateUiSetting(configIni, uiIni, ApplicationConstants.SettingSectionWindow, ApplicationConstants.SettingKeyWindowState);

                    if (configChanged)
                    {
                        _iniService.WriteFile(configIni, configPath);
                        migrated = true;
                    }
                }

                if (migrated)
                {
                    EnsureUiSettingsDirectoryExists();
                    _iniService.WriteFile(uiIni, _uiSettingsFilePath);
                    ServiceLocator.LogService?.LogMessage("Migration: moved UI settings to ui_settings.ini.");
                }
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService?.LogWarning($"Migration: could not move UI settings to ui_settings.ini: {ex.Message}");
            }
        }

        private IEnumerable<string> GetUiSettingsMigrationConfigPaths()
        {
            yield return _configFilePath;
        }

        private bool MigrateUiSetting(IniFile sourceIni, IniFile targetIni, string section, string key)
        {
            if (!string.IsNullOrEmpty(_iniService.GetValue(targetIni, section, key)))
                return false;

            string value = _iniService.GetValue(sourceIni, section, key);
            if (string.IsNullOrEmpty(value))
                return false;

            _iniService.SetValue(targetIni, section, key, value);
            return _iniService.RemoveValue(sourceIni, section, key);
        }

        private void LoadApplicationSettingsFromConfig(IniFile iniFile, ApplicationSettings settings)
        {
            string viewMode = _iniService.GetValue(iniFile, ApplicationConstants.SettingSectionApplication, ApplicationConstants.SettingKeyViewMode);
            settings.ViewMode = string.IsNullOrWhiteSpace(viewMode) ? ApplicationConstants.ViewModeDefault : viewMode;
            settings.SortBy = _iniService.GetValue(iniFile, ApplicationConstants.SettingSectionApplication, ApplicationConstants.SettingKeySortBy) ?? ApplicationConstants.SortByDefault;
            settings.SortDirection = _iniService.GetValue(iniFile, ApplicationConstants.SettingSectionApplication, ApplicationConstants.SettingKeySortDirection) ?? ApplicationConstants.SortDirectionDefault;
            settings.DetailsColumnOrder = _iniService.GetValue(iniFile, ApplicationConstants.SettingSectionApplication, ApplicationConstants.SettingKeyDetailsColumnOrder) ?? ApplicationConstants.DefaultColumnOrder;
            var rawDetailsColumnWidths = _iniService.GetValue(iniFile, ApplicationConstants.SettingSectionApplication, ApplicationConstants.SettingKeyDetailsColumnWidths);
            settings.DetailsColumnWidths = ApplicationConstants.NormalizeDetailsColumnWidths(rawDetailsColumnWidths);

            var fullLaunchOptionsStr = _iniService.GetValue(iniFile, ApplicationConstants.SettingSectionApplication, "full_launch_options");
            if (bool.TryParse(fullLaunchOptionsStr, out bool fullLaunchOptions))
                settings.FullLaunchOptions = fullLaunchOptions;
            else
                settings.FullLaunchOptions = false;

            var logosViewDropShadowStr = _iniService.GetValue(
                iniFile,
                ApplicationConstants.SettingSectionApplication,
                ApplicationConstants.SettingKeyLogosViewDropShadow);
            if (bool.TryParse(logosViewDropShadowStr, out bool logosViewDropShadow))
                settings.LogosViewDropShadow = logosViewDropShadow;
            else
                settings.LogosViewDropShadow = true;

            // Prefer lan_enabled; fall back to legacy lan_players_online (stripped on save).
            var lanEnabledStr = _iniService.GetValue(
                iniFile,
                ApplicationConstants.SettingSectionApplication,
                ApplicationConstants.SettingKeyLanEnabled);
            if (string.IsNullOrWhiteSpace(lanEnabledStr))
            {
                lanEnabledStr = _iniService.GetValue(
                    iniFile,
                    ApplicationConstants.SettingSectionApplication,
                    "lan_players_online");
            }
            if (bool.TryParse(lanEnabledStr, out bool lanEnabled))
                settings.LanEnabled = lanEnabled;
            else
                settings.LanEnabled = false;

            settings.LanPersonaState = LanPersonaStateHelper.ParseSettings(
                _iniService.GetValue(
                    iniFile,
                    ApplicationConstants.SettingSectionApplication,
                    ApplicationConstants.SettingKeyLanPersonaState));
            settings.LanCustomStatus = TruncateLanCustomStatus(
                _iniService.GetValue(
                    iniFile,
                    ApplicationConstants.SettingSectionApplication,
                    ApplicationConstants.SettingKeyLanCustomStatus));
            settings.LanFavoriteSteamIds = ParseLanFavoriteSteamIds(
                _iniService.GetValue(
                    iniFile,
                    ApplicationConstants.SettingSectionApplication,
                    ApplicationConstants.SettingKeyLanFavoriteSteamIds));
            settings.LanMutedSteamIds = ParseLanFavoriteSteamIds(
                _iniService.GetValue(
                    iniFile,
                    ApplicationConstants.SettingSectionApplication,
                    ApplicationConstants.SettingKeyLanMutedSteamIds));
            settings.LanChatBlockedSteamIds = ParseLanFavoriteSteamIds(
                _iniService.GetValue(
                    iniFile,
                    ApplicationConstants.SettingSectionApplication,
                    ApplicationConstants.SettingKeyLanChatBlockedSteamIds));
            settings.LanChatTrustedSteamIds = ParseLanFavoriteSteamIds(
                _iniService.GetValue(
                    iniFile,
                    ApplicationConstants.SettingSectionApplication,
                    ApplicationConstants.SettingKeyLanChatTrustedSteamIds));
            settings.LanSeenPeers = ParseLanSeenPeers(
                _iniService.GetValue(
                    iniFile,
                    ApplicationConstants.SettingSectionApplication,
                    ApplicationConstants.SettingKeyLanSeenPeers));

            var autoUpdateStr = _iniService.GetValue(iniFile, ApplicationConstants.SettingSectionApplication, "auto_update");
            if (bool.TryParse(autoUpdateStr, out bool autoUpdate))
                settings.AutoUpdate = autoUpdate;
            else
                settings.AutoUpdate = true;

            var isFirstRunStr = _iniService.GetValue(iniFile, ApplicationConstants.SettingSectionApplication, "is_first_run");
            if (bool.TryParse(isFirstRunStr, out bool isFirstRun))
                settings.IsFirstRun = isFirstRun;
            else
                settings.IsFirstRun = true;
        }

        private void ApplyUiSettings(ApplicationSettings settings)
        {
            if (settings == null)
                return;

            IniFile uiIni = File.Exists(_uiSettingsFilePath)
                ? _iniService.ParseFile(_uiSettingsFilePath)
                : null;
            IniFile configIni = File.Exists(_configFilePath)
                ? _iniService.ParseFile(_configFilePath)
                : null;

            settings.ThemeMode = ReadThemeMode(uiIni) ?? ReadThemeMode(configIni) ?? ThemeMode.System;
            settings.WindowState = ReadWindowState(uiIni) ?? ReadWindowState(configIni) ?? new WindowState();
            settings.LanListOpen = ReadLanListOpen(uiIni) ?? false;
            settings.LanListWindowState = ReadLanListWindowState(uiIni);
            settings.LanChatWindowState = ReadLanChatWindowState(uiIni);
        }

        private bool? ReadLanListOpen(IniFile iniFile)
        {
            if (iniFile == null)
                return null;

            string value = _iniService.GetValue(
                iniFile,
                ApplicationConstants.SettingSectionWindow,
                ApplicationConstants.SettingKeyLanListOpen);
            if (string.IsNullOrEmpty(value))
                return null;

            return bool.TryParse(value, out bool open) ? open : (bool?)null;
        }

        private ThemeMode? ReadThemeMode(IniFile iniFile)
        {
            if (iniFile == null)
                return null;

            var themeModeStr = _iniService.GetValue(
                iniFile,
                ApplicationConstants.SettingSectionApplication,
                ApplicationConstants.SettingKeyThemeMode);
            if (string.IsNullOrEmpty(themeModeStr))
                return null;

            return Enum.TryParse(themeModeStr, true, out ThemeMode themeMode)
                ? themeMode
                : (ThemeMode?)null;
        }

        private WindowState ReadWindowState(IniFile iniFile)
        {
            return ReadWindowStateKeys(
                iniFile,
                ApplicationConstants.SettingKeyWindowSize,
                ApplicationConstants.SettingKeyWindowLocation,
                ApplicationConstants.SettingKeyWindowState);
        }

        private WindowState ReadLanListWindowState(IniFile iniFile)
        {
            return ReadWindowStateKeys(
                iniFile,
                ApplicationConstants.SettingKeyLanListSize,
                ApplicationConstants.SettingKeyLanListLocation,
                ApplicationConstants.SettingKeyLanListState);
        }

        private WindowState ReadLanChatWindowState(IniFile iniFile)
        {
            return ReadWindowStateKeys(
                iniFile,
                ApplicationConstants.SettingKeyLanChatSize,
                ApplicationConstants.SettingKeyLanChatLocation,
                ApplicationConstants.SettingKeyLanChatState);
        }

        private WindowState ReadWindowStateKeys(IniFile iniFile, string sizeKey, string locationKey, string stateKey)
        {
            if (iniFile == null)
                return null;

            var windowState = new WindowState();
            bool hasValue = false;

            var sizeStr = _iniService.GetValue(iniFile, ApplicationConstants.SettingSectionWindow, sizeKey);
            if (!string.IsNullOrEmpty(sizeStr))
            {
                var sizeParts = sizeStr.Split(',');
                if (sizeParts.Length == 2 &&
                    int.TryParse(sizeParts[0].Trim(), out int width) &&
                    int.TryParse(sizeParts[1].Trim(), out int height))
                {
                    windowState.Size = new Size(width, height);
                    hasValue = true;
                }
            }

            var locationStr = _iniService.GetValue(iniFile, ApplicationConstants.SettingSectionWindow, locationKey);
            if (!string.IsNullOrEmpty(locationStr))
            {
                var locParts = locationStr.Split(',');
                if (locParts.Length == 2 &&
                    int.TryParse(locParts[0].Trim(), out int x) &&
                    int.TryParse(locParts[1].Trim(), out int y))
                {
                    windowState.Location = new Point(x, y);
                    hasValue = true;
                }
            }

            var stateStr = _iniService.GetValue(iniFile, ApplicationConstants.SettingSectionWindow, stateKey);
            if (!string.IsNullOrEmpty(stateStr) && Enum.TryParse<FormWindowState>(stateStr, true, out FormWindowState state))
            {
                windowState.State = state;
                hasValue = true;
            }

            return hasValue ? windowState : null;
        }

        private void SaveUiSettings(ApplicationSettings settings)
        {
            if (settings == null)
                return;

            EnsureUiSettingsDirectoryExists();

            var uiIni = File.Exists(_uiSettingsFilePath)
                ? _iniService.ParseFile(_uiSettingsFilePath)
                : new IniFile();

            _iniService.SetValue(
                uiIni,
                ApplicationConstants.SettingSectionApplication,
                ApplicationConstants.SettingKeyThemeMode,
                settings.ThemeMode.ToString());

            if (settings.WindowState != null)
            {
                var ws = settings.WindowState;
                _iniService.SetValue(
                    uiIni,
                    ApplicationConstants.SettingSectionWindow,
                    ApplicationConstants.SettingKeyWindowSize,
                    $"{ws.Size.Width},{ws.Size.Height}");
                _iniService.SetValue(
                    uiIni,
                    ApplicationConstants.SettingSectionWindow,
                    ApplicationConstants.SettingKeyWindowLocation,
                    $"{ws.Location.X},{ws.Location.Y}");
                _iniService.SetValue(
                    uiIni,
                    ApplicationConstants.SettingSectionWindow,
                    ApplicationConstants.SettingKeyWindowState,
                    ws.State.ToString());
            }

            if (settings.LanListWindowState != null)
            {
                var lanWs = settings.LanListWindowState;
                _iniService.SetValue(
                    uiIni,
                    ApplicationConstants.SettingSectionWindow,
                    ApplicationConstants.SettingKeyLanListSize,
                    $"{lanWs.Size.Width},{lanWs.Size.Height}");
                _iniService.SetValue(
                    uiIni,
                    ApplicationConstants.SettingSectionWindow,
                    ApplicationConstants.SettingKeyLanListLocation,
                    $"{lanWs.Location.X},{lanWs.Location.Y}");
                _iniService.SetValue(
                    uiIni,
                    ApplicationConstants.SettingSectionWindow,
                    ApplicationConstants.SettingKeyLanListState,
                    lanWs.State.ToString());
            }

            if (settings.LanChatWindowState != null)
            {
                var chatWs = settings.LanChatWindowState;
                _iniService.SetValue(
                    uiIni,
                    ApplicationConstants.SettingSectionWindow,
                    ApplicationConstants.SettingKeyLanChatSize,
                    $"{chatWs.Size.Width},{chatWs.Size.Height}");
                _iniService.SetValue(
                    uiIni,
                    ApplicationConstants.SettingSectionWindow,
                    ApplicationConstants.SettingKeyLanChatLocation,
                    $"{chatWs.Location.X},{chatWs.Location.Y}");
                _iniService.SetValue(
                    uiIni,
                    ApplicationConstants.SettingSectionWindow,
                    ApplicationConstants.SettingKeyLanChatState,
                    chatWs.State.ToString());
            }

            _iniService.SetValue(
                uiIni,
                ApplicationConstants.SettingSectionWindow,
                ApplicationConstants.SettingKeyLanListOpen,
                settings.LanListOpen.ToString().ToLowerInvariant());

            _iniService.WriteFile(uiIni, _uiSettingsFilePath);
        }

        private void StripUiSettingsFromConfig(IniFile iniFile)
        {
            if (iniFile == null)
                return;

            _iniService.RemoveValue(iniFile, ApplicationConstants.SettingSectionApplication, ApplicationConstants.SettingKeyThemeMode);
            _iniService.RemoveValue(iniFile, ApplicationConstants.SettingSectionWindow, ApplicationConstants.SettingKeyWindowSize);
            _iniService.RemoveValue(iniFile, ApplicationConstants.SettingSectionWindow, ApplicationConstants.SettingKeyWindowLocation);
            _iniService.RemoveValue(iniFile, ApplicationConstants.SettingSectionWindow, ApplicationConstants.SettingKeyWindowState);
        }

        public ValidationResult SaveApplicationSettings(ApplicationSettings settings)
        {
            try
            {
                if (settings == null)
                    return ValidationResult.Failure("Settings cannot be null");

                EnsureConfigDirectoryExists();

                var iniFile = File.Exists(_configFilePath)
                    ? _iniService.ParseFile(_configFilePath)
                    : new IniFile();

                _iniService.SetValue(iniFile, ApplicationConstants.SettingSectionApplication, ApplicationConstants.SettingKeyViewMode, settings.ViewMode ?? ApplicationConstants.ViewModeDefault);
                _iniService.SetValue(iniFile, ApplicationConstants.SettingSectionApplication, ApplicationConstants.SettingKeySortBy, settings.SortBy ?? ApplicationConstants.SortByDefault);
                _iniService.SetValue(iniFile, ApplicationConstants.SettingSectionApplication, ApplicationConstants.SettingKeySortDirection, settings.SortDirection ?? ApplicationConstants.SortDirectionDefault);
                _iniService.SetValue(iniFile, ApplicationConstants.SettingSectionApplication, ApplicationConstants.SettingKeyDetailsColumnOrder, settings.DetailsColumnOrder ?? ApplicationConstants.DefaultColumnOrder);
                _iniService.SetValue(iniFile, ApplicationConstants.SettingSectionApplication, ApplicationConstants.SettingKeyDetailsColumnWidths, settings.DetailsColumnWidths ?? ApplicationConstants.DefaultDetailsColumnWidths);
                _iniService.SetValue(iniFile, ApplicationConstants.SettingSectionApplication, "auto_update", settings.AutoUpdate.ToString().ToLower());
                _iniService.SetValue(iniFile, ApplicationConstants.SettingSectionApplication, "is_first_run", settings.IsFirstRun.ToString().ToLower());
                _iniService.SetValue(iniFile, ApplicationConstants.SettingSectionApplication, "full_launch_options", settings.FullLaunchOptions.ToString().ToLower());
                _iniService.SetValue(
                    iniFile,
                    ApplicationConstants.SettingSectionApplication,
                    ApplicationConstants.SettingKeyLogosViewDropShadow,
                    settings.LogosViewDropShadow.ToString().ToLower());
                _iniService.SetValue(
                    iniFile,
                    ApplicationConstants.SettingSectionApplication,
                    ApplicationConstants.SettingKeyLanEnabled,
                    settings.LanEnabled.ToString().ToLower());
                _iniService.SetValue(
                    iniFile,
                    ApplicationConstants.SettingSectionApplication,
                    ApplicationConstants.SettingKeyLanPersonaState,
                    LanPersonaStateHelper.FormatSettings(settings.LanPersonaState));
                _iniService.SetValue(
                    iniFile,
                    ApplicationConstants.SettingSectionApplication,
                    ApplicationConstants.SettingKeyLanCustomStatus,
                    TruncateLanCustomStatus(settings.LanCustomStatus));
                // Strip legacy lan_lfg_appid — looking-to-play is session-only now.
                _iniService.RemoveValue(
                    iniFile,
                    ApplicationConstants.SettingSectionApplication,
                    "lan_lfg_appid");
                // Strip renamed lan_players_online → lan_enabled.
                _iniService.RemoveValue(
                    iniFile,
                    ApplicationConstants.SettingSectionApplication,
                    "lan_players_online");
                _iniService.SetValue(
                    iniFile,
                    ApplicationConstants.SettingSectionApplication,
                    ApplicationConstants.SettingKeyLanFavoriteSteamIds,
                    FormatLanFavoriteSteamIds(settings.LanFavoriteSteamIds));
                _iniService.SetValue(
                    iniFile,
                    ApplicationConstants.SettingSectionApplication,
                    ApplicationConstants.SettingKeyLanMutedSteamIds,
                    FormatLanFavoriteSteamIds(settings.LanMutedSteamIds));
                _iniService.SetValue(
                    iniFile,
                    ApplicationConstants.SettingSectionApplication,
                    ApplicationConstants.SettingKeyLanChatBlockedSteamIds,
                    FormatLanFavoriteSteamIds(settings.LanChatBlockedSteamIds));
                _iniService.SetValue(
                    iniFile,
                    ApplicationConstants.SettingSectionApplication,
                    ApplicationConstants.SettingKeyLanChatTrustedSteamIds,
                    FormatLanFavoriteSteamIds(settings.LanChatTrustedSteamIds));
                _iniService.SetValue(
                    iniFile,
                    ApplicationConstants.SettingSectionApplication,
                    ApplicationConstants.SettingKeyLanSeenPeers,
                    FormatLanSeenPeers(settings.LanSeenPeers));

                StripUiSettingsFromConfig(iniFile);
                _iniService.WriteFile(iniFile, _configFilePath);
                SaveUiSettings(settings);
                return ValidationResult.Success();
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService?.LogError("Failed to save application settings", ex);
                Feedback?.SetMessage("Could not save application settings.", TaskReportKind.Error);
                return ValidationResult.Failure($"Failed to save application settings: {ex.Message}");
            }
        }

        public GlobalSettings LoadGlobalUserSettings()
        {
            try
            {
                var settingsFile = Path.Combine(_globalSettingsPath, PathConstants.GoldbergGlobalUserJsonFileName);
                if (File.Exists(settingsFile))
                {
                    var json = File.ReadAllText(settingsFile);
                    return JsonConvert.DeserializeObject<GlobalSettings>(json) ?? new GlobalSettings();
                }
            }
            catch (Exception ex)
            {
                LogRedactionHelper.WriteDebug($"Failed to load global user settings: {ex.Message}");
            }

            return new GlobalSettings();
        }

        public ValidationResult SaveGlobalUserSettings(GlobalSettings settings)
        {
            try
            {
                if (settings == null)
                    return ValidationResult.Failure("Settings cannot be null");

                Feedback?.SetMessage("Saving global settings...");
                Directory.CreateDirectory(_globalSettingsPath);
                var settingsFile = Path.Combine(_globalSettingsPath, PathConstants.GoldbergGlobalUserJsonFileName);
                var json = JsonConvert.SerializeObject(settings, JsonFormatting.Indented);
                File.WriteAllText(settingsFile, json);
                Feedback?.SetMessageWithAutoClear("Global settings saved successfully");

                return ValidationResult.Success();
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService?.LogError("Failed to save global user settings", ex);
                Feedback?.SetMessage("Could not save global settings.", TaskReportKind.Error);
                return ValidationResult.Failure($"Failed to save global user settings: {ex.Message}");
            }
        }

        public WindowState GetWindowState()
        {
            return GetAppSetting(s => s.WindowState ?? new WindowState());
        }

        public bool HasPersistedWindowLayout()
        {
            return HasPersistedWindowLayoutInFile(_uiSettingsFilePath)
                || HasPersistedWindowLayoutInFile(_configFilePath);
        }

        public bool HasPersistedWindowLocation()
        {
            return HasPersistedWindowSettingKey(_uiSettingsFilePath, ApplicationConstants.SettingKeyWindowLocation)
                || HasPersistedWindowSettingKey(_configFilePath, ApplicationConstants.SettingKeyWindowLocation);
        }

        private bool HasPersistedWindowLayoutInFile(string path)
        {
            if (!File.Exists(path))
                return false;

            var iniFile = _iniService.ParseFile(path);
            return ReadWindowState(iniFile) != null;
        }

        private bool HasPersistedWindowSettingKey(string path, string key)
        {
            if (!File.Exists(path))
                return false;

            var iniFile = _iniService.ParseFile(path);
            return !string.IsNullOrEmpty(_iniService.GetValue(
                iniFile,
                ApplicationConstants.SettingSectionWindow,
                key));
        }

        public ValidationResult SetWindowState(WindowState windowState)
        {
            if (windowState == null)
                return ValidationResult.Failure("Window state cannot be null");
            return SetAppSetting(s => { s.WindowState = windowState; }, "Failed to set window state");
        }

        public bool GetLanListOpen()
        {
            return GetAppSetting(s => s.LanListOpen);
        }

        public ValidationResult SetLanListOpen(bool open)
        {
            return SetAppSetting(s => { s.LanListOpen = open; }, "Failed to set LAN list open state");
        }

        public WindowState GetLanListWindowState()
        {
            return GetAppSetting(s => s.LanListWindowState);
        }

        public bool HasPersistedLanListWindowLayout()
        {
            return HasPersistedLanListWindowLayoutInFile(_uiSettingsFilePath);
        }

        public bool HasPersistedLanListWindowLocation()
        {
            return HasPersistedWindowSettingKey(_uiSettingsFilePath, ApplicationConstants.SettingKeyLanListLocation);
        }

        private bool HasPersistedLanListWindowLayoutInFile(string path)
        {
            if (!File.Exists(path))
                return false;

            var iniFile = _iniService.ParseFile(path);
            return ReadLanListWindowState(iniFile) != null;
        }

        public ValidationResult SetLanListWindowState(WindowState windowState)
        {
            if (windowState == null)
                return ValidationResult.Failure("LAN list window state cannot be null");
            return SetAppSetting(s => { s.LanListWindowState = windowState; }, "Failed to set LAN list window state");
        }

        public WindowState GetLanChatWindowState()
        {
            return GetAppSetting(s => s.LanChatWindowState);
        }

        public bool HasPersistedLanChatWindowLayout()
        {
            if (!File.Exists(_uiSettingsFilePath))
                return false;

            var iniFile = _iniService.ParseFile(_uiSettingsFilePath);
            return ReadLanChatWindowState(iniFile) != null;
        }

        public ValidationResult SetLanChatWindowState(WindowState windowState)
        {
            if (windowState == null)
                return ValidationResult.Failure("LAN chat window state cannot be null");
            return SetAppSetting(s => { s.LanChatWindowState = windowState; }, "Failed to set LAN chat window state");
        }

        public bool IsFirstRun()
        {
            return GetAppSetting(s => s.IsFirstRun);
        }

        public ValidationResult CompleteFirstRun()
        {
            return SetAppSetting(s => { s.IsFirstRun = false; }, "Failed to complete first run");
        }

        public string GetGoldbergVersion()
        {
            try
            {
                if (!File.Exists(_configFilePath))
                    return null;

                var iniFile = _iniService.ParseFile(_configFilePath);
                return _iniService.GetValue(iniFile, GoldbergForkConstants.IniSection, GoldbergForkConstants.IniKeyVersion);
            }
            catch (Exception ex)
            {
                LogRedactionHelper.WriteDebug($"Failed to read Goldberg version: {ex.Message}");
                return null;
            }
        }

        public ValidationResult SetGoldbergVersion(string version)
        {
            try
            {
                EnsureConfigDirectoryExists();
                var iniFile = _iniService.ParseFile(_configFilePath);
                _iniService.SetValue(iniFile, GoldbergForkConstants.IniSection, GoldbergForkConstants.IniKeyVersion, version ?? string.Empty);
                _iniService.WriteFile(iniFile, _configFilePath);
                return ValidationResult.Success();
            }
            catch (Exception ex)
            {
                return ValidationResult.Failure($"Failed to set Goldberg version: {ex.Message}");
            }
        }

        public bool IsGoldbergForkConfigured()
        {
            try
            {
                if (!File.Exists(_configFilePath))
                    return false;

                var iniFile = _iniService.ParseFile(_configFilePath);
                var raw = _iniService.GetValue(iniFile, GoldbergForkConstants.IniSection, GoldbergForkConstants.IniKeyFork);
                return !string.IsNullOrWhiteSpace(raw);
            }
            catch (Exception ex)
            {
                LogRedactionHelper.WriteDebug($"Failed to read Goldberg fork configured flag: {ex.Message}");
                return false;
            }
        }

        public GoldbergForkSource GetGoldbergForkSource()
        {
            try
            {
                if (!File.Exists(_configFilePath))
                    return GoldbergForkSource.Detanup;

                var iniFile = _iniService.ParseFile(_configFilePath);
                var raw = _iniService.GetValue(iniFile, GoldbergForkConstants.IniSection, GoldbergForkConstants.IniKeyFork);
                return GoldbergForkSourceIni.Parse(raw);
            }
            catch (Exception ex)
            {
                LogRedactionHelper.WriteDebug($"Failed to read Goldberg fork source: {ex.Message}");
                return GoldbergForkSource.Detanup;
            }
        }

        public GoldbergReleaseChannel GetGoldbergReleaseChannel()
        {
            try
            {
                if (!File.Exists(_configFilePath))
                    return GoldbergReleaseChannel.Auto;

                var iniFile = _iniService.ParseFile(_configFilePath);
                var raw = _iniService.GetValue(
                    iniFile,
                    GoldbergForkConstants.IniSection,
                    GoldbergForkConstants.IniKeyReleaseChannel);
                return GoldbergReleaseChannelIni.Parse(raw);
            }
            catch (Exception ex)
            {
                LogRedactionHelper.WriteDebug($"Failed to read Goldberg release channel: {ex.Message}");
                return GoldbergReleaseChannel.Auto;
            }
        }

        public ValidationResult SetGoldbergForkSource(GoldbergForkSource source)
        {
            return SetGoldbergForkSource(source, GoldbergReleaseChannel.Auto);
        }

        public ValidationResult SetGoldbergForkSource(GoldbergForkSource source, GoldbergReleaseChannel channel)
        {
            try
            {
                EnsureConfigDirectoryExists();
                var iniFile = _iniService.ParseFile(_configFilePath);
                var previousForkRaw = _iniService.GetValue(iniFile, GoldbergForkConstants.IniSection, GoldbergForkConstants.IniKeyFork);
                var previousFork = GoldbergForkSourceIni.Parse(previousForkRaw);
                var previousChannelRaw = _iniService.GetValue(
                    iniFile,
                    GoldbergForkConstants.IniSection,
                    GoldbergForkConstants.IniKeyReleaseChannel);
                var previousChannel = GoldbergReleaseChannelIni.Parse(previousChannelRaw);
                bool selectionChanged = (!string.IsNullOrWhiteSpace(previousForkRaw) && previousFork != source)
                    || !GoldbergReleaseChannelIni.AreEquivalent(previousChannel, channel);

                _iniService.SetValue(
                    iniFile,
                    GoldbergForkConstants.IniSection,
                    GoldbergForkConstants.IniKeyFork,
                    GoldbergForkSourceIni.ToStorageValue(source));
                _iniService.SetValue(
                    iniFile,
                    GoldbergForkConstants.IniSection,
                    GoldbergForkConstants.IniKeyReleaseChannel,
                    GoldbergReleaseChannelIni.ToStorageValue(channel));
                if (selectionChanged)
                    _iniService.SetValue(iniFile, GoldbergForkConstants.IniSection, GoldbergForkConstants.IniKeyVersion, string.Empty);
                _iniService.WriteFile(iniFile, _configFilePath);
                return ValidationResult.Success();
            }
            catch (Exception ex)
            {
                return ValidationResult.Failure($"Failed to set Goldberg fork source: {ex.Message}");
            }
        }

        public ThemeMode GetThemeMode()
        {
            try
            {
                return GetAppSetting(s => s.ThemeMode);
            }
            catch (Exception ex)
            {
                LogRedactionHelper.WriteDebug($"Failed to read theme mode: {ex.Message}");
                return ThemeMode.System;
            }
        }

        public ValidationResult SetThemeMode(ThemeMode themeMode)
        {
            return SetAppSetting(s => { s.ThemeMode = themeMode; }, "Failed to set theme mode");
        }

        public string GetViewMode()
        {
            return GetAppStringSetting(s => s.ViewMode, ApplicationConstants.ViewModeDefault);
        }

        public ValidationResult SetViewMode(string viewMode)
        {
            return SetAppStringSetting(viewMode, "View mode cannot be null or empty", (s, v) => s.ViewMode = v, "Failed to set view mode");
        }

        public bool GetLogosViewDropShadow()
        {
            try
            {
                return GetAppSetting(s => s.LogosViewDropShadow);
            }
            catch (Exception ex)
            {
                LogRedactionHelper.WriteDebug($"Failed to read logos view drop shadow: {ex.Message}");
                return true;
            }
        }

        public string GetSortBy()
        {
            return GetAppStringSetting(s => s.SortBy, ApplicationConstants.SortByDefault);
        }

        public ValidationResult SetSortBy(string sortBy)
        {
            return SetAppStringSetting(sortBy, "Sort field cannot be null or empty", (s, v) => s.SortBy = v, "Failed to set sort field");
        }

        public string GetSortDirection()
        {
            return GetAppStringSetting(s => s.SortDirection, ApplicationConstants.SortDirectionDefault);
        }

        public ValidationResult SetSortDirection(string sortDirection)
        {
            return SetAppStringSetting(sortDirection, "Sort direction cannot be null or empty", (s, v) => s.SortDirection = v, "Failed to set sort direction");
        }

        public string GetDetailsColumnOrder()
        {
            return GetAppStringSetting(s => s.DetailsColumnOrder, ApplicationConstants.DefaultColumnOrder);
        }

        public ValidationResult SetDetailsColumnOrder(string columnOrder)
        {
            return SetAppStringSetting(columnOrder, "Column order cannot be null or empty", (s, v) => s.DetailsColumnOrder = v, "Failed to set column order");
        }

        public string GetDetailsColumnWidths()
        {
            return GetAppStringSetting(s => s.DetailsColumnWidths, ApplicationConstants.DefaultDetailsColumnWidths);
        }

        public ValidationResult SetDetailsColumnWidths(string widths)
        {
            if (string.IsNullOrWhiteSpace(widths))
                return ValidationResult.Failure("Column widths cannot be empty.");

            if (!ApplicationConstants.TryParseDetailsColumnWidths(widths, out _, out _, out _))
                return ValidationResult.Failure("Column widths must be three integers (Name, App ID, Path) within the allowed range.");

            var normalized = ApplicationConstants.NormalizeDetailsColumnWidths(widths);
            return SetAppStringSetting(normalized, "Column widths cannot be null or empty", (s, v) => s.DetailsColumnWidths = v, "Failed to set column widths");
        }

        // Optional dev override for launcher update API (e.g. local mock server). Release builds use GitHub constants when unset.
        public string GetLauncherUpdateReleasesApiUrlOverride()
        {
            try
            {
                if (!File.Exists(_configFilePath))
                    return null;

                var iniFile = _iniService.ParseFile(_configFilePath);
                return _iniService.GetValue(
                    iniFile,
                    LauncherReleaseConstants.IniSectionApplication,
                    LauncherReleaseConstants.IniKeyLauncherUpdateApiUrl);
            }
            catch (Exception ex)
            {
                LogRedactionHelper.WriteDebug($"Failed to read launcher update API override: {ex.Message}");
                return null;
            }
        }

        // Does not download assets (sounds, fonts, glyphs); use EnsureGlobalConfigFilesExistAsync for full setup.
        public ValidationResult EnsureMinimalConfigFilesExist()
        {
            try
            {
                Directory.CreateDirectory(_globalSettingsPath);
                EnsureConfigsUserIniExists();
                EnsureConfigsOverlayIniExists();
                return ValidationResult.Success();
            }
            catch (Exception ex)
            {
                return ValidationResult.Failure($"Failed to ensure minimal config: {ex.Message}");
            }
        }

        public async Task<ValidationResult> EnsureGlobalConfigFilesExistAsync()
        {
            return await EnsureGlobalFolderCoreAsync(includeAssets: true, failurePrefix: "Failed to ensure global config files exist")
                .ConfigureAwait(false);
        }

        private async Task<ValidationResult> EnsureGlobalFolderCoreAsync(bool includeAssets, string failurePrefix)
        {
            try
            {
                Directory.CreateDirectory(_globalSettingsPath);
                EnsureConfigsUserIniExists();
                EnsureConfigsOverlayIniExists();
                if (!includeAssets)
                    return ValidationResult.Success();

                // Both batches run in parallel and soft-fail (log only).
                Task batchA = EnsureSteamLineageAssetsAsync();
                Task batchB = EnsureExampleAssetsAsync();
                await Task.WhenAll(batchA, batchB).ConfigureAwait(false);

                return ValidationResult.Success();
            }
            catch (Exception ex)
            {
                return ValidationResult.Failure($"{failurePrefix}: {ex.Message}");
            }
        }

        // Batch A: Steam.dll + overlay WAVs + LocalAppData clientui image (local Steam → CDN). Soft-fail; logs only.
        private async Task EnsureSteamLineageAssetsAsync()
        {
            try
            {
                int prepared = 0;

                if (!SteamInstallationPathHelper.TryEnsureSteamDllFromSteamClient(
                        PathConstants.GoldbergSteamOldDirectory, out string steamDllError)
                    && !SteamInstallationPathHelper.IsSteamDllPresentInGoldbergFolder())
                {
                    ValidationResult cdnDll = await ServiceLocator.AssetDownloadService
                        .DownloadSteamDllAsync(PathConstants.GoldbergSteamOldDirectory)
                        .ConfigureAwait(false);
                    if (!cdnDll.IsValid)
                    {
                        ServiceLocator.LogService?.LogWarning(
                            "Steam lineage assets incomplete (Steam.dll): " + (steamDllError ?? "missing")
                            + "; CDN fallback: " + (cdnDll.ErrorMessage ?? "failed")
                            + " (Steam.dll mode unavailable until fixed).");
                    }
                    else
                    {
                        ServiceLocator.LogService?.LogDebug("Steam.dll ready (source: Steam CDN).");
                        prepared++;
                    }
                }

                var soundsOutcome = await EnsureSoundFilesExistAsync().ConfigureAwait(false);
                if (!soundsOutcome.Result.IsValid)
                    ServiceLocator.LogService?.LogWarning(
                        "Steam lineage assets incomplete (overlay sounds): " + soundsOutcome.Result.ErrorMessage);
                else if (soundsOutcome.Prepared)
                    prepared++;

                var imageOutcome = await EnsureSteamClientUiHashedImageExistsAsync().ConfigureAwait(false);
                if (!imageOutcome.Result.IsValid)
                    ServiceLocator.LogService?.LogWarning(
                        "Steam lineage assets incomplete (clientui image): " + imageOutcome.Result.ErrorMessage);
                else if (imageOutcome.Prepared)
                    prepared++;

                if (prepared > 0)
                    ServiceLocator.LogService?.LogMessage("Steam lineage assets ready.");
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService?.LogWarning("Steam lineage assets soft-fail: " + ex.Message);
            }
        }

        // Batch B: avatar + font + glyphs from EXAMPLE. Soft-fail; logs only.
        private async Task EnsureExampleAssetsAsync()
        {
            try
            {
                await EnsureAvatarExistsAsync().ConfigureAwait(false);

                ValidationResult fontResult = await EnsureFontFilesExistAsync().ConfigureAwait(false);
                if (!fontResult.IsValid)
                    ServiceLocator.LogService?.LogWarning("Overlay font self-heal soft-fail: " + fontResult.ErrorMessage);

                ValidationResult glyphResult = await EnsureGlyphFilesExistAsync().ConfigureAwait(false);
                if (!glyphResult.IsValid)
                    ServiceLocator.LogService?.LogWarning("Controller glyphs self-heal soft-fail: " + glyphResult.ErrorMessage);
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService?.LogWarning("EXAMPLE assets soft-fail: " + ex.Message);
            }
        }

        private void EnsureConfigsUserIniExists()
        {
            var filePath = Path.Combine(_globalSettingsPath, PathConstants.GoldbergUserIniFileName);
            if (File.Exists(filePath))
                return;

            var defaultContent = string.Format(@"# ############################################################################## #
# you do not have to specify everything, pick and choose the options you need only
# ############################################################################## #

[user::general]
# user account name
# default=gse orca
account_name=SmartGoldberg
# your account ID in Steam64 format
# if the specified ID is invalid, the emu will ignore it and generate a proper one
# default=randomly generated by the emu only once and saved in the global settings
account_steamid={2}
# the language reported to the app/game
# this must exist in 'supported_languages.txt', otherwise it will be ignored by the emu
# look for the column 'API language code' here: {0}
# default=english
language=english
# report a country IP if the game queries it
# ISO 3166-1-alpha-2 format, use this link to get the 'Alpha-2' country code: {1}
# default=US
ip_country=US
", ApplicationConstants.SteamPartnerLocalizationLanguagesUrl, ApplicationConstants.IbanCountryCodesUrl,
                SteamIdHelper.GenerateRandomSteamId64());
            File.WriteAllText(filePath, defaultContent);
        }

        // App overlay defaults differ from Goldberg's (overlay on, achievement progress shown); new games inherit them from this file.
        private void EnsureConfigsOverlayIniExists()
        {
            var filePath = Path.Combine(_globalSettingsPath, PathConstants.GoldbergOverlayIniFileName);
            if (File.Exists(filePath))
                return;

            SaveResult result = new GoldbergCfgService(_globalSettingsPath).SaveGlobalOverlaySettings(new OverlaySettings());
            if (!result.IsSuccess)
                ServiceLocator.LogService?.LogWarning("Could not create global overlay settings: " + result.ErrorMessage);
        }

        private async Task<(ValidationResult Result, bool Prepared)> EnsureSoundFilesExistAsync()
        {
            try
            {
                var soundsPath = Path.Combine(_globalSettingsPath, PathConstants.GoldbergGlobalSoundsFolderName);
                Directory.CreateDirectory(soundsPath);

                var achievementSoundPath = Path.Combine(soundsPath, PathConstants.SteamClientUiAchievementNotificationWav);
                var friendSoundPath = Path.Combine(soundsPath, PathConstants.SteamClientUiFriendNotificationWav);

                if (File.Exists(achievementSoundPath) && File.Exists(friendSoundPath))
                {
                    OverlayNotificationSoundsStaging.EnsureLibraryAliasesInSoundsFolder(soundsPath);
                    return (ValidationResult.Success(), false);
                }

                if (TryCopyFromSteam(soundsPath).IsValid)
                {
                    OverlayNotificationSoundsStaging.EnsureLibraryAliasesInSoundsFolder(soundsPath);
                    ServiceLocator.LogService?.LogDebug(
                        "Overlay notification sounds ready (source: Steam steamui\\sounds).");
                    return (ValidationResult.Success(), true);
                }

                ValidationResult cdnResult = await ServiceLocator.AssetDownloadService
                    .DownloadSoundFilesAsync(soundsPath)
                    .ConfigureAwait(false);
                if (cdnResult.IsValid)
                {
                    OverlayNotificationSoundsStaging.EnsureLibraryAliasesInSoundsFolder(soundsPath);
                    ServiceLocator.LogService?.LogDebug(
                        "Overlay notification sounds ready (source: Steam CDN).");
                    return (cdnResult, true);
                }

                return (cdnResult, false);
            }
            catch (Exception ex)
            {
                return (ValidationResult.Failure($"Failed to ensure sound files exist: {ex.Message}"), false);
            }
        }

        private ValidationResult TryCopyFromSteam(string soundsPath)
        {
            try
            {
                if (SteamInstallationPathHelper.TryCopyOverlayNotificationSoundsFromSteam(soundsPath))
                    return ValidationResult.Success();

                return ValidationResult.Failure(
                    "Steam overlay notification sounds were not found or could not be copied from steamui\\sounds.");
            }
            catch (Exception ex)
            {
                return ValidationResult.Failure($"Failed to copy from Steam: {ex.Message}");
            }
        }

        private async Task<(ValidationResult Result, bool Prepared)> EnsureSteamClientUiHashedImageExistsAsync()
        {
            try
            {
                string destPath = PathConstants.LocalAppDataSteamClientUiHashedImagePath;
                if (File.Exists(destPath))
                    return (ValidationResult.Success(), false);

                if (SteamInstallationPathHelper.TryCopySteamClientUiHashedImageFromSteam(destPath))
                {
                    ServiceLocator.LogService?.LogDebug(
                        "Steam clientui image ready (source: Steam clientui\\images).");
                    return (ValidationResult.Success(), true);
                }

                ValidationResult cdnResult = await ServiceLocator.AssetDownloadService
                    .DownloadSteamClientUiHashedImageAsync(destPath)
                    .ConfigureAwait(false);
                if (cdnResult.IsValid)
                {
                    ServiceLocator.LogService?.LogDebug(
                        "Steam clientui image ready (source: Steam CDN).");
                    return (cdnResult, true);
                }

                return (cdnResult, false);
            }
            catch (Exception ex)
            {
                return (ValidationResult.Failure("Failed to ensure Steam clientui image exists: " + ex.Message), false);
            }
        }

        public ValidationResult SaveGlobalAccountAvatarFromFile(string sourceImagePath)
        {
            if (string.IsNullOrWhiteSpace(sourceImagePath))
                return ValidationResult.Failure("No image file selected.");
            if (!File.Exists(sourceImagePath))
                return ValidationResult.Failure("Image file not found.");

            try
            {
                var avatarPath = PathConstants.GlobalAccountAvatarPath;
                Directory.CreateDirectory(Path.GetDirectoryName(avatarPath));
                File.Copy(sourceImagePath, avatarPath, true);
                return ValidationResult.Success();
            }
            catch (Exception ex)
            {
                return ValidationResult.Failure($"Failed to save avatar: {ex.Message}");
            }
        }

        public async Task<ValidationResult> ResetGlobalAccountAvatarAsync()
        {
            try
            {
                var avatarPath = PathConstants.GlobalAccountAvatarPath;
                Directory.CreateDirectory(Path.GetDirectoryName(avatarPath));
                bool success = await ServiceLocator.AssetDownloadService.DownloadAvatarAsync(avatarPath).ConfigureAwait(false);
                return success
                    ? ValidationResult.Success()
                    : ValidationResult.Failure("Failed to download default avatar.");
            }
            catch (Exception ex)
            {
                return ValidationResult.Failure($"Failed to reset avatar: {ex.Message}");
            }
        }

        private async Task EnsureAvatarExistsAsync()
        {
            try
            {
                var avatarPath = PathConstants.GlobalAccountAvatarPath;
                
                if (!File.Exists(avatarPath))
                {
                    await DownloadAvatarFromSourceAsync(avatarPath).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                LogRedactionHelper.WriteDebug($"Failed to ensure avatar exists: {ex.Message}");
            }
        }

        private static async Task DownloadAvatarFromSourceAsync(string avatarPath)
        {
            try
            {
                bool success = await ServiceLocator.AssetDownloadService.DownloadAvatarAsync(avatarPath).ConfigureAwait(false);
                if (!success)
                    LogRedactionHelper.WriteDebug("Failed to download avatar from EXAMPLE");
            }
            catch (Exception ex)
            {
                LogRedactionHelper.WriteDebug($"Failed to download avatar from EXAMPLE: {ex.Message}");
            }
        }

        private static async Task<ValidationResult> EnsureFontFilesExistAsync()
        {
            try
            {
                var fontsPath = PathConstants.GlobalFontsPath;
                Directory.CreateDirectory(fontsPath);

                var fontPath = Path.Combine(fontsPath, PathConstants.GoldbergGlobalDefaultOverlayFontFileName);
                if (File.Exists(fontPath))
                {
                    return ValidationResult.Success();
                }

                return await ServiceLocator.AssetDownloadService.DownloadFontFilesAsync(fontsPath).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return ValidationResult.Failure($"Failed to ensure font files exist: {ex.Message}");
            }
        }

        private static async Task<ValidationResult> EnsureGlyphFilesExistAsync()
        {
            try
            {
                var glyphsPath = PathConstants.GlobalControllerGlyphsPath;
                Directory.CreateDirectory(glyphsPath);

                var requiredGlyphFiles = new[]
                {
                    "xbox_button_select.png",
                    "xbox_button_start.png",
                    "button_b.png",
                    "button_a.png",
                    "button_x.png",
                    "button_y.png",
                    "stick_dpad_e.png",
                    "stick_dpad_s.png",
                    "stick_dpad_w.png",
                    "stick_dpad_n.png",
                    "trigger_r_pull.png",
                    "trigger_l_pull.png",
                    "shoulder_r.png",
                    "shoulder_l.png"
                };

                if (requiredGlyphFiles.All(fileName => File.Exists(Path.Combine(glyphsPath, fileName))))
                    return ValidationResult.Success();

                return await ServiceLocator.AssetDownloadService.DownloadControllerGlyphsAsync(glyphsPath).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return ValidationResult.Failure($"Failed to ensure glyph files exist: {ex.Message}");
            }
        }

        private static IRegistryService Registry => ServiceLocator.RegistryService;

        public Dictionary<string, string> LoadSteamIdProfiles() => Registry.LoadSteamIdProfiles();

        public ValidationResult UpsertSteamIdProfile(string steamId, string name) =>
            Registry.UpsertSteamIdProfile(steamId, name);

        public ValidationResult RemoveSteamIdProfile(string steamId) =>
            Registry.RemoveSteamIdProfile(steamId);

        public bool IsLanFavoriteSteamId(string steamId)
        {
            return IsSteamIdInSettingsList(steamId, s => s.LanFavoriteSteamIds);
        }

        public ValidationResult SetLanFavoriteSteamId(string steamId, bool favorite)
        {
            return MutateSteamIdSettingsList(
                steamId,
                favorite,
                s => s.LanFavoriteSteamIds,
                (s, list) => s.LanFavoriteSteamIds = list);
        }

        public bool IsLanMutedSteamId(string steamId)
        {
            return IsSteamIdInSettingsList(steamId, s => s.LanMutedSteamIds);
        }

        public ValidationResult SetLanMutedSteamId(string steamId, bool muted)
        {
            return MutateSteamIdSettingsList(
                steamId,
                muted,
                s => s.LanMutedSteamIds,
                (s, list) => s.LanMutedSteamIds = list);
        }

        public bool IsLanChatBlockedSteamId(string steamId)
        {
            return IsSteamIdInSettingsList(steamId, s => s.LanChatBlockedSteamIds);
        }

        public ValidationResult SetLanChatBlockedSteamId(string steamId, bool blocked)
        {
            return MutateSteamIdSettingsList(
                steamId,
                blocked,
                s => s.LanChatBlockedSteamIds,
                (s, list) => s.LanChatBlockedSteamIds = list,
                // Block clears prior accept trust.
                (settings, id, include) =>
                {
                    if (!include)
                        return false;
                    if (settings.LanChatTrustedSteamIds == null)
                        settings.LanChatTrustedSteamIds = new List<string>();
                    return settings.LanChatTrustedSteamIds.Remove(id);
                });
        }

        public bool IsLanChatTrustedSteamId(string steamId)
        {
            return IsSteamIdInSettingsList(steamId, s => s.LanChatTrustedSteamIds);
        }

        public ValidationResult SetLanChatTrustedSteamId(string steamId, bool trusted)
        {
            return MutateSteamIdSettingsList(
                steamId,
                trusted,
                s => s.LanChatTrustedSteamIds,
                (s, list) => s.LanChatTrustedSteamIds = list);
        }

        public List<LanSeenPeerRecord> GetLanSeenPeers()
        {
            ApplicationSettings settings = LoadApplicationSettings();
            List<LanSeenPeerRecord> list = settings != null ? settings.LanSeenPeers : null;
            if (list == null || list.Count == 0)
                return new List<LanSeenPeerRecord>();

            var copy = new List<LanSeenPeerRecord>(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                LanSeenPeerRecord item = list[i];
                if (item == null || string.IsNullOrWhiteSpace(item.SteamId))
                    continue;
                copy.Add(CloneLanSeenPeer(item));
            }

            return copy;
        }

        // Replace the persisted Players seen list (presence owns prune/order).
        public ValidationResult SaveLanSeenPeers(IList<LanSeenPeerRecord> peers)
        {
            ApplicationSettings settings = LoadApplicationSettings();
            settings.LanSeenPeers = NormalizeLanSeenPeers(peers);
            return SaveApplicationSettings(settings);
        }

        public bool IsLanSeenPeer(string steamId)
        {
            if (string.IsNullOrWhiteSpace(steamId))
                return false;
            steamId = steamId.Trim();
            ApplicationSettings settings = LoadApplicationSettings();
            List<LanSeenPeerRecord> list = settings != null ? settings.LanSeenPeers : null;
            return FindSeenPeerIndex(list, steamId) >= 0;
        }

        public ValidationResult RemoveLanSeenPeer(string steamId)
        {
            if (string.IsNullOrWhiteSpace(steamId))
                return ValidationResult.Failure("Steam ID is required.");
            steamId = steamId.Trim();

            ApplicationSettings settings = LoadApplicationSettings();
            if (settings.LanSeenPeers == null || settings.LanSeenPeers.Count == 0)
                return ValidationResult.Success();

            int index = FindSeenPeerIndex(settings.LanSeenPeers, steamId);
            if (index < 0)
                return ValidationResult.Success();

            settings.LanSeenPeers.RemoveAt(index);
            return SaveApplicationSettings(settings);
        }

        private bool IsSteamIdInSettingsList(string steamId, Func<ApplicationSettings, List<string>> getList)
        {
            if (string.IsNullOrWhiteSpace(steamId) || getList == null)
                return false;
            ApplicationSettings settings = LoadApplicationSettings();
            List<string> list = getList(settings);
            return list != null && list.Contains(steamId.Trim());
        }

        private ValidationResult MutateSteamIdSettingsList(
            string steamId,
            bool include,
            Func<ApplicationSettings, List<string>> getList,
            Action<ApplicationSettings, List<string>> setList,
            Func<ApplicationSettings, string, bool, bool> extraMutate = null)
        {
            if (string.IsNullOrWhiteSpace(steamId))
                return ValidationResult.Failure("Steam ID is required.");
            if (getList == null || setList == null)
                return ValidationResult.Failure("Steam ID list is required.");

            steamId = steamId.Trim();
            ApplicationSettings settings = LoadApplicationSettings();
            List<string> list = getList(settings);
            if (list == null)
            {
                list = new List<string>();
                setList(settings, list);
            }

            bool changed = false;
            bool has = list.Contains(steamId);
            if (include && !has)
            {
                list.Add(steamId);
                changed = true;
            }
            else if (!include && has)
            {
                list.Remove(steamId);
                changed = true;
            }

            if (extraMutate != null && extraMutate(settings, steamId, include))
                changed = true;

            if (!changed)
                return ValidationResult.Success();

            return SaveApplicationSettings(settings);
        }

        public ValidationResult SetLanPersonaState(LanPersonaState state)
        {
            ApplicationSettings settings = LoadApplicationSettings();
            settings.LanPersonaState = state;
            return SaveApplicationSettings(settings);
        }

        private static string TruncateLanCustomStatus(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            value = value.Trim();
            if (value.Length > LanPresenceConstants.MaxCustomStatusChars)
                return value.Substring(0, LanPresenceConstants.MaxCustomStatusChars);
            return value;
        }

        private static List<string> ParseLanFavoriteSteamIds(string value)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(value))
                return list;

            string[] parts = value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                string id = parts[i].Trim();
                if (id.Length == 0 || id.Length > LanPresenceConstants.MaxSteamIdChars)
                    continue;
                bool digits = true;
                for (int c = 0; c < id.Length; c++)
                {
                    if (id[c] < '0' || id[c] > '9')
                    {
                        digits = false;
                        break;
                    }
                }

                if (!digits)
                    continue;
                if (!list.Contains(id))
                    list.Add(id);
            }

            return list;
        }

        private static string FormatLanFavoriteSteamIds(List<string> ids)
        {
            if (ids == null || ids.Count == 0)
                return string.Empty;
            return string.Join(",", ids);
        }

        // steamId|displayName|utcTicks|avatarHash;… — name may not contain | or ;.
        private static List<LanSeenPeerRecord> ParseLanSeenPeers(string value)
        {
            var list = new List<LanSeenPeerRecord>();
            if (string.IsNullOrWhiteSpace(value))
                return list;

            string[] records = value.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < records.Length; i++)
            {
                string record = records[i].Trim();
                if (record.Length == 0)
                    continue;

                string[] parts = record.Split(new[] { '|' });
                if (parts.Length < 3)
                    continue;

                string steamId = parts[0].Trim();
                if (!IsDigitsSteamId(steamId))
                    continue;

                string displayName = parts[1].Trim();
                if (displayName.Length > LanPresenceConstants.MaxNameChars)
                    displayName = displayName.Substring(0, LanPresenceConstants.MaxNameChars);

                long ticks;
                if (!long.TryParse(parts[2].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out ticks)
                    || ticks <= 0)
                    continue;

                DateTime lastSeenUtc;
                try
                {
                    lastSeenUtc = new DateTime(ticks, DateTimeKind.Utc);
                }
                catch
                {
                    continue;
                }

                string avatarHash = parts.Length >= 4 ? parts[3].Trim() : string.Empty;
                if (avatarHash.Length > LanPresenceConstants.MaxAvatarHashChars)
                    avatarHash = avatarHash.Substring(0, LanPresenceConstants.MaxAvatarHashChars);

                if (FindSeenPeerIndex(list, steamId) >= 0)
                    continue;

                list.Add(new LanSeenPeerRecord
                {
                    SteamId = steamId,
                    DisplayName = displayName,
                    AvatarHash = avatarHash,
                    LastSeenUtc = lastSeenUtc
                });
            }

            PruneLanSeenPeersInPlace(list);
            return list;
        }

        private static string FormatLanSeenPeers(List<LanSeenPeerRecord> peers)
        {
            List<LanSeenPeerRecord> normalized = NormalizeLanSeenPeers(peers);
            if (normalized.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            for (int i = 0; i < normalized.Count; i++)
            {
                LanSeenPeerRecord peer = normalized[i];
                if (i > 0)
                    sb.Append(';');
                sb.Append(peer.SteamId);
                sb.Append('|');
                sb.Append(SanitizeSeenPeerName(peer.DisplayName));
                sb.Append('|');
                sb.Append(peer.LastSeenUtc.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture));
                sb.Append('|');
                sb.Append(SanitizeSeenPeerHash(peer.AvatarHash));
            }

            return sb.ToString();
        }

        private static List<LanSeenPeerRecord> NormalizeLanSeenPeers(IList<LanSeenPeerRecord> peers)
        {
            var list = new List<LanSeenPeerRecord>();
            if (peers == null)
                return list;

            for (int i = 0; i < peers.Count; i++)
            {
                LanSeenPeerRecord peer = peers[i];
                if (peer == null || !IsDigitsSteamId(peer.SteamId))
                    continue;

                string steamId = peer.SteamId.Trim();
                int existing = FindSeenPeerIndex(list, steamId);
                var next = new LanSeenPeerRecord
                {
                    SteamId = steamId,
                    DisplayName = SanitizeSeenPeerName(peer.DisplayName),
                    AvatarHash = SanitizeSeenPeerHash(peer.AvatarHash),
                    LastSeenUtc = peer.LastSeenUtc.Kind == DateTimeKind.Utc
                        ? peer.LastSeenUtc
                        : peer.LastSeenUtc.ToUniversalTime()
                };

                if (existing >= 0)
                {
                    if (next.LastSeenUtc >= list[existing].LastSeenUtc)
                        list[existing] = next;
                }
                else
                {
                    list.Add(next);
                }
            }

            PruneLanSeenPeersInPlace(list);
            return list;
        }

        private static void PruneLanSeenPeersInPlace(List<LanSeenPeerRecord> list)
        {
            if (list == null || list.Count <= LanPresenceConstants.MaxSeenPeers)
                return;

            list.Sort((a, b) => b.LastSeenUtc.CompareTo(a.LastSeenUtc));
            list.RemoveRange(LanPresenceConstants.MaxSeenPeers, list.Count - LanPresenceConstants.MaxSeenPeers);
        }

        private static int FindSeenPeerIndex(List<LanSeenPeerRecord> list, string steamId)
        {
            if (list == null || string.IsNullOrEmpty(steamId))
                return -1;
            for (int i = 0; i < list.Count; i++)
            {
                LanSeenPeerRecord item = list[i];
                if (item != null && string.Equals(item.SteamId, steamId, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        private static LanSeenPeerRecord CloneLanSeenPeer(LanSeenPeerRecord source)
        {
            return new LanSeenPeerRecord
            {
                SteamId = source.SteamId,
                DisplayName = source.DisplayName ?? string.Empty,
                AvatarHash = source.AvatarHash ?? string.Empty,
                LastSeenUtc = source.LastSeenUtc
            };
        }

        private static string SanitizeSeenPeerName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;
            name = name.Trim().Replace('|', ' ').Replace(';', ' ');
            if (name.Length > LanPresenceConstants.MaxNameChars)
                return name.Substring(0, LanPresenceConstants.MaxNameChars);
            return name;
        }

        private static string SanitizeSeenPeerHash(string hash)
        {
            if (string.IsNullOrWhiteSpace(hash))
                return string.Empty;
            hash = hash.Trim();
            if (hash.Length > LanPresenceConstants.MaxAvatarHashChars)
                return hash.Substring(0, LanPresenceConstants.MaxAvatarHashChars);
            return hash;
        }

        private static bool IsDigitsSteamId(string steamId)
        {
            if (string.IsNullOrWhiteSpace(steamId))
                return false;
            steamId = steamId.Trim();
            if (steamId.Length == 0 || steamId.Length > LanPresenceConstants.MaxSteamIdChars)
                return false;
            for (int c = 0; c < steamId.Length; c++)
            {
                if (steamId[c] < '0' || steamId[c] > '9')
                    return false;
            }

            return true;
        }

    }
}
