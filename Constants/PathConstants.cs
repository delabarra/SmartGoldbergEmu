using System;
using System.IO;

namespace SmartGoldbergEmu.Constants
{
    /// <summary>
    /// Centralized constants for application paths.
    /// Eliminates path duplication across services and ensures consistency.
    /// </summary>
    public static class PathConstants
    {
        private const string ConfigFileName = "settings.ini";

        /// <summary>
        /// Pre-3.x application settings file name (2.x XML import and INI migration only).
        /// </summary>
        public const string LegacyConfigFileName = "SmartGoldbergEmu.cfg";

        /// <summary>
        /// Sidecar written after legacy XML import completes (removed on next startup when safe).
        /// </summary>
        public const string LegacyImportedConfigFileName = "SmartGoldbergEmu.cfg.imported";

        /// <summary>
        /// Main launcher executable file name beside the install root.
        /// </summary>
        public const string LauncherMainExecutableFileName = "SmartGoldbergEmu.exe";

        /// <summary>
        /// Downloaded launcher update archive name under <see cref="LauncherUpdateWorkDirectory"/>.
        /// </summary>
        public const string LauncherUpdateArchiveFileName = "SmartGoldbergEmu-update.zip";

        /// <summary>
        /// Extract subfolder name under <see cref="LauncherUpdateWorkDirectory"/>.
        /// </summary>
        public const string LauncherUpdateExtractFolderName = "extracted";

        /// <summary>
        /// Steamless install subfolder containing API plugins (see <see cref="SteamlessApiPluginRelativePath"/>).
        /// </summary>
        public const string SteamlessPluginsFolderName = "Plugins";

        /// <summary>
        /// Legacy dev-tool folder beside the launcher (generate_interfaces cleanup after Goldberg update).
        /// Not the repository <c>tools/</c> dev-scripts folder.
        /// </summary>
        public const string LauncherDevToolsFolderName = "tools";

        /// <summary>
        /// Legacy gbe_fork generate_interfaces tool folder under <see cref="LauncherDevToolsDirectory"/>.
        /// </summary>
        public const string GoldbergGenerateInterfacesToolFolderName = "generate_interfaces";

        /// <summary>
        /// Per-game Goldberg folder name under each app ID directory (matches Goldberg layout).
        /// </summary>
        public const string SteamSettingsFolderName = "steam_settings";

        /// <summary>
        /// Goldberg <c>steam_appid.txt</c> file name (beside game exe or under <see cref="SteamSettingsFolderName"/>).
        /// </summary>
        public const string SteamAppIdFileName = "steam_appid.txt";

        /// <summary>
        /// Folder name for per-app configs under the install root (matches shipped layout).
        /// </summary>
        public const string GamesDirectoryFolderName = "games";

        /// <summary>
        /// Per-game auxiliary folder under <c>games\{appId}</c> for exported game assets and library artwork.
        /// </summary>
        public const string GamesPerAppResourcesFolderName = "resources";

        // Filenames under games/{appId}/resources/ (Steam CDN / library artwork contract).
        public const string SteamGameResourcesHeaderImageFileName = "header.jpg";
        public const string SteamGameResourcesCapsuleCoverImageFileName = "cover.jpg";
        public const string SteamGameResourcesLibraryLogoImageFileName = "logo.png";
        public const string SteamGameResourcesClientIconFileExtension = ".ico";
        public const string SteamGameResourcesLegacyLibraryCapsuleImageFileName = "library_600x900.jpg";
        public const string SteamGameResourcesCapsuleImageFileName = "capsule.jpg";
        public const string SteamGameResourcesSmallCapsuleImageFileName = "capsule_231x87.jpg";
        public const string SteamGameResourcesMissingAssetsNoteFileName = "missing_assets.txt";

        /// <summary>
        /// Default Goldberg saves root folder name under %AppData% (Goldberg contract).
        /// </summary>
        public const string GseSavesFolderName = "GSE Saves";

        /// <summary>
        /// Install-root folder name for Goldberg emulator API binaries shipped next to the launcher.
        /// </summary>
        public const string GoldbergDirectoryFolderName = "goldberg";

