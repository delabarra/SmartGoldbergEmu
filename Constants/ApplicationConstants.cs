using System;
using System.Globalization;

namespace SmartGoldbergEmu.Constants
{
    /// <summary>
    /// Centralized constants for the SmartGoldbergEmu application.
    /// Contains view modes, sort options, and other magic strings used throughout the application.
    /// </summary>
    public static class ApplicationConstants
    {
        #region View Modes

        /// <summary>
        /// Wide horizontal library art (Steam-style header / store banner layout).
        /// </summary>
        public const string ViewModeTile = "Store Banner";

        /// <summary>
        /// Tall portrait library art (vertical capsule / library cover layout).
        /// </summary>
        public const string ViewModeCompactTiles = "Library Cover";

        /// <summary>
        /// Legacy INI value for <see cref="ViewModeTile"/> before rename.
        /// </summary>
        public const string ViewModeTileLegacy = "Tile";

        /// <summary>
        /// Legacy INI value for <see cref="ViewModeTile"/> (Store Capsule label).
        /// </summary>
        public const string ViewModeTileLegacyStoreCapsule = "Store Capsule";

        /// <summary>
        /// Legacy INI value for <see cref="ViewModeCompactTiles"/> before rename.
        /// </summary>
        public const string ViewModeCompactTilesLegacy = "Compact Tiles";

        /// <summary>
        /// Legacy INI value for <see cref="ViewModeCompactTiles"/> (Digital Cover label).
        /// </summary>
        public const string ViewModeCompactTilesLegacyDigitalCover = "Digital Cover";

        /// <summary>
        /// Icons view mode - displays games as large icons.
        /// </summary>
        public const string ViewModeIcons = "Icons";

        /// <summary>
        /// Logos view mode - displays games as logo tiles.
        /// </summary>
        public const string ViewModeLogos = "Logos";

        /// <summary>
        /// Details view mode - displays games in a detailed list with columns.
        /// </summary>
        public const string ViewModeDetails = "Details";

        /// <summary>
        /// Default view mode when no preference is saved.
        /// </summary>
        public const string ViewModeDefault = ViewModeTile;

        /// <summary>
        /// Maps persisted view mode strings to current canonical values (handles legacy INI entries).
        /// </summary>
        public static string NormalizeViewMode(string viewMode)
        {
            if (string.IsNullOrWhiteSpace(viewMode))
                return ViewModeDefault;
            if (viewMode.Equals(ViewModeTileLegacy, StringComparison.OrdinalIgnoreCase)
                || viewMode.Equals(ViewModeTileLegacyStoreCapsule, StringComparison.OrdinalIgnoreCase))
                return ViewModeTile;
            if (viewMode.Equals(ViewModeCompactTilesLegacy, StringComparison.OrdinalIgnoreCase)
                || viewMode.Equals(ViewModeCompactTilesLegacyDigitalCover, StringComparison.OrdinalIgnoreCase))
                return ViewModeCompactTiles;
            return viewMode;
        }

        #endregion

        #region Sort Options

        /// <summary>
        /// Sort by game name.
        /// </summary>
        public const string SortByName = "Name";

        /// <summary>
        /// Sort by App ID.
        /// </summary>
        public const string SortByAppId = "AppId";

        /// <summary>
        /// No sorting applied.
        /// </summary>
        public const string SortByNone = "None";

        /// <summary>
        /// Default sort option when no preference is saved.
        /// </summary>
        public const string SortByDefault = SortByNone;

        /// <summary>
        /// Ascending sort direction.
        /// </summary>
        public const string SortDirectionAsc = "Asc";

        /// <summary>
        /// Descending sort direction.
        /// </summary>
        public const string SortDirectionDesc = "Desc";

        /// <summary>
        /// Default sort direction when no preference is saved.
        /// </summary>
        public const string SortDirectionDefault = SortDirectionAsc;

        #endregion

        #region Game images

        /// <summary>
        /// Community site for custom library artwork when automatic Steam downloads are incomplete.
        /// </summary>
        public const string SteamGridDbHomeUrl = "https://www.steamgriddb.com/";

        #endregion

        #region Column Names

        /// <summary>
        /// Name column header text.
        /// </summary>
        public const string ColumnName = "Name";

        /// <summary>
        /// App ID column header text.
        /// </summary>
        public const string ColumnAppId = "App ID";

