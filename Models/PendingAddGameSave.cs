using System;
using SmartGoldbergEmu.Services;

namespace SmartGoldbergEmu.Models
{
    // Committed by GameSaveWriter after the settings dialog closes.
    public sealed class PendingAddGameSave
    {
        public GameConfig GameConfig { get; set; }
        public OnlineAppData Metadata { get; set; }
        public AchievementPreviewKind AchievementPreview { get; set; }
        public AddGamePrefetchedSchemas PrefetchedSchemas { get; set; }
        public GameSettingsSnapshot SettingsSnapshot { get; set; }
        public string CustomStatsRawJson { get; set; }
        public bool CredentialsTouched { get; set; }
        public bool IsUpdateOfExisting { get; set; }
        public GoldbergFilesService.AdditionalFilesSaveRequest AdditionalFilesSaveRequest { get; set; }
        public Action SaveDlcAndPaths { get; set; }
        public Action OnAssetsDownloaded { get; set; }
        public Action OnSuccessfulSaveCompleted { get; set; }
    }
}
