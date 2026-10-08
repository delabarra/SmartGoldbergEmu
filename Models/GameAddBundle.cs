namespace SmartGoldbergEmu.Models
{
    // In-memory add-game preview data, before any games/{appId}/ folder or games.ini row exists.
    public class GameAddBundle
    {
        public GameConfig Game { get; set; }

        public OnlineAppData Metadata { get; set; }

        // Catalog captured during add-game collect (resources/{appId}.json shape).
        public AppCatalogSnapshot Catalog { get; set; }

        // Add: global settings merged; update: per-game steam_settings merged.
        public GameSettingsSnapshot FormDefaults { get; set; }

        // True when collect is refreshing an existing library GUID (duplicate Update choice).
        public bool IsUpdateOfExisting { get; set; }

        public AchievementPreviewKind AchievementPreview { get; set; }

        // Includes synthetic rows unless AchievementPreview is RealList.
        public string AchievementsPreviewJson { get; set; }

        public string ItemsJson { get; set; }

        public AddGamePrefetchedSchemas PrefetchedSchemas { get; set; }

        public GameAddBundle()
        {
            Game = new GameConfig();
            AchievementsPreviewJson = string.Empty;
            ItemsJson = "{}";
            AchievementPreview = AchievementPreviewKind.NoApiKey;
        }

        // Drop collect-time Steam trees and preview JSON once the dialog/save pipeline no longer needs them.
        // Do not call while a PendingAddSave still needs Game.AppInfo/Catalog for asset download.
        public void ReleaseHeavyRuntimeData()
        {
            AchievementsPreviewJson = null;
            ItemsJson = null;
            PrefetchedSchemas = null;
            Metadata = null;
            Catalog = null;
            Game?.ReleaseHeavyRuntimeData();
        }
    }
}
