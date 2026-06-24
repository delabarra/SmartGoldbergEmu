using SmartGoldbergEmu.Constants;

namespace SmartGoldbergEmu.Models
{
    /// <summary>
    /// Represents global Goldberg emulator settings.
    /// </summary>
    public class GlobalSettings
    {
        /// <summary>
        /// Gets or sets the global account name.
        /// </summary>
        public string AccountName { get; set; }

        /// <summary>
        /// Gets or sets the global Steam ID.
        /// </summary>
        public string AccountSteamId { get; set; }

        /// <summary>
        /// Gets or sets the global language preference.
        /// </summary>
        public string Language { get; set; }

        /// <summary>
        /// Gets or sets whether Steam Deck mode is enabled globally.
        /// </summary>
        public bool SteamDeck { get; set; }

        /// <summary>
        /// Gets or sets whether account avatar is enabled globally.
        /// </summary>
        public bool EnableAccountAvatar { get; set; }

        /// <summary>
        /// Initializes a new instance of the GlobalSettings class with default values.
        /// </summary>
        public GlobalSettings()
        {
            AccountName = ApplicationConstants.DefaultAccountName;
            AccountSteamId = ApplicationConstants.DefaultSteamId;
            Language = ApplicationConstants.DefaultLanguage;
            SteamDeck = false;
            EnableAccountAvatar = false;
        }
    }
}
