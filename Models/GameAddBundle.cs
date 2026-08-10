namespace SmartGoldbergEmu.Models
{
    /// <summary>
    /// In-memory data for add-game preview before any <c>games/{appId}/</c> folder or <c>games.ini</c> row exists.
    /// </summary>
    public class GameAddBundle
    {
        public GameConfig Game { get; set; }

        public OnlineAppData Metadata { get; set; }

        // Kit-first catalog SoT captured during add-game collect (resources/{appId}.json shape).
        public AppCatalogSnapshot Catalog { get; set; }

        /// <summary>Form defaults (add: global merge; update: per-game steam_settings merged).</summary>
        public GameSettingsSnapshot FormDefaults { get; set; }

        // True when collect is refreshing an existing library GUID (duplicate Update choice).
        public bool IsUpdateOfExisting { get; set; }

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

        // Drop collect-time Steam trees and preview JSON once the dialog/save pipeline no longer needs them.
        public void ReleaseHeavyRuntimeData()
        {
            AchievementsPreviewJson = null;
            ItemsJson = null;
            Metadata = null;
            Catalog = null;
            Game?.ReleaseHeavyRuntimeData();
        }
    }
}
