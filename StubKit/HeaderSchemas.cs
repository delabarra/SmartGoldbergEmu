using System;

namespace SmartGoldbergEmu.StubKit
{
    internal sealed class StubHeaderState
    {
        public StubVariant Variant;
        public int HeaderSize;
        public uint SiteRva;
        public uint XorKey;
        public uint Signature;
        public uint OriginalEntryPoint;
        public uint Flags;
        public uint CodeSectionVirtualAddress;
        public uint CodeSectionRawSize;
        public uint SteamAppId;
        public uint HasTlsCallback;
        public byte[] AesKey;
        public byte[] AesIv;
        public byte[] StolenData;
        public uint ResolvedOep;
        public bool UsedTlsSite;
        public bool UsedTlsOepOverride;
        public BindAction BindAction;
        public bool UsedEncryption;
    }

    // Offset maps for SteamStub 3.x recovered headers (after chained DWORD decode).
    internal static class HeaderSchemas
    {
        public const uint SigV30 = 0xC0DEC0DE;
        public const uint SigV31 = 0xC0DEC0DF;
        public const uint FlagNoEncryption = 0x04;

        public static readonly int[] V3HeaderSizes = { 0xF0, 0xD0, 0xB0 };

        public static bool TryParseV3(byte[] raw, int headerSize, bool isPe32Plus, uint siteRva, out StubHeaderState state)
        {
            state = null;
            if (raw == null || raw.Length < headerSize || headerSize < 8)
                return false;

            uint signature = BitConverter.ToUInt32(raw, 4);
            StubVariant variant;
            if (signature == SigV31 && headerSize == 0xF0)
            {
                variant = isPe32Plus ? StubVariant.V31_x64 : StubVariant.V31_x86;
                state = ParseV31(raw, siteRva, variant);
                return true;
            }

            if (signature == SigV30 && (headerSize == 0xB0 || headerSize == 0xD0))
            {
                variant = isPe32Plus ? StubVariant.V30_x64 : StubVariant.V30_x86;
                state = ParseV30(raw, headerSize, siteRva, variant);
                return true;
            }

            return false;
        }

        private static StubHeaderState ParseV31(byte[] h, uint siteRva, StubVariant variant)
        {
            return new StubHeaderState
            {
                Variant = variant,
                HeaderSize = 0xF0,
                SiteRva = siteRva,
                XorKey = BitConverter.ToUInt32(h, 0x00),
                Signature = BitConverter.ToUInt32(h, 0x04),
                OriginalEntryPoint = BitConverter.ToUInt32(h, 0x20),
                SteamAppId = BitConverter.ToUInt32(h, 0x38),
                Flags = BitConverter.ToUInt32(h, 0x3C),
                CodeSectionVirtualAddress = BitConverter.ToUInt32(h, 0x48),
                CodeSectionRawSize = BitConverter.ToUInt32(h, 0x50),
                AesKey = StubCiphers.Slice(h, 0x58, 0x20),
                AesIv = StubCiphers.Slice(h, 0x78, 0x10),
                StolenData = StubCiphers.Slice(h, 0x88, 0x10),
                ResolvedOep = BitConverter.ToUInt32(h, 0x20)
            };
        }

        private static StubHeaderState ParseV30(byte[] h, int headerSize, uint siteRva, StubVariant variant)
        {
            uint hasTls = headerSize >= 0x9C ? BitConverter.ToUInt32(h, 0x98) : 0;
            uint oep = BitConverter.ToUInt32(h, 0x1C);
            return new StubHeaderState
            {
                Variant = variant,
                HeaderSize = headerSize,
                SiteRva = siteRva,
                XorKey = BitConverter.ToUInt32(h, 0x00),
                Signature = BitConverter.ToUInt32(h, 0x04),
                OriginalEntryPoint = oep,
                SteamAppId = BitConverter.ToUInt32(h, 0x30),
                Flags = BitConverter.ToUInt32(h, 0x34),
                CodeSectionVirtualAddress = BitConverter.ToUInt32(h, 0x40),
                CodeSectionRawSize = BitConverter.ToUInt32(h, 0x44),
                AesKey = StubCiphers.Slice(h, 0x48, 0x20),
                AesIv = StubCiphers.Slice(h, 0x68, 0x10),
                StolenData = StubCiphers.Slice(h, 0x78, 0x10),
                HasTlsCallback = hasTls,
                ResolvedOep = oep
            };
        }
    }
}
