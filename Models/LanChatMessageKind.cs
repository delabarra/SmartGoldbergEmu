namespace SmartGoldbergEmu.Models
{
    public enum LanChatMessageKind
    {
        Text = 0,
        // Local-only: sender confirmation after a lobby invite UDP nudge.
        GameInviteSent = 1,
        // Inbound lobby invite display line; Accept uses session pending payload, never this Text.
        GameInvite = 2,
        // Local-only outcome after Accept / Decline.
        GameInviteResult = 3,
        // Local-only: recipient replied that lobby invites are on cooldown.
        GameInviteCooldown = 4
    }
}
