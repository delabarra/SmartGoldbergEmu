using System;
using System.Collections.Generic;
using AppDataKit;

namespace SmartGoldbergEmu.Models
{
    public enum AppMetadataFetchFailure
    {
        None,
        TimedOut,
        Unavailable
    }

    // Kit-first catalog SoT for one AppID (persisted as resources/{appId}.json).
    public sealed class AppCatalogSnapshot
    {
        public uint AppId { get; set; }
        public DateTime FetchedAtUtc { get; set; }

        public AppMetadataSection Metadata { get; set; }
        public DlcSection Dlc { get; set; }
        public GameAssetsSection Assets { get; set; }
        public AchievementsSection Achievements { get; set; }
        public StatsSection Stats { get; set; }
        public ItemsSection Items { get; set; }

        // Thin display adapter (not a parallel SoT).
        public OnlineAppData Online { get; set; }

        // AppInfoKeyValue is the catalog SoT tree (steamcmd or PICS-recovered, converted once).
        public AppInfoKeyValue AppInfo => Metadata?.AppInfo;

        public IReadOnlyList<uint> AppDepotIds { get; set; } = Array.Empty<uint>();
        public string InstallDir { get; set; }
        public string SupportedLanguages { get; set; }

        public bool FromAppDataKit { get; set; }
        public AppMetadataFetchFailure Failure { get; set; }

        public bool IsUsable
        {
            get
            {
                return Online != null && !string.IsNullOrWhiteSpace(Online.Name);
            }
        }

        public Dictionary<long, string> ToDlcDictionary()
        {
            var map = new Dictionary<long, string>();
            if (Dlc?.Items == null)
                return map;

            foreach (DlcEntry entry in Dlc.Items)
            {
                if (entry == null || entry.AppId == 0)
                    continue;
                long id = entry.AppId;
                string name = string.IsNullOrWhiteSpace(entry.Name) ? ("DLC " + id) : entry.Name.Trim();
                map[id] = name;
            }

            return map;
        }

        public void ApplyDlcDictionary(Dictionary<long, string> dlc)
        {
            if (dlc == null || dlc.Count == 0)
            {
                Dlc = new DlcSection
                {
                    Status = SnapshotSectionStatus.Ok,
                    Items = Array.Empty<DlcEntry>()
                };
                return;
            }

            var items = new List<DlcEntry>(dlc.Count);
            foreach (KeyValuePair<long, string> kvp in dlc)
            {
                if (kvp.Key <= 0 || kvp.Key > uint.MaxValue)
                    continue;
                items.Add(new DlcEntry
                {
                    AppId = (uint)kvp.Key,
                    Name = string.IsNullOrWhiteSpace(kvp.Value) ? ("DLC " + kvp.Key) : kvp.Value.Trim(),
                    Type = "dlc"
                });
            }

            Dlc = new DlcSection
            {
                Status = SnapshotSectionStatus.Ok,
                Items = items
            };
        }
    }
}
