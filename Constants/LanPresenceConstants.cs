namespace SmartGoldbergEmu.Constants
{
    public static class LanPresenceConstants
    {
        // Dedicated SGE launcher presence port (not Goldberg listen_port 47584).
        public const int UdpPort = 47590;

        public const int ProtocolVersion = 1;

        public const string MessagePresence = "p";
        public const string MessageAvatarRequest = "r";
        public const string MessageAvatarResponse = "a";

        public const int HeartbeatIntervalMs = 3000;
        public const int PeerExpireMs = 12000;
        public const int UiRefreshIntervalMs = 1000;

        // Keep under common LAN UDP practical size so avatar replies are not fragmented away.
        public const int MaxPacketBytes = 8192;
        public const int MaxNameChars = 64;
        public const int MaxGameNameChars = 128;
        public const int MaxCustomStatusChars = 64;
        public const int MaxSteamIdChars = 20;
        public const int MaxAvatarHashChars = 64;

        public const string PersonaOnline = "online";
        public const string PersonaAway = "away";
        // Auto-Away only while saved persona is Online; manual Away/Hidden are not overridden.
        public const int AwayIdleMs = 5 * 60 * 1000;
        // 96 fits LAN list + HiDPI; larger (128+) often blows a single UDP datagram after base64.
        public const int AvatarPixelSize = 96;
        // Base64 expands ~4/3; leave room for JSON wrapper under MaxPacketBytes.
        public const int MaxAvatarJpegBytes = 5500;
        public const int AvatarRequestRetryMs = 15000;
        // Untrusted peer JPEG only (we send 96×96; reject absurd decode sizes).
        public const int MaxAvatarDecodeDimension = 256;
        // Local account_avatar.jpg is often larger than 256 (Steam uploads); downscale after load.
        public const int MaxLocalAvatarDecodeDimension = 4096;
        // Accept/write rate limits (per Steam64 / per remote IP).
        public const int AvatarAcceptMaxPerPeer = 4;
        public const int AvatarAcceptWindowMs = 60000;
        public const int AvatarReplyMaxPerRemote = 8;
        public const int AvatarReplyWindowMs = 60000;

        public const string AvatarCacheFolderName = "lan_presence_avatars";
    }
}
