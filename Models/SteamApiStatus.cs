using System.Collections.Generic;

namespace SmartGoldbergEmu.Models
{
    public class SteamApiStatus
    {
        public bool X32Found { get; set; }
        public string X32Path { get; set; }
        public bool X32IsClean { get; set; }
        public bool X64Found { get; set; }
        public string X64Path { get; set; }
        public bool X64IsClean { get; set; }
        public List<string> CleanBackups { get; set; } = new List<string>();
        // All steam_api candidates under the scan root (not only the best primary per arch).
        public List<SteamApiFinding> Findings { get; set; } = new List<SteamApiFinding>();
    }
}
