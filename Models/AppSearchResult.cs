namespace SmartGoldbergEmu.Models
{
    /// <summary>
    /// Represents a search result for a game by name, containing App ID and game name.
    /// </summary>
    public class AppSearchResult
    {
        /// <summary>
        /// Gets or sets the Steam App ID.
        /// </summary>
        public ulong AppId { get; set; }

        /// <summary>
        /// Gets or sets the game name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the source of this search result (e.g., "PICS", "Steam App List").
        /// </summary>
        public string Source { get; set; }

        public override string ToString()
        {
            return $"{Name} ({AppId})";
        }
    }
}

