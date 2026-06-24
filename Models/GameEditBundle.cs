using System.Collections.Generic;

namespace SmartGoldbergEmu.Models
{
    /// <summary>
    /// In-memory snapshot of an existing library game for edit mode (loaded once per dialog open).
    /// </summary>
    public class GameEditBundle
    {
        public GameConfig Game { get; set; }

        public GameSettingsSnapshot SettingsSnapshot { get; set; }

        public GameEditSidecarContent Sidecars { get; set; }

        public Dictionary<long, string> DlcData { get; set; }

        /// <summary>Ticket/alt values loaded from registry when absent in per-game INI (display recovery).</summary>
        public string RegistryTicket { get; set; }

        public string RegistryAltSteamId { get; set; }

        public GameEditBundle()
        {
            Game = new GameConfig();
            SettingsSnapshot = new GameSettingsSnapshot();
            Sidecars = new GameEditSidecarContent();
            DlcData = new Dictionary<long, string>();
        }
    }
}