        /// <summary>
        /// Steamless command-line executable name (release layout).
        /// </summary>
        public const string SteamlessCliExecutableName = "Steamless.CLI.exe";

        public const string SteamlessCliQuietFlag = "--quiet";
        public const string SteamlessCliKeepBindFlag = "--keepbind";
        public const string SteamlessCliKeepStubFlag = "--keepstub";
        public const string SteamlessCliDumpPayloadFlag = "--dumppayload";
        public const string SteamlessCliDumpDrmpFlag = "--dumpdrmp";
        public const string SteamlessCliRealignFlag = "--realign";
        public const string SteamlessCliRecalcChecksumFlag = "--recalcchecksum";
        public const string SteamlessCliExperimentalFlag = "--exp";

        /// <summary>
        /// Relative path to the Steamless API plugin required by the CLI.
        /// </summary>
        public const string SteamlessApiPluginRelativePath = "Plugins\\Steamless.API.dll";

        /// <summary>
        /// Suffix Steamless appends to the input executable file name for the unpacked output.
        /// </summary>
        public const string SteamlessUnpackedExecutableSuffix = ".unpacked.exe";

        /// <summary>
        /// Infix before the extension for the original executable backed up before Steamless replace (e.g. game.exe → game_o.exe).
        /// </summary>
        public const string SteamlessOriginalExecutableBackupInfix = "_o";

        /// <summary>
        /// Folder name for Goldberg global INI and overlay assets under %AppData%\GSE Saves\ (Goldberg contract).
        /// </summary>
        public const string GoldbergGlobalSettingsFolderName = "settings";

        /// <summary>
        /// Subfolder names under Goldberg global settings (%AppData%\GSE Saves\settings\).
        /// </summary>
        public const string GoldbergGlobalFontsFolderName = "fonts";
        public const string GoldbergGlobalSoundsFolderName = "sounds";
        public const string GoldbergGlobalControllerFolderName = "controller";
        public const string GoldbergGlobalGlyphsFolderName = "glyphs";

        /// <summary>
        /// Global overlay account avatar file name under Goldberg global settings.
        /// </summary>
        public const string GlobalAccountAvatarFileName = "account_avatar.jpg";

        /// <summary>
        /// Default overlay font file name under Goldberg global <c>settings\fonts</c>.
        /// </summary>
        public const string GoldbergGlobalDefaultOverlayFontFileName = "Roboto-Medium.ttf";

        /// <summary>
        /// Goldberg overlay notification WAV names (emulator reads these; excluded from sound picker lists).
        /// </summary>
        public const string SteamClientUiFriendNotificationWav = "overlay_friend_notification.wav";
        public const string SteamClientUiAchievementNotificationWav = "overlay_achievement_notification.wav";

        /// <summary>
        /// Steam <c>steamui\sounds</c> source WAV names copied into global sounds as library entries.
        /// </summary>
        public const string SteamClientUiAchievementSourceWav = "desktop_toast_default.wav";
        public const string SteamClientUiFriendSourceWav = "recording_highlight.wav";

