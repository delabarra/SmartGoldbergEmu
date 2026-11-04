namespace SmartGoldbergEmu.LanKit
{
    // One-pass summary of GetFriend* for LanKit diagnostics (logged, not a public API).
    internal sealed class LanFriendScanStats
    {
        public int FriendCount;
        public int PlayingCount;
        public int WithLobbyCount;
        public int WithConnectCount;
        public int TargetSeen;
        public int SameAppCount;
        public int SameAppWithJoinCount;
        public int TargetWithJoinCount;
        public string DistinctAppIds = string.Empty;
        public string SamplePorts = string.Empty;
        public string SampleIps = string.Empty;

        public string ToLogFragment()
        {
            return "friends=" + FriendCount
                + " playing=" + PlayingCount
                + " lobby=" + WithLobbyCount
                + " connect=" + WithConnectCount
                + " targetSeen=" + TargetSeen
                + " targetJoin=" + TargetWithJoinCount
                + " sameApp=" + SameAppCount
                + " sameAppJoin=" + SameAppWithJoinCount
                + " appIds=[" + (DistinctAppIds ?? string.Empty) + "]"
                + " ips=[" + (SampleIps ?? string.Empty) + "]"
                + " ports=[" + (SamplePorts ?? string.Empty) + "]";
        }
    }
}
