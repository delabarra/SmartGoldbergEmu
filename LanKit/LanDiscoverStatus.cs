namespace SmartGoldbergEmu.LanKit
{
    public enum LanDiscoverStatus
    {
        // No join args and no useful peer sighting.
        None = 0,
        // Peer Steam64 (or same-AppId friend) was on the network but had no connect/+connect_lobby yet.
        PeerSeenWithoutJoin = 1,
        // Rich-presence connect or +connect_lobby was found.
        JoinArgsFound = 2
    }
}