        /// <summary>
        /// Whether <paramref name="fileName"/> is a Goldberg overlay default sound (not shown in Settings sound combos).
        /// </summary>
        public static bool IsGoldbergOverlayNotificationSoundFileName(string fileName)
        {
            return string.Equals(fileName, SteamClientUiAchievementNotificationWav, StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileName, SteamClientUiFriendNotificationWav, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 7-Zip reduced standalone executable name used during emulator updates.
        /// </summary>
        public const string LauncherSevenZipReducedExecutableName = "7zr.exe";

        /// <summary>
        /// Bundled placeholder and shipped documentation file names next to the launcher.
        /// </summary>
        public const string LauncherResourcesFolderName = "Resources";
        public const string LauncherImagesSubfolderName = "Images";
        public const string LauncherAchievementPlaceholderImageFileName = "achievement.png";
        /// <summary>
        /// Second-instance URI handoff: pending request files under <see cref="LocalAppDataPerUserDirectory"/>.
        /// </summary>
        public const string LauncherUriProtocolPendingFilePrefix = "uri_";
        public const string LauncherUriProtocolPendingFileExtension = ".txt";
        public const string LauncherUriProtocolPendingFileSearchPattern = LauncherUriProtocolPendingFilePrefix + "*.txt";

        /// <summary>
        /// Detached launch-cleanup watcher: per-AppId session manifests under <see cref="LocalAppDataPerUserDirectory"/>.
        /// </summary>
        public const string LaunchSessionManifestFolderName = "launch_sessions";

        public const string LaunchSessionManifestFileExtension = ".json";

        /// <summary>
        /// Optional per-game mods folder under <c>steam_settings</c>.
        /// </summary>
        public const string GoldbergSteamSettingsModsFolderName = "mods";

        /// <summary>
        /// Subfolder under per-game <c>steam_settings</c> for Steam HTTP request cache (Goldberg layout).
        /// </summary>
        public const string GoldbergSteamSettingsHttpFolderName = "http";

        /// <summary>
        /// Legacy plaintext API key file next to the launcher (migration source only).
        /// </summary>
        public const string LegacyApiKeyFileName = "steam_apikey.txt";

        /// <summary>
        /// Goldberg INI file names under <c>steam_settings</c> or global GSE <c>settings</c> (emulator contract).
        /// </summary>
        public const string GoldbergOverlayIniFileName = "configs.overlay.ini";
        public const string GoldbergMainIniFileName = "configs.main.ini";
        public const string GoldbergAppIniFileName = "configs.app.ini";
        public const string GoldbergUserIniFileName = "configs.user.ini";

        public const string GoldbergLeaderboardsFileName = "leaderboards.txt";
        public const string GoldbergStatsJsonFileName = "stats.json";
        public const string GoldbergStatsDbJsonFileName = "stats_db.json";

        public const string GoldbergBranchesJsonFileName = "branches.json";
        public const string GoldbergDepotsFileName = "depots.txt";
        public const string GoldbergItemsJsonFileName = "items.json";
        public const string GoldbergItemsNoteFileName = "items_note.txt";
        public const string GoldbergInstalledAppIdsFileName = "installed_app_ids.txt";
        public const string GoldbergSupportedLanguagesFileName = "supported_languages.txt";
        public const string GoldbergCustomBroadcastsFileName = "custom_broadcasts.txt";
        public const string GoldbergSubscribedGroupsFileName = "subscribed_groups.txt";
        public const string GoldbergSubscribedGroupsClansFileName = "subscribed_groups_clans.txt";
        public const string GoldbergAutoAcceptInviteFileName = "auto_accept_invite.txt";
        public const string GoldbergInternetServersFileName = "internet_servers.txt";
        public const string GoldbergFavoriteServersFileName = "favorite_servers.txt";
        public const string GoldbergHistoryServersFileName = "history_servers.txt";
        public const string GoldbergSteamInterfacesFileName = "steam_interfaces.txt";
        public const string GoldbergSteamInterfacesExampleFileName = "steam_interfaces.EXAMPLE.txt";

        public const string GoldbergDefaultItemsJsonFileName = "default_items.json";
        public const string GoldbergDefaultItemsExampleJsonFileName = "default_items.EXAMPLE.json";
        public const string GoldbergGcJsonFileName = "gc.json";
        public const string GoldbergPurchasedKeysFileName = "purchased_keys.txt";
        public const string GoldbergModsJsonFileName = "mods.json";
        public const string GoldbergModImagesFolderName = "mod_images";
        public const string GoldbergGlobalUserJsonFileName = "global_user.json";

        /// <summary>
        /// Per-game custom launch options INI next to <c>steam_settings</c> (launcher extension).
        /// </summary>
        public const string LauncherUserLaunchOptionsIniFileName = "user.launch.options.ini";

        /// <summary>
        /// Goldberg client DLL file names shipped under <c>goldberg</c> (fork release layout).
        /// </summary>
        public const string GoldbergSteamClientDll32 = "steamclient.dll";
        public const string GoldbergSteamClientDll64 = "steamclient64.dll";
        public const string GoldbergGameOverlayRendererDll32 = "GameOverlayRenderer.dll";
        public const string GoldbergGameOverlayRendererDll64 = "GameOverlayRenderer64.dll";
        public const string GoldbergStandardSteamApiDll32 = "steam_api.dll";
        public const string GoldbergStandardSteamApiDll64 = "steam_api64.dll";
        public const string GoldbergSteamDllFileName = "Steam.dll";

        /// <summary>
        /// Unmodified Steam client <c>Steam.dll</c> kept beside the Goldberg build for manual swap if the patched copy fails.
        /// </summary>
        public const string GoldbergSteamOriginalDllFileName = "steam_o.dll";

        public static string GoldbergExperimentalDirectory =>
            Path.Combine(GoldbergDirectory, GoldbergInstallLayout.ExperimentalFolderName);

        public static string GoldbergSteamClientExperimentalDirectory =>
            Path.Combine(GoldbergDirectory, GoldbergInstallLayout.SteamClientExperimentalFolderName);

        public static string GoldbergSteamOldDirectory =>
            Path.Combine(GoldbergDirectory, GoldbergInstallLayout.SteamOldFolderName);

        public static string GoldbergSteamClientExtraDllsDirectory =>
            Path.Combine(GoldbergDirectory, GoldbergInstallLayout.SteamClientExtraDllsFolderName);

        public static string CombineGoldbergExperimentalSteamApiPath(bool useX64) =>
            Path.Combine(
                GoldbergExperimentalDirectory,
                useX64 ? GoldbergStandardSteamApiDll64 : GoldbergStandardSteamApiDll32);

        public static string CombineGoldbergExperimentalSteamClientPath(bool useX64) =>
            Path.Combine(
                GoldbergExperimentalDirectory,
                useX64 ? GoldbergSteamClientDll64 : GoldbergSteamClientDll32);

        public static bool HasGoldbergExperimentalFiles(bool useX64) =>
            File.Exists(CombineGoldbergExperimentalSteamApiPath(useX64))
            && File.Exists(CombineGoldbergExperimentalSteamClientPath(useX64));

        public static bool HasSteamClientGoldbergFiles() =>
            File.Exists(CombineGoldbergSteamClientDllPath(false))
            && File.Exists(CombineGoldbergGameOverlayRendererPath(false))
            && File.Exists(CombineGoldbergSteamClientDllPath(true))
            && File.Exists(CombineGoldbergGameOverlayRendererPath(true));

        public static string CombineGoldbergSteamClientDllPath(bool useX64) =>
            Path.Combine(
                GoldbergSteamClientExperimentalDirectory,
                useX64 ? GoldbergSteamClientDll64 : GoldbergSteamClientDll32);

        public static string CombineGoldbergGameOverlayRendererPath(bool useX64) =>
            Path.Combine(
                GoldbergSteamClientExperimentalDirectory,
                useX64 ? GoldbergGameOverlayRendererDll64 : GoldbergGameOverlayRendererDll32);

        public static string CombineGoldbergSteamDllPath() =>
            Path.Combine(GoldbergSteamOldDirectory, GoldbergSteamDllFileName);

        public static string CombineGoldbergSteamOriginalDllPath() =>
            Path.Combine(GoldbergSteamOldDirectory, GoldbergSteamOriginalDllFileName);

        /// <summary>
        /// Per-game extra-DLL folder under <see cref="SteamSettingsFolderName"/> (Goldberg loads via LoadLibraryW).
        /// </summary>
        public const string GoldbergLoadDllsFolderName = "load_dlls";

        public const string GoldbergLoadDllsLoadOrderFileName = "load_order.txt";

        public static string CombineGameSteamSettingsLoadDllsDirectory(ulong appId) =>
            Path.Combine(GetGameSteamSettingsPath(appId), GoldbergLoadDllsFolderName);

        public static string CombineGameSteamSettingsSoundsDirectory(ulong appId) =>
            Path.Combine(GetGameSteamSettingsPath(appId), GoldbergGlobalSoundsFolderName);

        public static string CombineSteamSettingsLoadDllsDirectory(string steamSettingsDirectory) =>
            Path.Combine(steamSettingsDirectory, GoldbergLoadDllsFolderName);

        /// <summary>
        /// Controller binding file names under Goldberg global <c>settings\controller</c>.
        /// </summary>
        /// <summary>
        /// Steam client subfolder and VDF name for library folder discovery.
        /// </summary>
        public const string SteamAppsDirectoryName = "steamapps";

        public const string SteamLibraryFoldersVdfFileName = "libraryfolders.vdf";
        // Quoted key inside libraryfolders.vdf for each library root path.
        public const string SteamLibraryFoldersVdfPathKey = "path";

        public const string SteamProductInfoValveKeyValuesFileExtension = ".vdf";
        public const string SteamApiRedistributableDllSearchPattern = "steam_api*.dll";

        // App backup sidecar beside steam_api / steam_api64 (our .bkp-style copy before swap or Goldberg deploy).
        public const string SteamApiBackupSidecarExtension = ".sge";

        public const string SteamApiDllDeploymentLegacyBackupExtension = ".sge.bak";

        public const string SteamAppManifestFilePrefix = "appmanifest_";
        public const string SteamAppManifestFileExtension = ".acf";

        public const string SteamAppsCommonDirectoryName = "common";
        public const string SteamClientSteamUiFolderName = "steamui";
        public const string SteamClientUiSoundsFolderName = GoldbergGlobalSoundsFolderName;

        /// <summary>
        /// Default Steam client folder name under Program Files (x86) when probing library VDF.
        /// </summary>
        public const string SteamClientRelativeRootFolderName = "Steam";

        public const string SteamUserDataFolderName = "userdata";

        // {steamInstallationRoot}\userdata\{Steam3AccountID}\
        public static string CombineSteamUserDataAccountPath(string steamInstallationRoot, string steam3AccountId)
        {
            if (string.IsNullOrWhiteSpace(steamInstallationRoot) || string.IsNullOrWhiteSpace(steam3AccountId))
                return null;
            string root = steamInstallationRoot.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (root.Length == 0)
                return null;
            return Path.Combine(root, SteamUserDataFolderName, steam3AccountId.Trim());
        }

        // {steamInstallationRoot}\userdata\{Steam3AccountID}\{appId}\
        public static string CombineSteamUserDataGamePath(string steamInstallationRoot, string steam3AccountId, ulong appId)
        {
            string accountPath = CombineSteamUserDataAccountPath(steamInstallationRoot, steam3AccountId);
            if (string.IsNullOrEmpty(accountPath) || appId == 0)
                return null;
            return Path.Combine(accountPath, appId.ToString());
        }

        // {steamInstallationRoot}\steamui\sounds
        public static string CombineSteamClientUiSoundsPath(string steamInstallationRoot)
        {
            if (string.IsNullOrWhiteSpace(steamInstallationRoot))
                return null;
            string root = steamInstallationRoot.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (root.Length == 0)
                return null;
            return Path.Combine(root, SteamClientSteamUiFolderName, SteamClientUiSoundsFolderName);
        }

        // Program Files (x86)\Steam when registry does not yield a path.
        public static string GetProgramFilesX86DefaultSteamInstallationRoot()
        {
            string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (string.IsNullOrWhiteSpace(pf86))
                return null;
            return Path.Combine(pf86, SteamClientRelativeRootFolderName);
        }

        /// <summary>
        /// Library manifest file name under the games directory.
        /// </summary>
        public const string GamesIniFileName = "games.ini";

        // Full path to games.ini under a given games root (see GamesIniPath for default install layout).
        public static string CombineGamesIniPath(string gamesDirectoryRoot)
        {
            return Path.Combine(gamesDirectoryRoot, GamesIniFileName);
        }

        // {gamesDirectoryRoot}\{appIdFolder}
        public static string CombineGameFolder(string gamesDirectoryRoot, string appIdFolderName)
        {
            return Path.Combine(gamesDirectoryRoot, appIdFolderName);
        }

        // {gamesDirectoryRoot}\{appIdFolder}\resources
        public static string CombineGamesPerAppResourcesDirectory(string gamesDirectoryRoot, string appIdFolderName)
        {
            return Path.Combine(CombineGameFolder(gamesDirectoryRoot, appIdFolderName), GamesPerAppResourcesFolderName);
        }

        // {gamesDirectoryRoot}\{appIdFolder}\resources\{appId}.vdf (PICS export written on game save)
        public static string CombineGamesPerAppValveDataFilePath(string gamesDirectoryRoot, string appIdFolderName)
        {
            return Path.Combine(
                CombineGamesPerAppResourcesDirectory(gamesDirectoryRoot, appIdFolderName),
                appIdFolderName + SteamProductInfoValveKeyValuesFileExtension);
        }

        public static string GetSteamGameResourcesClientIconFileName(ulong appId)
        {
            return appId.ToString() + SteamGameResourcesClientIconFileExtension;
        }

        // {gamesDirectoryRoot}\{appIdFolder}\steam_settings
        public static string CombineGameSteamSettingsDirectory(string gamesDirectoryRoot, string appIdFolderName)
        {
            return Path.Combine(CombineGameFolder(gamesDirectoryRoot, appIdFolderName), SteamSettingsFolderName);
        }

        /// <summary>
        /// Per-user folder name under %LocalAppData% (IPC, UI settings) and legacy config cleanup.
        /// </summary>
        public const string LauncherPerUserFolderName = "SmartGoldbergEmu";

        /// <summary>
        /// Folder name for update download and extract scratch space under the install root.
        /// </summary>
        public const string LauncherUpdateTempFolderName = "temp";

        /// <summary>
        /// Folder name for the user-assets slice inside the update temp layout (matches release archive layout).
        /// </summary>
        public const string LauncherUpdateUserAssetsUnpackFolderName = "userassets";

        /// <summary>
        /// Subfolder under <see cref="LauncherUpdateTempFolderName"/> for launcher release download and extract.
        /// </summary>
        public const string LauncherUpdateWorkFolderName = "launcher-update";

        /// <summary>
        /// Extracted embedded updater executable name under <see cref="LauncherUpdateWorkDirectory"/>.
        /// </summary>
        public const string LauncherUpdateEmbeddedUpdaterFileName = "SmartGoldbergEmu.LauncherUpdate.exe";

        /// <summary>
        /// JSON manifest filename written before spawning the embedded updater.
        /// </summary>
        public const string LauncherUpdateApplyManifestFileName = "apply-manifest.json";

        /// <summary>
        /// Scratch folder for launcher release download, extract, and apply.
        /// Location: {AppBaseDirectory}\temp\launcher-update\
        /// </summary>
        public static string LauncherUpdateWorkDirectory =>
            Path.Combine(AppBaseDirectory, LauncherUpdateTempFolderName, LauncherUpdateWorkFolderName);

        /// <summary>
        /// Folder name under %Temp% for per-game <c>steam_settings</c> backups.
        /// </summary>
        public const string LauncherBackupTempFolderName = "SmartGoldbergEmu_Backup";

        /// <summary>
        /// Base directory where the application is running.
        /// </summary>
        public static string AppBaseDirectory => AppDomain.CurrentDomain.BaseDirectory;

        /// <summary>
        /// Directory containing SmartGoldbergEmu.exe (preferred over <see cref="AppBaseDirectory"/> for optional bundled tools).
        /// </summary>
        public static string LauncherInstallDirectory
        {
            get
            {
                try
                {
                    string location = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    if (!string.IsNullOrEmpty(location))
                    {
                        string dir = Path.GetDirectoryName(location);
                        if (!string.IsNullOrEmpty(dir))
                            return dir;
                    }
                }
                catch (Exception)
                {
                }

                return AppBaseDirectory;
            }
        }

        /// <summary>
        /// Directory where game configurations and emulator files are stored.
        /// Location: {AppBaseDirectory}\games\
        /// </summary>
        public static string GamesDirectory => Path.Combine(AppBaseDirectory, GamesDirectoryFolderName);

        /// <summary>
        /// Full path to the Steamless API plugin under a user-provided install root.
        /// </summary>
        public static string CombineSteamlessApiPluginPath(string steamlessInstallRoot)
        {
            if (string.IsNullOrWhiteSpace(steamlessInstallRoot))
                return null;
            return Path.Combine(steamlessInstallRoot.Trim(), SteamlessPluginsFolderName, "Steamless.API.dll");
        }

        public static string CombineSteamlessPluginsDirectory(string steamlessInstallRoot)
        {
            if (string.IsNullOrWhiteSpace(steamlessInstallRoot))
                return null;
            return Path.Combine(steamlessInstallRoot.Trim(), SteamlessPluginsFolderName);
        }

        public static string BuildSteamlessOriginalBackupPath(string executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath))
                return null;

            string directory = Path.GetDirectoryName(executablePath);
            string fileName = Path.GetFileName(executablePath);
            if (string.IsNullOrEmpty(fileName))
                return null;

            string extension = Path.GetExtension(fileName);
            string baseName = Path.GetFileNameWithoutExtension(fileName);
            string backupFileName = baseName + SteamlessOriginalExecutableBackupInfix + extension;

            return string.IsNullOrEmpty(directory)
                ? backupFileName
                : Path.Combine(directory, backupFileName);
        }

