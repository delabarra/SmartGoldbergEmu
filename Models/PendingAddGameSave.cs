using System;
using SmartGoldbergEmu.Services;

namespace SmartGoldbergEmu.Models
{
    /// <summary>
    /// Captured add-game save payload; committed by <see cref="GameSaveWriter"/> after the settings dialog closes.
    /// </summary>
    public sealed class PendingAddGameSave
    {
        public GameConfig GameConfig { get; set; }
        public OnlineAppData Metadata { get; set; }
        public AchievementPreviewKind AchievementPreview { get; set; }
        public GameSettingsSnapshot SettingsSnapshot { get; set; }
        public string CustomStatsRawJson { get; set; }
        public bool CredentialsTouched { get; set; }
        public GoldbergFilesService.AdditionalFilesSaveRequest AdditionalFilesSaveRequest { get; set; }
        public Action SaveDlcAndPaths { get; set; }
        public Action OnAssetsDownloaded { get; set; }
        public Action OnSuccessfulSaveCompleted { get; set; }
    }
}
