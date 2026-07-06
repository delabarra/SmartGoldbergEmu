namespace SmartGoldbergEmu.ExtractKit.Internal
{
    internal static class Bra86
    {
        private static bool Test86MSByte(byte b)
        {
            return b == 0x00 || b == 0xFF;
        }

        // Canonical 7-Zip x86 BCJ branch filter, decode direction (faithful port of x86_Convert
        // with encoding == 0). Converts absolute CALL/JUMP (E8/E9 rel32) targets back to relative.
        // Designed for a single whole-buffer call (state starts at 0) but threads state correctly.
        public static int BranchConvStX86Dec(byte[] data, int offset, int size, uint ip, ref uint state)
        {
            int pos = 0;
            uint mask = state & 7;
            if (size < 5)
                return 0;

            size -= 4;
            ip += 5;

            for (;;)
            {
                int p = offset + pos;
                int end = offset + size;
                for (; p < end; p++)
                {
                    if ((data[p] & 0xFE) == 0xE8)
                        break;
                }

                {
                    int d = p - offset - pos;
                    pos = p - offset;
                    if (p >= end)
                    {
                        state = d > 2 ? 0u : (mask >> d);
                        return pos;
                    }

                    if (d > 2)
                    {
                        mask = 0;
                    }
                    else
                    {
                        mask >>= d;
                        if (mask != 0 && (mask > 4 || mask == 3 ||
                            Test86MSByte(data[p + (int)(mask >> 1) + 1])))
                        {
                            mask = (mask >> 1) | 4;
                            pos++;
                            continue;
                        }
                    }
                }

                if (Test86MSByte(data[p + 4]))
                {
                    uint v = ((uint)data[p + 4] << 24) | ((uint)data[p + 3] << 16) |
                             ((uint)data[p + 2] << 8) | data[p + 1];
                    uint cur = ip + (uint)pos;
                    pos += 5;
                    v -= cur;
                    if (mask != 0)
                    {
                        int sh = (int)((mask & 6) << 2);
                        if (Test86MSByte((byte)(v >> sh)))
                        {
                            v ^= (((uint)0x100 << sh) - 1);
                            v -= cur;
                        }
                        mask = 0;
                    }

                    data[p + 1] = (byte)v;
                    data[p + 2] = (byte)(v >> 8);
                    data[p + 3] = (byte)(v >> 16);
                    data[p + 4] = (byte)(0u - ((v >> 24) & 1));
                }
                else
                {
                    mask = (mask >> 1) | 4;
                    pos++;
                }
            }
        }
    }
}
