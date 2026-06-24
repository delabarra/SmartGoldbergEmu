using System;
using System.Text.RegularExpressions;

namespace SmartGoldbergEmu.Helpers
{
    public static class GoldbergVersionHelper
    {
        private static readonly Regex ForkDateVersionRegex = new Regex(
            @"(\d{4})_(\d{2})_(\d{2})",
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

            for (int i = 0; i < 3; i++)
            {
                if (leftParts[i] != rightParts[i])
                    return leftParts[i].CompareTo(rightParts[i]);
            }

            return 0;
        }

        private static int[] ParseForkDateParts(string normalized)
        {
            string[] segments = normalized.Split('_');
            if (segments.Length != 3)
                return new[] { 0, 0, 0 };

            int[] parts = new int[3];
            for (int i = 0; i < 3; i++)
            {
                if (!int.TryParse(segments[i], out parts[i]))
                    parts[i] = 0;
            }

            return parts;
        }
    }
}
