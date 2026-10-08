using System;
using System.Collections.Generic;
using AppDataKit;

namespace SmartGoldbergEmu.Models
{
    public class GameSetupResult
    {
        public ulong AppId { get; set; }
        public string GameName { get; set; }
        public OnlineAppData Metadata { get; set; }

        // From AppDataKit steamcmd, or PICS converted once via ConvertFromSteamKit.
        public AppInfoKeyValue AppInfo { get; set; }

        // Uses AppDataKit Store names when available.
        public Dictionary<long, string> PreFetchedDlcData { get; set; }

        // Metadata, AppInfo, and PreFetchedDlcData must stay in sync with this catalog.
        public AppCatalogSnapshot Catalog { get; set; }

        public bool Cancelled { get; set; }

        // True when metadata could not be loaded (status strip already shows an error).
        public bool MetadataFetchFailed { get; set; }
    }
}
