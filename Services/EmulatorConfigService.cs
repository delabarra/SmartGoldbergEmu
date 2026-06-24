using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using SmartGoldbergEmu.JsonKit;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Generators;
using SmartGoldbergEmu.Extensions;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Validation;

namespace SmartGoldbergEmu.Services
{
    public class EmulatorConfigService
    {
        private const float FloatMergeEpsilon = 0.001f;

        private readonly string _gamesDirectory;
        private readonly string _globalSettingsPath;
        private readonly GoldbergCfgService _goldbergCfgService;
        private readonly SteamApiKeyService _steamApiKeyService;

        public EmulatorConfigService()
            : this(PathConstants.GamesDirectory, PathConstants.GlobalSettingsPath, ServiceLocator.GoldbergCfgService)
        {
        }

        public EmulatorConfigService(
            string gamesDirectory,
            string globalSettingsPath,
            GoldbergCfgService goldbergCfgService = null,
            SteamApiKeyService steamApiKeyService = null)
        {
            _gamesDirectory = gamesDirectory;
            _globalSettingsPath = globalSettingsPath;
            _goldbergCfgService = goldbergCfgService ?? new GoldbergCfgService(globalSettingsPath);
            _steamApiKeyService = steamApiKeyService ?? ServiceLocator.SteamApiKeyService;
        }

        public GameSettingsSnapshot LoadGameSettingsSnapshot(ulong appId, bool mergePerGameSteamSettings = true)
        {
            var snapshot = new GameSettingsSnapshot { AppId = appId };

            try
            {
                var globalOverlay = _goldbergCfgService.LoadGlobalOverlaySettings();
                var globalMain = _goldbergCfgService.LoadGlobalMainSettings();
                var globalUser = _goldbergCfgService.LoadGlobalUserSettings();

                snapshot.Overlay = globalOverlay;
                snapshot.Main = globalMain;
                snapshot.User = globalUser;
                snapshot.App = new AppSettings();

                if (mergePerGameSteamSettings)
                {
                    var steamSettingsPath = GetGameSteamSettingsPath(appId);
                    if (Directory.Exists(steamSettingsPath))
                    {
                        snapshot.Overlay = MergeOverlaySettings(globalOverlay, LoadOverlaySettings(Path.Combine(steamSettingsPath, PathConstants.GoldbergOverlayIniFileName)));
                        snapshot.Main = MergeMainSettings(globalMain, LoadMainSettings(Path.Combine(steamSettingsPath, PathConstants.GoldbergMainIniFileName)));
                        snapshot.App = LoadAppSettings(Path.Combine(steamSettingsPath, PathConstants.GoldbergAppIniFileName));
                        snapshot.User = MergeUserSettings(globalUser, LoadUserSettings(Path.Combine(steamSettingsPath, PathConstants.GoldbergUserIniFileName)));
                    }
                }

                snapshot.CreatedAt = DateTime.Now;
            }
            catch (Exception ex)
            {
                LogRedactionHelper.WriteDebug($"Failed to load game settings snapshot: {ex.Message}");
            }

            return snapshot;
        }

        public SaveResult SavePerGameTicketAndAltSteamId(ulong appId, string ticket, string altSteamId)
        {
            if (appId == 0)
                return SaveResult.Failure("App ID cannot be zero.");

            try
            {
                string steamSettingsPath = GetGameSteamSettingsPath(appId);
                Directory.CreateDirectory(steamSettingsPath);
                string userPath = Path.Combine(steamSettingsPath, PathConstants.GoldbergUserIniFileName);
                UserSettings user = File.Exists(userPath)
                    ? LoadUserSettings(userPath)
                    : new UserSettings();
                user.Ticket = ticket ?? string.Empty;
                user.AltSteamId = altSteamId ?? string.Empty;
                return SaveUserSettings(userPath, user);
            }
            catch (Exception ex)
            {
                return SaveResult.Failure($"Failed to save per-game ticket settings: {ex.Message}");
            }
        }

        public SaveResult SaveConfigsAppIni(ulong appId, AppSettings appSettings)
        {
            if (appSettings == null)
                return SaveResult.Failure("App settings cannot be null");
            try
            {
                string steamSettingsPath = GetGameSteamSettingsPath(appId);
                Directory.CreateDirectory(steamSettingsPath);
                var appPath = Path.Combine(steamSettingsPath, PathConstants.GoldbergAppIniFileName);
                return SaveAppSettings(appPath, appSettings);
            }
            catch (Exception ex)
            {
                return SaveResult.Failure($"Failed to save {PathConstants.GoldbergAppIniFileName}: {ex.Message}");
            }
        }

        private static bool MergeFlag(bool game, bool global, bool defaultVal) => game != defaultVal ? game : global;

        private static bool PerGameOnlyBool(bool game, bool defaultVal) => game != defaultVal ? game : defaultVal;

        private static int PerGameOnlyInt(int game, int defaultVal) => game != defaultVal ? game : defaultVal;

        private static int MergeScalar(int game, int global, int defaultVal) => game != defaultVal ? game : global;

        private static float MergeScalar(float game, float global, float defaultVal) =>
            Math.Abs(game - defaultVal) > FloatMergeEpsilon ? game : global;

        private static string MergeStringOrGlobal(string game, string global, string defaultVal) =>
            !string.IsNullOrEmpty(game) && game != defaultVal ? game : global;

        private static string MergeStringGameOnly(string game, string defaultVal) =>
            !string.IsNullOrEmpty(game) && game != defaultVal ? game : string.Empty;

        private OverlaySettings MergeOverlaySettings(OverlaySettings global, OverlaySettings gameSpecific)
        {
            var m = new OverlaySettings();
            var d = new OverlaySettings();

            m.EnableExperimentalOverlay = MergeFlag(gameSpecific.EnableExperimentalOverlay, global.EnableExperimentalOverlay, d.EnableExperimentalOverlay);
            m.HookDelaySec = MergeScalar(gameSpecific.HookDelaySec, global.HookDelaySec, d.HookDelaySec);
            m.RendererDetectorTimeoutSec = MergeScalar(gameSpecific.RendererDetectorTimeoutSec, global.RendererDetectorTimeoutSec, d.RendererDetectorTimeoutSec);
            m.DisableAchievementNotification = MergeFlag(gameSpecific.DisableAchievementNotification, global.DisableAchievementNotification, d.DisableAchievementNotification);
            m.DisableFriendNotification = MergeFlag(gameSpecific.DisableFriendNotification, global.DisableFriendNotification, d.DisableFriendNotification);
            m.DisableAchievementProgress = MergeFlag(gameSpecific.DisableAchievementProgress, global.DisableAchievementProgress, d.DisableAchievementProgress);
            m.DisableWarningAny = MergeFlag(gameSpecific.DisableWarningAny, global.DisableWarningAny, d.DisableWarningAny);
            m.DisableWarningBadAppId = MergeFlag(gameSpecific.DisableWarningBadAppId, global.DisableWarningBadAppId, d.DisableWarningBadAppId);
            m.DisableWarningLocalSave = MergeFlag(gameSpecific.DisableWarningLocalSave, global.DisableWarningLocalSave, d.DisableWarningLocalSave);
            m.UploadAchievementsIconsToGpu = MergeFlag(gameSpecific.UploadAchievementsIconsToGpu, global.UploadAchievementsIconsToGpu, d.UploadAchievementsIconsToGpu);
            m.FpsAveragingWindow = MergeScalar(gameSpecific.FpsAveragingWindow, global.FpsAveragingWindow, d.FpsAveragingWindow);
            m.OverlayAlwaysShowUserInfo = MergeFlag(gameSpecific.OverlayAlwaysShowUserInfo, global.OverlayAlwaysShowUserInfo, d.OverlayAlwaysShowUserInfo);
            m.OverlayAlwaysShowFps = MergeFlag(gameSpecific.OverlayAlwaysShowFps, global.OverlayAlwaysShowFps, d.OverlayAlwaysShowFps);
            m.OverlayAlwaysShowFrametime = MergeFlag(gameSpecific.OverlayAlwaysShowFrametime, global.OverlayAlwaysShowFrametime, d.OverlayAlwaysShowFrametime);
            m.OverlayAlwaysShowPlaytime = MergeFlag(gameSpecific.OverlayAlwaysShowPlaytime, global.OverlayAlwaysShowPlaytime, d.OverlayAlwaysShowPlaytime);

            m.FontOverride = MergeStringOrGlobal(gameSpecific.FontOverride, global.FontOverride, d.FontOverride);
            m.FontSize = MergeScalar(gameSpecific.FontSize, global.FontSize, d.FontSize);
            m.IconSize = MergeScalar(gameSpecific.IconSize, global.IconSize, d.IconSize);
            m.FontGlyphExtraSpacingX = MergeScalar(gameSpecific.FontGlyphExtraSpacingX, global.FontGlyphExtraSpacingX, d.FontGlyphExtraSpacingX);
            m.FontGlyphExtraSpacingY = MergeScalar(gameSpecific.FontGlyphExtraSpacingY, global.FontGlyphExtraSpacingY, d.FontGlyphExtraSpacingY);

            m.NotificationR = MergeScalar(gameSpecific.NotificationR, global.NotificationR, d.NotificationR);
            m.NotificationG = MergeScalar(gameSpecific.NotificationG, global.NotificationG, d.NotificationG);
            m.NotificationB = MergeScalar(gameSpecific.NotificationB, global.NotificationB, d.NotificationB);
            m.NotificationA = MergeScalar(gameSpecific.NotificationA, global.NotificationA, d.NotificationA);
            m.BackgroundR = MergeScalar(gameSpecific.BackgroundR, global.BackgroundR, d.BackgroundR);
            m.BackgroundG = MergeScalar(gameSpecific.BackgroundG, global.BackgroundG, d.BackgroundG);
            m.BackgroundB = MergeScalar(gameSpecific.BackgroundB, global.BackgroundB, d.BackgroundB);
            m.BackgroundA = MergeScalar(gameSpecific.BackgroundA, global.BackgroundA, d.BackgroundA);
            m.ElementR = MergeScalar(gameSpecific.ElementR, global.ElementR, d.ElementR);
            m.ElementG = MergeScalar(gameSpecific.ElementG, global.ElementG, d.ElementG);
            m.ElementB = MergeScalar(gameSpecific.ElementB, global.ElementB, d.ElementB);
            m.ElementA = MergeScalar(gameSpecific.ElementA, global.ElementA, d.ElementA);
            m.ElementHoveredR = MergeScalar(gameSpecific.ElementHoveredR, global.ElementHoveredR, d.ElementHoveredR);
            m.ElementHoveredG = MergeScalar(gameSpecific.ElementHoveredG, global.ElementHoveredG, d.ElementHoveredG);
            m.ElementHoveredB = MergeScalar(gameSpecific.ElementHoveredB, global.ElementHoveredB, d.ElementHoveredB);
            m.ElementHoveredA = MergeScalar(gameSpecific.ElementHoveredA, global.ElementHoveredA, d.ElementHoveredA);
            m.ElementActiveR = MergeScalar(gameSpecific.ElementActiveR, global.ElementActiveR, d.ElementActiveR);
            m.ElementActiveG = MergeScalar(gameSpecific.ElementActiveG, global.ElementActiveG, d.ElementActiveG);
            m.ElementActiveB = MergeScalar(gameSpecific.ElementActiveB, global.ElementActiveB, d.ElementActiveB);
            m.ElementActiveA = MergeScalar(gameSpecific.ElementActiveA, global.ElementActiveA, d.ElementActiveA);
            m.StatsBackgroundR = MergeScalar(gameSpecific.StatsBackgroundR, global.StatsBackgroundR, d.StatsBackgroundR);
            m.StatsBackgroundG = MergeScalar(gameSpecific.StatsBackgroundG, global.StatsBackgroundG, d.StatsBackgroundG);
            m.StatsBackgroundB = MergeScalar(gameSpecific.StatsBackgroundB, global.StatsBackgroundB, d.StatsBackgroundB);
            m.StatsBackgroundA = MergeScalar(gameSpecific.StatsBackgroundA, global.StatsBackgroundA, d.StatsBackgroundA);
            m.StatsTextR = MergeScalar(gameSpecific.StatsTextR, global.StatsTextR, d.StatsTextR);
            m.StatsTextG = MergeScalar(gameSpecific.StatsTextG, global.StatsTextG, d.StatsTextG);
            m.StatsTextB = MergeScalar(gameSpecific.StatsTextB, global.StatsTextB, d.StatsTextB);
            m.StatsTextA = MergeScalar(gameSpecific.StatsTextA, global.StatsTextA, d.StatsTextA);

            m.NotificationRounding = MergeScalar(gameSpecific.NotificationRounding, global.NotificationRounding, d.NotificationRounding);
            m.NotificationMarginX = MergeScalar(gameSpecific.NotificationMarginX, global.NotificationMarginX, d.NotificationMarginX);
            m.NotificationMarginY = MergeScalar(gameSpecific.NotificationMarginY, global.NotificationMarginY, d.NotificationMarginY);

            m.NotificationAnimation = MergeScalar(gameSpecific.NotificationAnimation, global.NotificationAnimation, d.NotificationAnimation);
            m.NotificationDurationProgress = MergeScalar(gameSpecific.NotificationDurationProgress, global.NotificationDurationProgress, d.NotificationDurationProgress);
            m.NotificationDurationAchievement = MergeScalar(gameSpecific.NotificationDurationAchievement, global.NotificationDurationAchievement, d.NotificationDurationAchievement);
            m.NotificationDurationInvitation = MergeScalar(gameSpecific.NotificationDurationInvitation, global.NotificationDurationInvitation, d.NotificationDurationInvitation);
            m.NotificationDurationChat = MergeScalar(gameSpecific.NotificationDurationChat, global.NotificationDurationChat, d.NotificationDurationChat);

            m.AchievementUnlockDatetimeFormat = MergeStringOrGlobal(gameSpecific.AchievementUnlockDatetimeFormat, global.AchievementUnlockDatetimeFormat, d.AchievementUnlockDatetimeFormat);

            m.PosAchievement = MergeStringOrGlobal(gameSpecific.PosAchievement, global.PosAchievement, d.PosAchievement);
            m.PosInvitation = MergeStringOrGlobal(gameSpecific.PosInvitation, global.PosInvitation, d.PosInvitation);
            m.PosChatMsg = MergeStringOrGlobal(gameSpecific.PosChatMsg, global.PosChatMsg, d.PosChatMsg);

            m.StatsPosX = MergeScalar(gameSpecific.StatsPosX, global.StatsPosX, d.StatsPosX);
            m.StatsPosY = MergeScalar(gameSpecific.StatsPosY, global.StatsPosY, d.StatsPosY);

            m.FontOverrideAchievementTitle = MergeStringOrGlobal(gameSpecific.FontOverrideAchievementTitle, global.FontOverrideAchievementTitle, d.FontOverrideAchievementTitle);
            m.FontOverrideAchievementDescription = MergeStringOrGlobal(gameSpecific.FontOverrideAchievementDescription, global.FontOverrideAchievementDescription, d.FontOverrideAchievementDescription);
            m.FontSizeFps = MergeScalar(gameSpecific.FontSizeFps, global.FontSizeFps, d.FontSizeFps);
            m.FontSizeAchievementTitle = MergeScalar(gameSpecific.FontSizeAchievementTitle, global.FontSizeAchievementTitle, d.FontSizeAchievementTitle);
            m.FontSizeAchievementDescription = MergeScalar(gameSpecific.FontSizeAchievementDescription, global.FontSizeAchievementDescription, d.FontSizeAchievementDescription);
            m.FontAchievementTitleBold = MergeFlag(gameSpecific.FontAchievementTitleBold, global.FontAchievementTitleBold, d.FontAchievementTitleBold);

            return m;
        }

