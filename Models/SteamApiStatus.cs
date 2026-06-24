using System.Collections.Generic;

namespace SmartGoldbergEmu.Models
{
    /// <summary>
    /// Represents the validation status of Steam API DLL files in a game folder.
    /// </summary>
    public class SteamApiStatus
    {
        /// <summary>
        /// Gets or sets a value indicating whether steam_api.dll (32-bit) was found.
        /// </summary>
        public bool X32Found { get; set; }

        /// <summary>
        /// Gets or sets the full path to steam_api.dll. May be null if not found.
        /// </summary>
        public string X32Path { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether steam_api.dll hash matches a known good version.
        /// </summary>
        public bool X32IsClean { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether steam_api64.dll (64-bit) was found.
        /// </summary>
        public bool X64Found { get; set; }

        /// <summary>
        /// Gets or sets the full path to steam_api64.dll. May be null if not found.
        /// </summary>
        public string X64Path { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether steam_api64.dll hash matches a known good version.
        /// </summary>
        public bool X64IsClean { get; set; }

        /// <summary>
        /// Gets or sets the list of paths to clean backup DLL files found in the game folder.
        /// </summary>
        public List<string> CleanBackups { get; set; } = new List<string>();
    }
}

