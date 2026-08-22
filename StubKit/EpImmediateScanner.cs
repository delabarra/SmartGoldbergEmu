using System;
using System.Collections.Generic;

namespace SmartGoldbergEmu.StubKit
{
    // Scan stub EP for header VA / size / XOR key immediates (v2.x).
    internal static class EpImmediateScanner
    {
        public struct Result
        {
            public uint StructRva;
            public uint StructSize;
            public uint XorKey; // 0 => DecodeChainedDwords reads seed from first DWORD
            public bool HasMemImmediates;
        }

        public static bool TryScanV21(PeImage pe, out Result result)
        {
            result = default(Result);
            uint epOff = pe.RvaToOffset(pe.AddressOfEntryPoint);
            if (epOff < 4 || BitConverter.ToUInt32(pe.Data, (int)epOff - 4) != HeaderSchemas.SigV30)
                return false;

            var memImms = new List<uint>();
            var regImms = new List<uint>();
            CollectImmediates(pe.Data, (int)epOff, 4096, memImms, regImms);

            if (memImms.Count < 2 || regImms.Count < 1)
                return false;

            result.StructRva = memImms[0] - (uint)pe.ImageBase;
            result.XorKey = memImms[1];
            result.StructSize = regImms[0] * 4;
            result.HasMemImmediates = true;

            if (result.StructSize < 0x40 || result.StructSize > 0x2000)
                return false;
            return true;
        }

        public static bool TryScanV20(PeImage pe, out Result result)
        {
            result = default(Result);
            uint epOff = pe.RvaToOffset(pe.AddressOfEntryPoint);
            if (epOff < 4 || BitConverter.ToUInt32(pe.Data, (int)epOff - 4) != HeaderSchemas.SigV30)
                return false;

            var memImms = new List<uint>();
            var regImms = new List<uint>();
            CollectImmediates(pe.Data, (int)epOff, 4096, memImms, regImms);

            if (regImms.Count >= 2)
            {
                uint size = regImms[1] * 4;
                if (size == 856 || size == 884 || size == 952)
                {
                    result.StructRva = regImms[0] - (uint)pe.ImageBase;
                    result.StructSize = size;
                    result.XorKey = 0;
                    return true;
                }
            }

            if (memImms.Count >= 1 && regImms.Count >= 1)
            {
                uint size = regImms[0] * 4;
                if (size == 856 || size == 884 || size == 952)
                {
                    result.StructRva = memImms[0] - (uint)pe.ImageBase;
                    result.StructSize = size;
                    result.XorKey = 0;
                    result.HasMemImmediates = true;
                    return true;
                }
            }

            return false;
        }

        private static void CollectImmediates(byte[] data, int start, int maxLen, List<uint> memImms, List<uint> regImms)
        {
            int end = Math.Min(data.Length - 6, start + maxLen);
            int i = start;
            while (i < end)
            {
                byte op = data[i];

                if (op >= 0xB8 && op <= 0xBF && i + 5 <= end + 5)
                {
                    regImms.Add(BitConverter.ToUInt32(data, i + 1));
                    i += 5;
                    continue;
                }

                if (op == 0xC7 && i + 1 < data.Length)
                {
                    byte modrm = data[i + 1];
                    int mod = modrm >> 6;
                    int rm = modrm & 7;

                    if (mod == 0 && rm == 5 && i + 10 <= data.Length)
                    {
                        memImms.Add(BitConverter.ToUInt32(data, i + 6));
                        i += 10;
                        continue;
                    }

                    if (mod == 1 && i + 7 <= data.Length)
                    {
                        memImms.Add(BitConverter.ToUInt32(data, i + 3));
                        i += 7;
                        continue;
                    }

                    if (mod == 2 && i + 10 <= data.Length)
                    {
                        memImms.Add(BitConverter.ToUInt32(data, i + 6));
                        i += 10;
                        continue;
                    }
                }

                i++;
            }
        }
    }
}
