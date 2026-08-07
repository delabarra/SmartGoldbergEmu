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

        // In-memory app product info root (AppDataKit steamcmd, or PICS converted once via ConvertFromSteamKit).
        public AppInfoKeyValue AppInfo { get; set; }

        // DLC ids/names collected during setup (AppDataKit Store names when available).
        public Dictionary<long, string> PreFetchedDlcData { get; set; }

        // Kit-first catalog SoT captured during setup; Metadata/AppInfo/PreFetchedDlcData above stay in sync with this for compatibility.
        public AppCatalogSnapshot Catalog { get; set; }

        public bool Cancelled { get; set; }

        // True when metadata could not be loaded (status strip already shows an error).
        public bool MetadataFetchFailed { get; set; }
    }
}
