namespace SmartGoldbergEmu.Models
{
    public enum LanChatMessageKind
    {
        Text = 0,
        // Local-only: sender confirmation after a game invite UDP nudge.
        GameInviteSent = 1,
        // Inbound game invite display line; Accept uses session pending payload, never this Text.
        GameInvite = 2,
        // Local-only outcome after Accept / Decline.
        GameInviteResult = 3,
        // Local-only: recipient replied that game invites are on cooldown.
        GameInviteCooldown = 4
    }
}
