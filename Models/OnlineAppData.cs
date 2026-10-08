using System.Collections.Generic;

namespace SmartGoldbergEmu.Models
{
    public class OnlineAppData
    {
        public string AppId { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public bool IsFree { get; set; }
        public List<long> DlcIds { get; set; } = new List<long>();
        public List<int> Packages { get; set; } = new List<int>();
        public string HeaderImageUrl { get; set; }
        public string CapsuleImageUrl { get; set; }
        public string CapsuleImageV5Url { get; set; }
        public string SupportedLanguages { get; set; }
        public string DataSources { get; set; }
        // Folder name under steamapps/common from PICS config.installdir, e.g. "Duke Nukem 3D".
        public string InstallDir { get; set; }
        public bool Success { get; set; }
    }
}

