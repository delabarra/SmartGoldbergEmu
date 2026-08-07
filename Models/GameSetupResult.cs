using System;
using System.Collections.Generic;
using SteamKit;

namespace SmartGoldbergEmu.Models
{
    public class GameSetupResult
    {
        public ulong AppId { get; set; }
        public string GameName { get; set; }
        public OnlineAppData Metadata { get; set; }

        /// <summary>
        /// In-memory app product info root (SteamKit KeyValue from AppDataKit conversion or PICS).
        /// </summary>
        public KeyValue AppPicsKeyValue { get; set; }

        // DLC ids/names collected during setup (AppDataKit Store names when available).
        public Dictionary<long, string> PreFetchedDlcData { get; set; }

        public bool Cancelled { get; set; }

        // True when metadata could not be loaded (status strip already shows an error).
        public bool MetadataFetchFailed { get; set; }
    }
}
