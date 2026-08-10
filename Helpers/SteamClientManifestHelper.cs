using System;
using System.Text.RegularExpressions;
using SmartGoldbergEmu.Constants;

namespace SmartGoldbergEmu.Helpers
{
    // Parses Valve KeyValues snippets from steam_client_win32 (and similar) manifests.
    public static class SteamClientManifestHelper
    {
        // Returns the bins_win32 package file name (e.g. bins_win32.zip.vz.…), or null.
        public static bool TryGetBinsWin32ZipVzFileName(string manifestText, out string zipVzFileName)
        {
            return TryGetPackageZipVzFileName(manifestText, "bins_win32", out zipVzFileName);
        }

        // Returns the resources_all package file name (contains clientui\images), or null.
        public static bool TryGetResourcesAllZipVzFileName(string manifestText, out string zipVzFileName)
        {
            return TryGetPackageZipVzFileName(
                manifestText,
                SteamStaticCdnConstants.ClientResourcesAllPackageName,
                out zipVzFileName);
        }

        public static bool TryGetPackageZipVzFileName(
            string manifestText,
            string packageName,
            out string zipVzFileName)
        {
            zipVzFileName = null;
            if (string.IsNullOrWhiteSpace(manifestText) || string.IsNullOrWhiteSpace(packageName))
                return false;

            string pattern = "\"" + Regex.Escape(packageName.Trim()) + "\"\\s*\\{[^}]*\"zipvz\"\\s*\"([^\"]+)\"";
            Match match = Regex.Match(
                manifestText,
                pattern,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!match.Success || match.Groups.Count < 2)
                return false;

            string value = match.Groups[1].Value.Trim();
            if (string.IsNullOrEmpty(value)
                || value.IndexOf(packageName.Trim(), StringComparison.OrdinalIgnoreCase) < 0
                || value.IndexOf(".zip.vz.", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            zipVzFileName = value;
            return true;
        }

        public static string BuildClientPackageRelativePath(string zipVzFileName)
        {
            if (string.IsNullOrWhiteSpace(zipVzFileName))
                return null;
            return "client/" + zipVzFileName.Trim().TrimStart('/');
        }
    }
}
