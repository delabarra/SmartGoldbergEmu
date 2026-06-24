namespace SmartGoldbergEmu.Models
{
    /// <summary>
    /// Result of checking for emulator updates.
    /// </summary>
    public class UpdateCheckResult
    {
        /// <summary>
        /// Whether the update check completed successfully.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets whether the update check was successful (alias for Success for consistency with other result types).
        /// </summary>
        public bool IsSuccess => Success;

        /// <summary>
        /// Whether an update is available.
        /// </summary>
        public bool UpdateAvailable { get; set; }

        /// <summary>
        /// Currently installed version.
        /// </summary>
        public string CurrentVersion { get; set; }

        /// <summary>
        /// Latest available version from GitHub.
        /// </summary>
        public string LatestVersion { get; set; }

        /// <summary>
        /// Release notes/changelog from GitHub.
        /// </summary>
        public string ReleaseNotes { get; set; }

        /// <summary>
        /// Whether the request timed out.
        /// </summary>
        public bool TimedOut { get; set; }

        /// <summary>
        /// Error message if the check failed.
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Download URL for the update.
        /// </summary>
        public string DownloadUrl { get; set; }

        /// <summary>
        /// True when the resolved release comes from the Goldberg repack (not upstream fork releases).
        /// </summary>
        public bool FromRepack { get; set; }
    }
}

