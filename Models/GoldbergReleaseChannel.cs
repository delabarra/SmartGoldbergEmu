using System;

namespace SmartGoldbergEmu.Models
{
    // Prefer repack, then upstream (Auto); or force one source (DEBUG fork picker).
    public enum GoldbergReleaseChannel
    {
        Auto,
        Repack,
        Upstream
    }

    public static class GoldbergReleaseChannelIni
    {
        public const string ValueAuto = "auto";
        public const string ValueRepack = "repack";
        public const string ValueUpstream = "upstream";

        public static GoldbergReleaseChannel Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return GoldbergReleaseChannel.Auto;
            string trimmed = value.Trim();
            if (string.Equals(trimmed, ValueRepack, StringComparison.OrdinalIgnoreCase))
                return GoldbergReleaseChannel.Repack;
            if (string.Equals(trimmed, ValueUpstream, StringComparison.OrdinalIgnoreCase))
                return GoldbergReleaseChannel.Upstream;
            return GoldbergReleaseChannel.Auto;
        }

        public static string ToStorageValue(GoldbergReleaseChannel channel)
        {
            switch (channel)
            {
                case GoldbergReleaseChannel.Repack:
                    return ValueRepack;
                case GoldbergReleaseChannel.Upstream:
                    return ValueUpstream;
                default:
                    return ValueAuto;
            }
        }

        // Auto prefers repack first; DEBUG fork UI maps Auto onto the Repack radio.
        public static bool AreEquivalent(GoldbergReleaseChannel left, GoldbergReleaseChannel right)
        {
            if (left == right)
                return true;
            return IsRepackPreferring(left) && IsRepackPreferring(right);
        }

        private static bool IsRepackPreferring(GoldbergReleaseChannel channel)
        {
            return channel == GoldbergReleaseChannel.Auto || channel == GoldbergReleaseChannel.Repack;
        }
    }
}
