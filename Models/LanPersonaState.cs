namespace SmartGoldbergEmu.Models
{
    // Local LAN Players persona while the channel is on. Offline is lan_players_online=false, not this enum.
    public enum LanPersonaState
    {
        Online = 0,
        Away = 1,
        // Listen to peers; do not advertise until changed.
        Hidden = 2,
        // Advertise presence; LAN chat is ignored until status changes.
        Busy = 3
    }
}
