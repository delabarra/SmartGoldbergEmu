using System;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.JsonKit;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Helpers
{
    public sealed class GoldbergResolvedRelease
    {
        public string DownloadUrl { get; set; }
        public string LatestVersion { get; set; }
        public string ReleaseNotes { get; set; }
        public string ArchiveFileName { get; set; }
        public bool FromRepack { get; set; }
    }

    public static class GoldbergReleaseResolveHelper
    {
        public static bool TryParseRepackRelease(string json, GoldbergForkSource fork, GoldbergResolvedRelease result)
        {
            if (string.IsNullOrWhiteSpace(json) || result == null)
                return false;

            JsonObject releaseData = JsonObject.Parse(json);
            string prefix = GoldbergForkConstants.GetRepackAssetNamePrefix(fork);

            string downloadUrl = null;
            string archiveFileName = null;
            string matchedSuffix = null;
            foreach (JsonObject asset in (JsonArray)releaseData["assets"])
            {
                string name = asset["name"]?.ToString();
                if (string.IsNullOrEmpty(name))
                    continue;
                if (!name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                string suffix = GoldbergForkConstants.TryGetRepackWinAssetSuffix(name);
                if (suffix == null)
                    continue;

                // Current repack publishes .7z; keep .zip for older releases.
                if (matchedSuffix == GoldbergForkConstants.RepackWinAssetSuffix7z
                    && suffix == GoldbergForkConstants.RepackWinAssetSuffixZip)
                {
                    continue;
                }

                downloadUrl = asset["browser_download_url"]?.ToString();
                archiveFileName = name;
                matchedSuffix = suffix;
                if (suffix == GoldbergForkConstants.RepackWinAssetSuffix7z)
                    break;
            }

            if (string.IsNullOrEmpty(downloadUrl) || string.IsNullOrEmpty(archiveFileName))
                return false;

            if (!TryExtractRepackForkVersion(archiveFileName, prefix, matchedSuffix, out string forkVersion))
                return false;

            result.DownloadUrl = downloadUrl;
            result.LatestVersion = forkVersion;
            result.ReleaseNotes = releaseData["body"]?.ToString();
            result.ArchiveFileName = archiveFileName;
            result.FromRepack = true;
            return true;
        }

        public static bool TryParseUpstreamRelease(string json, GoldbergResolvedRelease result)
        {
            if (string.IsNullOrWhiteSpace(json) || result == null)
                return false;

            JsonObject releaseData = JsonObject.Parse(json);
            string tagName = releaseData["tag_name"]?.ToString();
            string downloadUrl = null;

            foreach (JsonObject asset in (JsonArray)releaseData["assets"])
            {
                if (asset["name"]?.ToString() == GoldbergForkConstants.UpstreamWinReleaseAssetName)
                {
                    downloadUrl = asset["browser_download_url"]?.ToString();
                    break;
                }
            }

            if (string.IsNullOrEmpty(downloadUrl))
                return false;

            string latestVersion = tagName;
            if (GoldbergVersionHelper.TryNormalizeForkVersion(tagName, out string normalizedVersion))
                latestVersion = normalizedVersion;

            result.DownloadUrl = downloadUrl;
            result.LatestVersion = latestVersion;
            result.ReleaseNotes = releaseData["body"]?.ToString();
            result.ArchiveFileName = GoldbergForkConstants.UpstreamWinReleaseAssetName;
            result.FromRepack = false;
            return true;
        }

        private static bool TryExtractRepackForkVersion(string archiveFileName, string prefix, string suffix, out string version)
        {
            version = null;
            if (string.IsNullOrEmpty(archiveFileName)
                || archiveFileName.Length <= prefix.Length + suffix.Length)
            {
                return false;
            }

            version = archiveFileName.Substring(prefix.Length, archiveFileName.Length - prefix.Length - suffix.Length);
            return GoldbergVersionHelper.TryNormalizeForkVersion(version, out string normalized)
                && string.Equals(version, normalized, StringComparison.Ordinal);
        }
    }
}
