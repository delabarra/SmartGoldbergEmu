namespace SmartGoldbergEmu.Models
{
    // Local LAN Players persona (when the LAN channel is Online). Offline is Go Offline, not this enum.
    public enum LanPersonaState
    {
        Online = 0,
        Away = 1,
        // Listen to peers; do not advertise until changed.
        Hidden = 2
    }
}
