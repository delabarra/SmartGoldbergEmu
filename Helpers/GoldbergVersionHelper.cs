using System;
using System.Text.RegularExpressions;

namespace SmartGoldbergEmu.Helpers
{
    public static class GoldbergVersionHelper
    {
        // Detanup tags/assets use YYYY_MM_DD, or YYYY_MM_DD_N for same-day rebuilds (e.g. 2026_09_16_2).
        private static readonly Regex ForkDateVersionRegex = new Regex(
            @"(\d{4})_(\d{2})_(\d{2})(?:_(\d+))?",
            RegexOptions.Compiled);

        public static bool TryNormalizeForkVersion(string raw, out string normalized)
        {
            normalized = null;
            if (string.IsNullOrWhiteSpace(raw))
                return false;

            Match match = ForkDateVersionRegex.Match(raw.Trim());
            if (!match.Success)
                return false;

            normalized = match.Groups[1].Value + "_" + match.Groups[2].Value + "_" + match.Groups[3].Value;
            if (match.Groups[4].Success)
                normalized += "_" + match.Groups[4].Value;
            return true;
        }

        public static bool IsNewerGoldbergVersion(string current, string latest)
        {
            if (string.IsNullOrEmpty(latest))
                return false;
            if (string.IsNullOrEmpty(current))
                return true;

            if (current.Equals("pre-existent", StringComparison.OrdinalIgnoreCase))
                return true;

            if (TryNormalizeForkVersion(current, out string currentNormalized)
                && TryNormalizeForkVersion(latest, out string latestNormalized))
            {
                return CompareForkDateVersions(currentNormalized, latestNormalized) < 0;
            }

            return VersionComparisonHelper.IsNewerVersion(current, latest);
        }

        private static int CompareForkDateVersions(string left, string right)
        {
            int[] leftParts = ParseForkDateParts(left);
            int[] rightParts = ParseForkDateParts(right);

            for (int i = 0; i < 4; i++)
            {
                if (leftParts[i] != rightParts[i])
                    return leftParts[i].CompareTo(rightParts[i]);
            }

            return 0;
        }

        private static int[] ParseForkDateParts(string normalized)
        {
            string[] segments = normalized.Split('_');
            var parts = new int[4];
            int count = segments.Length < 4 ? segments.Length : 4;
            for (int i = 0; i < count; i++)
            {
                if (!int.TryParse(segments[i], out parts[i]))
                    parts[i] = 0;
            }

            // Bare YYYY_MM_DD sorts as rebuild 0, so YYYY_MM_DD_1 is newer.
            return parts;
        }
    }
}
