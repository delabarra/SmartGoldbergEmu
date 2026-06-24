using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            settings.ViewMode = ApplicationConstants.NormalizeViewMode(
                _iniService.GetValue(iniFile, ApplicationConstants.SettingSectionApplication, ApplicationConstants.SettingKeyViewMode)
                ?? ApplicationConstants.ViewModeDefault);
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

            settings.SteamlessCliPath = _iniService.GetValue(
                iniFile,
                ApplicationConstants.SettingSectionApplication,
                ApplicationConstants.SettingKeySteamlessCliPath);

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
            if (iniFile == null)
                return null;

            var windowState = new WindowState();
            bool hasValue = false;

            var sizeStr = _iniService.GetValue(iniFile, ApplicationConstants.SettingSectionWindow, ApplicationConstants.SettingKeyWindowSize);
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

            var locationStr = _iniService.GetValue(iniFile, ApplicationConstants.SettingSectionWindow, ApplicationConstants.SettingKeyWindowLocation);
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

            var stateStr = _iniService.GetValue(iniFile, ApplicationConstants.SettingSectionWindow, ApplicationConstants.SettingKeyWindowState);
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
                    ApplicationConstants.SettingKeySteamlessCliPath,
                    settings.SteamlessCliPath ?? string.Empty);

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
                Feedback?.SetMessage("Global settings saved successfully");

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

        public ValidationResult SetGoldbergForkSource(GoldbergForkSource source)
        {
            try
            {
                EnsureConfigDirectoryExists();
                var iniFile = _iniService.ParseFile(_configFilePath);
                var previousRaw = _iniService.GetValue(iniFile, GoldbergForkConstants.IniSection, GoldbergForkConstants.IniKeyFork);
                var previous = GoldbergForkSourceIni.Parse(previousRaw);
                bool forkChanged = !string.IsNullOrWhiteSpace(previousRaw)
                    && previous != source;

                _iniService.SetValue(iniFile, GoldbergForkConstants.IniSection, GoldbergForkConstants.IniKeyFork, GoldbergForkSourceIni.ToStorageValue(source));
                if (forkChanged)
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
            return ApplicationConstants.NormalizeViewMode(
                GetAppStringSetting(s => s.ViewMode, ApplicationConstants.ViewModeDefault));
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

        public ValidationResult SetLogosViewDropShadow(bool enabled)
        {
            return SetAppSetting(s => { s.LogosViewDropShadow = enabled; }, "Failed to set logos view drop shadow");
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

        public string GetSteamlessCliPath()
        {
            var path = GetAppSetting(s => s.SteamlessCliPath);
            return string.IsNullOrWhiteSpace(path) ? null : path.Trim();
        }

        public ValidationResult SetSteamlessCliPath(string cliPath)
        {
            return SetAppSetting(s => s.SteamlessCliPath = cliPath?.Trim(), "Failed to save Steamless CLI path");
        }

        public bool GetAutoUpdate()
        {
            return GetAppSetting(s => s.AutoUpdate);
        }

        public ValidationResult SetAutoUpdate(bool autoUpdate)
        {
            return SetAppSetting(s => { s.AutoUpdate = autoUpdate; }, "Failed to set auto-update");
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
                if (!includeAssets)
                    return ValidationResult.Success();

                ValidationResult soundResult = await EnsureSoundFilesExistAsync().ConfigureAwait(false);
                if (!soundResult.IsValid)
                    return soundResult;

                ValidationResult fontResult = await EnsureFontFilesExistAsync().ConfigureAwait(false);
                if (!fontResult.IsValid)
                    return fontResult;

                ValidationResult glyphResult = await EnsureGlyphFilesExistAsync().ConfigureAwait(false);
                if (!glyphResult.IsValid)
                    return glyphResult;

                return ValidationResult.Success();
            }
            catch (Exception ex)
            {
                return ValidationResult.Failure($"{failurePrefix}: {ex.Message}");
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
account_steamid=76561198055134316
# the language reported to the app/game
# this must exist in 'supported_languages.txt', otherwise it will be ignored by the emu
# look for the column 'API language code' here: {0}
# default=english
language=english
# report a country IP if the game queries it
# ISO 3166-1-alpha-2 format, use this link to get the 'Alpha-2' country code: {1}
# default=US
ip_country=US
", ApplicationConstants.SteamPartnerLocalizationLanguagesUrl, ApplicationConstants.IbanCountryCodesUrl);
            File.WriteAllText(filePath, defaultContent);
        }

        private async Task<ValidationResult> EnsureSoundFilesExistAsync()
        {
            try
            {
                var soundsPath = Path.Combine(_globalSettingsPath, PathConstants.GoldbergGlobalSoundsFolderName);
                Directory.CreateDirectory(soundsPath);

                var achievementSoundPath = Path.Combine(soundsPath, PathConstants.SteamClientUiAchievementNotificationWav);
                var friendSoundPath = Path.Combine(soundsPath, PathConstants.SteamClientUiFriendNotificationWav);

                ValidationResult result;
                if (File.Exists(achievementSoundPath) && File.Exists(friendSoundPath))
                    result = ValidationResult.Success();
                else
                {
                    var steamResult = TryCopyFromSteam(soundsPath);
                    result = steamResult.IsValid
                        ? steamResult
                        : await ServiceLocator.AssetDownloadService.DownloadSoundFilesAsync(soundsPath).ConfigureAwait(false);
                }

                if (result.IsValid)
                {
                    await EnsureAvatarExistsAsync().ConfigureAwait(false);
                }
                return result;
            }
            catch (Exception ex)
            {
                return ValidationResult.Failure($"Failed to ensure sound files exist: {ex.Message}");
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
                    await DownloadAvatarFromGitHubAsync(avatarPath).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                LogRedactionHelper.WriteDebug($"Failed to ensure avatar exists: {ex.Message}");
            }
        }

        private static async Task DownloadAvatarFromGitHubAsync(string avatarPath)
        {
            try
            {
                bool success = await ServiceLocator.AssetDownloadService.DownloadAvatarAsync(avatarPath).ConfigureAwait(false);
                if (!success)
                    LogRedactionHelper.WriteDebug("Failed to download avatar from GitHub");
            }
            catch (Exception ex)
            {
                LogRedactionHelper.WriteDebug($"Failed to download avatar from GitHub: {ex.Message}");
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

    }
}
