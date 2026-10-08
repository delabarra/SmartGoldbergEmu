namespace SteamKit
{
    // Values 996-999 are local to this app, not Steam EResult wire constants; Ok matches Steam OK.
    public static class SteamLogonWaitResult
    {
        public const uint Ok = 1;

        public const uint WaitTimedOut = 996;

        public const uint LogonResponseParseFailed = 997;

        // Includes an intentional SteamClient.Disconnect before logon completed.
        public const uint DisconnectedWhileWaiting = 998;

        // No CM handshake completed.
        public const uint ConnectionFailed = 999;
    }
}
