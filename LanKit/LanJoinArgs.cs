namespace SmartGoldbergEmu.LanKit
{
    public sealed class LanJoinArgs
    {
        public LanJoinKind Kind { get; set; }

        // Full process argument fragment (e.g. "+connect_lobby 123" or a rich-presence connect string).
        public string ConnectArguments { get; set; }

        // Friend's reported AppId when GetFriendGamePlayed succeeded; otherwise 0.
        public uint AppId { get; set; }

        public static LanJoinArgs Empty { get; } = new LanJoinArgs
        {
            Kind = LanJoinKind.None,
            ConnectArguments = string.Empty,
            AppId = 0
        };
    }
}
