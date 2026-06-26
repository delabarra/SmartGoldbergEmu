using System;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Constants
{
    // URLs and INI keys for selecting which Goldberg fork to download from.
    public static class GoldbergForkConstants
    {
        public const string IniSection = "emulator";
        public const string IniKeyFork = "goldberg_fork";
        public const string IniKeyVersion = "goldberg_version";

        public const string RepackReleasesApiUrl = "https://api.github.com/repos/delabarra/GoldbergEmu-Forks-Repacked/releases/latest";
        public const string RepackRepositoryWebUrl = "https://github.com/delabarra/GoldbergEmu-Forks-Repacked";
        public const string RepackWinAssetSuffix7z = "-win.7z";
        public const string RepackWinAssetSuffixZip = "-win.zip";
        public const string RepackDownloadArchiveFileName = "goldberg-download.zip";
        public const string UpstreamDownloadArchiveFileName = "goldberg-download.7z";

        public const string ReleasesApiUrlDetanup = "https://api.github.com/repos/Detanup01/gbe_fork/releases/latest";
        public const string ReleasesApiUrlAlex = "https://api.github.com/repos/alex47exe/gse_fork/releases/latest";

        public const string RepositoryWebUrlDetanup = "https://github.com/Detanup01/gbe_fork";
        public const string RepositoryWebUrlAlex = "https://github.com/alex47exe/gse_fork";

        public const string UpstreamWinReleaseAssetName = "emu-win-release.7z";
        public const string WinReleaseAssetName = UpstreamWinReleaseAssetName;

        public static string TryGetRepackWinAssetSuffix(string assetName)
        {
            if (string.IsNullOrEmpty(assetName))
                return null;
            if (assetName.EndsWith(RepackWinAssetSuffix7z, StringComparison.OrdinalIgnoreCase))
                return RepackWinAssetSuffix7z;
            if (assetName.EndsWith(RepackWinAssetSuffixZip, StringComparison.OrdinalIgnoreCase))
                return RepackWinAssetSuffixZip;
            return null;
        }

        public static string GetLocalDownloadArchiveFileName(bool fromRepack, string releaseAssetFileName)
        {
            if (fromRepack
                && !string.IsNullOrEmpty(releaseAssetFileName)
                && releaseAssetFileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                return RepackDownloadArchiveFileName;
            }

            return UpstreamDownloadArchiveFileName;
        }

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

        public static string GetForkDisplayName(GoldbergForkSource fork)
        {
            return fork == GoldbergForkSource.Alex ? "alex47exe" : "Detanup01";
        }
    }
}
