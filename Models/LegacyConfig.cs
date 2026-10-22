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

    // Legacy 2.x per-game XML node; only the fields the importer maps are declared (XmlSerializer skips the rest).
    public class LegacyGameEntry
    {
        public string StartFolder { get; set; }
        public string AppName { get; set; }
        public ulong AppId { get; set; }
        public string Parameters { get; set; }
        public bool UseX64 { get; set; } = true;
        public bool DisableNetworking { get; set; }
        public bool DisableLANOnly { get; set; }
        public bool SteamDeck { get; set; }
        public bool Offline { get; set; }
        public bool UnlockAllDLC { get; set; }
        public string CustomIcon { get; set; }
        public string CustomBroadcasts { get; set; }
        public System.Guid GameGuid { get; set; }
        public string Path { get; set; }
    }
}
