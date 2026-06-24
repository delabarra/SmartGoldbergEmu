namespace SmartGoldbergEmu.Constants
{
    /// <summary>
    /// Constants for achievement-related operations
    /// </summary>
    public static class AchievementConstants
    {
        public const string SteamSettingsFolder = PathConstants.SteamSettingsFolderName;
        public const string AchievementsFileName = "achievements.json";
        public const string AchievementImagesFolder = "achievement_images";
        
        // Dummy achievement names
        public const string NoApiKeyAchievementName = "No_Key_To_Progress";
        public const string NoAchievementsAchievementName = "They_Wont_Let_You_Shine";
        
        // Dummy achievement display names
        public const string NoApiKeyAchievementDisplayName = "No API Key Configured";
        public const string NoAchievementsAchievementDisplayName = "No Achievements Available";
        
        // Dummy achievement descriptions
        public const string NoApiKeyAchievementDescription = "To generate achievements, you need to configure a Steam WebAPI key. Get one from: " + ApplicationConstants.SteamWebApiKeyRegistrationUrl;
        public const string NoAchievementsAchievementDescription = "This game doesn't have any achievements defined on Steam.";
        
        // API URLs
        public const string SteamUserStatsApiUrl = ApplicationConstants.SteamUserStatsSchemaApiUrlFormat;
        
        // Timeouts (in seconds)
        public const int HttpRequestShortTimeout = 10;
        public const int HttpRequestLongTimeout = 30;

        // Retry
        public const int HttpRetryCount = 3;
        public const int HttpRetryDelayMs = 2000;
    }
}

