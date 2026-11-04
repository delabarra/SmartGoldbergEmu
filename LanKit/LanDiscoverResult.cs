namespace SmartGoldbergEmu.LanKit
{
    public sealed class LanDiscoverResult
    {
        public LanDiscoverStatus Status { get; set; }

        public LanJoinArgs Join { get; set; }

        // Highest friend count observed during polling (0 = nobody on the emu LAN from this process).
        public int MaxFriendsSeen { get; set; }

        public static LanDiscoverResult Empty { get; } = new LanDiscoverResult
        {
            Status = LanDiscoverStatus.None,
            Join = LanJoinArgs.Empty,
            MaxFriendsSeen = 0
        };
    }
}