        private MainSettings MergeMainSettings(MainSettings global, MainSettings gameSpecific)
        {
            var m = new MainSettings();
            var d = new MainSettings();

            m.NewAppTicket = MergeFlag(gameSpecific.NewAppTicket, global.NewAppTicket, d.NewAppTicket);
            m.GcToken = MergeFlag(gameSpecific.GcToken, global.GcToken, d.GcToken);
            m.SteamDeck = MergeFlag(gameSpecific.SteamDeck, global.SteamDeck, d.SteamDeck);
            m.EnableAccountAvatar = MergeFlag(gameSpecific.EnableAccountAvatar, global.EnableAccountAvatar, d.EnableAccountAvatar);
            m.EnableVoiceChat = MergeFlag(gameSpecific.EnableVoiceChat, global.EnableVoiceChat, d.EnableVoiceChat);

            m.DisableLeaderboardsCreateUnknown = MergeFlag(gameSpecific.DisableLeaderboardsCreateUnknown, global.DisableLeaderboardsCreateUnknown, d.DisableLeaderboardsCreateUnknown);
            m.AllowUnknownStats = MergeFlag(gameSpecific.AllowUnknownStats, global.AllowUnknownStats, d.AllowUnknownStats);
            m.StatAchievementProgressFunctionality = MergeFlag(gameSpecific.StatAchievementProgressFunctionality, global.StatAchievementProgressFunctionality, d.StatAchievementProgressFunctionality);
            m.SaveOnlyHigherStatAchievementProgress = MergeFlag(gameSpecific.SaveOnlyHigherStatAchievementProgress, global.SaveOnlyHigherStatAchievementProgress, d.SaveOnlyHigherStatAchievementProgress);
            m.PaginatedAchievementsIcons = MergeScalar(gameSpecific.PaginatedAchievementsIcons, global.PaginatedAchievementsIcons, d.PaginatedAchievementsIcons);
            m.RecordPlaytime = MergeFlag(gameSpecific.RecordPlaytime, global.RecordPlaytime, d.RecordPlaytime);

            m.BlockUnknownClients = PerGameOnlyBool(gameSpecific.BlockUnknownClients, d.BlockUnknownClients);
            m.ImmediateGameserverStats = PerGameOnlyBool(gameSpecific.ImmediateGameserverStats, d.ImmediateGameserverStats);
            m.MatchmakingServerListActualType = PerGameOnlyBool(gameSpecific.MatchmakingServerListActualType, d.MatchmakingServerListActualType);
            m.MatchmakingServerDetailsViaSourceQuery = PerGameOnlyBool(gameSpecific.MatchmakingServerDetailsViaSourceQuery, d.MatchmakingServerDetailsViaSourceQuery);
            m.DisableLanOnly = PerGameOnlyBool(gameSpecific.DisableLanOnly, d.DisableLanOnly);
            m.DisableNetworking = PerGameOnlyBool(gameSpecific.DisableNetworking, d.DisableNetworking);
            m.ListenPort = PerGameOnlyInt(gameSpecific.ListenPort, d.ListenPort);
            m.Offline = PerGameOnlyBool(gameSpecific.Offline, d.Offline);
            m.DisableSharingStatsWithGameserver = PerGameOnlyBool(gameSpecific.DisableSharingStatsWithGameserver, d.DisableSharingStatsWithGameserver);
            m.DisableSourceQuery = PerGameOnlyBool(gameSpecific.DisableSourceQuery, d.DisableSourceQuery);
            m.ShareLeaderboardsOverNetwork = PerGameOnlyBool(gameSpecific.ShareLeaderboardsOverNetwork, d.ShareLeaderboardsOverNetwork);
            m.DisableLobbyCreation = PerGameOnlyBool(gameSpecific.DisableLobbyCreation, d.DisableLobbyCreation);
            m.DownloadSteamhttpRequests = PerGameOnlyBool(gameSpecific.DownloadSteamhttpRequests, d.DownloadSteamhttpRequests);
            m.OldP2PPacketSharingMode = PerGameOnlyInt(gameSpecific.OldP2PPacketSharingMode, d.OldP2PPacketSharingMode);

            m.AchievementsBypass = MergeFlag(gameSpecific.AchievementsBypass, global.AchievementsBypass, d.AchievementsBypass);
            m.ForceSteamhttpSuccess = MergeFlag(gameSpecific.ForceSteamhttpSuccess, global.ForceSteamhttpSuccess, d.ForceSteamhttpSuccess);
            m.DisableSteamoverlaygameidEnvVar = MergeFlag(gameSpecific.DisableSteamoverlaygameidEnvVar, global.DisableSteamoverlaygameidEnvVar, d.DisableSteamoverlaygameidEnvVar);
            m.EnableSteamPreownedIds = MergeFlag(gameSpecific.EnableSteamPreownedIds, global.EnableSteamPreownedIds, d.EnableSteamPreownedIds);
            m.SteamGameStatsReportsDir = MergeStringOrGlobal(gameSpecific.SteamGameStatsReportsDir, global.SteamGameStatsReportsDir, d.SteamGameStatsReportsDir);
            m.FreeWeekend = MergeFlag(gameSpecific.FreeWeekend, global.FreeWeekend, d.FreeWeekend);
            m.Use32BitInventoryItemIds = MergeFlag(gameSpecific.Use32BitInventoryItemIds, global.Use32BitInventoryItemIds, d.Use32BitInventoryItemIds);

            return m;
        }

        private UserSettings MergeUserSettings(UserSettings global, UserSettings gameSpecific)
        {
            var m = new UserSettings();
            var d = new UserSettings();

            m.AccountName = MergeStringGameOnly(gameSpecific.AccountName, d.AccountName);
            m.AccountSteamId = MergeStringGameOnly(gameSpecific.AccountSteamId, d.AccountSteamId);
            m.ClanTag = MergeStringGameOnly(gameSpecific.ClanTag, d.ClanTag);

            // Ticket/alt are per-game (+ registry backup); never inherit from global user INI for game settings UI.
            m.Ticket = MergeStringGameOnly(gameSpecific.Ticket, d.Ticket);
            m.AltSteamId = MergeStringGameOnly(gameSpecific.AltSteamId, d.AltSteamId);
            m.AltSteamIdCount = MergeScalar(gameSpecific.AltSteamIdCount, global.AltSteamIdCount, d.AltSteamIdCount);
            m.Language = MergeStringOrGlobal(gameSpecific.Language, global.Language, d.Language);
            m.IpCountry = MergeStringOrGlobal(gameSpecific.IpCountry, global.IpCountry, d.IpCountry);

            m.LocalSavePath = string.IsNullOrEmpty(global.LocalSavePath) ? d.LocalSavePath : global.LocalSavePath;
            m.SavesFolderName = string.IsNullOrEmpty(global.SavesFolderName) ? d.SavesFolderName : global.SavesFolderName;

            return m;
        }

        private void PersistGameUserIni(string steamSettingsPath, UserSettings user, List<SaveResult> results)
        {
            if (HasUserSettingsOverrides(user, _goldbergCfgService.LoadGlobalUserSettings()))
                results.Add(SaveUserSettings(Path.Combine(steamSettingsPath, PathConstants.GoldbergUserIniFileName), user));
            else
                TryDeleteGameUserOverrideIni(steamSettingsPath);
        }

        private static void TryDeleteGameUserOverrideIni(string steamSettingsPath)
        {
            var path = Path.Combine(steamSettingsPath, PathConstants.GoldbergUserIniFileName);
            if (!File.Exists(path))
                return;
            try
            {
                File.Delete(path);
            }
            catch (Exception ex)
            {
                LogRedactionHelper.WriteDebug($"Failed to delete game-specific user settings: {ex.Message}");
            }
        }

        private static SaveResult CompleteSaveResults(List<SaveResult> results)
        {
            var ok = results.Count(r => r.IsSuccess);
            var fail = results.Count(r => !r.IsSuccess);
            if (fail > 0)
            {
                var errors = string.Join("; ", results.Where(r => !r.IsSuccess).Select(r => r.ErrorMessage));
                return SaveResult.Failure($"Some settings failed to save: {errors}", ok, fail);
            }
            return SaveResult.Success(ok);
        }

        public SaveResult SaveAllGameSettings(ulong appId, GameSettingsSnapshot snapshot)
        {
            if (snapshot == null)
                return SaveResult.Failure("Snapshot cannot be null");

            var results = new List<SaveResult>();
            var steamSettingsPath = GetGameSteamSettingsPath(appId);

            try
            {
                Directory.CreateDirectory(steamSettingsPath);

                string mainPath = Path.Combine(steamSettingsPath, PathConstants.GoldbergMainIniFileName);
                results.Add(SaveGameNetworkMainSettings(mainPath, snapshot.Main));
                results.Add(SaveGameStatsAchievementsMainSettings(mainPath, snapshot.Main));
                results.Add(SaveAppSettings(Path.Combine(steamSettingsPath, PathConstants.GoldbergAppIniFileName), snapshot.App));
                PersistGameUserIni(steamSettingsPath, snapshot.User, results);

                return CompleteSaveResults(results);
            }
            catch (Exception ex)
            {
                return SaveResult.Failure($"Failed to save game settings: {ex.Message}");
            }
        }

        public SaveResult SaveModifiedGameSettings(ulong appId, GameSettingsSnapshot snapshot)
        {
            if (snapshot == null)
                return SaveResult.Failure("Snapshot cannot be null");

            var results = new List<SaveResult>();
            var steamSettingsPath = GetGameSteamSettingsPath(appId);

            try
            {
                Directory.CreateDirectory(steamSettingsPath);

                if (HasOverlayChanges(snapshot))
                    results.Add(SaveOverlaySettings(Path.Combine(steamSettingsPath, PathConstants.GoldbergOverlayIniFileName), snapshot.Overlay));
                string mainPath = Path.Combine(steamSettingsPath, PathConstants.GoldbergMainIniFileName);
                if (HasNetworkMainChanges(snapshot))
                    results.Add(SaveGameNetworkMainSettings(mainPath, snapshot.Main));
                if (HasStatsAchievementsMainChanges(snapshot))
                    results.Add(SaveGameStatsAchievementsMainSettings(mainPath, snapshot.Main));
                if (HasAppChanges(snapshot))
                    results.Add(SaveAppSettings(Path.Combine(steamSettingsPath, PathConstants.GoldbergAppIniFileName), snapshot.App));

                PersistGameUserIni(steamSettingsPath, snapshot.User, results);

                return CompleteSaveResults(results);
            }
            catch (Exception ex)
            {
                return SaveResult.Failure($"Failed to save game settings: {ex.Message}");
            }
        }

        public bool HasUnsavedChanges(GameSettingsSnapshot snapshot)
        {
            if (snapshot == null) return false;

            var originalSnapshot = LoadGameSettingsSnapshot(snapshot.AppId);
            return HasOverlayChanges(snapshot, originalSnapshot) ||
                   HasNetworkMainChanges(snapshot, originalSnapshot) ||
                   HasStatsAchievementsMainChanges(snapshot, originalSnapshot) ||
                   HasAppChanges(snapshot, originalSnapshot) ||
                   HasUserChanges(snapshot, originalSnapshot);
        }

        public GlobalSettings LoadGlobalSettings()
        {
            var settings = new GlobalSettings();

            try
            {
                var globalConfigPath = Path.Combine(_globalSettingsPath, PathConstants.GoldbergMainIniFileName);
                if (File.Exists(globalConfigPath))
                {
                    var mainSettings = LoadMainSettings(globalConfigPath);
                    settings.SteamDeck = mainSettings.SteamDeck;
                    settings.EnableAccountAvatar = mainSettings.EnableAccountAvatar;
                }

                var userSettings = _goldbergCfgService.LoadGlobalUserSettings();
                settings.AccountName = userSettings.AccountName;
                settings.AccountSteamId = userSettings.AccountSteamId;
                settings.Language = userSettings.Language;
            }
            catch (Exception ex)
            {
                LogRedactionHelper.WriteDebug($"Failed to load global settings: {ex.Message}");
            }

            return settings;
        }

        public string GetLanguageForAchievements(ulong appId)
        {
            if (appId > 0)
            {
                var saved = LoadGameSettingsSnapshot(appId);
                if (!string.IsNullOrEmpty(saved?.User?.Language))
                    return saved.User.Language;
            }

            var global = LoadGlobalSettings();
            return !string.IsNullOrEmpty(global?.Language) ? global.Language : "english";
        }

