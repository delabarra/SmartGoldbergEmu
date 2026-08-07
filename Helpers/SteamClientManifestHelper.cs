using System;
using System.Text.RegularExpressions;

namespace SmartGoldbergEmu.Helpers
{
    // Parses Valve KeyValues snippets from steam_client_win32 (and similar) manifests.
    public static class SteamClientManifestHelper
    {
        private static readonly Regex BinsWin32ZipVz = new Regex(
            "\"bins_win32\"\\s*\\{[^}]*\"zipvz\"\\s*\"([^\"]+)\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // Returns the bins_win32 package file name (e.g. bins_win32.zip.vz.…), or null.
        public static bool TryGetBinsWin32ZipVzFileName(string manifestText, out string zipVzFileName)
        {
            zipVzFileName = null;
            if (string.IsNullOrWhiteSpace(manifestText))
                return false;

            Match match = BinsWin32ZipVz.Match(manifestText);
            if (!match.Success || match.Groups.Count < 2)
                return false;

            string value = match.Groups[1].Value.Trim();
            if (string.IsNullOrEmpty(value)
                || value.IndexOf("bins_win32", StringComparison.OrdinalIgnoreCase) < 0
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
