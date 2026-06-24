namespace SmartGoldbergEmu.Models
{
    /// <summary>
    /// How achievements are represented in the add-game preview (before save writes disk).
    /// </summary>
    public enum AchievementPreviewKind
    {
        /// <summary>Steam Web API key missing — only the No_Key reminder row is shown.</summary>
        NoApiKey,

        /// <summary>Key present; Steam reports no achievements for this app.</summary>
        NoAchievementsOnSteam,

        /// <summary>Key present; real achievement schema loaded for preview.</summary>
        RealList
    }
}
