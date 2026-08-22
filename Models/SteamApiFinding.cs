namespace SmartGoldbergEmu.Models
{
    // One steam_api / steam_api64 candidate under a game folder.
    public class SteamApiFinding
    {
        public string Path { get; set; }
        public bool Is64Bit { get; set; }
        public bool IsClean { get; set; }
    }
}