        /// <summary>
        /// Path column header text.
        /// </summary>
        public const string ColumnPath = "Path";

        /// <summary>
        /// Default column order for Details view.
        /// </summary>
        public const string DefaultColumnOrder = "Name,App ID,Path";

        /// <summary>
        /// Default Details data column widths (px) for Name, App ID, Path. Path stretches to the list client edge on layout.
        /// </summary>
        public const string DefaultDetailsColumnWidths = "200,100,300";

        public const int DetailsColumnWidthMin = 40;

        public const int DetailsColumnWidthMax = 4000;

        #endregion

        #region Application Settings Keys

        /// <summary>
        /// INI file key for view mode setting.
        /// </summary>
        public const string SettingKeyViewMode = "view_mode";

        /// <summary>
        /// INI file key for sort by setting.
        /// </summary>
        public const string SettingKeySortBy = "sort_by";

        /// <summary>
        /// INI file key for sort direction setting.
        /// </summary>
        public const string SettingKeySortDirection = "sort_direction";

        /// <summary>
        /// INI file key for details column order setting.
        /// </summary>
        public const string SettingKeyDetailsColumnOrder = "details_column_order";

        /// <summary>
        /// INI file key for Details data column widths (Name, App ID, Path).
        /// </summary>
        public const string SettingKeyDetailsColumnWidths = "details_column_widths";

        /// <summary>
        /// INI file key for Logos view ImageList drop shadow (debug/tuning).
        /// </summary>
        public const string SettingKeyLogosViewDropShadow = "logos_view_drop_shadow";

        /// <summary>
        /// INI file key for the saved Steamless.CLI.exe path.
        /// </summary>
        public const string SettingKeySteamlessCliPath = "steamless_cli_path";

        /// <summary>
        /// INI file key for theme mode (Light, Dark, System).
        /// </summary>
        public const string SettingKeyThemeMode = "theme_mode";

        /// <summary>
        /// INI file section for main window layout.
        /// </summary>
        public const string SettingSectionWindow = "window";

        /// <summary>
        /// INI file key for main window size (width,height).
        /// </summary>
        public const string SettingKeyWindowSize = "size";

        /// <summary>
        /// INI file key for main window location (x,y).
        /// </summary>
        public const string SettingKeyWindowLocation = "location";

        /// <summary>
        /// INI file key for main window state (Normal, Maximized, Minimized).
        /// </summary>
        public const string SettingKeyWindowState = "state";

        /// <summary>
        /// Open file dialog filter for selecting Steamless.CLI.exe.
        /// </summary>
        public const string SteamlessCliFileDialogFilter = "Steamless CLI (Steamless.CLI.exe)|Steamless.CLI.exe|Executable (*.exe)|*.exe";

        /// <summary>
        /// INI file section name for application settings.
        /// </summary>
        public const string SettingSectionApplication = "application";

        #endregion

        #region File Extensions

        /// <summary>
        /// Executable file extension filter for game selection.
        /// </summary>
        public const string ExecutableFileFilter = "Executable Files (*.exe;*.bat)|*.exe;*.bat|All Files (*.*)|*.*";

        /// <summary>
        /// Shortcut file extension filter.
        /// </summary>
        public const string ShortcutFileFilter = "Internet Shortcut (*.url)|*.url";

        #endregion

        #region URI Protocol

        /// <summary>
        /// URI protocol scheme for SmartGoldbergEmu.
        /// </summary>
        public const string UriProtocolScheme = "sge";

        /// <summary>
        /// Scheme plus authority separator (e.g. <c>sge://</c>).
        /// </summary>
        public const string UriProtocolAuthorityPrefix = UriProtocolScheme + "://";

        /// <summary>
        /// Run command segment after the authority (e.g. <c>run/</c>).
        /// </summary>
        public const string UriProtocolRunCommandSegment = "run/";

        /// <summary>
        /// Full prefix for <c>sge://run/</c> launch URIs.
        /// </summary>
        public const string UriProtocolCommandPrefix = UriProtocolAuthorityPrefix + UriProtocolRunCommandSegment;

        /// <summary>
        /// HKCU root for per-user protocol and class registrations.
        /// </summary>
        public const string UriProtocolCurrentUserClassesRegistryRoot = @"Software\Classes";

