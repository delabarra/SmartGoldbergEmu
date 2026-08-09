namespace SmartGoldbergEmu.Constants
{
    public static class AchievementConstants
    {
        public const string SteamSettingsFolder = PathConstants.SteamSettingsFolderName;
        public const string AchievementsFileName = "achievements.json";
        public const string AchievementImagesFolder = "achievement_images";

        public const string NoApiKeyAchievementName = "No_Key_To_Progress";
        public const string NoAchievementsAchievementName = "They_Wont_Let_You_Shine";

        public const string NoApiKeyAchievementDisplayName = "No API Key Configured";
        public const string NoAchievementsAchievementDisplayName = "No Achievements Available";

        public const string NoApiKeyAchievementDescription = "To generate achievements, you need to configure a Steam WebAPI key. Get one from: " + ApplicationConstants.SteamWebApiKeyRegistrationUrl;
        public const string NoAchievementsAchievementDescription = "This game doesn't have any achievements defined on Steam.";

        public const int HttpRequestLongTimeout = 30;

        // Cap parallel icon GETs; unbounded WhenAll + per-call HttpClient spikes LOH/working set on large games.
        public const int MaxConcurrentIconDownloads = 8;
    }
}
