using System;
using System.IO;

namespace SmartGoldbergEmu.Constants
{
    public static class PathConstants
    {
        private const string ConfigFileName = "settings.ini";

        // Pre-3.x settings file; read only for 2.x XML import and INI migration.
        public const string LegacyConfigFileName = "SmartGoldbergEmu.cfg";

        // Written after legacy XML import completes; removed on a later startup when safe.
        public const string LegacyImportedConfigFileName = "SmartGoldbergEmu.cfg.imported";

        public const string LauncherMainExecutableFileName = "SmartGoldbergEmu.exe";

        // Under LauncherUpdateWorkDirectory.
        public const string LauncherUpdateArchiveFileName = "SmartGoldbergEmu-update.zip";

        // Under LauncherUpdateWorkDirectory.
        public const string LauncherUpdateExtractFolderName = "extracted";

        // Legacy dev-tool folder beside the launcher, cleaned up after Goldberg update (not the repository tools/ folder).
        public const string LauncherDevToolsFolderName = "tools";

        // Legacy gbe_fork tool folder under LauncherDevToolsDirectory.
        public const string GoldbergGenerateInterfacesToolFolderName = "generate_interfaces";

        public const string SteamSettingsFolderName = "steam_settings";

        // Goldberg reads this beside the game exe or under steam_settings.
        public const string SteamAppIdFileName = "steam_appid.txt";

        public const string GamesDirectoryFolderName = "games";

        public const string GamesPerAppResourcesFolderName = "resources";

        // Filenames under games/{appId}/resources/ (Steam CDN / library artwork contract).
        public const string SteamGameResourcesHeaderImageFileName = "header.jpg";
        public const string SteamGameResourcesHeader2xImageFileName = "header_2x.jpg";
        public const string SteamGameResourcesLibraryHeaderImageFileName = "library_header.jpg";
        public const string SteamGameResourcesLibraryHeader2xImageFileName = "library_header_2x.jpg";
        public const string SteamGameResourcesCapsuleCoverImageFileName = "cover.jpg";
        public const string SteamGameResourcesLibraryLogoImageFileName = "logo.png";
        public const string SteamGameResourcesLibraryLogo2xImageFileName = "logo_2x.png";
        public const string SteamGameResourcesClientIconFileExtension = ".ico";
        public const string SteamGameResourcesLibraryCapsuleImageFileName = "library_capsule.jpg";
        public const string SteamGameResourcesLibraryCapsule2xImageFileName = "library_capsule_2x.jpg";
        public const string SteamGameResourcesLegacyLibraryCapsuleImageFileName = "library_600x900.jpg";
        public const string SteamGameResourcesLegacyLibraryCapsule2xImageFileName = "library_600x900_2x.jpg";
        public const string SteamGameResourcesLibraryHeroImageFileName = "library_hero.jpg";
        public const string SteamGameResourcesLibraryHero2xImageFileName = "library_hero_2x.jpg";
        public const string SteamGameResourcesLibraryHeroBlurImageFileName = "library_hero_blur.jpg";
        public const string SteamGameResourcesLibraryLogoPicsImageFileName = "library_logo.png";
        public const string SteamGameResourcesLibraryLogoPics2xImageFileName = "library_logo_2x.png";
        public const string SteamGameResourcesCapsuleImageFileName = "capsule.jpg";
        public const string SteamGameResourcesSmallCapsuleImageFileName = "capsule_231x87.jpg";
        public const string SteamGameResourcesSmallCapsule2xImageFileName = "capsule_231x87_2x.jpg";
        public const string SteamGameResourcesLargeCapsuleImageFileName = "capsule_616x353.jpg";
        public const string SteamGameResourcesLargeCapsule2xImageFileName = "capsule_616x353_2x.jpg";
        public const string SteamGameResourcesHeroCapsuleImageFileName = "hero_capsule.jpg";
        public const string SteamGameResourcesHeroCapsule2xImageFileName = "hero_capsule_2x.jpg";
        public const string SteamGameResourcesPageBackgroundImageFileName = "page_bg_v6.jpg";
        public const string SteamGameResourcesPageBackgroundRawImageFileName = "page_bg_raw.jpg";
        public const string SteamGameResourcesLegacyPageBackgroundImageFileName = "page_bg_generated.jpg";
        public const string SteamGameResourcesMissingAssetsNoteFileName = "missing_assets.txt";

