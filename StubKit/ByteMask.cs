using System;

namespace SmartGoldbergEmu.StubKit
{
    internal sealed class ByteMaskPattern
    {
        public readonly byte[] Needle;
        public readonly byte[] Mask; // 0xFF = compare, 0 = wildcard

        public ByteMaskPattern(byte[] needle, byte[] mask)
        {
            Needle = needle;
            Mask = mask;
        }
    }

    internal static class ByteMask
    {
        public static int Find(byte[] data, ByteMaskPattern pattern)
        {
            if (data == null || pattern == null || pattern.Needle == null || pattern.Mask == null)
                return -1;
            return Find(data, pattern.Needle, pattern.Mask);
        }

        public static int Find(byte[] data, byte[] needle, byte[] mask)
        {
            if (data == null || needle == null || mask == null || needle.Length == 0 || needle.Length != mask.Length)
                return -1;
            if (data.Length < needle.Length)
                return -1;

            for (int i = 0; i <= data.Length - needle.Length; i++)
            {
                bool ok = true;
                for (int j = 0; j < needle.Length; j++)
                {
                    if (mask[j] != 0 && data[i + j] != needle[j])
                    {
                        ok = false;
                        break;
                    }
                }
                if (ok)
                    return i;
            }
            return -1;
        }

        // Compile once at static init: "AA ?? BB" hex with ?? wildcards.
        public static ByteMaskPattern Compile(string hexWithWildcards)
        {
            if (string.IsNullOrWhiteSpace(hexWithWildcards))
                throw new ArgumentException("pattern");

            string[] parts = hexWithWildcards.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var needle = new byte[parts.Length];
            var mask = new byte[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == "?" || parts[i] == "??")
                {
                    needle[i] = 0;
                    mask[i] = 0;
                }
                else
                {
                    needle[i] = Convert.ToByte(parts[i], 16);
                    mask[i] = 0xFF;
                }
            }
            return new ByteMaskPattern(needle, mask);
        }

        // Secondary bind hints (not primary classification).
        public static readonly ByteMaskPattern V10BindPrologue =
            Compile("60 81 EC 00 10 00 00 BE ?? ?? ?? ?? B9 6A");

        public static readonly ByteMaskPattern V10OepEpilogue =
            Compile("61 B8 ?? ?? ?? ?? FF E0");

        public static readonly ByteMaskPattern V21BindPrologue =
            Compile("53 51 52 56 57 55 8B EC 81 EC 00 10 00 00 C7");

        public static readonly ByteMaskPattern V20BindPrologue =
            Compile("53 51 52 56 57 55 8B EC 81 EC 00 10 00 00 BE");

        public static readonly ByteMaskPattern V3x86BindPrologue =
            Compile("E8 00 00 00 00 50 53 51 52 56 57 55 8B 44 24 1C 2D 05 00 00 00 8B CC 83 E4 F0 51 51 51 50");

        public static readonly ByteMaskPattern V3x64BindPrologue =
            Compile("E8 00 00 00 00 50 53 51 52 56 57 55 41 50");

        public static readonly ByteMaskPattern TlsOepX64 =
            Compile("48 81 EA ?? ?? ?? ?? 8B 12 81 F2");

        public static readonly ByteMaskPattern TlsOepX86 =
            Compile("81 EA ?? ?? ?? ?? 8B 12 81 F2");

        public static readonly ByteMaskPattern DrmpOffsetsPrimary =
            Compile("8B ?? ?? ?? ?? ?? 89 ?? ?? ?? ?? ?? 8B ?? ?? ?? ?? ?? 89 ?? ?? ?? ?? ?? 8B ?? ?? ?? ?? ?? 89 ?? ?? ?? ?? ?? 8B ?? ?? ?? ?? ?? 89 ?? ?? ?? ?? ?? 8B ?? ?? ?? ?? ?? 89 ?? ?? ?? ?? ?? 8D ?? ?? ?? ?? ?? 05");

        public static readonly ByteMaskPattern DrmpOffsetsSecondary =
            Compile("8B ?? ?? ?? ?? ?? 89 ?? ?? ?? ?? ?? 8B ?? ?? ?? ?? ?? 89 ?? ?? ?? ?? ?? 8B ?? ?? ?? ?? ?? 89 ?? ?? ?? ?? ?? 8B ?? ?? ?? ?? ?? 89 ?? ?? ?? ?? ?? 8B");

        public static readonly ByteMaskPattern DrmpOffsetsFallback =
            Compile("8B ?? ?? ?? ?? ?? 89 ?? ?? ?? ?? ?? 8B ?? ?? ?? ?? ?? A3 ?? ?? ?? ?? 8B ?? ?? ?? ?? ?? A3 ?? ?? ?? ?? 8B ?? ?? ?? ?? ?? A3 ?? ?? ?? ?? 8B");
    }
}
