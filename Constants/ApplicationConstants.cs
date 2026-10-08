using System;
using System.Globalization;

namespace SmartGoldbergEmu.Constants
{
    public static class ApplicationConstants
    {
        #region View Modes

        // Wide horizontal library art (store header / banner layout).
        public const string ViewModeTile = "Store Banner";

        // Tall portrait library art (library cover layout).
        public const string ViewModeCompactTiles = "Library Cover";

        // Legacy persisted view_mode values; NormalizeViewMode maps them to the current ones.
        public const string ViewModeTileLegacy = "Tile";

        public const string ViewModeTileLegacyStoreCapsule = "Store Capsule";

        public const string ViewModeCompactTilesLegacy = "Compact Tiles";

        public const string ViewModeCompactTilesLegacyDigitalCover = "Digital Cover";

        public const string ViewModeIcons = "Icons";

        public const string ViewModeLogos = "Logos";

        public const string ViewModeDetails = "Details";

        public const string ViewModeDefault = ViewModeTile;

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

        public const string SortByName = "Name";

        public const string SortByAppId = "AppId";

        public const string SortByNone = "None";

        public const string SortByDefault = SortByNone;

        public const string SortDirectionAsc = "Asc";

        public const string SortDirectionDesc = "Desc";

        public const string SortDirectionDefault = SortDirectionAsc;

        #endregion

        #region Game images

        public const string SteamGridDbHomeUrl = "https://www.steamgriddb.com/";

        #endregion

        #region Column Names

        public const string ColumnName = "Name";

        public const string ColumnAppId = "App ID";

        public const string ColumnPath = "Path";

        public const string DefaultColumnOrder = "Name,App ID,Path";

        // Pixel widths for Name, App ID, Path; Path stretches to the list's client edge on layout.
        public const string DefaultDetailsColumnWidths = "200,100,300";

        public const int DetailsColumnWidthMin = 40;

        public const int DetailsColumnWidthMax = 4000;

        #endregion

        #region Application Settings Keys

        public const string SettingKeyViewMode = "view_mode";

        public const string SettingKeySortBy = "sort_by";

        public const string SettingKeySortDirection = "sort_direction";

        public const string SettingKeyDetailsColumnOrder = "details_column_order";

        public const string SettingKeyDetailsColumnWidths = "details_column_widths";

        // Debug/tuning toggle for the Logos view ImageList drop shadow.
        public const string SettingKeyLogosViewDropShadow = "logos_view_drop_shadow";

        // Light, Dark, or System.
        public const string SettingKeyThemeMode = "theme_mode";

        public const string SettingSectionWindow = "window";

        // Stored as "width,height".
        public const string SettingKeyWindowSize = "size";

        // Stored as "x,y".
        public const string SettingKeyWindowLocation = "location";

        // Normal, Maximized, or Minimized.
        public const string SettingKeyWindowState = "state";

        public const string SettingSectionApplication = "application";

        // Last OpenFileDialog / FolderBrowserDialog directories per FileDialogBrowseHelper.Purpose (ui_settings.ini).
        public const string SettingSectionBrowseFolders = "browse_folders";

        #endregion

        #region File Extensions

        public const string ExecutableFileFilter = "Executable Files (*.exe;*.bat)|*.exe;*.bat|All Files (*.*)|*.*";

        public const string ShortcutFileFilter = "Internet Shortcut (*.url)|*.url";

        #endregion

        #region URI Protocol

        public const string UriProtocolScheme = "sge";

        public const string UriProtocolAuthorityPrefix = UriProtocolScheme + "://";

        public const string UriProtocolRunCommandSegment = "run/";

        public const string UriProtocolCommandPrefix = UriProtocolAuthorityPrefix + UriProtocolRunCommandSegment;

        // Under HKCU (per-user protocol registration).
        public const string UriProtocolCurrentUserClassesRegistryRoot = @"Software\Classes";

        // ProgId default value is this prefix plus UriProtocolRegistrationFriendlyDescription.
        public const string UriProtocolRegistryUrlClassPrefix = "URL:";

        public const string UriProtocolRegistrationFriendlyDescription = "SmartGoldbergEmu Protocol";

        public const string UriProtocolRegistryUrlProtocolMarkerValueName = "URL Protocol";

        public const string UriProtocolRegistryDefaultIconSubKey = "DefaultIcon";

        public const string UriProtocolRegistryShellOpenCommandSubKey = @"shell\open\command";

        public const string HttpUriSchemePrefix = "http://";

        public const string HttpsUriSchemePrefix = "https://";

        #endregion

        #region Windows system registry (optional reads)

        // AppsUseLightTheme and related values (system vs app light/dark).
        public const string WindowsCurrentUserThemesPersonalizeRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

        public const string WindowsAppsUseLightThemeRegistryValueName = "AppsUseLightTheme";

        #endregion

        #region Window Management

        public const string WindowTitle = "SmartGoldbergEmu Launcher";

