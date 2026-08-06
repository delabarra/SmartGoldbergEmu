using System;

namespace SmartGoldbergEmu.StubKit
{
    // SteamStub 2.0 (x86): header via EP immediates; optional DWORD-chain code XOR.
    internal static class VariantV20
    {
        private const uint FlagEncodedCode = 0x04;

        public sealed class Info
        {
            public uint OepVa;
            public uint Flags;
            public uint CodeSectionVa;
            public uint CodeSectionSize;
            public uint CodeXorKey;
            public uint SteamAppId;
            public int HeaderSize;
            public BindAction BindAction;
        }

        public static Info Read(PeImage pe)
        {
            EpImmediateScanner.Result scan;
            if (!EpImmediateScanner.TryScanV20(pe, out scan))
                throw new InvalidOperationException("Could not locate V2.0 DRM header from EP.");

            var header = new byte[scan.StructSize];
            Buffer.BlockCopy(pe.Data, (int)pe.RvaToOffset(scan.StructRva), header, 0, (int)scan.StructSize);
            StubCiphers.DecodeChainedDwords(header, scan.StructSize, scan.XorKey);

            int oepOff = scan.StructSize == 856 ? 0x28 : 0x2C;
            int flagsOff = scan.StructSize == 856 ? 0x14 : 0x18;

            return new Info
            {
                HeaderSize = (int)scan.StructSize,
                Flags = BitConverter.ToUInt32(header, flagsOff),
                OepVa = BitConverter.ToUInt32(header, oepOff),
                CodeSectionVa = BitConverter.ToUInt32(header, oepOff + 4),
                CodeSectionSize = BitConverter.ToUInt32(header, oepOff + 8),
                CodeXorKey = BitConverter.ToUInt32(header, oepOff + 12),
                SteamAppId = BitConverter.ToUInt32(header, oepOff + 16)
            };
        }

        public static void Remove(PeImage pe, Info info, UnpackOptions options = null)
        {
            if ((info.Flags & FlagEncodedCode) != 0)
            {
                uint codeRva = 0;
                if (info.CodeSectionVa != 0)
                    codeRva = pe.VaToRva(info.CodeSectionVa);
                if (codeRva == 0)
                {
                    int opt = pe.PeOffset + 24;
                    codeRva = BitConverter.ToUInt32(pe.Data, opt + 20);
                }

                var codeSec = pe.SectionFromRva(codeRva);
                if (codeSec == null || codeSec.SizeOfRawData == 0)
                    throw new InvalidOperationException("V2.0 code section not found.");

                var code = new byte[codeSec.SizeOfRawData];
                Buffer.BlockCopy(pe.Data, (int)codeSec.PointerToRawData, code, 0, code.Length);

                uint dwords = info.CodeSectionSize != 0
                    ? info.CodeSectionSize >> 2
                    : (uint)code.Length >> 2;
                if (dwords > (uint)code.Length / 4)
                    dwords = (uint)code.Length / 4;

                StubCiphers.DecodeChainedDwordRun(code, dwords, info.CodeXorKey);
                Buffer.BlockCopy(code, 0, pe.Data, (int)codeSec.PointerToRawData, code.Length);
            }

            pe.AddressOfEntryPoint = pe.VaToRva(info.OepVa);
            pe.CheckSum = 0;

            var bind = pe.FindSection(".bind");
            if (bind == null)
                throw new InvalidOperationException(".bind missing.");
            info.BindAction = BindFinish.Apply(pe, bind, options);
        }
    }
}
