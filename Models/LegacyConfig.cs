using System.Collections.Generic;
using System.Xml.Serialization;

namespace SmartGoldbergEmu.Models
{
    [XmlRoot("SavedConf")]
    public class LegacyConfig
    {
        [XmlElement("webapi_key")]
        public string WebApiKey { get; set; }

        [XmlArray("apps")]
        [XmlArrayItem("GameConfig")]
        public List<LegacyGameEntry> Apps { get; set; }
    }

    // Legacy 2.x per-game XML (identity + emulation flags in one node).
    public class LegacyGameEntry
    {
        public string StartFolder { get; set; }
        public string AppName { get; set; }
        public ulong AppId { get; set; }
        public string Parameters { get; set; }
        public bool UseX64 { get; set; } = true;
        public bool DisableOverlay { get; set; }
        public bool DisableNetworking { get; set; }
        public bool DisableLANOnly { get; set; }
        public bool EnableHTTP { get; set; }
        public bool DisableAvatar { get; set; }
        public bool DisableSQuery { get; set; }
        public bool DisableAchNotif { get; set; }
        public bool DisableFriendNotif { get; set; }
        public bool SteamDeck { get; set; }
        public bool AchBypass { get; set; }
        public bool Offline { get; set; }
        public bool UnknownStats { get; set; }
        public bool SaveHigherStat { get; set; } = true;
        public bool GameserverStat { get; set; }
        public bool DisableStatShare { get; set; }
        public bool UnlockAllDLC { get; set; }
        public bool DisLobbyCreation { get; set; }
        public bool ShareLeaderboard { get; set; }
        public bool UnknownLeaderboard { get; set; }
        public bool ActualType { get; set; }
        public bool MatchmakeSource { get; set; }
        public bool HttpSuccess { get; set; }
        public string LocalSave { get; set; }
        public string CustomIcon { get; set; }
        public string CustomBroadcasts { get; set; }
        public string EnvVars { get; set; }
        public System.Guid GameGuid { get; set; }
        public string Path { get; set; }
    }
}
