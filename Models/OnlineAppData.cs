using System.Collections.Generic;

namespace SmartGoldbergEmu.Models
{
    /// <summary>
    /// App metadata snapshot (filled from Steam PICS product info).
    /// </summary>
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
        /// <summary>
        /// Steam manifest-style install folder name under <c>steamapps/common</c> (from PICS <c>config.installdir</c>), e.g. <c>Duke Nukem 3D</c>.
        /// </summary>
        public string InstallDir { get; set; }
        public bool Success { get; set; }
    }
}

