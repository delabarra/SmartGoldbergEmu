using System;

namespace SmartGoldbergEmu.Models
{
    public sealed class LanChatMessage
    {
        public string MessageId { get; set; }
        public string FromSteamId { get; set; }
        public string Text { get; set; }
        public DateTime TimestampUtc { get; set; }
        public bool IsLocal { get; set; }
        public LanChatMessageKind Kind { get; set; }
    }
}