        public bool CreateDefaultConfigFiles(ulong appId)
        {
            try
            {
                var steamSettingsPath = GetGameSteamSettingsPath(appId);
                Directory.CreateDirectory(steamSettingsPath);
                Directory.CreateDirectory(Path.Combine(steamSettingsPath, PathConstants.GoldbergSteamSettingsModsFolderName));

                var overlayPath = Path.Combine(steamSettingsPath, PathConstants.GoldbergOverlayIniFileName);
                if (!File.Exists(overlayPath))
                {
                    SaveOverlaySettings(overlayPath, new OverlaySettings());
                }

                var mainPath = Path.Combine(steamSettingsPath, PathConstants.GoldbergMainIniFileName);
                if (!File.Exists(mainPath))
                {
                    SaveMainSettings(mainPath, new MainSettings());
                }

                var appPath = Path.Combine(steamSettingsPath, PathConstants.GoldbergAppIniFileName);
                if (!File.Exists(appPath))
                {
                    SaveAppSettings(appPath, new AppSettings());
                }

                var userPath = Path.Combine(steamSettingsPath, PathConstants.GoldbergUserIniFileName);
                if (!File.Exists(userPath))
                {
                    SaveUserSettings(userPath, new UserSettings());
                }

                return true;
            }
            catch (Exception ex)
            {
                LogRedactionHelper.WriteDebug($"Failed to create default config files: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> GenerateMetadataFilesAsync(Models.GameConfig gameConfig, Models.OnlineAppData metadata = null, CancellationToken cancellationToken = default)
        {
            if (gameConfig == null || gameConfig.AppId == 0)
                return false;

            try
            {
                string steamSettingsPath = GetGameSteamSettingsPath(gameConfig.AppId);
                Directory.CreateDirectory(steamSettingsPath);

                bool anyFileGenerated = false;
                string installedAppIdsPath = Path.Combine(steamSettingsPath, PathConstants.GoldbergInstalledAppIdsFileName);
                var localInstall = TryLoadLocalInstallSnapshot(gameConfig);
                string currentBranch = localInstall.CurrentBranch ?? TryGetCurrentBranchFromLocalConfig(steamSettingsPath);

                var packageData = await TryLoadPackageDataAsync(gameConfig, cancellationToken).ConfigureAwait(false);
                var appData = await TryLoadAppInfoDataAsync(gameConfig, cancellationToken).ConfigureAwait(false);

                var installedAppIds = BuildInstalledAppIds(installedAppIdsPath, gameConfig, metadata, packageData, localInstall);

                WriteBranchesJsonIfAbsent(steamSettingsPath, gameConfig, packageData, currentBranch);
                if (await TryEnsureStatsJsonAsync(gameConfig, cancellationToken).ConfigureAwait(false))
                    anyFileGenerated = true;
                WriteAchievementsFromPicsIfAbsent(steamSettingsPath, gameConfig, appData?.Achievements, ref anyFileGenerated);
                TryFetchAchievementsOnlineIfMissing(steamSettingsPath, gameConfig);

                WriteSteamAppId(steamSettingsPath, gameConfig.AppId, ref anyFileGenerated);
                WriteSupportedLanguages(steamSettingsPath, gameConfig, metadata, ref anyFileGenerated);
                WriteInstalledAppIds(installedAppIdsPath, installedAppIds, ref anyFileGenerated);
                TryPopulateConfigsAppIni(gameConfig, metadata, ref anyFileGenerated);
                WriteDepotsIfAbsent(steamSettingsPath, gameConfig, localInstall, appData?.Depots, packageData?.Depots, ref anyFileGenerated);
                WriteLeaderboardsIfAbsent(steamSettingsPath, gameConfig, appData?.Leaderboards, ref anyFileGenerated);
                TryFetchLeaderboardsOnlineIfMissing(steamSettingsPath, gameConfig);
                WriteSteamInterfacesIfSourceAvailable(steamSettingsPath, gameConfig, ref anyFileGenerated);
                ServiceLocator.GoldbergArtifactService.TryGenerateItemsOnlineIfMissing(gameConfig);
                CopyDefaultItemsIfSourceAvailable(steamSettingsPath, gameConfig, ref anyFileGenerated);

                return anyFileGenerated;
            }
            catch (Exception ex)
            {
                LogRedactionHelper.WriteDebug($"Failed to generate metadata files: {ex.Message}");
                return false;
            }
        }

        private static void WriteSteamAppId(string steamSettingsPath, ulong appId, ref bool anyFileGenerated)
        {
            var steamAppIdPath = Path.Combine(steamSettingsPath, PathConstants.SteamAppIdFileName);
            File.WriteAllText(steamAppIdPath, appId.ToString());
            anyFileGenerated = true;
        }

        public ValidationResult TryEnsureSteamAppIdBesideExecutable(GameConfig gameConfig)
        {
            if (gameConfig == null || gameConfig.AppId == 0)
                return ValidationResult.Failure("Game has no App ID.");

            if (string.IsNullOrWhiteSpace(gameConfig.Path))
                return ValidationResult.Failure("Game has no executable path.");

            if (!GameFolderPathHelper.TryGetExecutableDirectory(gameConfig, out string folderPath))
                return ValidationResult.Failure("Could not resolve the executable folder.");

            if (!PathValidationHelper.IsSafeFilePath(folderPath))
                return ValidationResult.Failure("Invalid folder path detected.");

            if (!Directory.Exists(folderPath))
                return ValidationResult.Failure($"The executable folder does not exist:\n{folderPath}");

            string appIdFilePath = Path.Combine(folderPath, PathConstants.SteamAppIdFileName);
            if (!PathValidationHelper.IsSafeFilePath(appIdFilePath))
                return ValidationResult.Failure("Invalid file path detected.");

            try
            {
                File.WriteAllText(appIdFilePath, gameConfig.AppId.ToString());
                return ValidationResult.Success();
            }
            catch (UnauthorizedAccessException ex)
            {
                ServiceLocator.LogService?.LogError(
                    $"Failed to create {PathConstants.SteamAppIdFileName}: Access denied. {ex.Message}",
                    ex);
                return ValidationResult.Failure("Failed to create file: Access denied. Please check folder permissions.");
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService?.LogError($"Failed to create {PathConstants.SteamAppIdFileName}: {ex.Message}", ex);
                return ValidationResult.Failure($"Failed to create file: {ex.Message}");
            }
        }

        private void WriteSupportedLanguages(string steamSettingsPath, GameConfig gameConfig, OnlineAppData metadata, ref bool anyFileGenerated)
        {
            List<string> languages = ResolveSupportedLanguages(gameConfig, metadata);
            if (languages == null || languages.Count == 0)
                return;

            var supportedLanguagesPath = Path.Combine(steamSettingsPath, PathConstants.GoldbergSupportedLanguagesFileName);
            File.WriteAllLines(supportedLanguagesPath, languages);
            anyFileGenerated = true;
        }

        private void WriteInstalledAppIds(string installedAppIdsPath, List<ulong> installedAppIds, ref bool anyFileGenerated)
        {
            var appIdLines = installedAppIds.Select(id => id.ToString()).ToList();
            File.WriteAllLines(installedAppIdsPath, appIdLines);
            anyFileGenerated = true;
        }

        private List<string> ResolveSupportedLanguages(GameConfig gameConfig, OnlineAppData metadata)
        {
            if (gameConfig.SupportedLanguages != null && gameConfig.SupportedLanguages.Count > 0)
                return gameConfig.SupportedLanguages;

            if (metadata == null || string.IsNullOrEmpty(metadata.SupportedLanguages))
                return null;

            var languageParts = metadata.SupportedLanguages
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrEmpty(l))
                .ToList();

            bool looksLikeDisplayNames = languageParts.Any(l => l.Contains(" ") || (l.Length > 0 && char.IsUpper(l[0])));
            return looksLikeDisplayNames
                ? ConvertLanguageDisplayNamesToCodes(languageParts)
                : languageParts;
        }

        private List<ulong> BuildInstalledAppIds(string installedAppIdsPath, GameConfig gameConfig, OnlineAppData metadata, PackageExtractionResult packageData, LocalInstallSnapshot localInstall)
        {
            var installedAppIds = LoadExistingInstalledAppIds(installedAppIdsPath);
            EnsureMainAppId(installedAppIds, gameConfig.AppId);
            AddLocalRelatedAppIds(installedAppIds, localInstall);
            AddDlcAppIds(installedAppIds, gameConfig, metadata);
            AddPackageAppIds(installedAppIds, gameConfig, packageData);
            return installedAppIds;
        }

        private static List<ulong> LoadExistingInstalledAppIds(string installedAppIdsPath)
        {
            var installedAppIds = new List<ulong>();
            if (!File.Exists(installedAppIdsPath))
                return installedAppIds;

            foreach (var line in File.ReadAllLines(installedAppIdsPath))
            {
                var trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine))
                    continue;
                if (!ulong.TryParse(trimmedLine, out ulong existingAppId) || existingAppId == 0)
                    continue;
                if (!installedAppIds.Contains(existingAppId))
                    installedAppIds.Add(existingAppId);
            }

            return installedAppIds;
        }

        private static void EnsureMainAppId(List<ulong> installedAppIds, ulong appId)
        {
            if (!installedAppIds.Contains(appId))
                installedAppIds.Insert(0, appId);
        }

        private static void AddDlcAppIds(List<ulong> installedAppIds, GameConfig gameConfig, OnlineAppData metadata)
        {
            if (gameConfig.PreFetchedDlcData != null && gameConfig.PreFetchedDlcData.Count > 0)
            {
                foreach (var dlcId in gameConfig.PreFetchedDlcData.Keys)
                {
                    if (dlcId > 0 && !installedAppIds.Contains((ulong)dlcId))
                        installedAppIds.Add((ulong)dlcId);
                }
                return;
            }

            if (metadata == null || metadata.DlcIds == null || metadata.DlcIds.Count == 0)
                return;

            foreach (var dlcId in metadata.DlcIds)
            {
                if (dlcId > 0 && !installedAppIds.Contains((ulong)dlcId))
                    installedAppIds.Add((ulong)dlcId);
            }
        }

        private static void AddPackageAppIds(List<ulong> installedAppIds, GameConfig gameConfig, PackageExtractionResult packageData)
        {
            if (packageData?.AppIds == null || packageData.AppIds.Count == 0)
                return;

            foreach (var packageAppId in packageData.AppIds)
            {
                if (!ulong.TryParse(packageAppId, out ulong appId))
                    continue;
                if (appId == 0 || appId == gameConfig.AppId || installedAppIds.Contains(appId))
                    continue;
                installedAppIds.Add(appId);
            }
        }

        private static void AddLocalRelatedAppIds(List<ulong> installedAppIds, LocalInstallSnapshot localInstall)
        {
            if (localInstall == null || localInstall.RelatedAppIds == null || localInstall.RelatedAppIds.Count == 0)
                return;

            foreach (var appId in localInstall.RelatedAppIds)
            {
                if (appId == 0 || installedAppIds.Contains(appId))
                    continue;
                installedAppIds.Add(appId);
            }
        }

        private static void WriteBranchesJsonIfAbsent(string steamSettingsPath, GameConfig gameConfig, PackageExtractionResult packageData, string currentBranch)
        {
            if (packageData?.Branches == null || packageData.Branches.Count == 0)
                return;

            var branchesPath = Path.Combine(steamSettingsPath, PathConstants.GoldbergBranchesJsonFileName);
            if (File.Exists(branchesPath))
            {
                ServiceLocator.LogService.LogDebug($"Preserving existing {PathConstants.GoldbergBranchesJsonFileName} for app {gameConfig.AppId} (user may have edited it)");
                return;
            }

            var branchesJson = packageData.Branches.Select(b => new
            {
                name = b.Name,
                description = b.Description ?? "",
                @protected = b.Protected,
                build_id = b.BuildId,
                time_updated = b.TimeUpdated,
                is_selected = !string.IsNullOrEmpty(currentBranch) && string.Equals(b.Name, currentBranch, StringComparison.OrdinalIgnoreCase)
            }).ToArray();

            File.WriteAllText(branchesPath, JsonConvert.SerializeObject(branchesJson, JsonFormatting.None));
            ServiceLocator.LogService.LogDebug($"Generated {PathConstants.GoldbergBranchesJsonFileName} with {packageData.Branches.Count} branch(es) for app {gameConfig.AppId}");
        }

        private string TryGetCurrentBranchFromLocalConfig(string steamSettingsPath)
        {
            try
            {
                var appPath = Path.Combine(steamSettingsPath, PathConstants.GoldbergAppIniFileName);
                if (!File.Exists(appPath))
                    return null;

                var appSettings = LoadAppSettings(appPath);
                var branch = appSettings?.BranchName;
                if (string.IsNullOrWhiteSpace(branch))
                    return null;
                return branch.Trim();
            }
            catch
            {
                return null;
            }
        }

        private async Task<PackageExtractionResult> ExtractPackageDataWithAppPicsRootAsync(GameConfig gameConfig, CancellationToken cancellationToken)
        {
            ulong appId = gameConfig.AppId;
            var steam = ServiceLocator.SteamProductInfoService;
            await steam.WarmGameConfigAppPicsRootAsync(gameConfig, cancellationToken).ConfigureAwait(false);
            var packageData = await steam.ExtractPackageDataForAppAsync(appId.ToString(), gameConfig.AppPicsKeyValue, cancellationToken).ConfigureAwait(false);
            ServiceLocator.LogService.LogDebug($"Package extraction (game assets) for app {appId}: Depots={packageData.Depots?.Count ?? 0}, Branches={packageData.Branches?.Count ?? 0}, AppIds={packageData.AppIds?.Count ?? 0}");
            return packageData;
        }

        private async Task<AppDataExtractionResult> ExtractAppDataWithAppPicsRootAsync(GameConfig gameConfig, CancellationToken cancellationToken)
        {
            ulong appId = gameConfig.AppId;
            await ServiceLocator.SteamProductInfoService.WarmGameConfigAppPicsRootAsync(gameConfig, cancellationToken).ConfigureAwait(false);
            return ServiceLocator.SteamProductInfoService.ExtractAppDataFromAppRoot(gameConfig.AppPicsKeyValue, appId.ToString());
        }

