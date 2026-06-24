using System;

namespace SmartGoldbergEmu.Models
{
    /// <summary>
    /// GitHub release source for downloading the Windows Goldberg emulator archive.
    /// </summary>
    public enum GoldbergForkSource
    {
        Detanup,
        Alex
    }

    /// <summary>
    /// Parses and formats <see cref="GoldbergForkSource"/> for INI storage.
    /// </summary>
    public static class GoldbergForkSourceIni
    {
        public const string ValueDetanup = "detanup";
        public const string ValueAlex = "alex";

        public static GoldbergForkSource Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return GoldbergForkSource.Detanup;
            if (string.Equals(value.Trim(), ValueAlex, StringComparison.OrdinalIgnoreCase))
                return GoldbergForkSource.Alex;
            return GoldbergForkSource.Detanup;
        }

        public static string ToStorageValue(GoldbergForkSource source)
        {
            return source == GoldbergForkSource.Alex ? ValueAlex : ValueDetanup;
        }
    }
}
