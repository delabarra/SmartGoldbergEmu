using System;
using System.Collections.Generic;

namespace SmartGoldbergEmu.Helpers
{
    // Semantic version comparison (v-prefix, MAJOR.MINOR.PATCH, optional -suffix).
    public static class VersionComparisonHelper
    {
        // True when latest is newer than current.
        public static bool IsNewerVersion(string current, string latest)
        {
            if (string.IsNullOrEmpty(latest))
                return false;
            if (string.IsNullOrEmpty(current))
                return true;

            if (current.Equals("pre-existent", StringComparison.OrdinalIgnoreCase))
                return true;

            int[] currentParts = ParseVersion(current);
            int[] latestParts = ParseVersion(latest);
            int length = Math.Max(currentParts.Length, latestParts.Length);

            for (int i = 0; i < length; i++)
            {
                int c = i < currentParts.Length ? currentParts[i] : 0;
                int l = i < latestParts.Length ? latestParts[i] : 0;
                if (l > c)
                    return true;
                if (l < c)
                    return false;
            }

            return false;
        }

        private static int[] ParseVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
                return new[] { 0, 0, 0 };

            version = version.Trim();
            if (version.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                version = version.Substring(1);

            int plus = version.IndexOf('+');
            if (plus >= 0)
                version = version.Substring(0, plus).Trim();

            int dash = version.IndexOf('-');
            if (dash >= 0)
                version = version.Substring(0, dash);

            string[] segments = version.Split('.');
            var parts = new List<int>(segments.Length);
            foreach (string segment in segments)
            {
                if (string.IsNullOrEmpty(segment))
                    continue;

                int digitEnd = 0;
                while (digitEnd < segment.Length && char.IsDigit(segment[digitEnd]))
                    digitEnd++;

                if (digitEnd == 0)
                    break;

                if (!int.TryParse(segment.Substring(0, digitEnd), out int value))
                    break;

                parts.Add(value);
            }

            if (parts.Count == 0)
                return new[] { 0, 0, 0 };

            return parts.ToArray();
        }
    }
}
