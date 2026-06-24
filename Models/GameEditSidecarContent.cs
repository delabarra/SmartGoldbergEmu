namespace SmartGoldbergEmu.Models
{
    /// <summary>
    /// Text/JSON sidecar payloads loaded from <c>games/{appId}/steam_settings/</c> for edit mode.
    /// </summary>
    public class GameEditSidecarContent
    {
        public string Leaderboards { get; set; } = string.Empty;
        public string CustomBroadcasts { get; set; } = string.Empty;
        public string SubscribedGroups { get; set; } = string.Empty;
        public string SubscribedGroupsClans { get; set; } = string.Empty;
        public string AutoAcceptInvite { get; set; } = string.Empty;
        public string BranchesJson { get; set; } = string.Empty;
        public string SteamInterfaces { get; set; } = string.Empty;
        public string AchievementsJson { get; set; } = string.Empty;
        public string ItemsJson { get; set; } = string.Empty;
        public string CustomStatsJson { get; set; } = string.Empty;
        public string SupportedLanguages { get; set; } = string.Empty;
    }
}