        public async Task<bool> TryEnsureStatsJsonAsync(GameConfig gameConfig, CancellationToken cancellationToken = default)
        {
            if (gameConfig == null || gameConfig.AppId == 0)
                return false;

            string steamSettingsPath = GetGameSteamSettingsPath(gameConfig.AppId);
            string statsPath = Path.Combine(steamSettingsPath, PathConstants.GoldbergStatsJsonFileName);
            if (File.Exists(statsPath))
                return false;

            Directory.CreateDirectory(steamSettingsPath);
            var statsGenerator = ServiceLocator.StatsGenerator;
            ulong appId = gameConfig.AppId;

            AppDataExtractionResult appData = await TryLoadAppInfoDataAsync(gameConfig, cancellationToken).ConfigureAwait(false);
            if (statsGenerator.TryWriteStatsJsonIfAbsent(steamSettingsPath, appData?.Stats, appId))
                return true;

            if (_steamApiKeyService.TryGetValidFormatKey(out string apiKey))
            {
                string language = GetLanguageForAchievements(appId);
                string apiJson = await statsGenerator.GetStatsJsonFromSteamApiAsync(appId.ToString(), language, apiKey)
                    .ConfigureAwait(false);
                if (statsGenerator.TryWriteStatsJsonIfAbsent(steamSettingsPath, apiJson, appId))
                    return true;
            }

            string statsDbJson = await StatsGenerator.TryGetGoldbergStatsJsonFromStatsDbAsync(appId.ToString()).ConfigureAwait(false);
            if (statsGenerator.TryWriteStatsJsonIfAbsent(steamSettingsPath, statsDbJson, appId))
                return true;

            ServiceLocator.LogService.LogDebug(
                $"No stats metadata for app {appId} (PICS appinfo, Steam Web API, and games-infos-datas had none).");
            return false;
        }

        private async Task<PackageExtractionResult> TryLoadPackageDataAsync(GameConfig gameConfig, CancellationToken cancellationToken)
        {
            try
            {
                return await ExtractPackageDataWithAppPicsRootAsync(gameConfig, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService.LogError($"Failed to extract package data from game assets for app {gameConfig.AppId}", ex);
                return null;
            }
        }

        private async Task<AppDataExtractionResult> TryLoadAppInfoDataAsync(GameConfig gameConfig, CancellationToken cancellationToken)
        {
            try
            {
                return await ExtractAppDataWithAppPicsRootAsync(gameConfig, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService.LogError($"Failed to extract app data from game assets for app {gameConfig.AppId}", ex);
                return null;
            }
        }

        private static void WriteDepotsIfAbsent(string steamSettingsPath, GameConfig gameConfig, LocalInstallSnapshot localInstall, List<string> depotsFromAppInfo, List<string> packageDepots, ref bool anyFileGenerated)
        {
            var depotsPath = Path.Combine(steamSettingsPath, PathConstants.GoldbergDepotsFileName);
            var acfInstalled = localInstall?.InstalledDepotIds ?? new List<string>();
            var appInfoDepots = depotsFromAppInfo ?? new List<string>();
            var depotsToWrite = acfInstalled.Count > 0
                ? acfInstalled
                : (appInfoDepots.Count > 0 ? appInfoDepots : (packageDepots ?? new List<string>()));
            if (depotsToWrite.Count == 0)
                return;

            if (File.Exists(depotsPath))
            {
                ServiceLocator.LogService.LogDebug($"Preserving existing {PathConstants.GoldbergDepotsFileName} for app {gameConfig.AppId} (user may have edited it)");
                return;
            }

            File.WriteAllLines(depotsPath, depotsToWrite.Distinct().ToList());
            anyFileGenerated = true;
            if (acfInstalled.Count > 0)
                ServiceLocator.LogService.LogDebug($"Generated {PathConstants.GoldbergDepotsFileName} from appmanifest InstalledDepots ({depotsToWrite.Count} depot(s)) for app {gameConfig.AppId}");
            else if (appInfoDepots.Count > 0)
                ServiceLocator.LogService.LogDebug($"Generated {PathConstants.GoldbergDepotsFileName} from game assets with {depotsToWrite.Count} depot(s) for app {gameConfig.AppId}");
            else
                ServiceLocator.LogService.LogDebug($"Generated {PathConstants.GoldbergDepotsFileName} from package fallback with {depotsToWrite.Count} depot(s) for app {gameConfig.AppId}");
        }

        private LocalInstallSnapshot TryLoadLocalInstallSnapshot(GameConfig gameConfig)
        {
            var snapshot = new LocalInstallSnapshot();

            try
            {
                string acfPath = FindAppManifestAcfPath(gameConfig);
                if (!string.IsNullOrEmpty(acfPath))
                    PopulateFromAcf(acfPath, snapshot);
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService.LogWarning($"Failed reading ACF local install data for app {gameConfig.AppId}: {ex.Message}");
            }

            snapshot.InstalledDepotIds = snapshot.InstalledDepotIds
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            snapshot.RelatedAppIds = snapshot.RelatedAppIds.Distinct().ToList();
            return snapshot;
        }

        private string FindAppManifestAcfPath(GameConfig gameConfig)
        {
            string fileName = PathConstants.SteamAppManifestFilePrefix + gameConfig.AppId + PathConstants.SteamAppManifestFileExtension;
            string startFolder = gameConfig.StartFolder ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(startFolder) && Directory.Exists(startFolder))
            {
                try
                {
                    foreach (string path in Directory.EnumerateFiles(startFolder, fileName, SearchOption.AllDirectories))
                        return path;
                }
                catch (Exception ex)
                {
                    ServiceLocator.LogService.LogWarning($"ACF search under game folder failed for app {gameConfig.AppId}: {ex.Message}");
                }

                try
                {
                    for (DirectoryInfo dir = new DirectoryInfo(startFolder); dir != null; dir = dir.Parent)
                    {
                        string inSteamApps = Path.Combine(dir.FullName, PathConstants.SteamAppsDirectoryName, fileName);
                        if (File.Exists(inSteamApps))
                            return inSteamApps;
                    }
                }
                catch
                {
                }
            }

            foreach (string libRoot in EnumerateSteamLibraryRootsForManifestSearch())
            {
                string candidate = Path.Combine(libRoot, PathConstants.SteamAppsDirectoryName, fileName);
                if (File.Exists(candidate))
                    return candidate;
            }

            return null;
        }

        private static IEnumerable<string> EnumerateSteamLibraryRootsForManifestSearch()
        {
            var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string steamRoot = ResolveSteamRootPath();
            if (!string.IsNullOrEmpty(steamRoot))
            {
                string n = NormalizeSteamLibraryRootPath(steamRoot);
                if (n != null && Directory.Exists(n) && yielded.Add(n))
                    yield return n;
            }

            foreach (string vdfPath in GetLibraryFoldersVdfCandidatePaths(steamRoot))
            {
                if (!File.Exists(vdfPath))
                    continue;
                foreach (string r in ReadLibraryRootsFromLibraryFoldersVdf(vdfPath))
                {
                    if (yielded.Add(r))
                        yield return r;
                }
            }
        }

        private static IEnumerable<string> GetLibraryFoldersVdfCandidatePaths(string resolvedSteamRoot)
        {
            var paths = new List<string>();
            if (!string.IsNullOrEmpty(resolvedSteamRoot))
                paths.Add(Path.Combine(resolvedSteamRoot, PathConstants.SteamAppsDirectoryName, PathConstants.SteamLibraryFoldersVdfFileName));
            string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (!string.IsNullOrEmpty(pf86))
                paths.Add(Path.Combine(pf86, PathConstants.SteamClientRelativeRootFolderName, PathConstants.SteamAppsDirectoryName, PathConstants.SteamLibraryFoldersVdfFileName));
            foreach (string p in paths.Distinct(StringComparer.OrdinalIgnoreCase))
                yield return p;
        }

        private static IEnumerable<string> ReadLibraryRootsFromLibraryFoldersVdf(string vdfPath)
        {
            string content;
            try
            {
                content = File.ReadAllText(vdfPath);
            }
            catch
            {
                yield break;
            }

            foreach (Match m in Regex.Matches(content, "\"" + Regex.Escape(PathConstants.SteamLibraryFoldersVdfPathKey) + "\"\\s+\"([^\"]*)\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                string n = NormalizeSteamLibraryRootPath(m.Groups[1].Value);
                if (n != null && Directory.Exists(n))
                    yield return n;
            }

            foreach (Match m in Regex.Matches(content, @"""(\d+)""\s+""([^""]*)""", RegexOptions.CultureInvariant))
            {
                string n = NormalizeSteamLibraryRootPath(m.Groups[2].Value);
                if (n == null || !Directory.Exists(n) || !LooksLikeSteamLibraryRootPath(m.Groups[2].Value))
                    continue;
                yield return n;
            }
        }

        private static string NormalizeSteamLibraryRootPath(string pathFromVdf)
        {
            if (string.IsNullOrWhiteSpace(pathFromVdf))
                return null;
            string t = pathFromVdf.Trim().Replace('/', Path.DirectorySeparatorChar);
            try
            {
                return Path.GetFullPath(t);
            }
            catch
            {
                return null;
            }
        }

        private static bool LooksLikeSteamLibraryRootPath(string rawValueFromVdf)
        {
            if (string.IsNullOrWhiteSpace(rawValueFromVdf))
                return false;
            string t = rawValueFromVdf.Trim();
            if (t.Length >= 2 && t[1] == ':' && char.IsLetter(t[0]))
                return true;
            if (t.StartsWith("\\\\", StringComparison.Ordinal))
                return true;
            if (t.StartsWith("/", StringComparison.Ordinal))
                return true;
            return false;
        }

        private static string ResolveSteamRootPath()
        {
            return SteamInstallationPathHelper.ResolveSteamRootFromCurrentUserIfPresent();
        }

        private static void PopulateFromAcf(string acfPath, LocalInstallSnapshot snapshot)
        {
            var root = ParseAcfFile(acfPath);
            if (root == null)
                return;

            var appState = root.GetChild(SteamAppManifestAcfKeys.AppState);
            if (appState == null)
                return;

            var userConfig = appState.GetChild(SteamAppManifestAcfKeys.UserConfig);
            var betaKey = userConfig?.GetValue(SteamPicsKeyNames.BetaKey);
            if (!string.IsNullOrWhiteSpace(betaKey))
                snapshot.CurrentBranch = betaKey.Trim();

            var installedDepots = appState.GetChild(SteamAppManifestAcfKeys.InstalledDepots);
            if (installedDepots != null && installedDepots.Children != null)
            {
                foreach (var kvp in installedDepots.Children)
                {
                    if (!ulong.TryParse(kvp.Key, out ulong depotId) || depotId == 0)
                        continue;
                    if (!snapshot.InstalledDepotIds.Contains(kvp.Key))
                        snapshot.InstalledDepotIds.Add(kvp.Key);
                }
            }

            AddAppIdsFromAcfSection(snapshot.RelatedAppIds, appState.GetChild(SteamAppManifestAcfKeys.MountedAppIDs));
            AddAppIdsFromAcfSection(snapshot.RelatedAppIds, appState.GetChild(SteamAppManifestAcfKeys.RelatedAppIDs));
            AddAppIdsFromAcfSection(snapshot.RelatedAppIds, appState.GetChild(SteamAppManifestAcfKeys.MountedApps));
        }

        private static void AddAppIdsFromAcfSection(List<ulong> target, AcfNode section)
        {
            if (section == null || section.Values == null || section.Values.Count == 0)
                return;

            foreach (var kvp in section.Values)
            {
                if (!ulong.TryParse(kvp.Value, out ulong appId) || appId == 0)
                    continue;
                if (!target.Contains(appId))
                    target.Add(appId);
            }
        }

        private static AcfNode ParseAcfFile(string path)
        {
            var root = new AcfNode();
            var stack = new Stack<AcfNode>();
            stack.Push(root);
            string pendingSectionName = null;

            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal))
                    continue;

                if (line == "{")
                {
                    if (string.IsNullOrEmpty(pendingSectionName))
                        continue;

                    var node = new AcfNode();
                    stack.Peek().Children[pendingSectionName] = node;
                    stack.Push(node);
                    pendingSectionName = null;
                    continue;
                }

                if (line == "}")
                {
                    if (stack.Count > 1)
                        stack.Pop();
                    pendingSectionName = null;
                    continue;
                }

                if (!TryExtractQuotedTokens(line, out List<string> tokens))
                    continue;

                if (tokens.Count == 1)
                {
                    pendingSectionName = tokens[0];
                    continue;
                }

                if (tokens.Count >= 2)
                {
                    stack.Peek().Values[tokens[0]] = tokens[1];
                    pendingSectionName = null;
                }
            }

            return root;
        }

        private static bool TryExtractQuotedTokens(string line, out List<string> tokens)
        {
            tokens = new List<string>();
            int i = 0;
            while (i < line.Length)
            {
                int start = line.IndexOf('"', i);
                if (start < 0)
                    break;
                int end = line.IndexOf('"', start + 1);
                if (end < 0)
                    break;
                tokens.Add(line.Substring(start + 1, end - start - 1));
                i = end + 1;
            }

            return tokens.Count > 0;
        }

        private sealed class LocalInstallSnapshot
        {
            public string CurrentBranch { get; set; }
            public List<string> InstalledDepotIds { get; set; } = new List<string>();
            public List<ulong> RelatedAppIds { get; set; } = new List<ulong>();
        }

        private sealed class AcfNode
        {
            public Dictionary<string, string> Values { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, AcfNode> Children { get; } = new Dictionary<string, AcfNode>(StringComparer.OrdinalIgnoreCase);

            public AcfNode GetChild(string key)
            {
                if (string.IsNullOrEmpty(key))
                    return null;
                Children.TryGetValue(key, out AcfNode child);
                return child;
            }

            public string GetValue(string key)
            {
                if (string.IsNullOrEmpty(key))
                    return null;
                Values.TryGetValue(key, out string value);
                return value;
            }
        }

        private static void WriteLeaderboardsIfAbsent(string steamSettingsPath, GameConfig gameConfig, List<string> leaderboards, ref bool anyFileGenerated)
        {
            if (leaderboards == null || leaderboards.Count == 0)
                return;

            var leaderboardsPath = Path.Combine(steamSettingsPath, PathConstants.GoldbergLeaderboardsFileName);
            if (File.Exists(leaderboardsPath))
                return;

            File.WriteAllLines(leaderboardsPath, leaderboards);
            anyFileGenerated = true;
            ServiceLocator.LogService.LogDebug($"Generated {PathConstants.GoldbergLeaderboardsFileName} with {leaderboards.Count} leaderboard(s) for app {gameConfig.AppId}");
        }