        // Goldberg saves root under %AppData% (emulator contract).
        public const string GseSavesFolderName = "GSE Saves";

        public const string GoldbergDirectoryFolderName = "goldberg";

        // Temporary unpacked output beside the input exe (e.g. game.exe.unpacked.exe).
        public const string StubUnpackedExecutableSuffix = ".unpacked.exe";

        // Original exe backup taken before stub replace (e.g. game.exe -> game_o.exe).
        public const string StubOriginalExecutableBackupInfix = "_o";

        // Goldberg global INI and overlay assets under %AppData%\GSE Saves\ (emulator contract).
        public const string GoldbergGlobalSettingsFolderName = "settings";

        // Subfolders under %AppData%\GSE Saves\settings\.
        public const string GoldbergGlobalFontsFolderName = "fonts";
        public const string GoldbergGlobalSoundsFolderName = "sounds";
        public const string GoldbergGlobalControllerFolderName = "controller";
        public const string GoldbergGlobalGlyphsFolderName = "glyphs";

        public const string GlobalAccountAvatarFileName = "account_avatar.jpg";

        public const string GoldbergGlobalDefaultOverlayFontFileName = "Roboto-Medium.ttf";

        // Goldberg reads these overlay notification sounds; they are excluded from sound picker lists.
        public const string SteamClientUiFriendNotificationWav = "overlay_friend_notification.wav";
        public const string SteamClientUiAchievementNotificationWav = "overlay_achievement_notification.wav";

        // Steam steamui\sounds source WAVs copied into global sounds as library entries.
        public const string SteamClientUiAchievementSourceWav = "desktop_toast_default.wav";
        public const string SteamClientUiFriendSourceWav = "recording_highlight.wav";

        public static bool IsGoldbergOverlayNotificationSoundFileName(string fileName)
        {
            return string.Equals(fileName, SteamClientUiAchievementNotificationWav, StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileName, SteamClientUiFriendNotificationWav, StringComparison.OrdinalIgnoreCase);
        }

        public const string LauncherResourcesFolderName = "Resources";
        public const string LauncherImagesSubfolderName = "Images";
        public const string LauncherAchievementPlaceholderImageFileName = "achievement.png";

        // Second-instance URI handoff: pending request files under LocalAppDataPerUserDirectory.
        public const string LauncherUriProtocolPendingFilePrefix = "uri_";
        public const string LauncherUriProtocolPendingFileExtension = ".txt";
        public const string LauncherUriProtocolPendingFileSearchPattern = LauncherUriProtocolPendingFilePrefix + "*.txt";

        // Detached launch-cleanup watcher: per-AppId session manifests under LocalAppDataPerUserDirectory.
        public const string LaunchSessionManifestFolderName = "launch_sessions";

        public const string LaunchSessionManifestFileExtension = ".json";

        public const string GoldbergSteamSettingsModsFolderName = "mods";

        // Goldberg Steam HTTP request cache under steam_settings.
        public const string GoldbergSteamSettingsHttpFolderName = "http";

        // Legacy plaintext API key file; read only to migrate the key into the registry.
        public const string LegacyApiKeyFileName = "steam_apikey.txt";

        // Goldberg INI files under steam_settings or global GSE settings (emulator contract).
        public const string GoldbergOverlayIniFileName = "configs.overlay.ini";
        public const string GoldbergMainIniFileName = "configs.main.ini";
        public const string GoldbergAppIniFileName = "configs.app.ini";
        public const string GoldbergUserIniFileName = "configs.user.ini";

        public const string GoldbergLeaderboardsFileName = "leaderboards.txt";
        public const string GoldbergStatsJsonFileName = "stats.json";
        public const string GoldbergStatsDbJsonFileName = "stats_db.json";
        public const string GoldbergAchievementsDbJsonFileName = "achievements_db.json";
        public const string GoldbergInventoryDbJsonFileName = "inventory_db.json";

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

        // Launcher extension, not a Goldberg file: per-game custom launch options next to steam_settings.
        public const string LauncherUserLaunchOptionsIniFileName = "user.launch.options.ini";

