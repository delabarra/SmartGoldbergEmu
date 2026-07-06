namespace SmartGoldbergEmu.ExtractKit.Internal
{
    internal static class Bcj2
    {
        public const int NumStreams = 4;

        public const int StreamMain = 0;
        public const int StreamCall = 1;
        public const int StreamJump = 2;
        public const int StreamRc = 3;

        public const int DecStateOrig0 = NumStreams;
        public const int DecStateOrig1 = NumStreams + 1;
        public const int DecStateOrig2 = NumStreams + 2;
        public const int DecStateOrig3 = NumStreams + 3;
        public const int DecStateOrig = NumStreams + 4;
        public const int DecStateError = NumStreams + 5;

        private const uint TopValue = 1u << 24;
        private const int NumBitModelTotalBits = 11;
        private const int BitModelTotal = 1 << NumBitModelTotalBits;
        private const int NumMoveBits = 5;

        private static bool Is32BitStream(int s)
        {
            return (uint)(s - StreamCall) < 2;
        }

        public static void DecInit(CBcj2Dec p)
        {
            p.State = StreamRc;
            p.Ip = 0;
            p.Temp = 0;
            p.Range = 0;
            p.Code = 0;
            for (int i = 0; i < p.Probs.Length; i++)
                p.Probs[i] = (ushort)(BitModelTotal >> 1);
        }

        public static int DecDecode(CBcj2Dec p)
        {
            uint v = p.Temp;
            if (p.Range <= 5)
            {
                uint code = p.Code;
                p.State = DecStateError;
                while (p.Range != 5)
                {
                    if (p.Range == 1 && code != 0)
                        return SzRes.ErrorData;
                    if (p.BufPos[StreamRc] == p.LimPos[StreamRc])
                    {
                        p.State = StreamRc;
                        return SzRes.Ok;
                    }
                    code = (code << 8) | p.Bufs[StreamRc][p.BufPos[StreamRc]++];
                    p.Code = code;
                    p.Range++;
                }

                if (code == 0xffffffffu)
                    return SzRes.ErrorData;
                p.Range = 0xffffffffu;
            }

            int state = p.State;
            if (Is32BitStream(state))
            {
                int cur = p.BufPos[state];
                if (cur == p.LimPos[state])
                    return SzRes.Ok;
                p.BufPos[state] = cur + 4;
                uint ip = p.Ip + 4;
                v = CpuArch.GetBe32a(p.Bufs[state], cur) - ip;
                p.Ip = ip;
                state = DecStateOrig0;
            }

            if ((uint)(state - DecStateOrig0) < 4)
            {
                int dest = p.DestPos;
                for (;;)
                {
                    if (dest == p.DestLim)
                    {
                        p.State = state;
                        p.Temp = v;
                        p.DestPos = dest;
                        return SzRes.Ok;
                    }

                    p.Dest[dest++] = (byte)v;
                    p.DestPos = dest;
                    if (++state == DecStateOrig3 + 1)
                        break;
                    v >>= 8;
                }
            }

            for (;;)
            {
                if (p.Range < TopValue)
                {
                    if (p.BufPos[StreamRc] == p.LimPos[StreamRc])
                    {
                        p.State = StreamRc;
                        p.Temp = v;
                        return SzRes.Ok;
                    }

                    p.Range <<= 8;
                    p.Code = (p.Code << 8) | p.Bufs[StreamRc][p.BufPos[StreamRc]++];
                }

                int src = p.BufPos[StreamMain];
                int srcLim;
                int dest = p.DestPos;
                {
                    int rem = p.LimPos[StreamMain] - src;
                    int num = p.DestLim - dest;
                    if (num >= rem)
                        num = rem;
                    num &= ~3;
                    srcLim = src + num;
                }

                const int NumShiftBits = 24;
                byte[] main = p.Bufs[StreamMain];

                if (src != srcLim)
                {
                    for (;;)
                    {
                        byte b0 = main[src];
                        p.Dest[dest++] = b0;
                        v = (v << NumShiftBits) | b0;
                        if (((b0 + (0x100 - 0xe8)) & 0xfe) == 0)
                            break;
                        if (((v - (((0x0fu << NumShiftBits) + 0x80))) &
                             ((((1u << (4 + NumShiftBits)) - 1u) << 4))) == 0)
                            break;

                        byte b1 = main[src + 1];
                        p.Dest[dest++] = b1;
                        v = (v << NumShiftBits) | b1;
                        if (((b1 + (0x100 - 0xe8)) & 0xfe) == 0)
                            break;
                        if (((v - (((0x0fu << NumShiftBits) + 0x80))) &
                             ((((1u << (4 + NumShiftBits)) - 1u) << 4))) == 0)
                            break;

                        byte b2 = main[src + 2];
                        p.Dest[dest++] = b2;
                        v = (v << NumShiftBits) | b2;
                        if (((b2 + (0x100 - 0xe8)) & 0xfe) == 0)
                            break;
                        if (((v - (((0x0fu << NumShiftBits) + 0x80))) &
                             ((((1u << (4 + NumShiftBits)) - 1u) << 4))) == 0)
                            break;

                        byte b3 = main[src + 3];
                        p.Dest[dest++] = b3;
                        v = (v << NumShiftBits) | b3;
                        if (((b3 + (0x100 - 0xe8)) & 0xfe) == 0)
                            break;
                        if (((v - (((0x0fu << NumShiftBits) + 0x80))) &
                             ((((1u << (4 + NumShiftBits)) - 1u) << 4))) == 0)
                            break;

                        src += 4;
                        if (src == srcLim)
                            break;
                    }
                }

                if (src == srcLim)
                {
                    for (;;)
                    {
                        if (src == p.LimPos[StreamMain] || dest == p.DestLim)
                        {
                            int num = src - p.BufPos[StreamMain];
                            p.BufPos[StreamMain] = src;
                            p.DestPos = dest;
                            p.Ip += (uint)num;
                            p.State = src == p.LimPos[StreamMain] ? StreamMain : DecStateOrig;
                            p.Temp = v;
                            return SzRes.Ok;
                        }

                        byte b = main[src];
                        p.Dest[dest++] = b;
                        v = (v << NumShiftBits) | b;
                        if (((b + (0x100 - 0xe8)) & 0xfe) == 0)
                            break;
                        if (((v - (((0x0fu << NumShiftBits) + 0x80))) &
                             ((((1u << (4 + NumShiftBits)) - 1u) << 4))) == 0)
                            break;
                        src++;
                    }
                }

                {
                    int processed = dest - p.DestPos;
                    p.DestPos = dest;
                    p.BufPos[StreamMain] += processed;
                    p.Ip += (uint)processed;
                }

                {
                    uint bound;
                    ushort ttt;
                    int c = (int)(((v + 0x17) >> 6) & 1);
                    int probIndex = (int)(((0 - c) & (byte)(v >> NumShiftBits)) + c + ((v >> 5) & 1));
                    ttt = p.Probs[probIndex];
                    bound = (p.Range >> NumBitModelTotalBits) * ttt;
                    if (p.Code < bound)
                    {
                        p.Range = bound;
                        p.Probs[probIndex] = (ushort)(ttt + ((BitModelTotal - ttt) >> NumMoveBits));
                        continue;
                    }

                    p.Range -= bound;
                    p.Code -= bound;
                    p.Probs[probIndex] = (ushort)(ttt - (ttt >> NumMoveBits));
                }

                {
                    int cj = (int)(((v + 0x57) >> 6) & 1) + StreamCall;
                    int cur = p.BufPos[cj];
                    if (cur == p.LimPos[cj])
                    {
                        p.State = cj;
                        break;
                    }

                    v = CpuArch.GetBe32a(p.Bufs[cj], cur);
                    p.BufPos[cj] = cur + 4;
                    uint ip = p.Ip + 4;
                    v -= ip;
                    p.Ip = ip;
                    dest = p.DestPos;
                    int rem = p.DestLim - dest;
                    if (rem < 4)
                    {
                        if (rem > 0)
                        {
                            p.Dest[dest++] = (byte)v;
                            v >>= 8;
                            if (rem > 1)
                            {
                                p.Dest[dest++] = (byte)v;
                                v >>= 8;
                                if (rem > 2)
                                {
                                    p.Dest[dest++] = (byte)v;
                                    v >>= 8;
                                }
                            }
                        }

                        p.Temp = v;
                        p.DestPos = dest;
                        p.State = DecStateOrig0 + rem;
                        break;
                    }

                    CpuArch.SetUi32(p.Dest, dest, v);
                    v >>= 24;
                    p.DestPos = dest + 4;
                }
            }

            if (p.Range < TopValue && p.BufPos[StreamRc] != p.LimPos[StreamRc])
            {
                p.Range <<= 8;
                p.Code = (p.Code << 8) | p.Bufs[StreamRc][p.BufPos[StreamRc]++];
            }

            return SzRes.Ok;
        }

        public static bool IsMaybeFinished(CBcj2Dec p)
        {
            return p.State == StreamMain && p.Code == 0;
        }
    }

    internal sealed class CBcj2Dec
    {
        public byte[][] Bufs = new byte[Bcj2.NumStreams][];
        public int[] BufPos = new int[Bcj2.NumStreams];
        public int[] LimPos = new int[Bcj2.NumStreams];
        public byte[] Dest;
        public int DestPos;
        public int DestLim;
        public int State;
        public uint Ip;
        public uint Temp;
        public uint Range;
        public uint Code;
        public ushort[] Probs = new ushort[2 + 256];
    }
}
