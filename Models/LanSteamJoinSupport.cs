namespace SmartGoldbergEmu.Models
{
    // Per-game override for LAN Invite/Join via Steam lobby id or rich-presence connect.
    // Default uses the curated denylist in LanGameJoinService.
    public enum LanSteamJoinSupport
    {
        Default = 0,
        On = 1,
        Off = 2,
    }
}
