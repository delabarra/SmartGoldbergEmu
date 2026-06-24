namespace SmartGoldbergEmu.Models
{
    /// <summary>
    /// How Goldberg is used at launch: Steam client mode, Experimental steam_api beside the game,
    /// Steam.dll deployed next to the executable, or no emulator deployment.
    /// </summary>
    public enum GoldbergLaunchMode
    {
        SteamClient = 0,
        StandardSteamApi = 1,
        SteamDllBesideExe = 2,
        NoEmulation = 3,
    }
}
