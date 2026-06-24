using System;
using System.Diagnostics;
using System.IO;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Services
{
    public class GoldbergCfgService
    {
        private readonly string _globalSettingsPath;
        private readonly IniFileService _iniService;

        public GoldbergCfgService()
            : this(PathConstants.GlobalSettingsPath)
        {
        }

        public GoldbergCfgService(string globalSettingsPath)
        {
            _globalSettingsPath = globalSettingsPath;
            _iniService = ServiceLocator.IniFileService;
        }

        public OverlaySettings LoadGlobalOverlaySettings()
        {
            return LoadOverlaySettingsFromPath(Path.Combine(_globalSettingsPath, PathConstants.GoldbergOverlayIniFileName));
        }

        public OverlaySettings LoadOverlaySettingsFromPath(string filePath)
        {
            return LoadIniFile(filePath, "overlay", new OverlaySettings(), ParseOverlaySetting);
        }

        public MainSettings LoadGlobalMainSettings()
        {
            return LoadGlobalIni(PathConstants.GoldbergMainIniFileName, "main", new MainSettings(), ParseMainSetting);
        }

        public UserSettings LoadGlobalUserSettings()
        {
            return LoadGlobalIni(PathConstants.GoldbergUserIniFileName, "user", new UserSettings(), ParseUserSetting);
        }

        public SaveResult SaveGlobalOverlaySettings(OverlaySettings settings)
        {
            if (settings == null)
                return SaveResult.Failure("Settings cannot be null");

            return TrySaveIni(PathConstants.GoldbergOverlayIniFileName, "Failed to save overlay settings", ini => ApplyOverlayToIni(ini, settings));
        }

        public SaveResult SaveGlobalMainSettings(MainSettings settings)
        {
            if (settings == null)
                return SaveResult.Failure("Settings cannot be null");

            return TrySaveIni(PathConstants.GoldbergMainIniFileName, "Failed to save main settings", ini =>
            {
                ApplyGlobalMainToIni(ini, settings);
                StripNetworkMainKeysFromIni(ini);
            });
        }

        public SaveResult SaveGlobalUserSettings(UserSettings settings)
        {
            if (settings == null)
                return SaveResult.Failure("Settings cannot be null");

            return TrySaveIni(PathConstants.GoldbergUserIniFileName, "Failed to save user settings", ini => ApplyUserToIni(ini, settings));
        }

        public UserSettings LoadUserSettingsFromPath(string filePath)
        {
            return LoadIniFile(filePath, "user", new UserSettings(), ParseUserSetting);
        }

        public SaveResult StripPerGameSaveLocationKeys(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return SaveResult.Success(0);

            try
            {
                if (!UserIniSaveLocationHelper.FileContainsSaveLocationKeys(filePath, _iniService))
                    return SaveResult.Success(0);

                if (!UserIniSaveLocationHelper.TryRemoveSaveLocationKeysFromFile(filePath, _iniService))
                    return SaveResult.Failure("Failed to strip per-game save location keys.");

                return SaveResult.Success(1);
            }
            catch (Exception ex)
            {
                return SaveResult.Failure("Failed to strip per-game save location keys: " + ex.Message);
            }
        }

        private T LoadGlobalIni<T>(string fileName, string kindForLog, T settings, Action<T, string, string, string> onKeyValue)
            where T : class
        {
            return LoadIniFile(Path.Combine(_globalSettingsPath, fileName), kindForLog, settings, onKeyValue);
        }

        private T LoadIniFile<T>(string path, string kindForLog, T settings, Action<T, string, string, string> onKeyValue)
            where T : class
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return settings;

            try
            {
                var iniFile = _iniService.ParseFile(path);
                foreach (var line in iniFile.Lines)
                {
                    if (line.Type == IniLineType.KeyValue)
                        onKeyValue(settings, line.Section, line.Key, line.Value);
                }
            }
            catch (Exception ex)
            {
                LogRedactionHelper.WriteDebug("Failed to load " + kindForLog + " settings: " + ex.Message);
            }

            return settings;
        }

        private SaveResult TrySaveIni(string fileName, string failurePrefix, Action<IniFile> mutate)
        {
            try
            {
                Directory.CreateDirectory(_globalSettingsPath);
                var filePath = Path.Combine(_globalSettingsPath, fileName);
                var iniFile = _iniService.ParseFile(filePath);
                mutate(iniFile);
                GoldbergIniDocumentationHelper.WriteIniFile(filePath, iniFile.Lines);
                return SaveResult.Success(1);
            }
            catch (Exception ex)
            {
                return SaveResult.Failure(failurePrefix + ": " + ex.Message);
            }
        }

        private void ApplyOverlayToIni(IniFile iniFile, OverlaySettings settings)
        {
            var d = new OverlaySettings();

            SetBoolIfNotDefault(iniFile, "overlay::general", "enable_experimental_overlay", settings.EnableExperimentalOverlay, d.EnableExperimentalOverlay);
            SetIntIfNotDefault(iniFile, "overlay::general", "hook_delay_sec", settings.HookDelaySec, d.HookDelaySec);
            SetIntIfNotDefault(iniFile, "overlay::general", "renderer_detector_timeout_sec", settings.RendererDetectorTimeoutSec, d.RendererDetectorTimeoutSec);
            SetBoolIfNotDefault(iniFile, "overlay::general", "disable_achievement_notification", settings.DisableAchievementNotification, d.DisableAchievementNotification);
            SetBoolIfNotDefault(iniFile, "overlay::general", "disable_friend_notification", settings.DisableFriendNotification, d.DisableFriendNotification);
            SetBoolIfNotDefault(iniFile, "overlay::general", "disable_achievement_progress", settings.DisableAchievementProgress, d.DisableAchievementProgress);
            SetBoolIfNotDefault(iniFile, "overlay::general", "disable_warning_any", settings.DisableWarningAny, d.DisableWarningAny);
            SetBoolIfNotDefault(iniFile, "overlay::general", "disable_warning_bad_appid", settings.DisableWarningBadAppId, d.DisableWarningBadAppId);
            SetBoolIfNotDefault(iniFile, "overlay::general", "disable_warning_local_save", settings.DisableWarningLocalSave, d.DisableWarningLocalSave);
            SetBoolIfNotDefault(iniFile, "overlay::general", "upload_achievements_icons_to_gpu", settings.UploadAchievementsIconsToGpu, d.UploadAchievementsIconsToGpu);
            SetIntIfNotDefault(iniFile, "overlay::general", "fps_averaging_window", settings.FpsAveragingWindow, d.FpsAveragingWindow);
            SetBoolIfNotDefault(iniFile, "overlay::general", "overlay_always_show_user_info", settings.OverlayAlwaysShowUserInfo, d.OverlayAlwaysShowUserInfo);
            SetBoolIfNotDefault(iniFile, "overlay::general", "overlay_always_show_fps", settings.OverlayAlwaysShowFps, d.OverlayAlwaysShowFps);
            SetBoolIfNotDefault(iniFile, "overlay::general", "overlay_always_show_frametime", settings.OverlayAlwaysShowFrametime, d.OverlayAlwaysShowFrametime);
            SetBoolIfNotDefault(iniFile, "overlay::general", "overlay_always_show_playtime", settings.OverlayAlwaysShowPlaytime, d.OverlayAlwaysShowPlaytime);

            _iniService.SetValue(iniFile, "overlay::appearance", "Font_Override", settings.FontOverride, skipIfDefault: true);
            SetFloatIfNotDefault(iniFile, "overlay::appearance", "Font_Size", settings.FontSize, d.FontSize);
            SetFloatIfNotDefault(iniFile, "overlay::appearance", "Icon_Size", settings.IconSize, d.IconSize);
            SetFloatIfNotDefault(iniFile, "overlay::appearance", "Font_Glyph_Extra_Spacing_x", settings.FontGlyphExtraSpacingX, d.FontGlyphExtraSpacingX);
            SetFloatIfNotDefault(iniFile, "overlay::appearance", "Font_Glyph_Extra_Spacing_y", settings.FontGlyphExtraSpacingY, d.FontGlyphExtraSpacingY);

            SetFloatIfNotDefault(iniFile, "overlay::appearance", "Notification_R", settings.NotificationR, d.NotificationR);
            SetFloatIfNotDefault(iniFile, "overlay::appearance", "Notification_G", settings.NotificationG, d.NotificationG);
            SetFloatIfNotDefault(iniFile, "overlay::appearance", "Notification_B", settings.NotificationB, d.NotificationB);
            SetFloatIfNotDefault(iniFile, "overlay::appearance", "Notification_A", settings.NotificationA, d.NotificationA);

            SetFloatIfNotDefault(iniFile, "overlay::appearance", "Notification_Rounding", settings.NotificationRounding, d.NotificationRounding);
            SetFloatIfNotDefault(iniFile, "overlay::appearance", "Notification_Margin_x", settings.NotificationMarginX, d.NotificationMarginX);
            SetFloatIfNotDefault(iniFile, "overlay::appearance", "Notification_Margin_y", settings.NotificationMarginY, d.NotificationMarginY);

            SetFloatIfNotDefault(iniFile, "overlay::appearance", "Notification_Animation", settings.NotificationAnimation, d.NotificationAnimation);
            SetFloatIfNotDefault(iniFile, "overlay::appearance", "Notification_Duration_Progress", settings.NotificationDurationProgress, d.NotificationDurationProgress);
            SetFloatIfNotDefault(iniFile, "overlay::appearance", "Notification_Duration_Achievement", settings.NotificationDurationAchievement, d.NotificationDurationAchievement);
            SetFloatIfNotDefault(iniFile, "overlay::appearance", "Notification_Duration_Invitation", settings.NotificationDurationInvitation, d.NotificationDurationInvitation);
            SetFloatIfNotDefault(iniFile, "overlay::appearance", "Notification_Duration_Chat", settings.NotificationDurationChat, d.NotificationDurationChat);

            SetValueIfNotDefault(iniFile, "overlay::appearance", "Achievement_Unlock_Datetime_Format", settings.AchievementUnlockDatetimeFormat, d.AchievementUnlockDatetimeFormat);

            SetFloatIfNotDefault(iniFile, "overlay::appearance", "Background_R", settings.BackgroundR, d.BackgroundR);
            SetFloatIfNotDefault(iniFile, "overlay::appearance", "Background_G", settings.BackgroundG, d.BackgroundG);
            SetFloatIfNotDefault(iniFile, "overlay::appearance", "Background_B", settings.BackgroundB, d.BackgroundB);
            SetFloatIfNotDefault(iniFile, "overlay::appearance", "Background_A", settings.BackgroundA, d.BackgroundA);

            SetFloatIfNotDefault(iniFile, "overlay::appearance", "Element_R", settings.ElementR, d.ElementR);
            SetFloatIfNotDefault(iniFile, "overlay::appearance", "Element_G", settings.ElementG, d.ElementG);
            SetFloatIfNotDefault(iniFile, "overlay::appearance", "Element_B", settings.ElementB, d.ElementB);
            SetFloatIfNotDefault(iniFile, "overlay::appearance", "Element_A", settings.ElementA, d.ElementA);

            SetFloatIfNotDefault(iniFile, "overlay::appearance", "ElementHovered_R", settings.ElementHoveredR, d.ElementHoveredR);
            SetFloatIfNotDefault(iniFile, "overlay::appearance", "ElementHovered_G", settings.ElementHoveredG, d.ElementHoveredG);
            SetFloatIfNotDefault(iniFile, "overlay::appearance", "ElementHovered_B", settings.ElementHoveredB, d.ElementHoveredB);
            SetFloatIfNotDefault(iniFile, "overlay::appearance", "ElementHovered_A", settings.ElementHoveredA, d.ElementHoveredA);

            SetFloatIfNotDefault(iniFile, "overlay::appearance", "ElementActive_R", settings.ElementActiveR, d.ElementActiveR);
            SetFloatIfNotDefault(iniFile, "overlay::appearance", "ElementActive_G", settings.ElementActiveG, d.ElementActiveG);
            SetFloatIfNotDefault(iniFile, "overlay::appearance", "ElementActive_B", settings.ElementActiveB, d.ElementActiveB);
            SetFloatIfNotDefault(iniFile, "overlay::appearance", "ElementActive_A", settings.ElementActiveA, d.ElementActiveA);

            SetValueIfNotDefault(iniFile, "overlay::appearance", "PosAchievement", settings.PosAchievement, d.PosAchievement);
            SetValueIfNotDefault(iniFile, "overlay::appearance", "PosInvitation", settings.PosInvitation, d.PosInvitation);
            SetValueIfNotDefault(iniFile, "overlay::appearance", "PosChatMsg", settings.PosChatMsg, d.PosChatMsg);

            _iniService.SetValue(iniFile, "overlay::appearance", "Font_Override_Achievement_Title", settings.FontOverrideAchievementTitle, skipIfDefault: true);
            _iniService.SetValue(iniFile, "overlay::appearance", "Font_Override_Achievement_Description", settings.FontOverrideAchievementDescription, skipIfDefault: true);
            if (settings.FontSizeFps > 0)
                SetFloatIfNotDefault(iniFile, "overlay::appearance", "Font_Size_FPS", settings.FontSizeFps, 0);
            if (settings.FontSizeAchievementTitle > 0)
                SetFloatIfNotDefault(iniFile, "overlay::appearance", "Font_Size_Achievement_Title", settings.FontSizeAchievementTitle, 0);
            if (settings.FontSizeAchievementDescription > 0)
                SetFloatIfNotDefault(iniFile, "overlay::appearance", "Font_Size_Achievement_Description", settings.FontSizeAchievementDescription, 0);
            SetBoolIfNotDefault(iniFile, "overlay::appearance", "Font_Achievement_Title_Bold", settings.FontAchievementTitleBold, d.FontAchievementTitleBold);

            SetFloatIfNotDefault(iniFile, "overlay::appearance", "Stats_Background_R", settings.StatsBackgroundR, d.StatsBackgroundR);
            SetFloatIfNotDefault(iniFile, "overlay::appearance", "Stats_Background_G", settings.StatsBackgroundG, d.StatsBackgroundG);
            SetFloatIfNotDefault(iniFile, "overlay::appearance", "Stats_Background_B", settings.StatsBackgroundB, d.StatsBackgroundB);
            SetFloatIfNotDefault(iniFile, "overlay::appearance", "Stats_Background_A", settings.StatsBackgroundA, d.StatsBackgroundA);

            SetFloatIfNotDefault(iniFile, "overlay::appearance", "Stats_Text_R", settings.StatsTextR, d.StatsTextR);
            SetFloatIfNotDefault(iniFile, "overlay::appearance", "Stats_Text_G", settings.StatsTextG, d.StatsTextG);
            SetFloatIfNotDefault(iniFile, "overlay::appearance", "Stats_Text_B", settings.StatsTextB, d.StatsTextB);
            SetFloatIfNotDefault(iniFile, "overlay::appearance", "Stats_Text_A", settings.StatsTextA, d.StatsTextA);

            SetFloatIfNotDefault(iniFile, "overlay::appearance", "Stats_Pos_x", settings.StatsPosX, d.StatsPosX);
            SetFloatIfNotDefault(iniFile, "overlay::appearance", "Stats_Pos_y", settings.StatsPosY, d.StatsPosY);
        }

        public void ApplyStatsAchievementsMainToIni(IniFile iniFile, MainSettings settings)
        {
            if (iniFile == null || settings == null)
                return;

            var d = new MainSettings();

            SetBoolIfNotDefault(iniFile, "main::stats", "disable_leaderboards_create_unknown", settings.DisableLeaderboardsCreateUnknown, d.DisableLeaderboardsCreateUnknown);
            SetBoolIfNotDefault(iniFile, "main::stats", "allow_unknown_stats", settings.AllowUnknownStats, d.AllowUnknownStats);
            SetBoolIfNotDefault(iniFile, "main::stats", "stat_achievement_progress_functionality", settings.StatAchievementProgressFunctionality, d.StatAchievementProgressFunctionality);
            SetBoolIfNotDefault(iniFile, "main::stats", "save_only_higher_stat_achievement_progress", settings.SaveOnlyHigherStatAchievementProgress, d.SaveOnlyHigherStatAchievementProgress);
            SetIntIfNotDefault(iniFile, "main::stats", "paginated_achievements_icons", settings.PaginatedAchievementsIcons, d.PaginatedAchievementsIcons);
            SetBoolIfNotDefault(iniFile, "main::stats", "record_playtime", settings.RecordPlaytime, d.RecordPlaytime);

            SetBoolIfNotDefault(iniFile, "main::misc", "achievements_bypass", settings.AchievementsBypass, d.AchievementsBypass);
            _iniService.SetValue(iniFile, "main::misc", "steam_game_stats_reports_dir", settings.SteamGameStatsReportsDir, skipIfDefault: true);
        }

        public void ApplyNetworkMainToIni(IniFile iniFile, MainSettings settings)
        {
            if (iniFile == null || settings == null)
                return;

            var d = new MainSettings();
            SetBoolIfNotDefault(iniFile, "main::general", "block_unknown_clients", settings.BlockUnknownClients, d.BlockUnknownClients);
            SetBoolIfNotDefault(iniFile, "main::general", "immediate_gameserver_stats", settings.ImmediateGameserverStats, d.ImmediateGameserverStats);
            SetBoolIfNotDefault(iniFile, "main::general", "matchmaking_server_list_actual_type", settings.MatchmakingServerListActualType, d.MatchmakingServerListActualType);
            SetBoolIfNotDefault(iniFile, "main::general", "matchmaking_server_details_via_source_query", settings.MatchmakingServerDetailsViaSourceQuery, d.MatchmakingServerDetailsViaSourceQuery);

            SetBoolIfNotDefault(iniFile, "main::connectivity", "disable_lan_only", settings.DisableLanOnly, d.DisableLanOnly);
            SetBoolIfNotDefault(iniFile, "main::connectivity", "disable_networking", settings.DisableNetworking, d.DisableNetworking);
            SetIntIfNotDefault(iniFile, "main::connectivity", "listen_port", settings.ListenPort, d.ListenPort);
            SetBoolIfNotDefault(iniFile, "main::connectivity", "offline", settings.Offline, d.Offline);
            SetBoolIfNotDefault(iniFile, "main::connectivity", "disable_sharing_stats_with_gameserver", settings.DisableSharingStatsWithGameserver, d.DisableSharingStatsWithGameserver);
            SetBoolIfNotDefault(iniFile, "main::connectivity", "disable_source_query", settings.DisableSourceQuery, d.DisableSourceQuery);
            SetBoolIfNotDefault(iniFile, "main::connectivity", "share_leaderboards_over_network", settings.ShareLeaderboardsOverNetwork, d.ShareLeaderboardsOverNetwork);
            SetBoolIfNotDefault(iniFile, "main::connectivity", "disable_lobby_creation", settings.DisableLobbyCreation, d.DisableLobbyCreation);
            SetBoolIfNotDefault(iniFile, "main::connectivity", "download_steamhttp_requests", settings.DownloadSteamhttpRequests, d.DownloadSteamhttpRequests);
            SetIntIfNotDefault(iniFile, "main::connectivity", "old_p2p_packet_sharing_mode", settings.OldP2PPacketSharingMode, d.OldP2PPacketSharingMode);
        }

        private void ApplyGlobalMainToIni(IniFile iniFile, MainSettings settings)
        {
            var d = new MainSettings();

            SetBoolIfNotDefault(iniFile, "main::general", "new_app_ticket", settings.NewAppTicket, d.NewAppTicket);
            SetBoolIfNotDefault(iniFile, "main::general", "gc_token", settings.GcToken, d.GcToken);
            SetBoolIfNotDefault(iniFile, "main::general", "steam_deck", settings.SteamDeck, d.SteamDeck);
            SetBoolIfNotDefault(iniFile, "main::general", "enable_account_avatar", settings.EnableAccountAvatar, d.EnableAccountAvatar);
            SetBoolIfNotDefault(iniFile, "main::general", "enable_voice_chat", settings.EnableVoiceChat, d.EnableVoiceChat);
            _iniService.RemoveValue(iniFile, "main::general", "crash_printer_location");

            SetBoolIfNotDefault(iniFile, "main::stats", "disable_leaderboards_create_unknown", settings.DisableLeaderboardsCreateUnknown, d.DisableLeaderboardsCreateUnknown);
            SetBoolIfNotDefault(iniFile, "main::stats", "allow_unknown_stats", settings.AllowUnknownStats, d.AllowUnknownStats);
            SetBoolIfNotDefault(iniFile, "main::stats", "stat_achievement_progress_functionality", settings.StatAchievementProgressFunctionality, d.StatAchievementProgressFunctionality);
            SetBoolIfNotDefault(iniFile, "main::stats", "save_only_higher_stat_achievement_progress", settings.SaveOnlyHigherStatAchievementProgress, d.SaveOnlyHigherStatAchievementProgress);
            SetIntIfNotDefault(iniFile, "main::stats", "paginated_achievements_icons", settings.PaginatedAchievementsIcons, d.PaginatedAchievementsIcons);
            SetBoolIfNotDefault(iniFile, "main::stats", "record_playtime", settings.RecordPlaytime, d.RecordPlaytime);

            SetBoolIfNotDefault(iniFile, "main::misc", "achievements_bypass", settings.AchievementsBypass, d.AchievementsBypass);
            SetBoolIfNotDefault(iniFile, "main::misc", "force_steamhttp_success", settings.ForceSteamhttpSuccess, d.ForceSteamhttpSuccess);
            SetBoolIfNotDefault(iniFile, "main::misc", "disable_steamoverlaygameid_env_var", settings.DisableSteamoverlaygameidEnvVar, d.DisableSteamoverlaygameidEnvVar);
            SetBoolIfNotDefault(iniFile, "main::misc", "enable_steam_preowned_ids", settings.EnableSteamPreownedIds, d.EnableSteamPreownedIds);
            _iniService.SetValue(iniFile, "main::misc", "steam_game_stats_reports_dir", settings.SteamGameStatsReportsDir, skipIfDefault: true);
            SetBoolIfNotDefault(iniFile, "main::misc", "free_weekend", settings.FreeWeekend, d.FreeWeekend);
            SetBoolIfNotDefault(iniFile, "main::misc", "use_32bit_inventory_item_ids", settings.Use32BitInventoryItemIds, d.Use32BitInventoryItemIds);
        }

        private void StripNetworkMainKeysFromIni(IniFile iniFile)
        {
            if (iniFile == null)
                return;

            _iniService.RemoveValue(iniFile, "main::general", "block_unknown_clients");
            _iniService.RemoveValue(iniFile, "main::general", "immediate_gameserver_stats");
            _iniService.RemoveValue(iniFile, "main::general", "matchmaking_server_list_actual_type");
            _iniService.RemoveValue(iniFile, "main::general", "matchmaking_server_details_via_source_query");

            foreach (var key in new[]
            {
                "disable_lan_only", "disable_networking", "listen_port", "offline",
                "disable_sharing_stats_with_gameserver", "disable_source_query",
                "share_leaderboards_over_network", "disable_lobby_creation",
                "download_steamhttp_requests", "old_p2p_packet_sharing_mode"
            })
                _iniService.RemoveValue(iniFile, "main::connectivity", key);
        }

        private void ApplyUserToIni(IniFile iniFile, UserSettings settings)
        {
            var d = new UserSettings();

            _iniService.SetValue(iniFile, "user::general", "account_name", settings.AccountName);
            _iniService.SetValue(iniFile, "user::general", "account_steamid", settings.AccountSteamId);

            _iniService.SetValue(iniFile, "user::general", "ticket", settings.Ticket, skipIfDefault: true);
            _iniService.SetValue(iniFile, "user::general", "alt_steamid", settings.AltSteamId, skipIfDefault: true);
            if (!string.IsNullOrEmpty(settings.AltSteamId))
                SetIntIfNotDefault(iniFile, "user::general", "alt_steamid_count", settings.AltSteamIdCount, d.AltSteamIdCount);

            SetValueIfNotDefault(iniFile, "user::general", "language", settings.Language, d.Language);
            SetValueIfNotDefault(iniFile, "user::general", "ip_country", settings.IpCountry, d.IpCountry);

            UserIniSaveLocationHelper.ApplySaveLocationToIni(_iniService, iniFile, settings);
        }

        private void ParseOverlaySetting(OverlaySettings settings, string section, string key, string value)
        {
            if (section == "overlay::general")
            {
                switch (key)
                {
                    case "enable_experimental_overlay":
                        settings.EnableExperimentalOverlay = IniParseHelper.StringToBool(value);
                        break;
                    case "hook_delay_sec":
                        settings.HookDelaySec = IniParseHelper.ParseInt(value);
                        break;
                    case "renderer_detector_timeout_sec":
                        settings.RendererDetectorTimeoutSec = IniParseHelper.ParseInt(value);
                        break;
                    case "disable_achievement_notification":
                        settings.DisableAchievementNotification = IniParseHelper.StringToBool(value);
                        break;
                    case "disable_friend_notification":
                        settings.DisableFriendNotification = IniParseHelper.StringToBool(value);
                        break;
                    case "disable_achievement_progress":
                        settings.DisableAchievementProgress = IniParseHelper.StringToBool(value);
                        break;
                    case "disable_warning_any":
                        settings.DisableWarningAny = IniParseHelper.StringToBool(value);
                        break;
                    case "disable_warning_bad_appid":
                        settings.DisableWarningBadAppId = IniParseHelper.StringToBool(value);
                        break;
                    case "disable_warning_local_save":
                        settings.DisableWarningLocalSave = IniParseHelper.StringToBool(value);
                        break;
                    case "upload_achievements_icons_to_gpu":
                        settings.UploadAchievementsIconsToGpu = IniParseHelper.StringToBool(value);
                        break;
                    case "fps_averaging_window":
                        settings.FpsAveragingWindow = IniParseHelper.ParseInt(value);
                        break;
                    case "overlay_always_show_user_info":
                        settings.OverlayAlwaysShowUserInfo = IniParseHelper.StringToBool(value);
                        break;
                    case "overlay_always_show_fps":
                        settings.OverlayAlwaysShowFps = IniParseHelper.StringToBool(value);
                        break;
                    case "overlay_always_show_frametime":
                        settings.OverlayAlwaysShowFrametime = IniParseHelper.StringToBool(value);
                        break;
                    case "overlay_always_show_playtime":
                        settings.OverlayAlwaysShowPlaytime = IniParseHelper.StringToBool(value);
                        break;
                }
            }
            else if (section == "overlay::appearance")
            {
                switch (key)
                {
                    case "Font_Override":
                        settings.FontOverride = value;
                        break;
                    case "Font_Size":
                        settings.FontSize = IniParseHelper.ParseFloat(value);
                        break;
                    case "Icon_Size":
                        settings.IconSize = IniParseHelper.ParseFloat(value);
                        break;
                    case "Font_Glyph_Extra_Spacing_x":
                        settings.FontGlyphExtraSpacingX = IniParseHelper.ParseFloat(value);
                        break;
                    case "Font_Glyph_Extra_Spacing_y":
                        settings.FontGlyphExtraSpacingY = IniParseHelper.ParseFloat(value);
                        break;
                    case "Notification_R":
                        settings.NotificationR = IniParseHelper.ParseFloat(value);
                        break;
                    case "Notification_G":
                        settings.NotificationG = IniParseHelper.ParseFloat(value);
                        break;
                    case "Notification_B":
                        settings.NotificationB = IniParseHelper.ParseFloat(value);
                        break;
                    case "Notification_A":
                        settings.NotificationA = IniParseHelper.ParseFloat(value);
                        break;
                    case "Notification_Rounding":
                        settings.NotificationRounding = IniParseHelper.ParseFloat(value);
                        break;
                    case "Notification_Margin_x":
                        settings.NotificationMarginX = IniParseHelper.ParseFloat(value);
                        break;
                    case "Notification_Margin_y":
                        settings.NotificationMarginY = IniParseHelper.ParseFloat(value);
                        break;
                    case "Notification_Animation":
                        settings.NotificationAnimation = IniParseHelper.ParseFloat(value);
                        break;
                    case "Notification_Duration_Progress":
                        settings.NotificationDurationProgress = IniParseHelper.ParseFloat(value);
                        break;
                    case "Notification_Duration_Achievement":
                        settings.NotificationDurationAchievement = IniParseHelper.ParseFloat(value);
                        break;
                    case "Notification_Duration_Invitation":
                        settings.NotificationDurationInvitation = IniParseHelper.ParseFloat(value);
                        break;
                    case "Notification_Duration_Chat":
                        settings.NotificationDurationChat = IniParseHelper.ParseFloat(value);
                        break;
                    case "Achievement_Unlock_Datetime_Format":
                        settings.AchievementUnlockDatetimeFormat = value;
                        break;
                    case "Background_R":
                        settings.BackgroundR = IniParseHelper.ParseFloat(value);
                        break;
                    case "Background_G":
                        settings.BackgroundG = IniParseHelper.ParseFloat(value);
                        break;
                    case "Background_B":
                        settings.BackgroundB = IniParseHelper.ParseFloat(value);
                        break;
                    case "Background_A":
                        settings.BackgroundA = IniParseHelper.ParseFloat(value);
                        break;
                    case "Element_R":
                        settings.ElementR = IniParseHelper.ParseFloat(value);
                        break;
                    case "Element_G":
                        settings.ElementG = IniParseHelper.ParseFloat(value);
                        break;
                    case "Element_B":
                        settings.ElementB = IniParseHelper.ParseFloat(value);
                        break;
                    case "Element_A":
                        settings.ElementA = IniParseHelper.ParseFloat(value);
                        break;
                    case "ElementHovered_R":
                        settings.ElementHoveredR = IniParseHelper.ParseFloat(value);
                        break;
                    case "ElementHovered_G":
                        settings.ElementHoveredG = IniParseHelper.ParseFloat(value);
                        break;
                    case "ElementHovered_B":
                        settings.ElementHoveredB = IniParseHelper.ParseFloat(value);
                        break;
                    case "ElementHovered_A":
                        settings.ElementHoveredA = IniParseHelper.ParseFloat(value);
                        break;
                    case "ElementActive_R":
                        settings.ElementActiveR = IniParseHelper.ParseFloat(value);
                        break;
                    case "ElementActive_G":
                        settings.ElementActiveG = IniParseHelper.ParseFloat(value);
                        break;
                    case "ElementActive_B":
                        settings.ElementActiveB = IniParseHelper.ParseFloat(value);
                        break;
                    case "ElementActive_A":
                        settings.ElementActiveA = IniParseHelper.ParseFloat(value);
                        break;
                    case "PosAchievement":
                        settings.PosAchievement = value;
                        break;
                    case "PosInvitation":
                        settings.PosInvitation = value;
                        break;
                    case "PosChatMsg":
                        settings.PosChatMsg = value;
                        break;
                    case "Font_Override_Achievement_Title":
                        settings.FontOverrideAchievementTitle = value;
                        break;
                    case "Font_Override_Achievement_Description":
                        settings.FontOverrideAchievementDescription = value;
                        break;
                    case "Font_Size_FPS":
                        settings.FontSizeFps = IniParseHelper.ParseFloat(value);
                        break;
                    case "Font_Size_Achievement_Title":
                        settings.FontSizeAchievementTitle = IniParseHelper.ParseFloat(value);
                        break;
                    case "Font_Size_Achievement_Description":
                        settings.FontSizeAchievementDescription = IniParseHelper.ParseFloat(value);
                        break;
                    case "Font_Achievement_Title_Bold":
                        settings.FontAchievementTitleBold = IniParseHelper.StringToBool(value);
                        break;
                    case "Stats_Background_R":
                        settings.StatsBackgroundR = IniParseHelper.ParseFloat(value);
                        break;
                    case "Stats_Background_G":
                        settings.StatsBackgroundG = IniParseHelper.ParseFloat(value);
                        break;
                    case "Stats_Background_B":
                        settings.StatsBackgroundB = IniParseHelper.ParseFloat(value);
                        break;
                    case "Stats_Background_A":
                        settings.StatsBackgroundA = IniParseHelper.ParseFloat(value);
                        break;
                    case "Stats_Text_R":
                        settings.StatsTextR = IniParseHelper.ParseFloat(value);
                        break;
                    case "Stats_Text_G":
                        settings.StatsTextG = IniParseHelper.ParseFloat(value);
                        break;
                    case "Stats_Text_B":
                        settings.StatsTextB = IniParseHelper.ParseFloat(value);
                        break;
                    case "Stats_Text_A":
                        settings.StatsTextA = IniParseHelper.ParseFloat(value);
                        break;
                    case "Stats_Pos_x":
                        settings.StatsPosX = IniParseHelper.ParseFloat(value);
                        break;
                    case "Stats_Pos_y":
                        settings.StatsPosY = IniParseHelper.ParseFloat(value);
                        break;
                }
            }
        }

        private void ParseMainSetting(MainSettings settings, string section, string key, string value)
        {
            if (section == "main::general")
            {
                switch (key)
                {
                    case "new_app_ticket":
                        settings.NewAppTicket = value == "1";
                        break;
                    case "gc_token":
                        settings.GcToken = value == "1";
                        break;
                    case "block_unknown_clients":
                        settings.BlockUnknownClients = value == "1";
                        break;
                    case "steam_deck":
                        settings.SteamDeck = value == "1";
                        break;
                    case "enable_account_avatar":
                        settings.EnableAccountAvatar = value == "1";
                        break;
                    case "enable_voice_chat":
                        settings.EnableVoiceChat = value == "1";
                        break;
                    case "immediate_gameserver_stats":
                        settings.ImmediateGameserverStats = value == "1";
                        break;
                    case "matchmaking_server_list_actual_type":
                        settings.MatchmakingServerListActualType = value == "1";
                        break;
                    case "matchmaking_server_details_via_source_query":
                        settings.MatchmakingServerDetailsViaSourceQuery = value == "1";
                        break;
                }
            }
            else if (section == "main::stats")
            {
                switch (key)
                {
                    case "disable_leaderboards_create_unknown":
                        settings.DisableLeaderboardsCreateUnknown = value == "1";
                        break;
                    case "allow_unknown_stats":
                        settings.AllowUnknownStats = value == "1";
                        break;
                    case "stat_achievement_progress_functionality":
                        settings.StatAchievementProgressFunctionality = value == "1";
                        break;
                    case "save_only_higher_stat_achievement_progress":
                        settings.SaveOnlyHigherStatAchievementProgress = value == "1";
                        break;
                    case "paginated_achievements_icons":
                        if (int.TryParse(value, out int paginatedIcons))
                            settings.PaginatedAchievementsIcons = paginatedIcons;
                        break;
                    case "record_playtime":
                        settings.RecordPlaytime = value == "1";
                        break;
                }
            }
            else if (section == "main::connectivity")
            {
                switch (key)
                {
                    case "disable_lan_only":
                        settings.DisableLanOnly = value == "1";
                        break;
                    case "disable_networking":
                        settings.DisableNetworking = value == "1";
                        break;
                    case "listen_port":
                        if (int.TryParse(value, out int port))
                            settings.ListenPort = port;
                        break;
                    case "offline":
                        settings.Offline = value == "1";
                        break;
                    case "disable_sharing_stats_with_gameserver":
                        settings.DisableSharingStatsWithGameserver = value == "1";
                        break;
                    case "disable_source_query":
                        settings.DisableSourceQuery = value == "1";
                        break;
                    case "share_leaderboards_over_network":
                        settings.ShareLeaderboardsOverNetwork = value == "1";
                        break;
                    case "disable_lobby_creation":
                        settings.DisableLobbyCreation = value == "1";
                        break;
                    case "download_steamhttp_requests":
                        settings.DownloadSteamhttpRequests = value == "1";
                        break;
                    case "old_p2p_packet_sharing_mode":
                        if (int.TryParse(value, out int p2pMode))
                            settings.OldP2PPacketSharingMode = p2pMode;
                        break;
                }
            }
            else if (section == "main::misc")
            {
                switch (key)
                {
                    case "achievements_bypass":
                        settings.AchievementsBypass = value == "1";
                        break;
                    case "force_steamhttp_success":
                        settings.ForceSteamhttpSuccess = value == "1";
                        break;
                    case "disable_steamoverlaygameid_env_var":
                        settings.DisableSteamoverlaygameidEnvVar = value == "1";
                        break;
                    case "enable_steam_preowned_ids":
                        settings.EnableSteamPreownedIds = value == "1";
                        break;
                    case "steam_game_stats_reports_dir":
                        settings.SteamGameStatsReportsDir = value;
                        break;
                    case "free_weekend":
                        settings.FreeWeekend = value == "1";
                        break;
                    case "use_32bit_inventory_item_ids":
                        settings.Use32BitInventoryItemIds = value == "1";
                        break;
                }
            }
        }

        private void ParseUserSetting(UserSettings settings, string section, string key, string value)
        {
            if (section == "user::general")
            {
                switch (key)
                {
                    case "account_name":
                        settings.AccountName = value;
                        break;
                    case "account_steamid":
                        settings.AccountSteamId = value;
                        break;
                    case "ticket":
                        settings.Ticket = value;
                        break;
                    case "alt_steamid":
                        settings.AltSteamId = value;
                        break;
                    case "alt_steamid_count":
                        if (int.TryParse(value, out int count))
                            settings.AltSteamIdCount = count;
                        break;
                    case "language":
                        settings.Language = value;
                        break;
                    case "ip_country":
                        settings.IpCountry = value;
                        break;
                }
            }
            else if (section == "user::saves")
            {
                switch (key)
                {
                    case "local_save_path":
                        settings.LocalSavePath = value?.Trim() ?? string.Empty;
                        break;
                    case "saves_folder_name":
                        settings.SavesFolderName = value?.Trim() ?? string.Empty;
                        break;
                }
            }
        }

        private void SetValueIfNotDefault(IniFile iniFile, string section, string key, string value, string defaultValue)
        {
            var existingValue = _iniService.GetValue(iniFile, section, key);

            if (value == defaultValue)
            {
                if (existingValue == string.Empty)
                {
                    _iniService.SetValue(iniFile, section, key, null, skipIfDefault: true);
                    return;
                }

                if (!string.IsNullOrEmpty(existingValue))
                {
                    _iniService.SetValue(iniFile, section, key, value);
                    return;
                }

                return;
            }

            _iniService.SetValue(iniFile, section, key, value);
        }

        private void SetBoolIfNotDefault(IniFile iniFile, string section, string key, bool value, bool defaultValue)
        {
            SetValueIfNotDefault(iniFile, section, key, IniParseHelper.BoolToString(value), IniParseHelper.BoolToString(defaultValue));
        }

        private void SetIntIfNotDefault(IniFile iniFile, string section, string key, int value, int defaultValue)
        {
            SetValueIfNotDefault(iniFile, section, key, IniParseHelper.IntToString(value), IniParseHelper.IntToString(defaultValue));
        }

        private void SetFloatIfNotDefault(IniFile iniFile, string section, string key, float value, float defaultValue)
        {
            SetValueIfNotDefault(iniFile, section, key, IniParseHelper.FloatToString(value), IniParseHelper.FloatToString(defaultValue));
        }
    }
}
