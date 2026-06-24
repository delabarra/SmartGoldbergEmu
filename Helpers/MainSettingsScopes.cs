using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Helpers
{
    // Splits configs.main.ini fields: global emulator vs per-game network/multiplayer.
    public static class MainSettingsScopes
    {
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
                DownloadSteamhttpRequests = s.DownloadSteamhttpRequests,
                OldP2PPacketSharingMode = s.OldP2PPacketSharingMode
            };
        }

        public static void ApplyNetworkSlice(MainSettings target, MainSettings network)
        {
            if (target == null || network == null)
                return;

            target.BlockUnknownClients = network.BlockUnknownClients;
            target.ImmediateGameserverStats = network.ImmediateGameserverStats;
            target.MatchmakingServerListActualType = network.MatchmakingServerListActualType;
            target.MatchmakingServerDetailsViaSourceQuery = network.MatchmakingServerDetailsViaSourceQuery;
            target.DisableLanOnly = network.DisableLanOnly;
            target.DisableNetworking = network.DisableNetworking;
            target.ListenPort = network.ListenPort;
            target.Offline = network.Offline;
            target.DisableSharingStatsWithGameserver = network.DisableSharingStatsWithGameserver;
            target.DisableSourceQuery = network.DisableSourceQuery;
            target.ShareLeaderboardsOverNetwork = network.ShareLeaderboardsOverNetwork;
            target.DisableLobbyCreation = network.DisableLobbyCreation;
            target.DownloadSteamhttpRequests = network.DownloadSteamhttpRequests;
            target.OldP2PPacketSharingMode = network.OldP2PPacketSharingMode;
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
                   a.DownloadSteamhttpRequests == b.DownloadSteamhttpRequests &&
                   a.OldP2PPacketSharingMode == b.OldP2PPacketSharingMode;
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
                   a.AchievementsBypass == b.AchievementsBypass &&
                   string.Equals(a.SteamGameStatsReportsDir ?? string.Empty, b.SteamGameStatsReportsDir ?? string.Empty, System.StringComparison.Ordinal);
        }
    }
}
