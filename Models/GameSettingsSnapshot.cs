using System;
using System.Collections.Generic;
using SmartGoldbergEmu.Constants;

namespace SmartGoldbergEmu.Models
{
    /// <summary>
    /// Represents a snapshot of game settings with dirty tracking capability.
    /// </summary>
    public class GameSettingsSnapshot
    {
        /// <summary>
        /// Gets or sets the App ID this snapshot belongs to.
        /// </summary>
        public ulong AppId { get; set; }

        /// <summary>
        /// Gets or sets the overlay settings.
        /// </summary>
        public OverlaySettings Overlay { get; set; }

        /// <summary>
        /// Gets or sets the main emulation settings.
        /// </summary>
        public MainSettings Main { get; set; }

        /// <summary>
        /// Gets or sets the app-specific settings.
        /// </summary>
        public AppSettings App { get; set; }

        /// <summary>
        /// Gets or sets the user-specific settings.
        /// </summary>
        public UserSettings User { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when this snapshot was created.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Initializes a new instance of the GameSettingsSnapshot class.
        /// </summary>
        public GameSettingsSnapshot()
        {
            Overlay = new OverlaySettings();
            Main = new MainSettings();
            App = new AppSettings();
            User = new UserSettings();
            CreatedAt = DateTime.Now;
        }
    }

    /// <summary>
    /// Represents overlay-specific settings.
    /// </summary>
    public class OverlaySettings
    {
        // General overlay settings
        public bool EnableExperimentalOverlay { get; set; } = true;
        public int HookDelaySec { get; set; } = 0;
        public int RendererDetectorTimeoutSec { get; set; } = 15;
        public bool DisableAchievementNotification { get; set; } = false;
        public bool DisableFriendNotification { get; set; } = false;
        public bool DisableAchievementProgress { get; set; } = false;
        public bool DisableWarningAny { get; set; } = false;
        public bool DisableWarningBadAppId { get; set; } = false;
        public bool DisableWarningLocalSave { get; set; } = false;
        public bool UploadAchievementsIconsToGpu { get; set; } = true;
        public int FpsAveragingWindow { get; set; } = 10;
        public bool OverlayAlwaysShowUserInfo { get; set; } = false;
        public bool OverlayAlwaysShowFps { get; set; } = false;
        public bool OverlayAlwaysShowFrametime { get; set; } = false;
        public bool OverlayAlwaysShowPlaytime { get; set; } = false;

        // Appearance settings
        public string FontOverride { get; set; } = string.Empty;
        public float FontSize { get; set; } = 20.0f;
        public float IconSize { get; set; } = 64.0f;
        public float FontGlyphExtraSpacingX { get; set; } = 1.0f;
        public float FontGlyphExtraSpacingY { get; set; } = 0.0f;

        // Notification colors (RGBA)
        public float NotificationR { get; set; } = 0.12f;
        public float NotificationG { get; set; } = 0.14f;
        public float NotificationB { get; set; } = 0.21f;
        public float NotificationA { get; set; } = 1.0f;

        // Notification appearance
        public float NotificationRounding { get; set; } = 10.0f;
        public float NotificationMarginX { get; set; } = 5.0f;
        public float NotificationMarginY { get; set; } = 5.0f;

        // Notification durations
        public float NotificationAnimation { get; set; } = 0.35f;
        public float NotificationDurationProgress { get; set; } = 6.0f;
        public float NotificationDurationAchievement { get; set; } = 7.0f;
        public float NotificationDurationInvitation { get; set; } = 8.0f;
        public float NotificationDurationChat { get; set; } = 4.0f;

        // Achievement datetime format
        public string AchievementUnlockDatetimeFormat { get; set; } = "%Y/%m/%d - %H:%M:%S";

        // Background colors (RGBA)
        public float BackgroundR { get; set; } = 0.12f;
        public float BackgroundG { get; set; } = 0.11f;
        public float BackgroundB { get; set; } = 0.11f;
        public float BackgroundA { get; set; } = 0.55f;

        // Element colors (RGBA)
        public float ElementR { get; set; } = 0.30f;
        public float ElementG { get; set; } = 0.32f;
        public float ElementB { get; set; } = 0.40f;
        public float ElementA { get; set; } = 1.0f;

        // Element hovered colors (RGBA)
        public float ElementHoveredR { get; set; } = 0.278f;
        public float ElementHoveredG { get; set; } = 0.393f;
        public float ElementHoveredB { get; set; } = 0.602f;
        public float ElementHoveredA { get; set; } = 1.0f;

        // Element active colors (RGBA)
        public float ElementActiveR { get; set; } = -1.0f;
        public float ElementActiveG { get; set; } = -1.0f;
        public float ElementActiveB { get; set; } = -1.0f;
        public float ElementActiveA { get; set; } = -1.0f;

