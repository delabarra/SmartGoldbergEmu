namespace SmartGoldbergEmu.Constants
{
    // LAN sticky-note chat over the presence UDP port (hostile-LAN hardening, not a messenger).
    public static class LanChatConstants
    {
        public const string MessageRequest = "cq";
        public const string MessageDecision = "cd";
        public const string MessageText = "cm";

        public const string DecisionAccept = "a";
        public const string DecisionIgnore = "i";
        public const string DecisionBlock = "b";

        // Enough for short INI / AppId snippets; keeps a single UDP datagram.
        public const int MaxMessageChars = 500;
        public const int MaxMessageIdChars = 32;

        public const int InviteMaxPerPeer = 3;
        public const int InviteWindowMs = 60000;
        public const int MessageMaxPerPeer = 20;
        public const int MessageWindowMs = 60000;

        // Soft-ignore cooldown before the same peer may invite again.
        public const int SoftIgnoreCooldownMs = 30000;

        public const int MaxRecentMessageIds = 64;

        public static bool IsChatType(string type)
        {
            return string.Equals(type, MessageRequest, System.StringComparison.Ordinal)
                || string.Equals(type, MessageDecision, System.StringComparison.Ordinal)
                || string.Equals(type, MessageText, System.StringComparison.Ordinal);
        }
    }
}
