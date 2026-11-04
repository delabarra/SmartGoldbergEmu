using System;
using System.Collections.Generic;

namespace SmartGoldbergEmu.Models
{
    // UI-safe game invite summary — join args stay in LanChatService until Accept.
    public sealed class LanPendingGameInviteSnapshot
    {
        public ulong AppId { get; set; }
        public string GameName { get; set; }
        public bool HasJoinArguments { get; set; }
    }

    public sealed class LanChatSessionSnapshot
    {
        public string PeerSteamId { get; set; }
        public string PeerDisplayName { get; set; }
        public LanChatSessionStatus Status { get; set; }
        public string PendingInviteMessageId { get; set; }
        public string StatusDetail { get; set; }
        public LanPendingGameInviteSnapshot PendingGameInvite { get; set; }
        public IReadOnlyList<LanChatMessage> Messages { get; set; }
    }

    public sealed class LanChatSessionEventArgs : EventArgs
    {
        public LanChatSessionEventArgs(LanChatSessionSnapshot session)
        {
            Session = session;
        }

        public LanChatSessionSnapshot Session { get; }
    }
}
