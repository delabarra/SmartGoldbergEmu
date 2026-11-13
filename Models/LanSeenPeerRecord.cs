using System;

namespace SmartGoldbergEmu.Models
{
    // Local-only remembered LAN peer for Players seen (never sent on the wire).
    public sealed class LanSeenPeerRecord
    {
        public string SteamId { get; set; }
        public string DisplayName { get; set; }
        public string AvatarHash { get; set; }
        public DateTime LastSeenUtc { get; set; }
    }
}
