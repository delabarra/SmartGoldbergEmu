namespace SteamKit
{
    /// <summary>
    /// Outcomes used while <see cref="SteamClient"/> establishes an anonymous CM session and the host
    /// awaits <see cref="SteamClient.OnLoggedOn"/>. Values 996–999 are local to this app, not official
    /// Steam EResult wire constants (except <see cref="Ok"/>, which matches Steam OK).
    /// </summary>
    public static class SteamLogonWaitResult
    {
        public const uint Ok = 1;

        /// <summary>No terminal signal before the wait budget elapsed.</summary>
        public const uint WaitTimedOut = 996;

        /// <summary><see cref="ClientMsgProtobuf.TryParseLogOnResponse"/> failed.</summary>
        public const uint LogonResponseParseFailed = 997;

        /// <summary>Socket closed before logon completed (includes intentional <see cref="SteamClient.Disconnect"/>).</summary>
        public const uint DisconnectedWhileWaiting = 998;

        /// <summary><see cref="SteamClient.OnConnectionFailed"/> (no CM / handshake completed).</summary>
        public const uint ConnectionFailed = 999;
    }
}