        /// <summary>
        /// ProgId default value uses this prefix plus <see cref="UriProtocolRegistrationFriendlyDescription"/>.
        /// </summary>
        public const string UriProtocolRegistryUrlClassPrefix = "URL:";

        /// <summary>
        /// Human-readable protocol name stored in the registry ProgId default value.
        /// </summary>
        public const string UriProtocolRegistrationFriendlyDescription = "SmartGoldbergEmu Protocol";

        /// <summary>
        /// Registry value name marking a URL protocol handler.
        /// </summary>
        public const string UriProtocolRegistryUrlProtocolMarkerValueName = "URL Protocol";

        /// <summary>
        /// Subkey for DefaultIcon under the protocol ProgId.
        /// </summary>
        public const string UriProtocolRegistryDefaultIconSubKey = "DefaultIcon";

        /// <summary>
        /// Subkey for shell open command under the protocol ProgId.
        /// </summary>
        public const string UriProtocolRegistryShellOpenCommandSubKey = @"shell\open\command";

        public const string HttpUriSchemePrefix = "http://";

        public const string HttpsUriSchemePrefix = "https://";

        #endregion

        #region Windows system registry (optional reads)

        // 7-Zip official installer — path value lives under HKLM and sometimes HKCU.
        public const string SevenZipRegistrySubKey = @"SOFTWARE\7-Zip";

        public const string SevenZipRegistryInstallPathValueName = "Path";

        // AppsUseLightTheme and related values (system vs app light/dark).
        public const string WindowsCurrentUserThemesPersonalizeRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

        public const string WindowsAppsUseLightThemeRegistryValueName = "AppsUseLightTheme";

        #endregion

        #region Window Management

        /// <summary>
        /// Window title for the main application form.
        /// </summary>
        public const string WindowTitle = "SmartGoldbergEmu Launcher";

        /// <summary>
        /// Mutex name for single instance enforcement.
        /// </summary>
        public const string MutexName = "SmartGoldbergEmu_SingleInstance_Mutex";

        /// <summary>
        /// Headless mode: wait for a game PID from a launch-session manifest, then run Goldberg deploy cleanup.
        /// Usage: <c>SmartGoldbergEmu.exe --launch-cleanup-watcher "path\to\manifest.json"</c>
        /// </summary>
        public const string LaunchCleanupWatcherCliFlag = "launch-cleanup-watcher";

        /// <summary>
        /// After launch, Steam registry redirects (ActiveProcess / SourceModInstallPath) are restored
        /// once this window elapses so games keep running with loaded Goldberg DLLs.
        /// </summary>
        public const int LaunchRegistryRedirectDurationMs = 15_000;

        #endregion

        #region Online app metadata URLs

        public const string SteamAppListArchiveAppListJsonUrl =
            "https://raw.githubusercontent.com/delabarra/ISteamApps-GetAppList-v2-Archive/refs/heads/main/appList.json";

        public const string SteamStoreAppDetailsApiUrlFormat = "https://store.steampowered.com/api/appdetails?appids={0}";
        public const string SteamWebApiKeyRegistrationUrl = "https://steamcommunity.com/dev/apikey";
        public const string SteamUserStatsSchemaApiUrlFormat = "https://api.steampowered.com/ISteamUserStats/GetSchemaForGame/v2/?l={0}&key={1}&appid={2}";
        public static readonly string GamesInfosDatasSteamStatsDbUrlFormat =
            "https://raw.githubusercontent.com/Nemirtingas/games-infos-datas/main/steam/{0}/" + PathConstants.GoldbergStatsDbJsonFileName;
        public const string SteamCommunityLeaderboardsXmlUrlFormat = "https://steamcommunity.com/stats/{0}/leaderboards/?xml=1";
        public const string SteamInventoryItemDefMetaApiUrlFormat = "https://api.steampowered.com/IInventoryService/GetItemDefMeta/v1?key={0}&appid={1}";
        public const string SteamGameInventoryItemDefArchiveApiUrlFormat = "https://api.steampowered.com/IGameInventory/GetItemDefArchive/v0001?appid={0}&digest={1}";
        public const string SteamPublishedFileDetailsApiUrlPrefix = "https://api.steampowered.com/IPublishedFileService/GetDetails/v1/?key=";
        public const string SteamDirectoryGetCmListForConnectUrl =
            "https://api.steampowered.com/ISteamDirectory/GetCMListForConnect/v1/?cellid=0&maxcount=50";
        public const string SteamSearchGamesApiUrlFormat = "https://steam-search.vercel.app/api/games?search={0}";
        public const string SevenZipStandaloneExeDownloadUrl = "https://www.7-zip.org/a/7zr.exe";
        public const string SteamStoreAppUrlFormat = "https://store.steampowered.com/app/{0}";
        public const string SteamCommunityAppUrlFormat = "https://steamcommunity.com/app/{0}";
        public const string SteamCommunityWorkshopUrlFormat = "https://steamcommunity.com/app/{0}/workshop/";