        private void TryFetchLeaderboardsOnlineIfMissing(string steamSettingsPath, GameConfig gameConfig)
        {
            var leaderboardsPath = Path.Combine(steamSettingsPath, PathConstants.GoldbergLeaderboardsFileName);
            if (File.Exists(leaderboardsPath))
                return;

            _ = Task.Run(async () =>
            {
                try
                {
                    var leaderboards = await SteamWebApiService.GetLeaderboardsFromCommunityAsync(gameConfig.AppId.ToString());
                    if (leaderboards == null || leaderboards.Count == 0)
                        return;
                    if (File.Exists(leaderboardsPath))
                        return;

                    File.WriteAllLines(leaderboardsPath, leaderboards);
                    ServiceLocator.LogService.LogDebug($"Generated {PathConstants.GoldbergLeaderboardsFileName} from community fallback with {leaderboards.Count} leaderboard(s) for app {gameConfig.AppId}");
                }
                catch (Exception ex)
                {
                    ServiceLocator.LogService.LogError($"Failed to fetch leaderboards fallback for app {gameConfig.AppId}", ex);
                }
            }).ForgetFaults(ServiceLocator.LogService, nameof(TryFetchLeaderboardsOnlineIfMissing));
        }

        private static void WriteSteamInterfacesIfSourceAvailable(string steamSettingsPath, GameConfig gameConfig, ref bool anyFileGenerated)
        {
            ServiceLocator.SteamInterfacesService.TryEnsureSteamInterfacesFile(steamSettingsPath, gameConfig, ref anyFileGenerated);
        }

        private static void TryPopulateConfigsAppIni(GameConfig gameConfig, OnlineAppData metadata, ref bool anyFileGenerated)
        {
            try
            {
                var filesService = ServiceLocator.GoldbergFilesService;
                if (filesService == null)
                    return;

                var existing = filesService.LoadAppConfigDlcAndPaths(gameConfig.AppId);
                var mergedDlc = existing?.DlcData != null
                    ? new Dictionary<long, string>(existing.DlcData)
                    : new Dictionary<long, string>();
                var mergedPaths = existing?.AppPaths != null
                    ? new Dictionary<string, string>(existing.AppPaths, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                bool changed = false;

                if (!string.IsNullOrWhiteSpace(gameConfig.StartFolder))
                {
                    string appIdKey = gameConfig.AppId.ToString();
                    if (!mergedPaths.ContainsKey(appIdKey))
                    {
                        mergedPaths[appIdKey] = gameConfig.StartFolder.Trim();
                        changed = true;
                    }
                }

                // Prefer local/pre-fetched DLC map because it includes names.
                if (gameConfig.PreFetchedDlcData != null)
                {
                    foreach (var kvp in gameConfig.PreFetchedDlcData)
                    {
                        long dlcId = kvp.Key;
                        if (dlcId <= 0 || mergedDlc.ContainsKey(dlcId))
                            continue;
                        string name = (kvp.Value ?? string.Empty).Trim();
                        if (string.IsNullOrEmpty(name))
                            continue;
                        mergedDlc[dlcId] = name;
                        changed = true;
                    }
                }
                // Do not write placeholder DLC names from ID-only metadata.
                // We only persist DLCs when a reliable name is available.

                if (!changed)
                    return;

                var save = filesService.SaveAppConfigDlcAndPaths(gameConfig.AppId, mergedDlc, mergedPaths);
                if (save != null && save.IsSuccess)
                    anyFileGenerated = true;
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService.LogWarning($"Failed to populate {PathConstants.GoldbergAppIniFileName} for app {gameConfig.AppId}: {ex.Message}");
            }
        }

        private static void WriteAchievementsFromPicsIfAbsent(string steamSettingsPath, GameConfig gameConfig, string achievementsJson, ref bool anyFileGenerated)
        {
            if (string.IsNullOrEmpty(achievementsJson))
                return;

            var achievementsPath = Path.Combine(steamSettingsPath, AchievementConstants.AchievementsFileName);
            if (File.Exists(achievementsPath))
                return;

            File.WriteAllText(achievementsPath, achievementsJson);
            anyFileGenerated = true;
            ServiceLocator.LogService.LogDebug($"Generated {AchievementConstants.AchievementsFileName} from game assets for app {gameConfig.AppId}");
        }

        private void TryFetchAchievementsOnlineIfMissing(string steamSettingsPath, GameConfig gameConfig)
        {
            var achievementsPath = Path.Combine(steamSettingsPath, AchievementConstants.AchievementsFileName);
            if (File.Exists(achievementsPath))
                return;

            string language = GetLanguageForAchievements(gameConfig.AppId);
            if (!_steamApiKeyService.TryGetValidFormatKey(out string apiKey))
                return;

            _ = Task.Run(async () =>
            {
                try
                {
                    ServiceLocator.LogService.LogDebug($"Attempting to fetch achievements from online API for app {gameConfig.AppId}");
                    var achievementSchema = await SteamWebApiService.GetAchievementsAsync(gameConfig.AppId.ToString(), language, apiKey);
                    if (achievementSchema == null || !achievementSchema.Success || achievementSchema.Achievements == null || achievementSchema.Achievements.Count == 0)
                        return;

                    var achievementsList = achievementSchema.Achievements.Select(a => new
                    {
                        name = a.Name,
                        displayName = a.DisplayName,
                        description = a.Description,
                        icon = a.Icon,
                        icongray = a.IconGray,
                        hidden = a.Hidden ? 1 : 0
                    }).ToList();

                    if (File.Exists(achievementsPath))
                        return;

                    File.WriteAllText(achievementsPath, JsonConvert.SerializeObject(achievementsList, JsonFormatting.Indented));
                    ServiceLocator.LogService.LogDebug($"Generated {AchievementConstants.AchievementsFileName} from online API with {achievementsList.Count} achievement(s) for app {gameConfig.AppId}");
                }
                catch (Exception ex)
                {
                    ServiceLocator.LogService.LogError($"Failed to fetch achievements from online API for app {gameConfig.AppId}", ex);
                }
            }).ForgetFaults(ServiceLocator.LogService, nameof(TryFetchAchievementsOnlineIfMissing));
        }

        private static void CopyDefaultItemsIfSourceAvailable(string steamSettingsPath, GameConfig gameConfig, ref bool anyFileGenerated)
        {
            var targetPath = Path.Combine(steamSettingsPath, PathConstants.GoldbergDefaultItemsJsonFileName);
            if (File.Exists(targetPath))
                return;

            var sourcePath = FindDefaultItemsSourcePath(gameConfig);
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
                return;

            try
            {
                var content = File.ReadAllText(sourcePath);
                if (string.IsNullOrWhiteSpace(content))
                    return;
                File.WriteAllText(targetPath, content);
                anyFileGenerated = true;
                ServiceLocator.LogService.LogDebug($"Generated {PathConstants.GoldbergDefaultItemsJsonFileName} from source file for app {gameConfig.AppId}");
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService.LogError($"Failed generating {PathConstants.GoldbergDefaultItemsJsonFileName} for app {gameConfig.AppId}", ex);
            }
        }

        private static string FindDefaultItemsSourcePath(GameConfig gameConfig)
        {
            var candidates = new List<string>();
            string startFolder = gameConfig.StartFolder ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(startFolder) && Directory.Exists(startFolder))
            {
                candidates.Add(Path.Combine(startFolder, PathConstants.GoldbergDefaultItemsJsonFileName));
                candidates.Add(Path.Combine(startFolder, PathConstants.GoldbergDefaultItemsExampleJsonFileName));
                candidates.Add(Path.Combine(startFolder, PathConstants.SteamSettingsFolderName, PathConstants.GoldbergDefaultItemsJsonFileName));
            }

            foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (File.Exists(candidate))
                    return candidate;
            }

            return null;
        }

        public ValidationResult ValidateConfigFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return ValidationResult.Failure("Config file does not exist");

