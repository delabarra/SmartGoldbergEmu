namespace SmartGoldbergEmu.Models
{
    // Text/JSON sidecar files loaded from games/{appId}/steam_settings/ for edit mode.
    public class GameEditSidecarContent
    {
        public string Leaderboards { get; set; } = string.Empty;
        public string CustomBroadcasts { get; set; } = string.Empty;
        public string SubscribedGroups { get; set; } = string.Empty;
        public string SubscribedGroupsClans { get; set; } = string.Empty;
        // null = no auto_accept_invite.txt; empty = file exists and accepts invites from anyone.
        public string AutoAcceptInvite { get; set; }
        public string BranchesJson { get; set; } = string.Empty;
        public string SteamInterfaces { get; set; } = string.Empty;
        public string AchievementsJson { get; set; } = string.Empty;
        public string ItemsJson { get; set; } = string.Empty;
        public string CustomStatsJson { get; set; } = string.Empty;
        public string SupportedLanguages { get; set; } = string.Empty;
    }
}
