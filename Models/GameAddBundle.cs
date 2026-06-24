namespace SmartGoldbergEmu.Models
{
    /// <summary>
    /// In-memory data for add-game preview before any <c>games/{appId}/</c> folder or <c>games.ini</c> row exists.
    /// </summary>
    public class GameAddBundle
    {
        public GameConfig Game { get; set; }

        public OnlineAppData Metadata { get; set; }

        /// <summary>Form defaults (global Goldberg merge; per-game steam_settings not loaded).</summary>
        public GameSettingsSnapshot FormDefaults { get; set; }

        public AchievementPreviewKind AchievementPreview { get; set; }

        /// <summary>Preview JSON for achievements list (includes synthetic rows when <see cref="AchievementPreview"/> is not <see cref="AchievementPreviewKind.RealList"/>).</summary>
        public string AchievementsPreviewJson { get; set; }

        public string ItemsJson { get; set; }

        public GameAddBundle()
        {
            Game = new GameConfig();
            AchievementsPreviewJson = string.Empty;
            ItemsJson = "{}";
            AchievementPreview = AchievementPreviewKind.NoApiKey;
        }
    }
}
