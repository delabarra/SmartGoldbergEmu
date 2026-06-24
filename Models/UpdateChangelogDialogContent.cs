using System.Collections.Generic;

namespace SmartGoldbergEmu.Models
{
    public class UpdateChangelogDialogContent
    {
        public string FormTitle { get; set; }

        public string Headline { get; set; }

        public string ReleaseNotes { get; set; }

        public string AdditionalInfo { get; set; }

        public string ProceedQuestion { get; set; }

        public IList<UpdateManualDownloadLink> ManualDownloadLinks { get; set; }
    }

    public class UpdateManualDownloadLink
    {
        public string Label { get; set; }

        public string Url { get; set; }
    }
}