        // Goldberg fork release DLL names under goldberg\.
        public const string GoldbergSteamClientDll32 = "steamclient.dll";
        public const string GoldbergSteamClientDll64 = "steamclient64.dll";
        public const string GoldbergGameOverlayRendererDll32 = "GameOverlayRenderer.dll";
        public const string GoldbergGameOverlayRendererDll64 = "GameOverlayRenderer64.dll";
        public const string GoldbergStandardSteamApiDll32 = "steam_api.dll";
        public const string GoldbergStandardSteamApiDll64 = "steam_api64.dll";
        public const string GoldbergSteamDllFileName = "Steam.dll";

        // Unmodified Steam client Steam.dll kept beside the Goldberg build for a manual swap if the patched copy fails.
        public const string GoldbergSteamOriginalDllFileName = "steam_o.dll";

        public static string GoldbergExperimentalDirectory =>
            Path.Combine(GoldbergDirectory, GoldbergInstallLayout.ExperimentalFolderName);

        public static string GoldbergSteamClientExperimentalDirectory =>
            Path.Combine(GoldbergDirectory, GoldbergInstallLayout.SteamClientExperimentalFolderName);

        public static string GoldbergSteamOldDirectory =>
            Path.Combine(GoldbergDirectory, GoldbergInstallLayout.SteamOldFolderName);

        public static string GoldbergSteamClientExtraDllsDirectory =>
            Path.Combine(
                GoldbergSteamClientExperimentalDirectory,
                GoldbergInstallLayout.SteamClientExperimentalExtraDllsFolderName);

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

        // Per-game extra-DLL folder under steam_settings; Goldberg loads each DLL with LoadLibraryW.
        public const string GoldbergLoadDllsFolderName = "load_dlls";

        public static string CombineGameSteamSettingsLoadDllsDirectory(ulong appId) =>
            Path.Combine(GetGameSteamSettingsPath(appId), GoldbergLoadDllsFolderName);

        public static string CombineGameSteamSettingsSoundsDirectory(ulong appId) =>
            Path.Combine(GetGameSteamSettingsPath(appId), GoldbergGlobalSoundsFolderName);

        public static string CombineSteamSettingsLoadDllsDirectory(string steamSettingsDirectory) =>
            Path.Combine(steamSettingsDirectory, GoldbergLoadDllsFolderName);

        public const string SteamAppsDirectoryName = "steamapps";

        public const string SteamLibraryFoldersVdfFileName = "libraryfolders.vdf";
        // Quoted key inside libraryfolders.vdf for each library root path.
        public const string SteamLibraryFoldersVdfPathKey = "path";

        public const string SteamProductInfoValveKeyValuesFileExtension = ".vdf";
        public const string SteamProductInfoCatalogJsonFileExtension = ".json";
        public const string SteamApiRedistributableDllSearchPattern = "steam_api*.dll";

        // Backup sidecar beside steam_api / steam_api64 (our .bkp-style copy before swap or Goldberg deploy).
        public const string SteamApiBackupSidecarExtension = ".sge";

        public const string SteamApiDllDeploymentLegacyBackupExtension = ".sge.bak";

        public const string SteamAppManifestFilePrefix = "appmanifest_";
        public const string SteamAppManifestFileExtension = ".acf";

        public const string SteamAppsCommonDirectoryName = "common";
        public const string SteamClientSteamUiFolderName = "steamui";
        public const string SteamClientUiSoundsFolderName = GoldbergGlobalSoundsFolderName;
        public const string SteamClientClientUiFolderName = "clientui";
        public const string SteamClientClientUiImagesFolderName = "images";

        // Hashed Steam clientui image cached under %LocalAppData%\SmartGoldbergEmu\ (self-heal: Steam, then CDN).
        public const string SteamClientUiHashedImageFileName = "8669e97b288da32670e77181618c3dfb.png";

        public const string SteamClientRelativeRootFolderName = "Steam";
        public const string SteamClientExecutableFileName = "steam.exe";

        public const string SteamUserDataFolderName = "userdata";

        public static string CombineSteamUserDataAccountPath(string steamInstallationRoot, string steam3AccountId)
        {
            if (string.IsNullOrWhiteSpace(steamInstallationRoot) || string.IsNullOrWhiteSpace(steam3AccountId))
                return null;
            string root = steamInstallationRoot.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (root.Length == 0)
                return null;
            return Path.Combine(root, SteamUserDataFolderName, steam3AccountId.Trim());
        }

