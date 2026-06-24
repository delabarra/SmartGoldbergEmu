using System.Collections.Generic;

namespace SmartGoldbergEmu.Models
{
    /// <summary>
    /// Achievement schema result from Steam Web API.
    /// </summary>
    public class AchievementSchema
    {
        public bool Success { get; set; }
        public string AppId { get; set; }
        public string GameName { get; set; }
        public string GameVersion { get; set; }
        public List<AchievementData> Achievements { get; set; } = new List<AchievementData>();
        public string ErrorMessage { get; set; }
    }

    /// <summary>
    /// Individual achievement data.
    /// </summary>
    public class AchievementData
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }
        public string IconGray { get; set; }
        public bool Hidden { get; set; }
    }
}

