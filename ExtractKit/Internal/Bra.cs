namespace SmartGoldbergEmu.ExtractKit.Internal
{
    internal static class Bra
    {
        public static void BranchConvArm64Dec(byte[] data, int offset, int size, uint pc)
        {
            const uint flag = 1u << (24 - 4);
            const uint mask = (1u << 24) - (flag << 1);
            size &= ~3;
            int p = offset;
            int lim = offset + size;
            pc -= (uint)p;
            pc -= 4;

            for (;;)
            {
                uint v;
                for (;;)
                {
                    if (p == lim)
                        return;
                    v = CpuArch.GetUi32a(data, p);
                    p += 4;
                    if (((v - 0x94000000u) & 0xfc000000u) == 0)
                    {
                        uint c = (pc + (uint)p) >> 2;
                        v -= c;
                        v &= 0x03ffffffu;
                        v |= 0x94000000u;
                        CpuArch.SetUi32a(data, p - 4, v);
                        break;
                    }

                    v -= 0x90000000u;
                    if ((v & 0x9f000000u) == 0)
                    {
                        v += flag;
                        if ((v & mask) != 0)
                            continue;
                        uint z = (v & 0xffffffe0u) | (v >> 26);
                        uint c = ((pc + (uint)p) >> (12 - 3)) & ~7u;
                        z -= c;
                        v &= 0x1fu;
                        v |= 0x90000000u;
                        v |= z << 26;
                        v |= 0x00ffffe0u & ((z & ((flag << 1) - 1)) - flag);
                        CpuArch.SetUi32a(data, p - 4, v);
                    }
                }
            }
        }

        public static void BranchConvArmDec(byte[] data, int offset, int size, uint pc)
        {
            size &= ~3;
            int p = offset;
            int lim = offset + size;
            pc -= (uint)p;
            pc += 8 - 4;

            for (;;)
            {
                for (;;)
                {
                    if (p >= lim)
                        return;
                    p += 4;
                    if (data[p - 1] == 0xeb)
                        break;
                    if (p >= lim)
                        return;
                    p += 4;
                    if (data[p - 1] == 0xeb)
                        break;
                }

                uint v = CpuArch.GetUi32a(data, p - 4);
                uint c = (pc + (uint)p) >> 2;
                v -= c;
                v &= 0x00ffffffu;
                v |= 0xeb000000u;
                CpuArch.SetUi32a(data, p - 4, v);
            }
        }

        public static void BranchConvPpcDec(byte[] data, int offset, int size, uint pc)
        {
            size &= ~3;
            int p = offset;
            int lim = offset + size;
            pc -= (uint)p;
            pc -= 4;

            for (;;)
            {
                uint v;
                for (;;)
                {
                    if (p == lim)
                        return;
                    v = CpuArch.GetBe32a(data, p);
                    p += 4;
                    if (((v - 0x48000001u) & 0xfc000003u) == 0)
                        break;
                }

                uint c = pc + (uint)p;
                v -= c;
                v &= 0x03ffffffu;
                v |= 0x48000000u;
                CpuArch.SetBe32a(data, p - 4, v);
            }
        }

        public static void BranchConvSparcDec(byte[] data, int offset, int size, uint pc)
        {
            const uint flag = 1u << 22;
            size &= ~3;
            int p = offset;
            int lim = offset + size;
            pc -= (uint)p;
            pc -= 4;

            for (;;)
            {
                uint v;
                for (;;)
                {
                    if (p == lim)
                        return;
                    v = CpuArch.GetBe32a(data, p);
                    p += 4;
                    v += unchecked(5u << 29);
                    v ^= unchecked(7u << 29);
                    v += flag;
                    if ((v & unchecked(0u - (flag << 1))) == 0)
                        break;
                }

                v <<= 2;
                uint c = pc + (uint)p;
                v -= c;
                v &= (flag << 3) - 1;
                v -= flag << 2;
                v >>= 2;
                v |= 1u << 30;
                CpuArch.SetBe32a(data, p - 4, v);
            }
        }

        public static void BranchConvArmtDec(byte[] data, int offset, int size, uint pc)
        {
            size &= ~1;
            if (size <= 2)
                return;
            size -= 2;
            int p = offset;
            int lim = offset + size;
            pc -= (uint)p;

            do
            {
                uint b1;
                for (;;)
                {
                    uint b3;
                    if (p >= lim)
                        return;
                    b3 = data[p + 3];
                    p += 2;
                    if ((b3 & (data[p - 2] ^ 8)) >= 0xf8)
                        break;
                    if (p >= lim)
                        return;
                    b1 = data[p + 3];
                    p += 2;
                    if ((b1 & (b3 ^ 8)) >= 0xf8)
                        break;
                }

                uint v = ((uint)CpuArch.GetUi16a(data, p - 2) << 11) |
                         ((uint)CpuArch.GetUi16a(data, p) & 0x7FF);
                p += 2;
                uint c = (pc + (uint)p) >> 1;
                v -= c;
                CpuArch.SetUi16a(data, p - 4, (ushort)(((v >> 11) & 0x7ff) | 0xf000));
                CpuArch.SetUi16a(data, p - 2, (ushort)(v | 0xf800));
            }
            while (p < lim);
        }

        public static void BranchConvIa64Dec(byte[] data, int offset, int size, uint pc)
        {
            size &= ~15;
            int p = offset;
            int lim = offset + size;
            pc -= 1 << 4;
            pc >>= 4 - 1;

            for (;;)
            {
                uint m;
                for (;;)
                {
                    if (p == lim)
                        return;
                    m = (uint)(0x334b0000 >> (data[p] & 0x1e));
                    p += 16;
                    pc += 1u << 1;
                    m &= 3;
                    if (m != 0)
                        break;
                }

                p += (int)m * 5 - 20;
                do
                {
                    uint t = CpuArch.GetUi32(data, p);
                    uint z = CpuArch.GetUi32(data, p + 1) >> (int)m;
                    p += 5;
                    if (((t >> (int)m) & (0x70u << 1)) == 0 &&
                        ((z - (0x5000000u << 1)) & (0xf000000u << 1)) == 0)
                    {
                        uint v = (0x8fffffu << 1) | 1;
                        v &= z;
                        z ^= v;
                        pc &= (0x1fffffu << 1) | 1;
                        v -= pc;
                        v &= ~(0x600000u << 1);
                        v += 0x700000u << 1;
                        v &= (0x8fffffu << 1) | 1;
                        z |= v;
                        z <<= (int)m;
                        CpuArch.SetUi32(data, p + 1 - 5, z);
                    }

                    m++;
                    m &= 3;
                }
                while (m != 0);
            }
        }

        public static void BranchConvRiscvDec(byte[] data, int offset, int size, uint pc)
        {
            const int instrSize = 2;
            size &= ~(instrSize - 1);
            if (size <= 6)
                return;
            int limSize = (int)size - 6;
            int p = offset;
            int lim = offset + limSize;
            pc -= (uint)p;

            for (;;)
            {
                uint a;
                for (;;)
                {
                    if (p >= lim)
                        return;
                    a = (uint)((CpuArch.GetUi16a(data, p) ^ 0x10u) + 1);
                    if ((a & 0x77) == 0)
                        break;
                    a = (uint)((CpuArch.GetUi16a(data, p + instrSize) ^ 0x10u) + 1);
                    p += instrSize * 2;
                    if ((a & 0x77) == 0)
                    {
                        p -= instrSize;
                        if (p >= lim)
                            return;
                        break;
                    }
                }

                if ((a & 8) == 0)
                {
                    a -= 0x100u;
                    if ((a & 0xd80u) != 0)
                    {
                        p += instrSize;
                        continue;
                    }

                    uint aOld = (a + 0xefu) & 0xfffu;
                    uint v = CpuArch.GetUi16a(data, p + 2);
                    v = ((v >> 8) | (v << 8)) >> 15;
                    v |= (uint)((a & 0xf000u) << 5);
                    v += pc + (uint)p;
                    a = aOld
                        | (v << 11 & 1u << 31)
                        | (v << 20 & 0x3ffu << 21)
                        | (v << 9 & 1u << 20)
                        | (v & 0xffu << 12);
                    CpuArch.SetUi32(data, p, a);
                    p += 4;
                    continue;
                }

                uint v2 = a;
                a = CpuArch.GetUi32(data, p);
                if ((v2 & 0xe80u) == 0)
                {
                    uint r = a >> 27;
                    if ((((v2 - ((3u << 12) | (2u << 7) | 8)) << 18)) < (r & 0x1du))
                    {
                        uint b = CpuArch.GetUi32(data, p + 4);
                        b = ((b >> 24) & 0xffu) | ((b >> 8) & 0xff00u) | ((b << 8) & 0xff0000u) | ((b << 24) & 0xff000000u);
                        v2 = a >> 12;
                        b -= pc + (uint)p;
                        a = (r << 7) + 0x17;
                        a += (b + 0x800u) & 0xfffff000u;
                        v2 |= b << 20;
                        CpuArch.SetUi32(data, p, a);
                        CpuArch.SetUi32(data, p + 4, v2);
                        p += 8;
                    }
                    else
                    {
                        p += 4 + instrSize;
                    }
                }
                else
                {
                    uint b = CpuArch.GetUi32(data, p + 4);
                    if ((((v2 - 3u) ^ (b << 8)) & (0xf8000u + 3u)) != 0)
                    {
                        p += 4 + instrSize;
                    }
                    else
                    {
                        v2 = (a & 0xfffff000u) | (b >> 20);
                        a = (b << 12) | (0x17 + (2u << 7));
                        CpuArch.SetUi32(data, p, a);
                        CpuArch.SetUi32(data, p + 4, v2);
                        p += 8;
                    }
                }
            }
        }
    }
}
