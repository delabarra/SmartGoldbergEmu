using System;
using System.Xml.Serialization;
using SteamKit;

namespace SmartGoldbergEmu.Models
{
    /// <summary>
    /// Represents a game configuration with only the essential identity properties.
    /// Emulation settings are stored in Goldberg config files, not here.
    /// </summary>
    public class GameConfig
    {
        /// <summary>
        /// Gets or sets the display name of the game.
        /// </summary>
        public string AppName { get; set; }

        /// <summary>
        /// Gets or sets the Steam App ID.
        /// </summary>
        public ulong AppId { get; set; }

        /// <summary>
        /// Gets or sets the game's start folder path.
        /// </summary>
        public string StartFolder { get; set; }

        /// <summary>
        /// Gets or sets the path to the game executable (relative to <see cref="StartFolder"/> when not rooted, like Steam manifest executable).
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Gets or sets the launch parameters for the game.
        /// </summary>
        public string Parameters { get; set; }

        /// <summary>
        /// Optional working directory when launching (relative to <see cref="StartFolder"/> when not rooted, or absolute under that folder).
        /// Used when no Steam (PICS) launch option supplies a working directory.
        /// </summary>
        public string WorkingDirectory { get; set; }

        /// <summary>
        /// Gets or sets the path to a custom icon file (.exe, .bat, or .ico).
        /// </summary>
        public string CustomIcon { get; set; }

        /// <summary>
        /// Gets or sets the unique GUID for this game entry.
        /// </summary>
        public Guid GameGuid { get; set; }

        /// <summary>
        /// Goldberg launch mode for this game (Steam client, Experimental steam_api, Steam.dll beside exe, or no emulation).
        /// </summary>
        public GoldbergLaunchMode LaunchMode { get; set; }

        /// <summary>
        /// Runtime-only Steam app product info from PICS as <see cref="KeyValue"/> (not persisted).
        /// </summary>
        [XmlIgnore]
        public KeyValue AppPicsKeyValue { get; set; }

        /// <summary>
        /// Gets or sets runtime-only pre-fetched DLC data (not persisted).
        /// </summary>
        [XmlIgnore]
        public System.Collections.Generic.Dictionary<long, string> PreFetchedDlcData { get; set; }

        /// <summary>
        /// Gets or sets runtime-only supported languages (not persisted).
        /// </summary>
        [XmlIgnore]
        public System.Collections.Generic.List<string> SupportedLanguages { get; set; }

        /// <summary>
        /// Gets or sets a flag indicating whether DLC check has been performed (not persisted).
        /// </summary>
        [XmlIgnore]
        public bool DlcCheckPerformed { get; set; }

        /// <summary>
        /// Initializes a new instance of the GameConfig class.
        /// </summary>
        public GameConfig()
        {
            AppName = string.Empty;
            AppId = 0;
            StartFolder = string.Empty;
            Path = string.Empty;
            Parameters = string.Empty;
            WorkingDirectory = string.Empty;
            CustomIcon = string.Empty;
            GameGuid = Guid.NewGuid();
            LaunchMode = GoldbergLaunchMode.SteamClient;
        }
    }
}
