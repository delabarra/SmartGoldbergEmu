using System;
using SmartGoldbergEmu.Abstractions;

namespace SmartGoldbergEmu.Models
{
    public sealed class GameSaveAddRequest
    {
        public GameConfig GameConfig { get; set; }
        public OnlineAppData Metadata { get; set; }
        public AchievementPreviewKind AchievementPreview { get; set; }
        public GameSettingsSaveRequest FormSaveRequest { get; set; }
        public ITaskReportService TaskReportService { get; set; }
        public Action OnAssetsDownloaded { get; set; }
        public Action OnSuccessfulSaveCompleted { get; set; }
        public bool CredentialsTouched { get; set; }
    }
}