        public static string CombineSteamUserDataGamePath(string steamInstallationRoot, string steam3AccountId, ulong appId)
        {
            string accountPath = CombineSteamUserDataAccountPath(steamInstallationRoot, steam3AccountId);
            if (string.IsNullOrEmpty(accountPath) || appId == 0)
                return null;
            return Path.Combine(accountPath, appId.ToString());
        }

        public static string CombineSteamClientUiSoundsPath(string steamInstallationRoot)
        {
            if (string.IsNullOrWhiteSpace(steamInstallationRoot))
                return null;
            string root = steamInstallationRoot.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (root.Length == 0)
                return null;
            return Path.Combine(root, SteamClientSteamUiFolderName, SteamClientUiSoundsFolderName);
        }

        public static string CombineSteamClientUiImagesPath(string steamInstallationRoot)
        {
            if (string.IsNullOrWhiteSpace(steamInstallationRoot))
                return null;
            string root = steamInstallationRoot.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (root.Length == 0)
                return null;
            return Path.Combine(root, SteamClientClientUiFolderName, SteamClientClientUiImagesFolderName);
        }

        public static string LocalAppDataSteamClientUiHashedImagePath =>
            Path.Combine(LocalAppDataPerUserDirectory, SteamClientUiHashedImageFileName);

        // Fallback when the registry does not yield a Steam install path.
        public static string GetProgramFilesX86DefaultSteamInstallationRoot()
        {
            string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (string.IsNullOrWhiteSpace(pf86))
                return null;
            return Path.Combine(pf86, SteamClientRelativeRootFolderName);
        }

        public const string GamesIniFileName = "games.ini";

        public static string CombineGamesIniPath(string gamesDirectoryRoot)
        {
            return Path.Combine(gamesDirectoryRoot, GamesIniFileName);
        }

        public static string CombineGameFolder(string gamesDirectoryRoot, string appIdFolderName)
        {
            return Path.Combine(gamesDirectoryRoot, appIdFolderName);
        }

        public static string CombineGamesPerAppResourcesDirectory(string gamesDirectoryRoot, string appIdFolderName)
        {
            return Path.Combine(CombineGameFolder(gamesDirectoryRoot, appIdFolderName), GamesPerAppResourcesFolderName);
        }

        // resources\{appId}.vdf: PICS Valve-text export; read fallback when the catalog JSON is missing.
        public static string CombineGamesPerAppValveDataFilePath(string gamesDirectoryRoot, string appIdFolderName)
        {
            return Path.Combine(
                CombineGamesPerAppResourcesDirectory(gamesDirectoryRoot, appIdFolderName),
                appIdFolderName + SteamProductInfoValveKeyValuesFileExtension);
        }

        // resources\{appId}.json: catalog snapshot (source of truth).
        public static string CombineGamesPerAppCatalogJsonFilePath(string gamesDirectoryRoot, string appIdFolderName)
        {
            return Path.Combine(
                CombineGamesPerAppResourcesDirectory(gamesDirectoryRoot, appIdFolderName),
                appIdFolderName + SteamProductInfoCatalogJsonFileExtension);
        }

        public static string GetSteamGameResourcesClientIconFileName(ulong appId)
        {
            return appId.ToString() + SteamGameResourcesClientIconFileExtension;
        }

        public static string CombineGameSteamSettingsDirectory(string gamesDirectoryRoot, string appIdFolderName)
        {
            return Path.Combine(CombineGameFolder(gamesDirectoryRoot, appIdFolderName), SteamSettingsFolderName);
        }

        // Under %LocalAppData%: IPC files, UI settings, and legacy config cleanup.
        public const string LauncherPerUserFolderName = "SmartGoldbergEmu";

        public const string LauncherUpdateTempFolderName = "temp";

        // Must match the release archive layout.
        public const string LauncherUpdateUserAssetsUnpackFolderName = "userassets";

        public const string LauncherUpdateWorkFolderName = "launcher-update";

