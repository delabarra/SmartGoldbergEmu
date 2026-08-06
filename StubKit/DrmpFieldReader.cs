using System;
using System.Collections.Generic;

namespace SmartGoldbergEmu.StubKit
{
    // Scrape SteamDRMP.dll for payload field offsets (flags/OEP/code/AES).
    internal static class DrmpFieldReader
    {
        public static bool TryGetOffsets(byte[] drmp, out List<int> offsets, out bool fallback)
        {
            offsets = null;
            fallback = false;

            int hit = ByteMask.Find(drmp, ByteMask.DrmpOffsetsPrimary);
            if (hit < 0)
            {
                hit = ByteMask.Find(drmp, ByteMask.DrmpOffsetsSecondary);
                if (hit < 0)
                {
                    hit = ByteMask.Find(drmp, ByteMask.DrmpOffsetsFallback);
                    if (hit < 0)
                        return false;
                    fallback = true;
                }
            }

            int len = Math.Min(1024, drmp.Length - hit);
            var block = new byte[len];
            Buffer.BlockCopy(drmp, hit, block, 0, len);

            offsets = ParseDynamic(block);
            if (offsets != null && offsets.Count == 8)
                return true;

            offsets = ParseFixed(block, fallback);
            return offsets != null && offsets.Count == 8;
        }

        // Five mov reg,[base+disp], then lea (AES key) and add reg,imm (AES IV; stolen = IV+16).
        private static List<int> ParseDynamic(byte[] data)
        {
            var offsets = new List<int>(8);
            bool skipMov = false;
            int i = 0;

            while (i < data.Length && offsets.Count < 8)
            {
                byte op = data[i];

                if (op >= 0x50 && op <= 0x5F)
                {
                    i++;
                    continue;
                }

                if ((op == 0x31 || op == 0x33) && i + 1 < data.Length)
                {
                    i += 2;
                    continue;
                }

                if (op == 0x05 && i + 5 <= data.Length)
                {
                    int iv = BitConverter.ToInt32(data, i + 1);
                    offsets.Add(iv);
                    offsets.Add(iv + 16);
                    i += 5;
                    continue;
                }

                if (op == 0xA3 && i + 5 <= data.Length)
                {
                    i += 5;
                    continue;
                }

                if (op == 0x8D && i + 1 < data.Length)
                {
                    int adv;
                    int disp;
                    if (TryReadMemDisp32(data, i + 1, out disp, out adv))
                    {
                        offsets.Add(disp);
                        skipMov = true;
                        i += 1 + adv;
                        continue;
                    }
                }

                if ((op == 0x8B || op == 0x89) && i + 1 < data.Length)
                {
                    int adv;
                    int disp;
                    bool isLoad = op == 0x8B;
                    if (!skipMov && isLoad && TryReadMemDisp32(data, i + 1, out disp, out adv))
                    {
                        offsets.Add(disp);
                        i += 1 + adv;
                        continue;
                    }

                    i += 1 + ModRmLength(data, i + 1);
                    continue;
                }

                i++;
            }

            return offsets.Count == 8 ? offsets : null;
        }

        private static List<int> ParseFixed(byte[] data, bool fallback)
        {
            if (data.Length < 80)
                return null;

            int offset2 = fallback ? 25 : 26;
            int offset3 = fallback ? 36 : 38;
            int offset4 = fallback ? 47 : 50;
            int offset5 = fallback ? 61 : 62;
            int offset6 = fallback ? 72 : 67;

            var offsets = new List<int>
            {
                BitConverter.ToInt32(data, 2),
                BitConverter.ToInt32(data, 14),
                BitConverter.ToInt32(data, offset2),
                BitConverter.ToInt32(data, offset3),
                BitConverter.ToInt32(data, offset4),
                BitConverter.ToInt32(data, offset5)
            };

            int aesIvOffset = BitConverter.ToInt32(data, offset6);
            offsets.Add(aesIvOffset);
            offsets.Add(aesIvOffset + 16);
            return offsets;
        }

        private static bool TryReadMemDisp32(byte[] data, int modrmIndex, out int disp, out int adv)
        {
            disp = 0;
            adv = 0;
            if (modrmIndex >= data.Length)
                return false;

            byte modrm = data[modrmIndex];
            int mod = modrm >> 6;
            int rm = modrm & 7;
            if (mod != 2 || rm == 4)
                return false;
            if (modrmIndex + 5 > data.Length)
                return false;

            disp = BitConverter.ToInt32(data, modrmIndex + 1);
            adv = 5;
            return true;
        }

        private static int ModRmLength(byte[] data, int modrmIndex)
        {
            if (modrmIndex >= data.Length)
                return 1;

            byte modrm = data[modrmIndex];
            int mod = modrm >> 6;
            int rm = modrm & 7;
            int len = 1;

            if (mod != 3 && rm == 4)
            {
                if (modrmIndex + 1 >= data.Length)
                    return len;
                len++;
                byte sib = data[modrmIndex + 1];
                if (mod == 0 && (sib & 7) == 5)
                    len += 4;
            }
            else if (mod == 0 && rm == 5)
            {
                len += 4;
            }

            if (mod == 1)
                len += 1;
            else if (mod == 2)
                len += 4;

            return len;
        }
    }
}
