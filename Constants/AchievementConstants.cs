namespace SmartGoldbergEmu.Constants
{
    public static class AchievementConstants
    {
        public const string SteamSettingsFolder = PathConstants.SteamSettingsFolderName;
        public const string AchievementsFileName = "achievements.json";
        public const string AchievementImagesFolder = "achievement_images";

        public const string NoApiKeyAchievementName = "No_Key_To_Progress";
        public const string NoAchievementsAchievementName = "They_Wont_Let_You_Shine";

        public const string NoApiKeyAchievementDisplayName = "Achievements Unavailable";
        public const string NoAchievementsAchievementDisplayName = "No Achievements Available";

        public const string NoApiKeyAchievementDescription = "Could not load an achievement schema for this app. Community data was unavailable, and no Steam Web API key is configured.";
        public const string NoAchievementsAchievementDescription = "This game doesn't have any achievements defined on Steam.";

        // Seconds per icon GET; keep modest so failed CDN candidates fail over quickly.
        public const int HttpRequestLongTimeout = 15;
    }
}
