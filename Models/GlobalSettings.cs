using SmartGoldbergEmu.Constants;

namespace SmartGoldbergEmu.Models
{
    public class GlobalSettings
    {
        public string AccountName { get; set; }

        public string AccountSteamId { get; set; }

        public string Language { get; set; }

        // 0=not on Steam hardware; 1=Steam Deck (see configs.main.EXAMPLE steam_hardware_type).
        public int SteamHardwareType { get; set; }

        public bool EnableAccountAvatar { get; set; }

        public GlobalSettings()
        {
            AccountName = ApplicationConstants.DefaultAccountName;
            AccountSteamId = ApplicationConstants.DefaultSteamId;
            Language = ApplicationConstants.DefaultLanguage;
            SteamHardwareType = 0;
            EnableAccountAvatar = false;
        }
    }
}
