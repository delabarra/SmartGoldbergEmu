using System.Collections.Generic;

namespace SmartGoldbergEmu.Models
{
    // Loaded once per edit-dialog open.
    public class GameEditBundle
    {
        public GameConfig Game { get; set; }

        public GameSettingsSnapshot SettingsSnapshot { get; set; }

        public GameEditSidecarContent Sidecars { get; set; }

        public Dictionary<long, string> DlcData { get; set; }

        // Loaded from resources/{appId}.json; null when not yet fetched.
        public AppCatalogSnapshot Catalog { get; set; }

        // Ticket and alt SteamID recovered from the registry for display when absent from the per-game INI.
        public string RegistryTicket { get; set; }

        public string RegistryAltSteamId { get; set; }

        public GameEditBundle()
        {
            Game = new GameConfig();
            SettingsSnapshot = new GameSettingsSnapshot();
            Sidecars = new GameEditSidecarContent();
            DlcData = new Dictionary<long, string>();
        }

        public void ReleaseHeavyRuntimeData()
        {
            Catalog = null;
            DlcData = null;
            Sidecars = null;
            SettingsSnapshot = null;
            Game?.ReleaseHeavyRuntimeData();
        }
    }
}