                var lines = File.ReadAllLines(filePath);
                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith(";") || trimmedLine.StartsWith("#"))
                        continue;

                    if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                        continue;

                    if (!trimmedLine.Contains("="))
                        return ValidationResult.Failure($"Invalid line format: {trimmedLine}");
                }

                return ValidationResult.Success();
            }
            catch (Exception ex)
            {
                return ValidationResult.Failure($"Failed to validate config file: {ex.Message}");
            }
        }

        public string BackupGameSettings(ulong appId)
        {
            try
            {
                var steamSettingsPath = GetGameSteamSettingsPath(appId);
                var backupPath = Path.Combine(PathConstants.LauncherBackupTempRootDirectory, appId.ToString(), DateTime.Now.ToString("yyyyMMdd_HHmmss"));

                if (Directory.Exists(steamSettingsPath))
                {
                    CopyDirectory(steamSettingsPath, backupPath);
                }

                return backupPath;
            }
            catch (Exception ex)
            {
                LogRedactionHelper.WriteDebug($"Failed to backup game settings: {ex.Message}");
                return string.Empty;
            }
        }

        public bool RestoreGameSettings(ulong appId, string backupPath)
        {
            try
            {
                if (!Directory.Exists(backupPath))
                    return false;

                var steamSettingsPath = GetGameSteamSettingsPath(appId);
                Directory.CreateDirectory(steamSettingsPath);

                CopyDirectory(backupPath, steamSettingsPath);
                return true;
            }
            catch (Exception ex)
            {
                LogRedactionHelper.WriteDebug($"Failed to restore game settings: {ex.Message}");
                return false;
            }
        }

        public string GetGameSteamSettingsPath(ulong appId)
        {
            return PathConstants.CombineGameSteamSettingsDirectory(_gamesDirectory, appId.ToString());
        }

        public string GetGameSavesPath(ulong appId)
        {
            if (appId == 0)
                return string.Empty;

            try
            {
                var snapshot = LoadGameSettingsSnapshot(appId);
                string relativeToDirectory = null;
                try
                {
                    var game = ServiceLocator.GameDataService?.GetGameByAppId(appId);
                    if (game != null)
                        GameFolderPathHelper.TryGetExecutableDirectory(game, out relativeToDirectory);
                }
                catch
                {
                }

                return GoldbergSavePathHelper.ResolveGameSavesPath(snapshot?.User, appId, relativeToDirectory);
            }
            catch (Exception ex)
            {
                LogRedactionHelper.WriteDebug($"Failed to resolve game saves path for {appId}: {ex.Message}");
                return GoldbergSavePathHelper.ResolveGameSavesPath(null, appId, null);
            }
        }

        // [user::saves] is global-only; remove stray keys from per-game configs.user.ini (legacy/manual edits).
        public SaveResult StripSaveLocationFromPerGameUserIni(ulong appId)
        {
            if (appId == 0)
                return SaveResult.Success(0);

            try
            {
                string userPath = Path.Combine(GetGameSteamSettingsPath(appId), PathConstants.GoldbergUserIniFileName);
                SaveResult result = _goldbergCfgService.StripPerGameSaveLocationKeys(userPath);
                if (!result.IsSuccess)
                    return SaveResult.Failure($"Failed to strip per-game save location for app {appId}: {result.ErrorMessage}");
                return result;
            }
            catch (Exception ex)
            {
                return SaveResult.Failure($"Failed to strip per-game save location for app {appId}: {ex.Message}");
            }
        }

        public void StripSaveLocationFromAllPerGameUserIni()
        {
            try
            {
                var games = ServiceLocator.GameDataService?.GetAllGames();
                if (games == null)
                    return;

                foreach (var game in games)
                {
                    if (game?.AppId != 0)
                        StripSaveLocationFromPerGameUserIni(game.AppId);
                }
            }
            catch (Exception ex)
            {
                LogRedactionHelper.WriteDebug($"Failed to strip per-game save locations: {ex.Message}");
            }
        }

        private OverlaySettings LoadOverlaySettings(string filePath)
        {
            return _goldbergCfgService.LoadOverlaySettingsFromPath(filePath);
        }

        private MainSettings LoadMainSettings(string filePath)
        {
            var settings = new MainSettings();
            if (!File.Exists(filePath)) return settings;

            var lines = File.ReadAllLines(filePath);
            string currentSection = null;

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith(";") || trimmedLine.StartsWith("#"))
                    continue;

                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    currentSection = trimmedLine.Substring(1, trimmedLine.Length - 2);
                    continue;
                }

                if (trimmedLine.Contains("="))
                {
                    var parts = trimmedLine.Split(new[] { '=' }, 2);
                    if (parts.Length == 2)
                    {
                        var key = parts[0].Trim();
                        var value = parts[1].Trim();
                        var isEnabled = value == "1";

                        switch (key)
                        {
                            // main::general
                            case "new_app_ticket":
                                if (currentSection == "main::general")
                                    settings.NewAppTicket = isEnabled;
                                break;
                            case "gc_token":
                                if (currentSection == "main::general")
                                    settings.GcToken = isEnabled;
                                break;
                            case "block_unknown_clients":
                                if (currentSection == "main::general")
                                    settings.BlockUnknownClients = isEnabled;
                                break;
                            case "steam_deck":
                                if (currentSection == "main::general")
                                    settings.SteamDeck = isEnabled;
                                break;
                            case "enable_account_avatar":
                                if (currentSection == "main::general")
                                    settings.EnableAccountAvatar = isEnabled;
                                break;
                            case "enable_voice_chat":
                                if (currentSection == "main::general")
                                    settings.EnableVoiceChat = isEnabled;
                                break;
                            case "immediate_gameserver_stats":
                                if (currentSection == "main::general")
                                    settings.ImmediateGameserverStats = isEnabled;
                                break;
                            case "matchmaking_server_list_actual_type":
                                if (currentSection == "main::general")
                                    settings.MatchmakingServerListActualType = isEnabled;
                                break;
                            case "matchmaking_server_details_via_source_query":
                                if (currentSection == "main::general")
                                    settings.MatchmakingServerDetailsViaSourceQuery = isEnabled;
                                break;
                            // main::stats
                            case "disable_leaderboards_create_unknown":
                                if (currentSection == "main::stats")
                                    settings.DisableLeaderboardsCreateUnknown = isEnabled;
                                break;
                            case "allow_unknown_stats":
                                if (currentSection == "main::stats")
                                    settings.AllowUnknownStats = isEnabled;
                                break;
                            case "stat_achievement_progress_functionality":
                                if (currentSection == "main::stats")
                                    settings.StatAchievementProgressFunctionality = isEnabled;
                                break;
                            case "save_only_higher_stat_achievement_progress":
                                if (currentSection == "main::stats")
                                    settings.SaveOnlyHigherStatAchievementProgress = isEnabled;
                                break;
                            case "paginated_achievements_icons":
                                if (currentSection == "main::stats" && int.TryParse(value, out int icons))
                                    settings.PaginatedAchievementsIcons = icons;
                                break;
                            case "record_playtime":
                                if (currentSection == "main::stats")
                                    settings.RecordPlaytime = isEnabled;
                                break;
                            // main::connectivity
                            case "disable_lan_only":
                                if (currentSection == "main::connectivity")
                                    settings.DisableLanOnly = isEnabled;
                                break;
                            case "disable_networking":
                                if (currentSection == "main::connectivity")
                                    settings.DisableNetworking = isEnabled;
                                break;
                            case "listen_port":
                                if (currentSection == "main::connectivity" && int.TryParse(value, out int port))
                                    settings.ListenPort = port;
                                break;
                            case "offline":
                                if (currentSection == "main::connectivity")
                                    settings.Offline = isEnabled;
                                break;
                            case "disable_sharing_stats_with_gameserver":
                                if (currentSection == "main::connectivity")
                                    settings.DisableSharingStatsWithGameserver = isEnabled;
                                break;
                            case "disable_source_query":
                                if (currentSection == "main::connectivity")
                                    settings.DisableSourceQuery = isEnabled;
                                break;
                            case "share_leaderboards_over_network":
                                if (currentSection == "main::connectivity")
                                    settings.ShareLeaderboardsOverNetwork = isEnabled;
                                break;
                            case "disable_lobby_creation":
                                if (currentSection == "main::connectivity")
                                    settings.DisableLobbyCreation = isEnabled;
                                break;
                            case "download_steamhttp_requests":
                                if (currentSection == "main::connectivity")
                                    settings.DownloadSteamhttpRequests = isEnabled;
                                break;
                            case "old_p2p_packet_sharing_mode":
                                if (currentSection == "main::connectivity" && int.TryParse(value, out int p2pMode))
                                    settings.OldP2PPacketSharingMode = p2pMode;
                                break;
                            // main::misc
                            case "achievements_bypass":
                                if (currentSection == "main::misc")
                                    settings.AchievementsBypass = isEnabled;
                                break;
                            case "force_steamhttp_success":
                                if (currentSection == "main::misc")
                                    settings.ForceSteamhttpSuccess = isEnabled;
                                break;
                            case "disable_steamoverlaygameid_env_var":
                                if (currentSection == "main::misc")
                                    settings.DisableSteamoverlaygameidEnvVar = isEnabled;
                                break;
                            case "enable_steam_preowned_ids":
                                if (currentSection == "main::misc")
                                    settings.EnableSteamPreownedIds = isEnabled;
                                break;
                            case "steam_game_stats_reports_dir":
                                if (currentSection == "main::misc")
                                    settings.SteamGameStatsReportsDir = value;
                                break;
                            case "free_weekend":
                                if (currentSection == "main::misc")
                                    settings.FreeWeekend = isEnabled;
                                break;
                            case "use_32bit_inventory_item_ids":
                                if (currentSection == "main::misc")
                                    settings.Use32BitInventoryItemIds = isEnabled;
                                break;
                        }
                    }
                }
            }

            return settings;
        }

        private List<string> ConvertLanguageDisplayNamesToCodes(List<string> displayNames)
        {
            var languageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
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

            var languageCodes = new List<string>();
            foreach (var displayName in displayNames)
            {
                if (string.IsNullOrWhiteSpace(displayName))
                    continue;

                // Try to find exact match first
                if (languageMap.TryGetValue(displayName, out string code))
                {
                    if (!languageCodes.Contains(code))
                        languageCodes.Add(code);
                }
                else
                {
                    // Fallback: convert to lowercase (might already be a code like "english")
                    var lowerName = displayName.ToLowerInvariant();
                    if (!languageCodes.Contains(lowerName))
                        languageCodes.Add(lowerName);
                }
            }

            return languageCodes;
        }

        private AppSettings LoadAppSettings(string filePath)
        {
            var settings = new AppSettings();
            if (!File.Exists(filePath)) return settings;

            var lines = File.ReadAllLines(filePath);
            string currentSection = null;

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith(";") || trimmedLine.StartsWith("#"))
                    continue;

                if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                {
                    currentSection = trimmedLine.Substring(1, trimmedLine.Length - 2);
                    continue;
                }

                if (trimmedLine.Contains("="))
                {
                    var parts = trimmedLine.Split(new[] { '=' }, 2);
                    if (parts.Length == 2)
                    {
                        var key = parts[0].Trim();
                        var value = parts[1].Trim();

                        switch (key)
                        {
                            case "unlock_all":
                                if (currentSection == "app::dlcs")
                                    settings.UnlockAllDLC = value == "1";
                                break;
                            case "branch_name":
                                if (currentSection == "app::general")
                                    settings.BranchName = value;
                                break;
                            case "is_beta_branch":
                                if (currentSection == "app::general")
                                    settings.IsBetaBranch = value == "1";
                                break;
                            case "steam_input":
                                if (currentSection == "app::controller")
                                    settings.SteamInput = value == "1";
                                break;
                            case "type":
                                if (currentSection == "app::controller" && !string.IsNullOrEmpty(value))
                                    settings.ControllerType = value;
                                break;
                        }
                    }
                }
            }

            return settings;
        }

        private UserSettings LoadUserSettings(string filePath)
        {
            var settings = _goldbergCfgService.LoadUserSettingsFromPath(filePath);
            // [user::saves] is global-only; per-game keys are ignored for merge/UI (stripped on save/launch).
            settings.LocalSavePath = string.Empty;
            settings.SavesFolderName = string.Empty;
            return settings;
        }

        private SaveResult SaveOverlaySettings(string filePath, OverlaySettings settings)
        {
            try
            {
                var cache = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
                string examplePath = null;
                var content = new StringBuilder();
                GoldbergIniDocumentationHelper.AppendIniHeader(content);
                GoldbergIniDocumentationHelper.AppendSection(content, "overlay::general");
                GoldbergIniDocumentationHelper.AppendOptionWithExampleOrFallback(
                    content, examplePath, "overlay::general", "enable_experimental_overlay", BoolToInt(settings.EnableExperimentalOverlay), cache,
                    "1=enable the experimental overlay, might cause crashes", "default=0");
                GoldbergIniDocumentationHelper.AppendOptionWithExampleOrFallback(
                    content, examplePath, "overlay::general", "disable_achievement_notification", BoolToInt(settings.DisableAchievementNotification), cache,
                    "1=disable the achievements notifications", "default=0");
                GoldbergIniDocumentationHelper.AppendOptionWithExampleOrFallback(
                    content, examplePath, "overlay::general", "disable_friend_notification", BoolToInt(settings.DisableFriendNotification), cache,
                    "1=disable friends invitations and messages notifications", "default=0");

                File.WriteAllText(filePath, content.ToString());
                return SaveResult.Success(1);
            }
            catch (Exception ex)
            {
                return SaveResult.Failure($"Failed to save overlay settings: {ex.Message}");
            }
        }

        private SaveResult SaveGameNetworkMainSettings(string filePath, MainSettings settings)
        {
            if (settings == null)
                return SaveResult.Failure("Network settings cannot be null");

            try
            {
                var network = MainSettingsScopes.ExtractNetworkSlice(settings);
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                var iniFile = File.Exists(filePath)
                    ? ServiceLocator.IniFileService.ParseFile(filePath)
                    : new IniFile();
                StripGlobalSessionMainKeysFromPerGameIni(iniFile);
                StripNetworkMainKeysFromPerGameIni(iniFile);
                _goldbergCfgService.ApplyNetworkMainToIni(iniFile, network);
                GoldbergIniDocumentationHelper.WriteIniFile(filePath, iniFile.Lines);
                return SaveResult.Success(1);
            }
            catch (Exception ex)
            {
                return SaveResult.Failure($"Failed to save game network settings: {ex.Message}");
            }
        }

        private SaveResult SaveGameStatsAchievementsMainSettings(string filePath, MainSettings settings)
        {
            if (settings == null)
                return SaveResult.Failure("Stats settings cannot be null");

            try
            {
                var statsAchievements = MainSettingsScopes.ExtractStatsAchievementsSlice(settings);
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                var iniFile = File.Exists(filePath)
                    ? ServiceLocator.IniFileService.ParseFile(filePath)
                    : new IniFile();
                StripGlobalSessionMainKeysFromPerGameIni(iniFile);
                StripStatsAchievementsMainKeysFromPerGameIni(iniFile);
                _goldbergCfgService.ApplyStatsAchievementsMainToIni(iniFile, statsAchievements);
                GoldbergIniDocumentationHelper.WriteIniFile(filePath, iniFile.Lines);
                return SaveResult.Success(1);
            }
            catch (Exception ex)
            {
                return SaveResult.Failure($"Failed to save game stats settings: {ex.Message}");
            }
        }

        private static void StripGlobalSessionMainKeysFromPerGameIni(IniFile iniFile)
        {
            if (iniFile == null)
                return;

            var iniService = ServiceLocator.IniFileService;
            foreach (var key in new[]
            {
                "new_app_ticket", "gc_token", "steam_deck", "enable_account_avatar", "enable_voice_chat",
                "crash_printer_location"
            })
                iniService.RemoveValue(iniFile, "main::general", key);

            foreach (var key in new[]
            {
                "force_steamhttp_success", "disable_steamoverlaygameid_env_var",
                "enable_steam_preowned_ids", "free_weekend", "use_32bit_inventory_item_ids"
            })
                iniService.RemoveValue(iniFile, "main::misc", key);
        }

        private static void StripNetworkMainKeysFromPerGameIni(IniFile iniFile)
        {
            if (iniFile == null)
                return;

            var iniService = ServiceLocator.IniFileService;
            foreach (var key in new[]
            {
                "block_unknown_clients", "immediate_gameserver_stats", "matchmaking_server_list_actual_type",
                "matchmaking_server_details_via_source_query"
            })
                iniService.RemoveValue(iniFile, "main::general", key);

            foreach (var key in new[]
            {
                "disable_lan_only", "disable_networking", "listen_port", "offline",
                "disable_sharing_stats_with_gameserver", "disable_source_query",
                "share_leaderboards_over_network", "disable_lobby_creation",
                "download_steamhttp_requests", "old_p2p_packet_sharing_mode"
            })
                iniService.RemoveValue(iniFile, "main::connectivity", key);
        }

        private static void StripStatsAchievementsMainKeysFromPerGameIni(IniFile iniFile)
        {
            if (iniFile == null)
                return;

            var iniService = ServiceLocator.IniFileService;
            foreach (var key in new[]
            {
                "disable_leaderboards_create_unknown", "allow_unknown_stats",
                "stat_achievement_progress_functionality", "save_only_higher_stat_achievement_progress",
                "paginated_achievements_icons", "record_playtime"
            })
                iniService.RemoveValue(iniFile, "main::stats", key);

            foreach (var key in new[] { "achievements_bypass", "steam_game_stats_reports_dir" })
                iniService.RemoveValue(iniFile, "main::misc", key);
        }

        private SaveResult SaveMainSettings(string filePath, MainSettings settings)
        {
            try
            {
                var d = new MainSettings();
                var cache = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
                string examplePath = null;
                var content = new StringBuilder();
                GoldbergIniDocumentationHelper.AppendIniHeader(content);

                GoldbergIniDocumentationHelper.AppendSection(content, "main::general");
                if (settings.NewAppTicket != d.NewAppTicket)
                    GoldbergIniDocumentationHelper.AppendOptionWithExampleOrFallback(content, examplePath, "main::general", "new_app_ticket", BoolToInt(settings.NewAppTicket), cache, "1=generate modern version of auth ticket, may need to be disabled for very old games", "default=1");
                if (settings.GcToken != d.GcToken)
                    GoldbergIniDocumentationHelper.AppendOptionWithExampleOrFallback(content, examplePath, "main::general", "gc_token", BoolToInt(settings.GcToken), cache, "1=generate/embed Game Coordinator token inside the new auth ticket", "default=1");
                if (settings.BlockUnknownClients != d.BlockUnknownClients)
                    GoldbergIniDocumentationHelper.AppendOptionWithExampleOrFallback(content, examplePath, "main::general", "block_unknown_clients", BoolToInt(settings.BlockUnknownClients), cache, "1=game server will only allow connections from legit Steam clients and known Steam emulators", "default=0");
                if (settings.SteamDeck != d.SteamDeck)
                    GoldbergIniDocumentationHelper.AppendOptionWithExampleOrFallback(content, examplePath, "main::general", "steam_deck", BoolToInt(settings.SteamDeck), cache, "1=pretend the app is running on a steam deck", "default=0");
                if (settings.EnableAccountAvatar != d.EnableAccountAvatar)
                    GoldbergIniDocumentationHelper.AppendOptionWithExampleOrFallback(content, examplePath, "main::general", "enable_account_avatar", BoolToInt(settings.EnableAccountAvatar), cache, "1=enable avatar functionality", "default=0");
                if (settings.EnableVoiceChat != d.EnableVoiceChat)
                    GoldbergIniDocumentationHelper.AppendOptionWithExampleOrFallback(content, examplePath, "main::general", "enable_voice_chat", BoolToInt(settings.EnableVoiceChat), cache, "enable the experimental voice chat feature", "default=0");
                if (settings.ImmediateGameserverStats != d.ImmediateGameserverStats)
                    GoldbergIniDocumentationHelper.AppendOptionWithExampleOrFallback(content, examplePath, "main::general", "immediate_gameserver_stats", BoolToInt(settings.ImmediateGameserverStats), cache, "1=synchronize user stats/achievements with game servers as soon as possible instead of caching them until the next call to `Steam_RunCallbacks()`", "default=0");
                if (settings.MatchmakingServerListActualType != d.MatchmakingServerListActualType)
                    GoldbergIniDocumentationHelper.AppendOptionWithExampleOrFallback(content, examplePath, "main::general", "matchmaking_server_list_actual_type", BoolToInt(settings.MatchmakingServerListActualType), cache, "1=use the proper type of the server list (internet, friends, etc...) when requested by the game", "default=0");
                if (settings.MatchmakingServerDetailsViaSourceQuery != d.MatchmakingServerDetailsViaSourceQuery)
                    GoldbergIniDocumentationHelper.AppendOptionWithExampleOrFallback(content, examplePath, "main::general", "matchmaking_server_details_via_source_query", BoolToInt(settings.MatchmakingServerDetailsViaSourceQuery), cache, "1=grab the server details for match making using an actual server query", "default=0");

                GoldbergIniDocumentationHelper.AppendSection(content, "main::stats");
                if (settings.DisableLeaderboardsCreateUnknown != d.DisableLeaderboardsCreateUnknown)
                    GoldbergIniDocumentationHelper.AppendOptionWithExampleOrFallback(content, examplePath, "main::stats", "disable_leaderboards_create_unknown", BoolToInt(settings.DisableLeaderboardsCreateUnknown), cache, "1=prevent `Steam_User_Stats::FindLeaderboard()` from always succeeding and creating the unknown leaderboard", "default=0");
                if (settings.AllowUnknownStats != d.AllowUnknownStats)
                    GoldbergIniDocumentationHelper.AppendOptionWithExampleOrFallback(content, examplePath, "main::stats", "allow_unknown_stats", BoolToInt(settings.AllowUnknownStats), cache, "1=allow unknown stats to be saved/updated", "default=0");
                if (settings.StatAchievementProgressFunctionality != d.StatAchievementProgressFunctionality)
                    GoldbergIniDocumentationHelper.AppendOptionWithExampleOrFallback(content, examplePath, "main::stats", "stat_achievement_progress_functionality", BoolToInt(settings.StatAchievementProgressFunctionality), cache, "1=enable functionality that reports achievement progress when tied stats are updated", "default=1");
                if (settings.SaveOnlyHigherStatAchievementProgress != d.SaveOnlyHigherStatAchievementProgress)
                    GoldbergIniDocumentationHelper.AppendOptionWithExampleOrFallback(content, examplePath, "main::stats", "save_only_higher_stat_achievement_progress", BoolToInt(settings.SaveOnlyHigherStatAchievementProgress), cache, "1=save stat achievement progress value only if it is higher than the current one", "default=1");
                if (settings.PaginatedAchievementsIcons != d.PaginatedAchievementsIcons)
                    GoldbergIniDocumentationHelper.AppendOptionWithExampleOrFallback(content, examplePath, "main::stats", "paginated_achievements_icons", settings.PaginatedAchievementsIcons.ToString(), cache, "this value controls how many icons to load each iteration when callbacks are triggered", "default=10");
                if (settings.RecordPlaytime != d.RecordPlaytime)
                    GoldbergIniDocumentationHelper.AppendOptionWithExampleOrFallback(content, examplePath, "main::stats", "record_playtime", BoolToInt(settings.RecordPlaytime), cache, "1=enable the functionality that allows the emu to record the user's playtime", "default=0");

                GoldbergIniDocumentationHelper.AppendSection(content, "main::connectivity");
                if (settings.DisableLanOnly != d.DisableLanOnly)
                    GoldbergIniDocumentationHelper.AppendOptionWithExampleOrFallback(content, examplePath, "main::connectivity", "disable_lan_only", BoolToInt(settings.DisableLanOnly), cache, "1=prevent hooking OS networking APIs and allow external requests", "default=0");
                if (settings.DisableNetworking != d.DisableNetworking)
                    GoldbergIniDocumentationHelper.AppendOptionWithExampleOrFallback(content, examplePath, "main::connectivity", "disable_networking", BoolToInt(settings.DisableNetworking), cache, "1=disable all steam networking interface functionality", "default=0");
                if (settings.ListenPort != d.ListenPort)
                    GoldbergIniDocumentationHelper.AppendOptionWithExampleOrFallback(content, examplePath, "main::connectivity", "listen_port", settings.ListenPort.ToString(), cache, "change the UDP/TCP port the emulator listens on", "default=47584");
                if (settings.Offline != d.Offline)
                    GoldbergIniDocumentationHelper.AppendOptionWithExampleOrFallback(content, examplePath, "main::connectivity", "offline", BoolToInt(settings.Offline), cache, "1=pretend steam is running in offline mode", "default=0");
                if (settings.DisableSharingStatsWithGameserver != d.DisableSharingStatsWithGameserver)
                    GoldbergIniDocumentationHelper.AppendOptionWithExampleOrFallback(content, examplePath, "main::connectivity", "disable_sharing_stats_with_gameserver", BoolToInt(settings.DisableSharingStatsWithGameserver), cache, "1=prevent sharing stats/achievements with game servers", "default=0");
                if (settings.DisableSourceQuery != d.DisableSourceQuery)
                    GoldbergIniDocumentationHelper.AppendOptionWithExampleOrFallback(content, examplePath, "main::connectivity", "disable_source_query", BoolToInt(settings.DisableSourceQuery), cache, "1=do not send server details to server browser", "default=0");
                if (settings.ShareLeaderboardsOverNetwork != d.ShareLeaderboardsOverNetwork)
                    GoldbergIniDocumentationHelper.AppendOptionWithExampleOrFallback(content, examplePath, "main::connectivity", "share_leaderboards_over_network", BoolToInt(settings.ShareLeaderboardsOverNetwork), cache, "1=enable sharing leaderboard scores over local network", "default=0");
                if (settings.DisableLobbyCreation != d.DisableLobbyCreation)
                    GoldbergIniDocumentationHelper.AppendOptionWithExampleOrFallback(content, examplePath, "main::connectivity", "disable_lobby_creation", BoolToInt(settings.DisableLobbyCreation), cache, "1=prevent lobby creation in steam matchmaking interface", "default=0");
                if (settings.DownloadSteamhttpRequests != d.DownloadSteamhttpRequests)
                    GoldbergIniDocumentationHelper.AppendOptionWithExampleOrFallback(content, examplePath, "main::connectivity", "download_steamhttp_requests", BoolToInt(settings.DownloadSteamhttpRequests), cache, $"1=attempt to download external HTTP(S) requests made via Steam_HTTP::SendHTTPRequest() inside \"{PathConstants.SteamSettingsFolderName}/{PathConstants.GoldbergSteamSettingsHttpFolderName}/\"", "default=0");
                if (settings.OldP2PPacketSharingMode != d.OldP2PPacketSharingMode)
                    GoldbergIniDocumentationHelper.AppendOptionWithExampleOrFallback(content, examplePath, "main::connectivity", "old_p2p_packet_sharing_mode", settings.OldP2PPacketSharingMode.ToString(), cache, "legacy p2p packet sharing mode", "default=0");

                GoldbergIniDocumentationHelper.AppendSection(content, "main::misc");
                if (settings.AchievementsBypass != d.AchievementsBypass)
                    GoldbergIniDocumentationHelper.AppendOptionWithExampleOrFallback(content, examplePath, "main::misc", "achievements_bypass", BoolToInt(settings.AchievementsBypass), cache, "1=force ISteamUserStats::SetAchievement() to always return true", "default=0");
                if (settings.ForceSteamhttpSuccess != d.ForceSteamhttpSuccess)
                    GoldbergIniDocumentationHelper.AppendOptionWithExampleOrFallback(content, examplePath, "main::misc", "force_steamhttp_success", BoolToInt(settings.ForceSteamhttpSuccess), cache, "force the function `Steam_HTTP::SendHTTPRequest()` to always succeed", "default=0");
                if (settings.DisableSteamoverlaygameidEnvVar != d.DisableSteamoverlaygameidEnvVar)
                    GoldbergIniDocumentationHelper.AppendOptionWithExampleOrFallback(content, examplePath, "main::misc", "disable_steamoverlaygameid_env_var", BoolToInt(settings.DisableSteamoverlaygameidEnvVar), cache, "1=don't write SteamOverlayGameId env var, allowing Steam Input to work", "default=0");
                if (settings.EnableSteamPreownedIds != d.EnableSteamPreownedIds)
                    GoldbergIniDocumentationHelper.AppendOptionWithExampleOrFallback(content, examplePath, "main::misc", "enable_steam_preowned_ids", BoolToInt(settings.EnableSteamPreownedIds), cache, "1=add many Steam apps to the list of owned DLCs and the emu's list of installed app IDs", "default=0");
                if (!string.IsNullOrEmpty(settings.SteamGameStatsReportsDir))
                    GoldbergIniDocumentationHelper.AppendOptionWithExampleOrFallback(content, examplePath, "main::misc", "steam_game_stats_reports_dir", settings.SteamGameStatsReportsDir, cache, "save `ISteamGameStats` data to a folder", "default=");
                if (settings.FreeWeekend != d.FreeWeekend)
                    GoldbergIniDocumentationHelper.AppendOptionWithExampleOrFallback(content, examplePath, "main::misc", "free_weekend", BoolToInt(settings.FreeWeekend), cache, "some games may have extra bonuses/achievements when being or playing with a free-weekend player", "default=0");
                if (settings.Use32BitInventoryItemIds != d.Use32BitInventoryItemIds)
                    GoldbergIniDocumentationHelper.AppendOptionWithExampleOrFallback(content, examplePath, "main::misc", "use_32bit_inventory_item_ids", BoolToInt(settings.Use32BitInventoryItemIds), cache, "workaround for very old Team Fortress 2 versions to generate 32-bit item IDs", "default=0");

                File.WriteAllText(filePath, content.ToString());
                return SaveResult.Success(1);
            }
            catch (Exception ex)
            {
                return SaveResult.Failure($"Failed to save main settings: {ex.Message}");
            }
        }

        private SaveResult SaveAppSettings(string filePath, AppSettings settings)
        {
            try
            {
                // Load existing file to preserve DLC entries and app paths
                var existingLines = new List<string>();
                if (File.Exists(filePath))
                {
                    existingLines.AddRange(File.ReadAllLines(filePath));
                }

                var existingDlcEntries = ParseNumericSectionEntries(existingLines, "app::dlcs");
                var existingPathEntries = ParseSectionEntries(existingLines, "app::paths");
                var cache = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
                string examplePath = null;
                var content = new StringBuilder();
                GoldbergIniDocumentationHelper.AppendIniHeader(content);

                GoldbergIniDocumentationHelper.AppendSection(content, "app::general");
                if (settings.IsBetaBranch)
                    GoldbergIniDocumentationHelper.AppendOptionWithExampleOrFallback(content, examplePath, "app::general", "is_beta_branch", BoolToInt(settings.IsBetaBranch), cache, "by default the emu will report a `non-beta` branch when the game calls `Steam_Apps::GetCurrentBetaName()`", "1=make the game/app think we're playing on a beta branch", "default=0");
                if (!string.Equals(settings.BranchName, SteamPicsKeyNames.SteamDefaultBranchName, StringComparison.Ordinal))
                    GoldbergIniDocumentationHelper.AppendOptionWithExampleOrFallback(content, examplePath, "app::general", "branch_name", settings.BranchName, cache, $"the name of the current branch, this must also exist in {PathConstants.GoldbergBranchesJsonFileName}", "otherwise will be ignored by the emu and the default 'public' branch will be used", "default=public");

                GoldbergIniDocumentationHelper.AppendSection(content, "app::dlcs");
                GoldbergIniDocumentationHelper.AppendOptionWithExampleOrFallback(content, examplePath, "app::dlcs", "unlock_all", BoolToInt(settings.UnlockAllDLC), cache, "1=report all DLCs as unlocked", "0=report only the DLCs mentioned", "default=1");
                var afterUnlock = GoldbergIniDocumentationHelper.GetConsecutiveCommentLinesAfterKey(examplePath, "app::dlcs", "unlock_all", cache);
                if (afterUnlock != null && afterUnlock.Count > 0)
                {
                    foreach (var line in afterUnlock)
                        content.AppendLine(line);
                }
                else
                    content.AppendLine("# format: ID=name");

                foreach (var kvp in existingDlcEntries.OrderBy(x => x.Key))
                    content.AppendLine(kvp.Key + "=" + kvp.Value);

                if (existingPathEntries.Count > 0)
                {
                    GoldbergIniDocumentationHelper.AppendSection(content, "app::paths");
                    var pathHeader = GoldbergIniDocumentationHelper.GetSectionHeaderCommentLines(examplePath, "app::paths", cache);
                    if (pathHeader != null && pathHeader.Count > 0)
                    {
                        foreach (var line in pathHeader)
                            content.AppendLine(line);
                    }
                    else
                    {
                        content.AppendLine("# some rare games might need one or more paths to appids");
                        content.AppendLine("# this sets the paths returned by Steam_Apps::GetAppInstallDir");
                    }

                    foreach (var kvp in existingPathEntries.OrderBy(x => ParseSortNumericPrefix(x.Key)).ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
                        content.AppendLine(kvp.Key + "=" + kvp.Value);
                }

                AppendUnmanagedAppIniSections(content, existingLines);

                File.WriteAllText(filePath, content.ToString());
                return SaveResult.Success(1);
            }
            catch (Exception ex)
            {
                return SaveResult.Failure($"Failed to save app settings: {ex.Message}");
            }
        }

        private SaveResult SaveUserSettings(string filePath, UserSettings settings)
        {
            if (settings == null)
                return SaveResult.Failure("Settings cannot be null");

            // Save location lives only in the global configs.user.ini. Strip these keys here so
            // a per-game configs.user.ini never carries them, which is what triggered gbe_fork's
            // "local_save_path detected" warning and made it ignore the global settings folder.
            settings = new UserSettings
            {
                AccountName = settings.AccountName,
                AccountSteamId = settings.AccountSteamId,
                Ticket = settings.Ticket,
                AltSteamId = settings.AltSteamId,
                AltSteamIdCount = settings.AltSteamIdCount,
                Language = settings.Language,
                IpCountry = settings.IpCountry,
                ClanTag = settings.ClanTag,
                LocalSavePath = string.Empty,
                SavesFolderName = string.Empty
            };

            try
            {
                bool hasForceOverride = !string.IsNullOrEmpty(settings.AccountName) ||
                                         !string.IsNullOrEmpty(settings.AccountSteamId) ||
                                         !string.IsNullOrEmpty(settings.Language) ||
                                         (!string.IsNullOrEmpty(settings.IpCountry) && settings.IpCountry != ApplicationConstants.DefaultIpCountry);

                // If user launch options exist, never delete this file.
                bool hasExistingUserLaunchOptions = false;
                if (File.Exists(filePath))
                {
                    try
                    {
                        foreach (var line in File.ReadAllLines(filePath))
                        {
                            var trimmed = line.Trim();
                            if (trimmed.Equals("[user::launch_options]", StringComparison.OrdinalIgnoreCase))
                            {
                                hasExistingUserLaunchOptions = true;
                                break;
                            }
                        }
                    }
                    catch
                    {
                        // Best-effort only; ignore read/parse errors.
                    }
                }

                // If no force override fields are set, check if we should delete the file
                // Only delete if ALL fields (including non-force-override) are empty/default
                if (!hasForceOverride)
                {
                    bool hasOtherSettings = !string.IsNullOrEmpty(settings.Ticket) ||
                                          !string.IsNullOrEmpty(settings.AltSteamId) ||
                                          !string.IsNullOrEmpty(settings.ClanTag);

                    if (!hasOtherSettings)
                    {
                        // All fields are empty/default - delete the file to fall back to global settings
                        if (!hasExistingUserLaunchOptions && File.Exists(filePath))
                        {
                            File.Delete(filePath);
                        }
                        return SaveResult.Success(0); // File deleted, no content written (or preserved because user launch options exist)
                    }
                }

                var cache = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
                string examplePath = null;
                var content = new StringBuilder();
                GoldbergIniDocumentationHelper.AppendIniHeader(content);

                // Preserve any existing user launch options section.
                string existingUserLaunchOptionsSection = string.Empty;
                if (File.Exists(filePath))
                {
                    try
                    {
                        var lines = File.ReadAllLines(filePath);
                        bool inSection = false;
                        var sectionBuilder = new StringBuilder();
                        for (int i = 0; i < lines.Length; i++)
                        {
                            var trimmed = lines[i].Trim();
                            if (!inSection)
                            {
                                if (trimmed.Equals("[user::launch_options]", StringComparison.OrdinalIgnoreCase))
                                {
                                    inSection = true;
                                    sectionBuilder.AppendLine(lines[i]);
                                }
                            }
                            else
                            {
                                // Stop at next section header.
                                if (trimmed.StartsWith("[") && trimmed.EndsWith("]") && !trimmed.Equals("[user::launch_options]", StringComparison.OrdinalIgnoreCase))
                                    break;
                                sectionBuilder.AppendLine(lines[i]);
                            }
                        }
                        existingUserLaunchOptionsSection = sectionBuilder.ToString().TrimEnd();
                    }
                    catch
                    {
                        existingUserLaunchOptionsSection = string.Empty;
                    }
                }
                
                // [user::general]
                GoldbergIniDocumentationHelper.AppendSection(content, "user::general");
                if (!string.IsNullOrEmpty(settings.AccountName))
                    GoldbergIniDocumentationHelper.AppendOptionWithExampleOrFallback(content, examplePath, "user::general", "account_name", settings.AccountName, cache, "user account name", "default=gse orca");
                if (!string.IsNullOrEmpty(settings.AccountSteamId))
                    GoldbergIniDocumentationHelper.AppendOptionWithExampleOrFallback(content, examplePath, "user::general", "account_steamid", settings.AccountSteamId, cache, "your account ID in Steam64 format", "if the specified ID is invalid, the emu will ignore it and generate a proper one", "default=randomly generated by the emu only once and saved in the global settings");
                if (!string.IsNullOrEmpty(settings.Ticket))
                    GoldbergIniDocumentationHelper.AppendOptionWithExampleOrFallback(content, examplePath, "user::general", "ticket", settings.Ticket, cache, "Example Base64 Ticket.");
                if (!string.IsNullOrEmpty(settings.AltSteamId))
                    GoldbergIniDocumentationHelper.AppendOptionWithExampleOrFallback(content, examplePath, "user::general", "alt_steamid", settings.AltSteamId, cache, "Alt SteamId for encrypted savegames.");
                if (!string.IsNullOrEmpty(settings.AltSteamId) && settings.AltSteamIdCount != 5)
                    GoldbergIniDocumentationHelper.AppendOptionWithExampleOrFallback(content, examplePath, "user::general", "alt_steamid_count", settings.AltSteamIdCount.ToString(), cache, "How many calls before swapping out the SteamId to Alt", "default=5");
                // Always save language if explicitly set (not empty) - empty means "use global", any value means "override to this"
                if (!string.IsNullOrEmpty(settings.Language))
                    GoldbergIniDocumentationHelper.AppendOptionWithExampleOrFallback(content, examplePath, "user::general", "language", settings.Language, cache, "the language reported to the app/game", $"this must exist in '{PathConstants.GoldbergSupportedLanguagesFileName}', otherwise it will be ignored by the emu", $"look for the column 'API language code' here: {ApplicationConstants.SteamPartnerLocalizationLanguagesUrl}", "default=english");
                if (!string.IsNullOrEmpty(settings.IpCountry) && settings.IpCountry != "US")
                    GoldbergIniDocumentationHelper.AppendOptionWithExampleOrFallback(content, examplePath, "user::general", "ip_country", settings.IpCountry, cache, "report a country IP if the game queries it", $"ISO 3166-1-alpha-2 format, use this link to get the 'Alpha-2' country code: {ApplicationConstants.IbanCountryCodesUrl}", "default=US");
                if (!string.IsNullOrEmpty(settings.ClanTag))
                    GoldbergIniDocumentationHelper.AppendOptionWithExampleOrFallback(content, examplePath, "user::general", "clan_tag", settings.ClanTag, cache, "custom clan tag value");

                // [user::saves] is intentionally omitted in per-game configs.user.ini.
                // local_save_path and saves_folder_name are global-only (see SaveGlobalUserSettings).

                content.AppendLine();
                if (!string.IsNullOrWhiteSpace(existingUserLaunchOptionsSection))
                    content.AppendLine(existingUserLaunchOptionsSection);

                File.WriteAllText(filePath, content.ToString());
                _goldbergCfgService.StripPerGameSaveLocationKeys(filePath);
                return SaveResult.Success(1);
            }
            catch (Exception ex)
            {
                return SaveResult.Failure($"Failed to save user settings: {ex.Message}");
            }
        }

        private bool HasOverlayChanges(GameSettingsSnapshot snapshot, GameSettingsSnapshot original = null)
        {
            if (original == null)
                original = LoadGameSettingsSnapshot(snapshot.AppId);

            return snapshot.Overlay.EnableExperimentalOverlay != original.Overlay.EnableExperimentalOverlay ||
                   snapshot.Overlay.DisableAchievementNotification != original.Overlay.DisableAchievementNotification ||
                   snapshot.Overlay.DisableFriendNotification != original.Overlay.DisableFriendNotification;
        }

        private bool HasNetworkMainChanges(GameSettingsSnapshot snapshot, GameSettingsSnapshot original = null)
        {
            if (original == null)
                original = LoadGameSettingsSnapshot(snapshot.AppId);

            return !MainSettingsScopes.NetworkSlicesEqual(snapshot.Main, original.Main);
        }

        private bool HasStatsAchievementsMainChanges(GameSettingsSnapshot snapshot, GameSettingsSnapshot original = null)
        {
            if (original == null)
                original = LoadGameSettingsSnapshot(snapshot.AppId);

            return !MainSettingsScopes.StatsAchievementsSlicesEqual(snapshot.Main, original.Main);
        }

        private bool HasAppChanges(GameSettingsSnapshot snapshot, GameSettingsSnapshot original = null)
        {
            if (original == null)
                original = LoadGameSettingsSnapshot(snapshot.AppId);

            return snapshot.App.UnlockAllDLC != original.App.UnlockAllDLC ||
                   snapshot.App.BranchName != original.App.BranchName ||
                   snapshot.App.IsBetaBranch != original.App.IsBetaBranch;
        }

        private bool HasUserSettingsOverrides(UserSettings gameSpecific, UserSettings global)
        {
            if (gameSpecific == null || global == null)
                return false;

            Func<string, string, bool> differs = (gameVal, globalVal) =>
            {
                string gameNorm = string.IsNullOrEmpty(gameVal) ? string.Empty : gameVal;
                string globalNorm = string.IsNullOrEmpty(globalVal) ? string.Empty : globalVal;
                return !string.IsNullOrEmpty(gameNorm) && gameNorm != globalNorm;
            };

            // LocalSavePath / SavesFolderName intentionally excluded: they are global-only.
            return differs(gameSpecific.AccountName, global.AccountName) ||
                   differs(gameSpecific.AccountSteamId, global.AccountSteamId) ||
                   differs(gameSpecific.Language, global.Language) ||
                   differs(gameSpecific.IpCountry, global.IpCountry) ||
                   differs(gameSpecific.ClanTag, global.ClanTag) ||
                   differs(gameSpecific.Ticket, global.Ticket) ||
                   differs(gameSpecific.AltSteamId, global.AltSteamId) ||
                   (gameSpecific.AltSteamIdCount != global.AltSteamIdCount);
        }

        private bool HasUserChanges(GameSettingsSnapshot snapshot, GameSettingsSnapshot original = null)
        {
            if (original == null)
                original = LoadGameSettingsSnapshot(snapshot.AppId);

            return snapshot.User.AccountName != original.User.AccountName ||
                   snapshot.User.AccountSteamId != original.User.AccountSteamId ||
                   snapshot.User.Ticket != original.User.Ticket ||
                   snapshot.User.AltSteamId != original.User.AltSteamId ||
                   snapshot.User.AltSteamIdCount != original.User.AltSteamIdCount ||
                   snapshot.User.Language != original.User.Language ||
                   snapshot.User.IpCountry != original.User.IpCountry ||
                   snapshot.User.ClanTag != original.User.ClanTag;
        }

        private Dictionary<string, string> LoadAppPathsFromAppIni(string filePath)
        {
            var appPaths = new Dictionary<string, string>();
            if (!File.Exists(filePath))
                return appPaths;

            try
            {
                var lines = File.ReadAllLines(filePath);
                string currentSection = null;

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith(";") || trimmedLine.StartsWith("#"))
                        continue;

                    if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                    {
                        currentSection = trimmedLine.Substring(1, trimmedLine.Length - 2);
                        continue;
                    }

                    if (currentSection == "app::paths" && trimmedLine.Contains("="))
                    {
                        var parts = trimmedLine.Split(new[] { '=' }, 2);
                        if (parts.Length == 2)
                        {
                            var key = parts[0].Trim();
                            var value = parts[1].Trim();
                            appPaths[key] = value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogRedactionHelper.WriteDebug($"Failed to load app paths: {ex.Message}");
            }

            return appPaths;
        }

        private static readonly HashSet<string> ManagedAppIniSections = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "app::general",
            "app::dlcs",
            "app::paths"
        };

        private static void AppendUnmanagedAppIniSections(StringBuilder content, IList<string> existingLines)
        {
            if (content == null || existingLines == null || existingLines.Count == 0)
                return;

            string currentSection = null;
            var sectionLines = new List<string>();

            void FlushSection()
            {
                if (string.IsNullOrEmpty(currentSection) || ManagedAppIniSections.Contains(currentSection))
                {
                    sectionLines.Clear();
                    return;
                }

                if (sectionLines.Count == 0)
                    return;

                if (content.Length > 0 && content[content.Length - 1] != '\n')
                    content.AppendLine();
                foreach (string line in sectionLines)
                    content.AppendLine(line);
                sectionLines.Clear();
            }

            foreach (string line in existingLines)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    FlushSection();
                    currentSection = trimmed.Substring(1, trimmed.Length - 2);
                    sectionLines.Add(line);
                    continue;
                }

                if (!string.IsNullOrEmpty(currentSection) && !ManagedAppIniSections.Contains(currentSection))
                    sectionLines.Add(line);
            }

            FlushSection();
        }

        private static Dictionary<long, string> ParseNumericSectionEntries(IEnumerable<string> lines, string sectionName)
        {
            var result = new Dictionary<long, string>();
            bool inSection = false;
            foreach (var line in lines ?? Enumerable.Empty<string>())
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    inSection = string.Equals(trimmed.Substring(1, trimmed.Length - 2), sectionName, StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (!inSection || string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";") || trimmed.StartsWith("#"))
                    continue;

                var parts = trimmed.Split(new[] { '=' }, 2);
                if (parts.Length != 2)
                    continue;

                var key = parts[0].Trim();
                if (string.Equals(key, "unlock_all", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (long.TryParse(key, out var parsed))
                    result[parsed] = parts[1].Trim();
            }

            return result;
        }

        private static Dictionary<string, string> ParseSectionEntries(IEnumerable<string> lines, string sectionName)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            bool inSection = false;
            foreach (var line in lines ?? Enumerable.Empty<string>())
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                {
                    inSection = string.Equals(trimmed.Substring(1, trimmed.Length - 2), sectionName, StringComparison.OrdinalIgnoreCase);
                    continue;
                }

                if (!inSection || string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";") || trimmed.StartsWith("#"))
                    continue;

                var parts = trimmed.Split(new[] { '=' }, 2);
                if (parts.Length != 2)
                    continue;

                result[parts[0].Trim()] = parts[1].Trim();
            }

            return result;
        }

        private static long ParseSortNumericPrefix(string value)
        {
            if (long.TryParse(value, out var parsed))
                return parsed;
            return long.MaxValue;
        }

        private static string BoolToInt(bool value) => value ? "1" : "0";

        private void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var fileName = Path.GetFileName(file);
                var destFile = Path.Combine(destDir, fileName);
                File.Copy(file, destFile, true);
            }

            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                var dirName = Path.GetFileName(subDir);
                var destSubDir = Path.Combine(destDir, dirName);
                CopyDirectory(subDir, destSubDir);
            }
        }
    }
}
