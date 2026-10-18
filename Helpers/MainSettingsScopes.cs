using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;

namespace SmartGoldbergEmu.Helpers
{
    // Splits configs.main.ini fields: global emulator vs per-game network/multiplayer.
    public static class MainSettingsScopes
    {
        private static readonly string[] NetworkGeneralKeys =
        {
            "block_unknown_clients", "immediate_gameserver_stats", "matchmaking_server_list_actual_type",
            "matchmaking_server_details_via_source_query"
        };

        private static readonly string[] NetworkConnectivityKeys =
        {
            "disable_lan_only", "disable_networking", "listen_port", "offline",
            "disable_sharing_stats_with_gameserver", "disable_source_query",
            "share_leaderboards_over_network", "disable_lobby_creation",
            "download_steamhttp_requests"
        };

        // Network-slice keys: kept out of the global configs.main.ini, cleared before rewriting a per-game slice.
        public static void RemoveNetworkKeys(IniFileService iniService, IniFile iniFile)
        {
            if (iniFile == null)
                return;

            foreach (var key in NetworkGeneralKeys)
                iniService.RemoveValue(iniFile, "main::general", key);
            foreach (var key in NetworkConnectivityKeys)
                iniService.RemoveValue(iniFile, "main::connectivity", key);
        }

        public static MainSettings ExtractNetworkSlice(MainSettings source)
        {
            var s = source ?? new MainSettings();
            return new MainSettings
            {
                BlockUnknownClients = s.BlockUnknownClients,
                ImmediateGameserverStats = s.ImmediateGameserverStats,
                MatchmakingServerListActualType = s.MatchmakingServerListActualType,
                MatchmakingServerDetailsViaSourceQuery = s.MatchmakingServerDetailsViaSourceQuery,
                DisableLanOnly = s.DisableLanOnly,
                DisableNetworking = s.DisableNetworking,
                ListenPort = s.ListenPort,
                Offline = s.Offline,
                DisableSharingStatsWithGameserver = s.DisableSharingStatsWithGameserver,
                DisableSourceQuery = s.DisableSourceQuery,
                ShareLeaderboardsOverNetwork = s.ShareLeaderboardsOverNetwork,
                DisableLobbyCreation = s.DisableLobbyCreation,
                DownloadSteamhttpRequests = s.DownloadSteamhttpRequests
            };
        }

        public static bool NetworkSlicesEqual(MainSettings a, MainSettings b)
        {
            if (a == null && b == null)
                return true;
            if (a == null || b == null)
                return false;

            return a.BlockUnknownClients == b.BlockUnknownClients &&
                   a.ImmediateGameserverStats == b.ImmediateGameserverStats &&
                   a.MatchmakingServerListActualType == b.MatchmakingServerListActualType &&
                   a.MatchmakingServerDetailsViaSourceQuery == b.MatchmakingServerDetailsViaSourceQuery &&
                   a.DisableLanOnly == b.DisableLanOnly &&
                   a.DisableNetworking == b.DisableNetworking &&
                   a.ListenPort == b.ListenPort &&
                   a.Offline == b.Offline &&
                   a.DisableSharingStatsWithGameserver == b.DisableSharingStatsWithGameserver &&
                   a.DisableSourceQuery == b.DisableSourceQuery &&
                   a.ShareLeaderboardsOverNetwork == b.ShareLeaderboardsOverNetwork &&
                   a.DisableLobbyCreation == b.DisableLobbyCreation &&
                   a.DownloadSteamhttpRequests == b.DownloadSteamhttpRequests;
        }

        public static MainSettings ExtractStatsAchievementsSlice(MainSettings source)
        {
            var s = source ?? new MainSettings();
            return new MainSettings
            {
                DisableLeaderboardsCreateUnknown = s.DisableLeaderboardsCreateUnknown,
                AllowUnknownStats = s.AllowUnknownStats,
                StatAchievementProgressFunctionality = s.StatAchievementProgressFunctionality,
                SaveOnlyHigherStatAchievementProgress = s.SaveOnlyHigherStatAchievementProgress,
                PaginatedAchievementsIcons = s.PaginatedAchievementsIcons,
                RecordPlaytime = s.RecordPlaytime,
                PauseTotalWhenUnfocused = s.PauseTotalWhenUnfocused,
                PauseSessionWhenUnfocused = s.PauseSessionWhenUnfocused,
                AchievementsBypass = s.AchievementsBypass,
                SteamGameStatsReportsDir = s.SteamGameStatsReportsDir
            };
        }

        public static bool StatsAchievementsSlicesEqual(MainSettings a, MainSettings b)
        {
            if (a == null && b == null)
                return true;
            if (a == null || b == null)
                return false;

            return a.DisableLeaderboardsCreateUnknown == b.DisableLeaderboardsCreateUnknown &&
                   a.AllowUnknownStats == b.AllowUnknownStats &&
                   a.StatAchievementProgressFunctionality == b.StatAchievementProgressFunctionality &&
                   a.SaveOnlyHigherStatAchievementProgress == b.SaveOnlyHigherStatAchievementProgress &&
                   a.PaginatedAchievementsIcons == b.PaginatedAchievementsIcons &&
                   a.RecordPlaytime == b.RecordPlaytime &&
                   a.PauseTotalWhenUnfocused == b.PauseTotalWhenUnfocused &&
                   a.PauseSessionWhenUnfocused == b.PauseSessionWhenUnfocused &&
                   a.AchievementsBypass == b.AchievementsBypass &&
                   string.Equals(a.SteamGameStatsReportsDir ?? string.Empty, b.SteamGameStatsReportsDir ?? string.Empty, System.StringComparison.Ordinal);
        }
    }
}
