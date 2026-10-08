namespace SmartGoldbergEmu.Models
{
    // How achievements appear in the add-game preview, before save writes to disk.
    public enum AchievementPreviewKind
    {
        // Steam Web API key missing; only the No_Key reminder row is shown.
        NoApiKey,

        NoAchievementsOnSteam,

        RealList
    }
}
