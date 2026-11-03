using System;

namespace SmartGoldbergEmu.Models
{
    public sealed class LanPresencePeer
    {
        public string SteamId { get; set; }
        public string DisplayName { get; set; }
        public ulong AppId { get; set; }
        public string GameName { get; set; }
        public string AvatarHash { get; set; }
        public string AvatarFilePath { get; set; }
        public DateTime LastSeenUtc { get; set; }

        // Last UDP source IP for this Steam64 (unicast chat / bind checks). Not shown in the list UI.
        public string RemoteAddress { get; set; }

        // Fresh heartbeat within expire window.
        public bool IsPresent { get; set; }

        public LanPersonaState PersonaState { get; set; }
        public string CustomStatus { get; set; }
        public ulong LookingForPlayAppId { get; set; }
        public string LookingForPlayGameName { get; set; }

        public bool IsPlaying => AppId != 0;
        public bool IsLookingForPlay => LookingForPlayAppId != 0;
    }
}
