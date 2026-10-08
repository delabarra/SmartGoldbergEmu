using System;
using System.Xml.Serialization;
using AppDataKit;

namespace SmartGoldbergEmu.Models
{
    // Game identity only; emulation settings live in Goldberg config files.
    public class GameConfig
    {
        public string AppName { get; set; }

        public ulong AppId { get; set; }

        public string StartFolder { get; set; }

        // Relative to StartFolder when not rooted, like the Steam manifest executable.
        public string Path { get; set; }

        public string Parameters { get; set; }

        // Relative to StartFolder when not rooted; used only when no Steam (PICS) launch option supplies one.
        public string WorkingDirectory { get; set; }

        // .exe, .bat, or .ico file.
        public string CustomIcon { get; set; }

        public Guid GameGuid { get; set; }

        public GoldbergLaunchMode LaunchMode { get; set; }

        [XmlIgnore]
        public AppInfoKeyValue AppInfo { get; set; }

        [XmlIgnore]
        public System.Collections.Generic.Dictionary<long, string> PreFetchedDlcData { get; set; }

        // Persisted separately as resources/{appId}.json via AppCatalogSnapshotStore.
        [XmlIgnore]
        public AppCatalogSnapshot Catalog { get; set; }

        [XmlIgnore]
        public System.Collections.Generic.List<string> SupportedLanguages { get; set; }

        [XmlIgnore]
        public bool DlcCheckPerformed { get; set; }

        // Drop in-memory Steam trees after they are written to disk / no longer needed by the UI row.
        public void ReleaseHeavyRuntimeData()
        {
            if (Catalog?.Items != null)
                Catalog.Items.ArchiveJson = null;

            AppInfo = null;
            Catalog = null;
            PreFetchedDlcData = null;
            SupportedLanguages = null;
        }

        public GameConfig()
        {
            AppName = string.Empty;
            AppId = 0;
            StartFolder = string.Empty;
            Path = string.Empty;
            Parameters = string.Empty;
            WorkingDirectory = string.Empty;
            CustomIcon = string.Empty;
            GameGuid = Guid.NewGuid();
            LaunchMode = GoldbergLaunchMode.SteamClient;
        }
    }
}
