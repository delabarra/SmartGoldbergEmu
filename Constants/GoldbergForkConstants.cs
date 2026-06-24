using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Constants
{
    /// <summary>
    /// URLs and INI keys for selecting which Goldberg fork to download from.
    /// </summary>
    public static class GoldbergForkConstants
    {
        public const string IniSection = "emulator";
        public const string IniKeyFork = "goldberg_fork";
        public const string IniKeyVersion = "goldberg_version";

        public const string RepackReleasesApiUrl = "https://api.github.com/repos/delabarra/GoldbergEmu-Forks-Repacked/releases/latest";
        public const string RepackRepositoryWebUrl = "https://github.com/delabarra/GoldbergEmu-Forks-Repacked";
        public const string RepackWinAssetSuffix = "-win.zip";
        public const string RepackDownloadArchiveFileName = "goldberg-download.zip";
        public const string UpstreamDownloadArchiveFileName = "goldberg-download.7z";

        public const string ReleasesApiUrlDetanup = "https://api.github.com/repos/Detanup01/gbe_fork/releases/latest";
        public const string ReleasesApiUrlAlex = "https://api.github.com/repos/alex47exe/gse_fork/releases/latest";

        public const string RepositoryWebUrlDetanup = "https://github.com/Detanup01/gbe_fork";
        public const string RepositoryWebUrlAlex = "https://github.com/alex47exe/gse_fork";

        /// <summary>
        /// Windows release asset name on upstream fork releases.
        /// </summary>
        public const string UpstreamWinReleaseAssetName = "emu-win-release.7z";

        /// <summary>
        /// Alias kept for call sites that refer to the upstream asset name.
        /// </summary>
        public const string WinReleaseAssetName = UpstreamWinReleaseAssetName;

        public static string GetRepackAssetNamePrefix(GoldbergForkSource fork)
        {
            return fork == GoldbergForkSource.Alex ? "alex47exe-" : "Detanup01-";
        }

        public static string GetUpstreamReleasesApiUrl(GoldbergForkSource fork)
        {
            return fork == GoldbergForkSource.Alex ? ReleasesApiUrlAlex : ReleasesApiUrlDetanup;
        }

        public static string GetReleasesApiUrl(GoldbergForkSource fork)
        {
            return GetUpstreamReleasesApiUrl(fork);
        }

        /// <summary>GitHub-style fork label for user-facing text (matches repo owners).</summary>
        public static string GetForkDisplayName(GoldbergForkSource fork)
        {
            return fork == GoldbergForkSource.Alex ? "alex47exe" : "Detanup01";
        }
    }
}