        // Extra appearance keys (gbe_fork steam_settings.EXAMPLE)
        public string FontOverrideAchievementTitle { get; set; } = string.Empty;
        public string FontOverrideAchievementDescription { get; set; } = string.Empty;
        public float FontSizeFps { get; set; }
        public float FontSizeAchievementTitle { get; set; }
        public float FontSizeAchievementDescription { get; set; }
        public bool FontAchievementTitleBold { get; set; }

        // Notification positions
        public string PosAchievement { get; set; } = "bot_right";
        public string PosInvitation { get; set; } = "top_right";
        public string PosChatMsg { get; set; } = "top_center";

        // Stats/FPS display colors (RGBA)
        public float StatsBackgroundR { get; set; } = 0.0f;
        public float StatsBackgroundG { get; set; } = 0.0f;
        public float StatsBackgroundB { get; set; } = 0.0f;
        public float StatsBackgroundA { get; set; } = 0.6f;

        public float StatsTextR { get; set; } = 0.8f;
        public float StatsTextG { get; set; } = 0.7f;
        public float StatsTextB { get; set; } = 0.0f;
        public float StatsTextA { get; set; } = 1.0f;

        // Stats position (percentage)
        public float StatsPosX { get; set; } = 0.0f;
        public float StatsPosY { get; set; } = 0.0f;
    }

    /// <summary>
    /// Represents main emulation settings.
    /// </summary>
    public class MainSettings
    {
        // General settings
        public bool NewAppTicket { get; set; } = true;
        public bool GcToken { get; set; } = true;
        public bool BlockUnknownClients { get; set; } = false;
        public bool SteamDeck { get; set; } = false;
        public bool EnableAccountAvatar { get; set; } = false;
        public bool EnableVoiceChat { get; set; } = false;
        public bool ImmediateGameserverStats { get; set; } = false;
        public bool MatchmakingServerListActualType { get; set; } = false;
        public bool MatchmakingServerDetailsViaSourceQuery { get; set; } = false;

        // Stats settings
        public bool DisableLeaderboardsCreateUnknown { get; set; } = false;
        public bool AllowUnknownStats { get; set; } = false;
        public bool StatAchievementProgressFunctionality { get; set; } = true;
        public bool SaveOnlyHigherStatAchievementProgress { get; set; } = true;
        public int PaginatedAchievementsIcons { get; set; } = 10;
        public bool RecordPlaytime { get; set; } = false;

        // Connectivity settings
        public bool DisableLanOnly { get; set; } = false;
        public bool DisableNetworking { get; set; } = false;
        public int ListenPort { get; set; } = 47584;
        public bool Offline { get; set; } = false;
        public bool DisableSharingStatsWithGameserver { get; set; } = false;
        public bool DisableSourceQuery { get; set; } = false;
        public bool ShareLeaderboardsOverNetwork { get; set; } = false;
        public bool DisableLobbyCreation { get; set; } = false;
        public bool DownloadSteamhttpRequests { get; set; } = false;
        public int OldP2PPacketSharingMode { get; set; } = 0;

        // Misc settings
        public bool AchievementsBypass { get; set; } = false;
        public bool ForceSteamhttpSuccess { get; set; } = false;
        public bool DisableSteamoverlaygameidEnvVar { get; set; } = false;
        public bool EnableSteamPreownedIds { get; set; } = false;
        public string SteamGameStatsReportsDir { get; set; } = string.Empty;
        public bool FreeWeekend { get; set; } = false;
        public bool Use32BitInventoryItemIds { get; set; } = false;
    }

    /// <summary>
    /// Represents app-specific settings.
    /// </summary>
    public class AppSettings
    {
        public bool UnlockAllDLC { get; set; } = false;
        public string BranchName { get; set; } = SteamPicsKeyNames.SteamDefaultBranchName;
        public bool IsBetaBranch { get; set; } = false;

        // [app::controller]
        public bool SteamInput { get; set; }
        public string ControllerType { get; set; } = "XBOX360";
    }

    /// <summary>
    /// Represents user-specific settings.
    /// </summary>
    public class UserSettings
    {
        // General user settings
        public string AccountName { get; set; } = string.Empty;
        public string AccountSteamId { get; set; } = string.Empty;
        public string Ticket { get; set; } = string.Empty;
        public string AltSteamId { get; set; } = string.Empty;
        public int AltSteamIdCount { get; set; } = 1;
        public string Language { get; set; } = ApplicationConstants.DefaultLanguage;
        public string IpCountry { get; set; } = ApplicationConstants.DefaultIpCountry;

        // Save settings
        public string LocalSavePath { get; set; } = string.Empty;
        public string SavesFolderName { get; set; } = ApplicationConstants.DefaultSavesFolderName;
        
        // Additional user settings (not in standard configs but may be used)
        public string ClanTag { get; set; } = string.Empty;
    }
}
