using System.Collections.Generic;

namespace SmartGoldbergEmu.Models
{
    public class ChangelogDialogContent
    {
        public string FormTitle { get; set; }

        public string Headline { get; set; }

        public string ReleaseNotes { get; set; }

        public string AdditionalInfo { get; set; }

        // Null uses the form default; empty hides the proceed prompt (view-only).
        public string ProceedQuestion { get; set; }

        public string OkButtonText { get; set; }

        public bool ShowCancelButton { get; set; } = true;

        // When false, link rows render without the "You can also…" caption (inline one-line style).
        public bool ShowManualDownloadCaption { get; set; } = true;

        public IList<UpdateManualDownloadLink> ManualDownloadLinks { get; set; }
    }

    public class UpdateManualDownloadLink
    {
        public string Label { get; set; }

        public string Url { get; set; }
    }
}
