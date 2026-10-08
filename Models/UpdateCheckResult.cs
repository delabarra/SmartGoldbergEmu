namespace SmartGoldbergEmu.Models
{
    public class UpdateCheckResult
    {
        public bool Success { get; set; }

        // Alias for Success, matching other result types.
        public bool IsSuccess => Success;

        public bool UpdateAvailable { get; set; }

        public string CurrentVersion { get; set; }

        public string LatestVersion { get; set; }

        public string ReleaseNotes { get; set; }

        public bool TimedOut { get; set; }

        public string ErrorMessage { get; set; }

        public string DownloadUrl { get; set; }

        // Release comes from the Goldberg repack rather than upstream fork releases.
        public bool FromRepack { get; set; }
    }
}