        public static string ApplicationLogFilePath =>
            Path.Combine(LauncherInstallDirectory, ApplicationConstants.ApplicationLogFileName);

        public static string LauncherMainExecutablePath =>
            Path.Combine(LauncherInstallDirectory, LauncherMainExecutableFileName);

        public static string LauncherDevToolsDirectory =>
            Path.Combine(LauncherInstallDirectory, LauncherDevToolsFolderName);

        public static string GoldbergGenerateInterfacesInstallDirectory =>
            Path.Combine(LauncherDevToolsDirectory, GoldbergGenerateInterfacesToolFolderName);

        /// <summary>
        /// Path to the games.ini file that stores the game library.
        /// Location: {GamesDirectory}\games.ini
        /// </summary>
        public static string GamesIniPath => CombineGamesIniPath(GamesDirectory);

        /// <summary>
        /// Global settings directory for Goldberg emulator configuration.
        /// Location: %AppData%\GSE Saves\settings\
        /// </summary>
        public static string GlobalSettingsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            GseSavesFolderName,
            GoldbergGlobalSettingsFolderName);

        public static string GetUserSavesRoot(string savesFolderName)
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                savesFolderName);
        }

        public static string GetUserSavesPath(string savesFolderName, ulong appId)
        {
            return Path.Combine(GetUserSavesRoot(savesFolderName), appId.ToString());
        }

        /// <summary>
        /// Application configuration file next to the launcher executable.
        /// Location: {AppBaseDirectory}\settings.ini
        /// </summary>
        public static string ConfigFilePath => Path.Combine(AppBaseDirectory, ConfigFileName);

        /// <summary>
        /// Former install-folder config path (2.x XML import and INI migration only).
        /// Location: {AppBaseDirectory}\SmartGoldbergEmu.cfg
        /// </summary>
        public static string LegacyExeConfigFilePath =>
            Path.Combine(AppBaseDirectory, LegacyConfigFileName);

        /// <summary>
        /// Legacy launcher folder under %LocalAppData% (leftover files removed after config migration).
        /// Location: %LocalAppData%\SmartGoldbergEmu\
        /// </summary>
        public static string LocalAppDataPerUserDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            LauncherPerUserFolderName);

        /// <summary>
        /// Per-user UI preferences INI file name (theme, window layout).
        /// </summary>
        public const string UiSettingsIniFileName = "ui_settings.ini";

        /// <summary>
        /// Per-user UI settings (theme, main window size/position/state).
        /// Location: %LocalAppData%\SmartGoldbergEmu\ui_settings.ini
        /// </summary>
        public static string UiSettingsFilePath =>
            Path.Combine(LocalAppDataPerUserDirectory, UiSettingsIniFileName);

        /// <summary>
        /// Former per-user config path (pre–exe-only layout). Used for one-time migration and legacy XML import lookup only.
        /// Location: %LocalAppData%\SmartGoldbergEmu\SmartGoldbergEmu.cfg
        /// </summary>
        public static string LegacyLocalAppDataConfigFilePath =>
            Path.Combine(LocalAppDataPerUserDirectory, LegacyConfigFileName);

        /// <summary>
        /// Goldberg emulator binaries root (subfolders: experimental, steamclient_experimental, steamclient_extra_dlls, steam_old).
        /// Location: {AppBaseDirectory}\goldberg\
        /// </summary>
        public static string GoldbergDirectory => Path.Combine(AppBaseDirectory, GoldbergDirectoryFolderName);

        /// <summary>
        /// Launch session manifests for the detached cleanup watcher.
        /// Location: %LocalAppData%\SmartGoldbergEmu\launch_sessions\
        /// </summary>
        public static string LaunchSessionManifestDirectory =>
            Path.Combine(LocalAppDataPerUserDirectory, LaunchSessionManifestFolderName);

        public static string CombineLaunchSessionManifestPath(ulong appId) =>
            Path.Combine(LaunchSessionManifestDirectory, appId.ToString() + LaunchSessionManifestFileExtension);

        /// <summary>
        /// Root directory for per-game <c>steam_settings</c> backups under the system temp folder (not under <see cref="LocalAppDataPerUserDirectory"/>).
        /// Location: %Temp%\SmartGoldbergEmu_Backup\
        /// </summary>
        public static string LauncherBackupTempRootDirectory =>
            Path.Combine(Path.GetTempPath(), LauncherBackupTempFolderName);

        /// <summary>
        /// Legacy API key file path (for migration to registry).
        /// Location: {AppBaseDirectory}\steam_apikey.txt
        /// </summary>
        public static string LegacyApiKeyFilePath => Path.Combine(AppBaseDirectory, LegacyApiKeyFileName);

        /// <summary>
        /// Gets the per-game library folder path.
        /// Location: {GamesDirectory}\{appId}\
        /// </summary>
        /// <param name="appId">The Steam App ID.</param>
        /// <returns>Path to the game's library folder.</returns>
        public static string GetGameFolder(ulong appId)
        {
            return CombineGameFolder(GamesDirectory, appId.ToString());
        }

        /// <summary>
        /// Gets the game-specific Steam settings folder path.
        /// Location: {GamesDirectory}\{appId}\steam_settings\
        /// </summary>
        /// <param name="appId">The Steam App ID.</param>
        /// <returns>Path to the game's Steam settings folder.</returns>
        public static string GetGameSteamSettingsPath(ulong appId)
        {
            return CombineGameSteamSettingsDirectory(GamesDirectory, appId.ToString());
        }

        /// <summary>
        /// Global fonts directory for custom overlay fonts.
        /// Location: %AppData%\GSE Saves\settings\fonts\
        /// </summary>
        public static string GlobalFontsPath => Path.Combine(GlobalSettingsPath, GoldbergGlobalFontsFolderName);

        /// <summary>
        /// Global sounds directory for overlay notification sounds.
        /// Location: %AppData%\GSE Saves\settings\sounds\
        /// </summary>
        public static string GlobalSoundsPath => Path.Combine(GlobalSettingsPath, GoldbergGlobalSoundsFolderName);

        /// <summary>
        /// Global controller glyphs directory for controller button images.
        /// Location: %AppData%\GSE Saves\settings\controller\glyphs\
        /// </summary>
        public static string GlobalControllerGlyphsPath => Path.Combine(
            GlobalSettingsPath,
            GoldbergGlobalControllerFolderName,
            GoldbergGlobalGlyphsFolderName);

        /// <summary>
        /// Global account avatar file path.
        /// Location: %AppData%\GSE Saves\settings\account_avatar.jpg
        /// </summary>
        public static string GlobalAccountAvatarPath => Path.Combine(GlobalSettingsPath, GlobalAccountAvatarFileName);
    }
}

