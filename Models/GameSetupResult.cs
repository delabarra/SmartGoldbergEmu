using SteamKit;

namespace SmartGoldbergEmu.Models
{
    public class GameSetupResult
    {
        public ulong AppId { get; set; }
        public string GameName { get; set; }
        public OnlineAppData Metadata { get; set; }

        /// <summary>
        /// When metadata came from Steam PICS, the in-memory app product info root (same as <see cref="GameConfig.AppPicsKeyValue"/>).
        /// </summary>
        public KeyValue AppPicsKeyValue { get; set; }

        public bool Cancelled { get; set; }

        // True when metadata could not be loaded (status strip already shows an error).
        public bool MetadataFetchFailed { get; set; }
    }
}
