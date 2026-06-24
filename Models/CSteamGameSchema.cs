using System.Collections.Generic;

namespace SmartGoldbergEmu.Models
{
    // Steam API response schema for game achievements
    public class CSteamGameSchema
    {
        public CGame game { get; set; }
    }

    public class CGame
    {
        public CAvailableGameStats availableGameStats { get; set; }
    }

    public class CAvailableGameStats
    {
        public List<CAchievement> achievements { get; set; }
    }
}