        public const string MutexName = "SmartGoldbergEmu_SingleInstance_Mutex";

        // Headless mode: wait for the game PID from a launch-session manifest, then run Goldberg deploy cleanup.
        // Usage: SmartGoldbergEmu.exe --launch-cleanup-watcher "path\to\manifest.json"
        public const string LaunchCleanupWatcherCliFlag = "launch-cleanup-watcher";

        // Steam registry redirects (ActiveProcess / SourceModInstallPath) are restored after this window; the game keeps the Goldberg DLLs it already loaded.
        public const int LaunchRegistryRedirectDurationMs = 15_000;

        #endregion

        #region Online app metadata URLs

        public const string SteamWebApiKeyRegistrationUrl = "https://steamcommunity.com/dev/apikey";
        public const string SteamUserStatsSchemaApiUrlFormat = "https://api.steampowered.com/ISteamUserStats/GetSchemaForGame/v2/?l={0}&key={1}&appid={2}";
        // Nemirtingas/games-infos-datas: steam/{appId}/{fileName} (achievements_db, stats_db, inventory_db, …).
        public const string GamesInfosDatasSteamFileUrlFormat =
            "https://raw.githubusercontent.com/Nemirtingas/games-infos-datas/main/steam/{0}/{1}";
        public static readonly string GamesInfosDatasSteamStatsDbUrlFormat =
            "https://raw.githubusercontent.com/Nemirtingas/games-infos-datas/main/steam/{0}/" + PathConstants.GoldbergStatsDbJsonFileName;
        public const string SteamCommunityLeaderboardsXmlUrlFormat = "https://steamcommunity.com/stats/{0}/leaderboards/?xml=1";
        public const string SteamPublishedFileDetailsApiUrlPrefix = "https://api.steampowered.com/IPublishedFileService/GetDetails/v1/?key=";
        // Steam ranks CMs for this client (IP/load). GetCMList is a global TCP dump if ForConnect has no netfilter.
        public const string SteamDirectoryGetCmListForConnectUrl =
            "https://api.steampowered.com/ISteamDirectory/GetCMListForConnect/v1/?cellid=0&maxcount=200";
        public const string SteamDirectoryGetCmListUrl =
            "https://api.steampowered.com/ISteamDirectory/GetCMList/v1/?cellid=0";
        // Store catalog search (games / Windows). Packages/bundles are skipped when parsing; delisted apps use SteamSearchGamesApiUrlFormat.
        public const string SteamStoreSearchCatalogUrlFormat =
            "https://store.steampowered.com/search/results/?term={0}&category1=998&os=win&cc=US&l=english&start=0&count=50";
        public const string SteamSearchGamesApiUrlFormat = "https://sgel-app-index.vercel.app/api/games?search={0}";
        public const string SteamStoreAppUrlFormat = "https://store.steampowered.com/app/{0}";
        public const string SteamCommunityAppUrlFormat = "https://steamcommunity.com/app/{0}";
        public const string SteamCommunityWorkshopUrlFormat = "https://steamcommunity.com/app/{0}/workshop/";

        public const string SteamDbAppUrlFormat = "https://steamdb.info/app/{0}";
        public const string SteamDbDepotsUrlFormat = "https://steamdb.info/app/{0}/depots/";
        public const string SteamDbConfigUrlFormat = "https://steamdb.info/app/{0}/config/";
        public const string ValveSteamCommandLineOptionsUrl = "https://developer.valvesoftware.com/wiki/Command_line_options_(Steam)";
        public const string SteamPartnerLocalizationLanguagesUrl = "https://partner.steamgames.com/doc/store/localization/languages";
        public const string IbanCountryCodesUrl = "https://www.iban.com/country-codes";

        #endregion

        #region Default Values

        public const string DefaultSavesFolderName = PathConstants.GseSavesFolderName;

        // Settings UI: steam\userdata\{Steam3AccountID}\{AppID}\
        public const string SteamUserdataPathDisplayFormat = "steam\\userdata\\{0}\\{1}\\";

        public const string DefaultAccountName = "SmartGoldberg";

        public const string DefaultSteamId = "76561197960287930";

        // Steam64 ID range (Steam3AccountID folder name is Steam64 minus base).
        public const ulong SteamId64Base = 76561197960265728UL;
        public const ulong SteamId64Max = 76561202255233023UL;

        public const string DefaultLanguage = "english";

        public const string DefaultIpCountry = "US";

        #endregion

        #region Diagnostics

        public const string ApplicationLogFileName = "console.log";

        #endregion

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

        public static string NormalizeDetailsColumnWidths(string raw)
        {
            return TryParseDetailsColumnWidths(raw, out int w0, out int w1, out int w2)
                ? string.Format(CultureInfo.InvariantCulture, "{0},{1},{2}", w0, w1, w2)
                : DefaultDetailsColumnWidths;
        }
    }
}
