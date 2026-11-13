using System;

namespace SmartGoldbergEmu.Models
{
    public sealed class LanGameInviteEventArgs : EventArgs
    {
        public string FromSteamId { get; set; }
        public string FromDisplayName { get; set; }
        public ulong AppId { get; set; }
        public string GameName { get; set; }

        // Optional host-probed connect / +connect_lobby fragment for Accept → launch.
        public string JoinArguments { get; set; }

        // Optional host launch-option identity so Accept can skip the picker when it matches locally.
        public LanLaunchOptionHint LaunchHint { get; set; }
    }

    public sealed class LanGameInviteCooldownEventArgs : EventArgs
    {
        public string FromSteamId { get; set; }
        public int RemainingSeconds { get; set; }
    }
}