        // Extracted from embedded resources into LauncherUpdateWorkDirectory.
        public const string LauncherUpdateEmbeddedUpdaterFileName = "SmartGoldbergEmu.LauncherUpdate.exe";

        // Written before spawning the embedded updater.
        public const string LauncherUpdateApplyManifestFileName = "apply-manifest.json";

        public static string LauncherUpdateWorkDirectory =>
            Path.Combine(AppBaseDirectory, LauncherUpdateTempFolderName, LauncherUpdateWorkFolderName);

        public const string LauncherBackupTempFolderName = "SmartGoldbergEmu_Backup";

        public static string AppBaseDirectory => AppDomain.CurrentDomain.BaseDirectory;

        // Preferred over AppBaseDirectory for optional bundled tools.
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

        public static string GamesDirectory => Path.Combine(AppBaseDirectory, GamesDirectoryFolderName);

        public static string BuildStubOriginalBackupPath(string executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath))
                return null;

            string directory = Path.GetDirectoryName(executablePath);
            string fileName = Path.GetFileName(executablePath);
            if (string.IsNullOrEmpty(fileName))
                return null;

            string extension = Path.GetExtension(fileName);
            string baseName = Path.GetFileNameWithoutExtension(fileName);
            string backupFileName = baseName + StubOriginalExecutableBackupInfix + extension;

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

        public static string GamesIniPath => CombineGamesIniPath(GamesDirectory);

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

        public static string ConfigFilePath => Path.Combine(AppBaseDirectory, ConfigFileName);

        // Former install-folder config path; 2.x XML import and INI migration only.
        public static string LegacyExeConfigFilePath =>
            Path.Combine(AppBaseDirectory, LegacyConfigFileName);

        public static string LocalAppDataPerUserDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            LauncherPerUserFolderName);

        // Per-user UI preferences (theme, main window size/position/state).
        public const string UiSettingsIniFileName = "ui_settings.ini";

        public const string SteamStaticCdnPreferencesFileName = SteamStaticCdnConstants.PreferencesCacheFileName;

        public static string UiSettingsFilePath =>
            Path.Combine(LocalAppDataPerUserDirectory, UiSettingsIniFileName);

        public static string SteamStaticCdnPreferencesFilePath =>
            Path.Combine(LocalAppDataPerUserDirectory, SteamStaticCdnPreferencesFileName);

        // Former per-user config path; one-time migration and legacy XML import lookup only.
        public static string LegacyLocalAppDataConfigFilePath =>
            Path.Combine(LocalAppDataPerUserDirectory, LegacyConfigFileName);

        // Subfolders: experimental, steamclient_experimental (extra_dlls for load_dlls staging), steam_old.
        public static string GoldbergDirectory => Path.Combine(AppBaseDirectory, GoldbergDirectoryFolderName);

        public static string LaunchSessionManifestDirectory =>
            Path.Combine(LocalAppDataPerUserDirectory, LaunchSessionManifestFolderName);

        public static string CombineLaunchSessionManifestPath(ulong appId) =>
            Path.Combine(LaunchSessionManifestDirectory, appId.ToString() + LaunchSessionManifestFileExtension);

        public static string LauncherBackupTempRootDirectory =>
            Path.Combine(Path.GetTempPath(), LauncherBackupTempFolderName);

        public static string LegacyApiKeyFilePath => Path.Combine(AppBaseDirectory, LegacyApiKeyFileName);

        public static string GetGameFolder(ulong appId)
        {
            return CombineGameFolder(GamesDirectory, appId.ToString());
        }

        public static string GetGameSteamSettingsPath(ulong appId)
        {
            return CombineGameSteamSettingsDirectory(GamesDirectory, appId.ToString());
        }

        public static string GlobalFontsPath => Path.Combine(GlobalSettingsPath, GoldbergGlobalFontsFolderName);

        public static string GlobalSoundsPath => Path.Combine(GlobalSettingsPath, GoldbergGlobalSoundsFolderName);

        public static string GlobalControllerGlyphsPath => Path.Combine(
            GlobalSettingsPath,
            GoldbergGlobalControllerFolderName,
            GoldbergGlobalGlyphsFolderName);

        public static string GlobalAccountAvatarPath => Path.Combine(GlobalSettingsPath, GlobalAccountAvatarFileName);
    }
}