        public const string SteamStoreAssetFileUrlFormat = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/{0}/{1}";
        public const string SteamCommunityAssetsClientIconIcoUrlFormat = "https://shared.fastly.steamstatic.com/community_assets/images/apps/{0}/{1}.ico";
        public const string SteamDbAppUrlFormat = "https://steamdb.info/app/{0}";
        public const string SteamDbDepotsUrlFormat = "https://steamdb.info/app/{0}/depots/";
        public const string SteamDbConfigUrlFormat = "https://steamdb.info/app/{0}/config/";
        public const string ValveSteamCommandLineOptionsUrl = "https://developer.valvesoftware.com/wiki/Command_line_options_(Steam)";
        public const string SteamPartnerLocalizationLanguagesUrl = "https://partner.steamgames.com/doc/store/localization/languages";
        public const string IbanCountryCodesUrl = "https://www.iban.com/country-codes";

        #endregion

        #region Default Values

        /// <summary>
        /// Default saves folder name used by Goldberg emulator.
        /// </summary>
        public const string DefaultSavesFolderName = PathConstants.GseSavesFolderName;

        // Settings UI: steam\userdata\{Steam3AccountID}\{AppID}\
        public const string SteamUserdataPathDisplayFormat = "steam\\userdata\\{0}\\{1}\\";

        /// <summary>
        /// Default account name for Goldberg emulator.
        /// </summary>
        public const string DefaultAccountName = "SmartGoldberg";

        /// <summary>
        /// Default Steam ID for Goldberg emulator.
        /// </summary>
        public const string DefaultSteamId = "76561197960287930";

        // Steam64 ID range (Steam3AccountID folder name is Steam64 minus base).
        public const ulong SteamId64Base = 76561197960265728UL;
        public const ulong SteamId64Max = 76561202255233023UL;

        /// <summary>
        /// Default language for Goldberg emulator.
        /// </summary>
        public const string DefaultLanguage = "english";

        /// <summary>
        /// Default IP country code for Goldberg emulator.
        /// </summary>
        public const string DefaultIpCountry = "US";

        #endregion

        #region Diagnostics

        /// <summary>
        /// Default diagnostic log file next to the executable (plain text).
        /// </summary>
        public const string ApplicationLogFileName = "console.log";

        #endregion

        /// <summary>
        /// Parses comma-separated Details widths for Name, App ID, Path.
        /// </summary>
        public static bool TryParseDetailsColumnWidths(string raw, out int nameWidth, out int appIdWidth, out int pathWidth)
        {
            nameWidth = appIdWidth = pathWidth = 0;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            var parts = raw.Split(new[] { ',' }, StringSplitOptions.None);
            if (parts.Length != 3)
                return false;

            if (!int.TryParse(parts[0].Trim(), out nameWidth) ||
                !int.TryParse(parts[1].Trim(), out appIdWidth) ||
                !int.TryParse(parts[2].Trim(), out pathWidth))
                return false;

            if (nameWidth < DetailsColumnWidthMin || nameWidth > DetailsColumnWidthMax ||
                appIdWidth < DetailsColumnWidthMin || appIdWidth > DetailsColumnWidthMax ||
                pathWidth < DetailsColumnWidthMin || pathWidth > DetailsColumnWidthMax)
                return false;

            return true;
        }

        /// <summary>
        /// Returns a valid comma-separated width string for Name, App ID, Path.
        /// </summary>
        public static string NormalizeDetailsColumnWidths(string raw)
        {
            return TryParseDetailsColumnWidths(raw, out int w0, out int w1, out int w2)
                ? string.Format(CultureInfo.InvariantCulture, "{0},{1},{2}", w0, w1, w2)
                : DefaultDetailsColumnWidths;
        }
    }
}

