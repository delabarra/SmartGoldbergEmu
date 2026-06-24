using System;

namespace SmartGoldbergEmu.Models
{
    public sealed class GameSaveEditRequest
    {
        public GameConfig GameConfig { get; set; }
        public GameConfig InitialGameConfig { get; set; }
        public GameSettingsSaveRequest FormSaveRequest { get; set; }
        public bool CredentialsTouched { get; set; }
        public Action OnSuccessfulSaveCompleted { get; set; }
    }
}
