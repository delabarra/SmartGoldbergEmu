/* LzmaDec.cs -- LZMA Decoder (ported from 7-Zip LzmaDec.c / LzmaDec.h)
 * Igor Pavlov : Public domain
 * Mechanical C# port for ExtractKit (.NET Framework 4.8, C# 7.3)
 */

using System;

namespace SmartGoldbergEmu.ExtractKit.Internal
{
    internal enum ELzmaFinishMode
    {
        LzmaFinishAny = 0,
        LzmaFinishEnd = 1
    }

    internal enum ELzmaStatus
    {
        LzmaStatusNotSpecified = 0,
        LzmaStatusFinishedWithMark = 1,
        LzmaStatusNotFinished = 2,
        LzmaStatusNeedsMoreInput = 3,
        LzmaStatusMaybeFinishedWithoutMark = 4
    }

    internal enum ELzmaDummy
    {
        DummyInputEof = 0,
        DummyLit = 1,
        DummyMatch = 2,
        DummyRep = 3
    }

    internal sealed class CLzmaProps
    {
        public byte Lc;
        public byte Lp;
        public byte Pb;
        public byte Pad;
        public uint DicSize;
    }

    internal sealed class CLzmaDec
    {
        public CLzmaProps Prop = new CLzmaProps();
        public ushort[] Probs;
        public int Probs1664;
        public byte[] Dic;
        public int DicBufSize;
        public int DicPos;
        public byte[] Buf;
        public int BufPos;
        public uint Range;
        public uint Code;
        public uint ProcessedPos;
        public uint CheckDicSize;
        public readonly uint[] Reps = new uint[4];
        public uint State;
        public uint RemainLen;
        public uint NumProbs;
        public int TempBufSize;
        public readonly byte[] TempBuf = new byte[LzmaDec.LzmaRequiredInputMax];

        public void Construct()
        {
            Dic = null;
            Probs = null;
        }
    }

    internal static class LzmaDec
    {
        public const int LzmaPropsSize = 5;
        public const int PropsSize = LzmaPropsSize;
        public const int LzmaRequiredInputMax = 20;

        private const uint kTopValue = 1u << 24;
        private const int kNumBitModelTotalBits = 11;
        private const int kBitModelTotal = 1 << kNumBitModelTotalBits;
        private const int RcInitSize = 5;
        private const int kNumMoveBits = 5;

        private const int kNumPosBitsMax = 4;
        private const int kNumPosStatesMax = 1 << kNumPosBitsMax;
        private const int kLenNumLowBits = 3;
        private const int kLenNumLowSymbols = 1 << kLenNumLowBits;
        private const int kLenNumHighBits = 8;
        private const int kLenNumHighSymbols = 1 << kLenNumHighBits;

        private const int LenLow = 0;
        private const int LenHigh = LenLow + 2 * (kNumPosStatesMax << kLenNumLowBits);
        private const int kNumLenProbs = LenHigh + kLenNumHighSymbols;
        private const int LenChoice = LenLow;
        private const int LenChoice2 = LenLow + (1 << kLenNumLowBits);

        private const int kNumStates = 12;
        private const int kNumStates2 = 16;
        private const int kNumLitStates = 7;

        private const int kStartPosModelIndex = 4;
        private const int kEndPosModelIndex = 14;
        private const int kNumFullDistances = 1 << (kEndPosModelIndex >> 1);

        private const int kNumPosSlotBits = 6;
        private const int kNumLenToPosStates = 4;

        private const int kNumAlignBits = 4;
        private const int kAlignTableSize = 1 << kNumAlignBits;

        private const int kMatchMinLen = 2;
        private const int kMatchSpecLenStart = kMatchMinLen + kLenNumLowSymbols * 2 + kLenNumHighSymbols;
        private const int kMatchSpecLenErrorData = 1 << 9;
        private const int kMatchSpecLenErrorFail = kMatchSpecLenErrorData - 1;

        private const int kStartOffset = 1664;
        private const int SpecPos = -kStartOffset;
        private const int IsRep0Long = SpecPos + kNumFullDistances;
        private const int RepLenCoder = IsRep0Long + (kNumStates2 << kNumPosBitsMax);
        private const int LenCoder = RepLenCoder + kNumLenProbs;
        private const int IsMatch = LenCoder + kNumLenProbs;
        private const int Align = IsMatch + (kNumStates2 << kNumPosBitsMax);
        private const int IsRep = Align + kAlignTableSize;
        private const int IsRepG0 = IsRep + kNumStates;
        private const int IsRepG1 = IsRepG0 + kNumStates;
        private const int IsRepG2 = IsRepG1 + kNumStates;
        private const int PosSlot = IsRepG2 + kNumStates;
        private const int Literal = PosSlot + (kNumLenToPosStates << kNumPosSlotBits);
        private const int NumBaseProbs = Literal + kStartOffset;

        private const int LzmaLitSize = 0x300;
        private const int LzmaDicMin = 1 << 12;

        private const uint kBadRepCode = 0xC0000000u - 0x400u;

        private static int Pi(int rel) { return kStartOffset + rel; }

        private static uint LzmaProps_GetNumProbs(CLzmaProps prop)
        {
            return (uint)NumBaseProbs + ((uint)LzmaLitSize << (prop.Lc + prop.Lp));
        }

        private static int CalcPosState(uint processedPos, uint pbMask)
        {
            return (int)((processedPos & pbMask) << 4);
        }

        private static void RcNormalize(ref uint range, ref uint code, byte[] buf, ref int bufPos)
        {
            if (range < kTopValue)
            {
                range <<= 8;
                code = (code << 8) | buf[bufPos++];
            }
        }

        private static bool RcIfBit0(ushort[] probs, int probIdx, ref uint range, ref uint code, byte[] buf, ref int bufPos, out uint bound, out int ttt)
        {
            ttt = probs[probIdx];
            RcNormalize(ref range, ref code, buf, ref bufPos);
            bound = (range >> kNumBitModelTotalBits) * (uint)ttt;
            return code < bound;
        }

        private static void RcUpdate0(ushort[] probs, int probIdx, ref uint range, uint bound, int ttt)
        {
            range = bound;
            probs[probIdx] = (ushort)(ttt + ((kBitModelTotal - ttt) >> kNumMoveBits));
        }

        private static void RcUpdate1(ushort[] probs, int probIdx, ref uint range, ref uint code, uint bound, int ttt)
        {
            range -= bound;
            code -= bound;
            probs[probIdx] = (ushort)(ttt - (ttt >> kNumMoveBits));
        }

        private static void RcTreeGetBit(ushort[] probs, ref int probBase, ref int i, ref uint range, ref uint code, byte[] buf, ref int bufPos)
        {
            int probIdx = probBase + i;
            uint bound;
            int ttt;
            if (RcIfBit0(probs, probIdx, ref range, ref code, buf, ref bufPos, out bound, out ttt))
            {
                RcUpdate0(probs, probIdx, ref range, bound, ttt);
                i <<= 1;
            }
            else
            {
                RcUpdate1(probs, probIdx, ref range, ref code, bound, ttt);
                i = (i << 1) + 1;
            }
        }

        private static void RcTreeDecode(ushort[] probs, ref int probBase, int limit, ref int i, ref uint range, ref uint code, byte[] buf, ref int bufPos)
        {
            i = 1;
            do
            {
                RcTreeGetBit(probs, ref probBase, ref i, ref range, ref code, buf, ref bufPos);
            } while (i < limit);
            i -= limit;
        }

        private static void RcTree6Decode(ushort[] probs, ref int probBase, ref uint distance, ref uint range, ref uint code, byte[] buf, ref int bufPos)
        {
            int i = 1;
            RcTreeGetBit(probs, ref probBase, ref i, ref range, ref code, buf, ref bufPos);
            RcTreeGetBit(probs, ref probBase, ref i, ref range, ref code, buf, ref bufPos);
            RcTreeGetBit(probs, ref probBase, ref i, ref range, ref code, buf, ref bufPos);
            RcTreeGetBit(probs, ref probBase, ref i, ref range, ref code, buf, ref bufPos);
            RcTreeGetBit(probs, ref probBase, ref i, ref range, ref code, buf, ref bufPos);
            RcTreeGetBit(probs, ref probBase, ref i, ref range, ref code, buf, ref bufPos);
            distance = (uint)(i - 0x40);
        }

        private static void RcNormalLiterDec(ushort[] probs, ref int probBase, ref int symbol, ref uint range, ref uint code, byte[] buf, ref int bufPos)
        {
            RcTreeGetBit(probs, ref probBase, ref symbol, ref range, ref code, buf, ref bufPos);
        }

        private static void RcMatchedLiterDec(ushort[] probs, ref int probBase, ref int symbol, ref int matchByte, ref int offs, ref uint range, ref uint code, byte[] buf, ref int bufPos)
        {
            matchByte += matchByte;
            int bit = offs;
            offs &= matchByte;
            int probIdx = probBase + offs + bit + symbol;
            uint bound;
            int ttt;
            if (RcIfBit0(probs, probIdx, ref range, ref code, buf, ref bufPos, out bound, out ttt))
            {
                RcUpdate0(probs, probIdx, ref range, bound, ttt);
                offs ^= bit;
                symbol <<= 1;
            }
            else
            {
                RcUpdate1(probs, probIdx, ref range, ref code, bound, ttt);
                symbol = (symbol << 1) + 1;
            }
        }

        private static void RcRevBitVar(ushort[] probs, ref int probBase, ref uint distance, ref uint m, ref uint range, ref uint code, byte[] buf, ref int bufPos)
        {
            int probIdx = probBase + (int)distance;
            uint bound;
            int ttt;
            if (RcIfBit0(probs, probIdx, ref range, ref code, buf, ref bufPos, out bound, out ttt))
            {
                RcUpdate0(probs, probIdx, ref range, bound, ttt);
                distance += m;
                m <<= 1;
            }
            else
            {
                RcUpdate1(probs, probIdx, ref range, ref code, bound, ttt);
                m <<= 1;
                distance += m;
            }
        }

        private static void RcRevBitConst(ushort[] probs, ref int probBase, ref int i, int m, ref uint range, ref uint code, byte[] buf, ref int bufPos)
        {
            int probIdx = probBase + i;
            uint bound;
            int ttt;
            if (RcIfBit0(probs, probIdx, ref range, ref code, buf, ref bufPos, out bound, out ttt))
            {
                RcUpdate0(probs, probIdx, ref range, bound, ttt);
                i += m;
            }
            else
            {
                RcUpdate1(probs, probIdx, ref range, ref code, bound, ttt);
                i += m * 2;
            }
        }

        private static void RcRevBitLast(ushort[] probs, ref int probBase, ref int i, int m, ref uint range, ref uint code, byte[] buf, ref int bufPos)
        {
            int probIdx = probBase + i;
            uint bound;
            int ttt;
            if (RcIfBit0(probs, probIdx, ref range, ref code, buf, ref bufPos, out bound, out ttt))
            {
                RcUpdate0(probs, probIdx, ref range, bound, ttt);
                i -= m;
            }
            else
            {
                RcUpdate1(probs, probIdx, ref range, ref code, bound, ttt);
            }
        }

        private static bool RcNormalizeCheck(ref uint range, ref uint code, byte[] buf, ref int bufPos, int bufLimitPos)
        {
            if (range < kTopValue)
            {
                if (bufPos >= bufLimitPos)
                    return false;
                range <<= 8;
                code = (code << 8) | buf[bufPos++];
            }
            return true;
        }

        // Look-ahead bit decode. Mirrors C IF_BIT_0_CHECK where NORMALIZE_CHECK early-returns
        // DUMMY_INPUT_EOF: returns false ONLY when more input is needed. The decoded bit is
        // reported via isBit0 so callers never confuse "needs input" with "bit == 1".
        private static bool RcBitCheck(ushort[] probs, int probIdx, ref uint range, ref uint code, byte[] buf, ref int bufPos, int bufLimitPos, out bool isBit0, out uint bound, out int ttt)
        {
            ttt = probs[probIdx];
            if (!RcNormalizeCheck(ref range, ref code, buf, ref bufPos, bufLimitPos))
            {
                isBit0 = false;
                bound = 0;
                return false;
            }
            bound = (range >> kNumBitModelTotalBits) * (uint)ttt;
            isBit0 = code < bound;
            return true;
        }

        private static void RcUpdate0Check(ref uint range, uint bound)
        {
            range = bound;
        }

        private static void RcUpdate1Check(ref uint range, ref uint code, uint bound)
        {
            range -= bound;
            code -= bound;
        }

        private static bool RcGetBit2Check(ushort[] probs, int probIdx, ref int i, ref uint range, ref uint code, byte[] buf, ref int bufPos, int bufLimitPos)
        {
            bool isBit0;
            uint bound;
            int ttt;
            if (!RcBitCheck(probs, probIdx, ref range, ref code, buf, ref bufPos, bufLimitPos, out isBit0, out bound, out ttt))
                return false;
            if (isBit0)
            {
                RcUpdate0Check(ref range, bound);
                i <<= 1;
            }
            else
            {
                RcUpdate1Check(ref range, ref code, bound);
                i = (i << 1) + 1;
            }
            return true;
        }

        private static bool RcTreeDecodeCheck(ushort[] probs, ref int probBase, int limit, ref int i, ref uint range, ref uint code, byte[] buf, ref int bufPos, int bufLimitPos)
        {
            i = 1;
            do
            {
                if (!RcGetBit2Check(probs, probBase + i, ref i, ref range, ref code, buf, ref bufPos, bufLimitPos))
                    return false;
            } while (i < limit);
            i -= limit;
            return true;
        }

        private static bool RcRevBitCheck(ushort[] probs, ref int probBase, ref int i, ref int m, ref uint range, ref uint code, byte[] buf, ref int bufPos, int bufLimitPos)
        {
            bool isBit0;
            uint bound;
            int ttt;
            if (!RcBitCheck(probs, probBase + i, ref range, ref code, buf, ref bufPos, bufLimitPos, out isBit0, out bound, out ttt))
                return false;
            if (isBit0)
            {
                RcUpdate0Check(ref range, bound);
                i += m;
                m += m;
            }
            else
            {
                RcUpdate1Check(ref range, ref code, bound);
                m += m;
                i += m;
            }
            return true;
        }

        private static bool IsDummyEndMarkerPossible(ELzmaDummy dummyRes)
        {
            return dummyRes == ELzmaDummy.DummyMatch;
        }


        public static void LzmaDec_Construct(CLzmaDec p) { p.Construct(); }
        public static void Construct(CLzmaDec p) => LzmaDec_Construct(p);

        public static void LzmaDec_InitDicAndState(CLzmaDec p, bool initDic, bool initState)
        {
            p.RemainLen = (uint)(kMatchSpecLenStart + 1);
            p.TempBufSize = 0;
            if (initDic)
            {
                p.ProcessedPos = 0;
                p.CheckDicSize = 0;
                p.RemainLen = (uint)(kMatchSpecLenStart + 2);
            }
            if (initState)
                p.RemainLen = (uint)(kMatchSpecLenStart + 2);
        }

        public static void InitDicAndState(CLzmaDec p, bool initDic, bool initState) =>
            LzmaDec_InitDicAndState(p, initDic, initState);

        public static void LzmaDec_Init(CLzmaDec p)
        {
            p.DicPos = 0;
            LzmaDec_InitDicAndState(p, true, true);
        }

        public static void Init(CLzmaDec p) => LzmaDec_Init(p);

        public static int LzmaProps_Decode(CLzmaProps p, byte[] data, int dataOffset, uint size)
        {
            if (size < LzmaPropsSize)
                return SzRes.ErrorUnsupported;
            uint dicSize = (uint)(data[dataOffset + 1]
                | (data[dataOffset + 2] << 8)
                | (data[dataOffset + 3] << 16)
                | (data[dataOffset + 4] << 24));
            if (dicSize < LzmaDicMin)
                dicSize = LzmaDicMin;
            p.DicSize = dicSize;
            byte d = data[dataOffset];
            if (d >= 9 * 5 * 5)
                return SzRes.ErrorUnsupported;
            p.Lc = (byte)(d % 9);
            d /= 9;
            p.Pb = (byte)(d / 5);
            p.Lp = (byte)(d % 5);
            return SzRes.Ok;
        }

        public static void LzmaDec_FreeProbs(CLzmaDec p, ISzAlloc alloc)
        {
            p.Probs = null;
        }

        public static void FreeProbs(CLzmaDec p, ISzAlloc alloc) => LzmaDec_FreeProbs(p, alloc);

        private static void LzmaDec_FreeDict(CLzmaDec p, ISzAlloc alloc)
        {
            SzAllocImpl.Free(alloc, p.Dic);
            p.Dic = null;
        }

        public static void LzmaDec_Free(CLzmaDec p, ISzAlloc alloc)
        {
            LzmaDec_FreeProbs(p, alloc);
            LzmaDec_FreeDict(p, alloc);
        }

        private static int LzmaDec_AllocateProbs2(CLzmaDec p, CLzmaProps propNew, ISzAlloc alloc)
        {
            uint numProbs = LzmaProps_GetNumProbs(propNew);
            if (p.Probs == null || numProbs != p.NumProbs)
            {
                LzmaDec_FreeProbs(p, alloc);
                p.Probs = new ushort[(int)numProbs];
                if (p.Probs == null)
                    return SzRes.ErrorMem;
                p.Probs1664 = kStartOffset;
                p.NumProbs = numProbs;
            }
            return SzRes.Ok;
        }

        public static int LzmaDec_AllocateProbs(CLzmaDec p, byte[] props, int propsOffset, uint propsSize, ISzAlloc alloc)
        {
            CLzmaProps propNew = new CLzmaProps();
            int res = LzmaProps_Decode(propNew, props, propsOffset, propsSize);
            if (res != SzRes.Ok) return res;
            res = LzmaDec_AllocateProbs2(p, propNew, alloc);
            if (res != SzRes.Ok) return res;
            p.Prop = propNew;
            return SzRes.Ok;
        }

        public static int AllocateProbs(CLzmaDec p, byte[] props, int propsSize, ISzAlloc alloc) =>
            LzmaDec_AllocateProbs(p, props, 0, (uint)propsSize, alloc);

        public static int AllocateProbs(CLzmaDec p, byte[] props, int propsOffset, int propsSize, ISzAlloc alloc) =>
            LzmaDec_AllocateProbs(p, props, propsOffset, (uint)propsSize, alloc);

        public static int LzmaDec_Allocate(CLzmaDec p, byte[] props, int propsOffset, uint propsSize, ISzAlloc alloc)
        {
            CLzmaProps propNew = new CLzmaProps();
            int res = LzmaProps_Decode(propNew, props, propsOffset, propsSize);
            if (res != SzRes.Ok) return res;
            res = LzmaDec_AllocateProbs2(p, propNew, alloc);
            if (res != SzRes.Ok) return res;
            uint dictSize = propNew.DicSize;
            int mask = (1 << 12) - 1;
            if (dictSize >= (1u << 30)) mask = (1 << 22) - 1;
            else if (dictSize >= (1u << 22)) mask = (1 << 20) - 1;
            int dicBufSize = (int)(((long)dictSize + mask) & ~mask);
            if (dicBufSize < (int)dictSize) dicBufSize = (int)dictSize;
            if (p.Dic == null || dicBufSize != p.DicBufSize)
            {
                LzmaDec_FreeDict(p, alloc);
                p.Dic = SzAllocImpl.Alloc(alloc, dicBufSize);
                if (p.Dic == null)
                {
                    LzmaDec_FreeProbs(p, alloc);
                    return SzRes.ErrorMem;
                }
            }
            p.DicBufSize = dicBufSize;
            p.Prop = propNew;
            return SzRes.Ok;
        }

        public static int Allocate(CLzmaDec p, byte[] props, int propsSize, ISzAlloc alloc) =>
            LzmaDec_Allocate(p, props, 0, (uint)propsSize, alloc);

        private static int LzmaDec_DecodeReal_3(CLzmaDec p, int limit, int bufLimitPos)
        {
            ushort[] probs = p.Probs;
            uint state = p.State;
            uint rep0 = p.Reps[0], rep1 = p.Reps[1], rep2 = p.Reps[2], rep3 = p.Reps[3];
            uint pbMask = (1u << p.Prop.Pb) - 1u;
            uint lc = p.Prop.Lc;
            uint lpMask = (0x100u << p.Prop.Lp) - (0x100u >> (int)lc);

            byte[] dic = p.Dic;
            int dicBufSize = p.DicBufSize;
            int dicPos = p.DicPos;

            uint processedPos = p.ProcessedPos;
            uint checkDicSize = p.CheckDicSize;
            int len = 0;

            byte[] buf = p.Buf;
            int bufPos = p.BufPos;
            uint range = p.Range;
            uint code = p.Code;

            do
            {
                int probIdx;
                uint bound;
                int ttt;
                int posState = CalcPosState(processedPos, pbMask);

                probIdx = Pi(IsMatch) + posState + (int)state;
                if (RcIfBit0(probs, probIdx, ref range, ref code, buf, ref bufPos, out bound, out ttt))
                {
                    RcUpdate0(probs, probIdx, ref range, bound, ttt);
                    int probLitBase = Pi(Literal);
                    if (processedPos != 0 || checkDicSize != 0)
                        probLitBase += (int)(3u * ((((processedPos << 8) + dic[(dicPos == 0 ? dicBufSize : dicPos) - 1]) & lpMask) << (int)lc));
                    processedPos++;

                    if (state < kNumLitStates)
                    {
                        state -= (state < 4) ? state : 3u;
                        int symbol = 1;
                        RcNormalLiterDec(probs, ref probLitBase, ref symbol, ref range, ref code, buf, ref bufPos);
                        RcNormalLiterDec(probs, ref probLitBase, ref symbol, ref range, ref code, buf, ref bufPos);
                        RcNormalLiterDec(probs, ref probLitBase, ref symbol, ref range, ref code, buf, ref bufPos);
                        RcNormalLiterDec(probs, ref probLitBase, ref symbol, ref range, ref code, buf, ref bufPos);
                        RcNormalLiterDec(probs, ref probLitBase, ref symbol, ref range, ref code, buf, ref bufPos);
                        RcNormalLiterDec(probs, ref probLitBase, ref symbol, ref range, ref code, buf, ref bufPos);
                        RcNormalLiterDec(probs, ref probLitBase, ref symbol, ref range, ref code, buf, ref bufPos);
                        RcNormalLiterDec(probs, ref probLitBase, ref symbol, ref range, ref code, buf, ref bufPos);
                        dic[dicPos++] = (byte)symbol;
                    }
                    else
                    {
                        int matchByte = dic[dicPos - (int)rep0 + (dicPos < rep0 ? dicBufSize : 0)];
                        int offs = 0x100;
                        state -= (state < 10) ? 3u : 6u;
                        int symbol = 1;
                        RcMatchedLiterDec(probs, ref probLitBase, ref symbol, ref matchByte, ref offs, ref range, ref code, buf, ref bufPos);
                        RcMatchedLiterDec(probs, ref probLitBase, ref symbol, ref matchByte, ref offs, ref range, ref code, buf, ref bufPos);
                        RcMatchedLiterDec(probs, ref probLitBase, ref symbol, ref matchByte, ref offs, ref range, ref code, buf, ref bufPos);
                        RcMatchedLiterDec(probs, ref probLitBase, ref symbol, ref matchByte, ref offs, ref range, ref code, buf, ref bufPos);
                        RcMatchedLiterDec(probs, ref probLitBase, ref symbol, ref matchByte, ref offs, ref range, ref code, buf, ref bufPos);
                        RcMatchedLiterDec(probs, ref probLitBase, ref symbol, ref matchByte, ref offs, ref range, ref code, buf, ref bufPos);
                        RcMatchedLiterDec(probs, ref probLitBase, ref symbol, ref matchByte, ref offs, ref range, ref code, buf, ref bufPos);
                        RcMatchedLiterDec(probs, ref probLitBase, ref symbol, ref matchByte, ref offs, ref range, ref code, buf, ref bufPos);
                        dic[dicPos++] = (byte)symbol;
                    }
                    continue;
                }

                RcUpdate1(probs, probIdx, ref range, ref code, bound, ttt);
                int probBase = Pi(IsRep) + (int)state;
                if (RcIfBit0(probs, probBase, ref range, ref code, buf, ref bufPos, out bound, out ttt))
                {
                    RcUpdate0(probs, probBase, ref range, bound, ttt);
                    state += kNumStates;
                    probBase = Pi(LenCoder);
                }
                else
                {
                    RcUpdate1(probs, probBase, ref range, ref code, bound, ttt);
                    probBase = Pi(IsRepG0) + (int)state;
                    if (RcIfBit0(probs, probBase, ref range, ref code, buf, ref bufPos, out bound, out ttt))
                    {
                        RcUpdate0(probs, probBase, ref range, bound, ttt);
                        probBase = Pi(IsRep0Long) + posState + (int)state;
                        if (RcIfBit0(probs, probBase, ref range, ref code, buf, ref bufPos, out bound, out ttt))
                        {
                            RcUpdate0(probs, probBase, ref range, bound, ttt);
                            dic[dicPos] = dic[dicPos - (int)rep0 + (dicPos < rep0 ? dicBufSize : 0)];
                            dicPos++;
                            processedPos++;
                            state = state < kNumLitStates ? 9u : 11u;
                            continue;
                        }
                        RcUpdate1(probs, probBase, ref range, ref code, bound, ttt);
                    }
                    else
                    {
                        uint distance;
                        RcUpdate1(probs, probBase, ref range, ref code, bound, ttt);
                        probBase = Pi(IsRepG1) + (int)state;
                        if (RcIfBit0(probs, probBase, ref range, ref code, buf, ref bufPos, out bound, out ttt))
                        {
                            RcUpdate0(probs, probBase, ref range, bound, ttt);
                            distance = rep1;
                        }
                        else
                        {
                            RcUpdate1(probs, probBase, ref range, ref code, bound, ttt);
                            probBase = Pi(IsRepG2) + (int)state;
                            if (RcIfBit0(probs, probBase, ref range, ref code, buf, ref bufPos, out bound, out ttt))
                            {
                                RcUpdate0(probs, probBase, ref range, bound, ttt);
                                distance = rep2;
                            }
                            else
                            {
                                RcUpdate1(probs, probBase, ref range, ref code, bound, ttt);
                                distance = rep3;
                                rep3 = rep2;
                            }
                            rep2 = rep1;
                        }
                        rep1 = rep0;
                        rep0 = distance;
                    }
                    state = state < kNumLitStates ? 8u : 11u;
                    probBase = Pi(RepLenCoder);
                }

                {
                    int probLenBase = probBase + LenChoice;
                    if (RcIfBit0(probs, probLenBase, ref range, ref code, buf, ref bufPos, out bound, out ttt))
                    {
                        RcUpdate0(probs, probLenBase, ref range, bound, ttt);
                        probLenBase = probBase + LenLow + posState;
                        len = 1;
                        RcTreeGetBit(probs, ref probLenBase, ref len, ref range, ref code, buf, ref bufPos);
                        RcTreeGetBit(probs, ref probLenBase, ref len, ref range, ref code, buf, ref bufPos);
                        RcTreeGetBit(probs, ref probLenBase, ref len, ref range, ref code, buf, ref bufPos);
                        len -= 8;
                    }
                    else
                    {
                        RcUpdate1(probs, probLenBase, ref range, ref code, bound, ttt);
                        probLenBase = probBase + LenChoice2;
                        if (RcIfBit0(probs, probLenBase, ref range, ref code, buf, ref bufPos, out bound, out ttt))
                        {
                            RcUpdate0(probs, probLenBase, ref range, bound, ttt);
                            probLenBase = probBase + LenLow + posState + (1 << kLenNumLowBits);
                            len = 1;
                            RcTreeGetBit(probs, ref probLenBase, ref len, ref range, ref code, buf, ref bufPos);
                            RcTreeGetBit(probs, ref probLenBase, ref len, ref range, ref code, buf, ref bufPos);
                            RcTreeGetBit(probs, ref probLenBase, ref len, ref range, ref code, buf, ref bufPos);
                        }
                        else
                        {
                            RcUpdate1(probs, probLenBase, ref range, ref code, bound, ttt);
                            probLenBase = probBase + LenHigh;
                            RcTreeDecode(probs, ref probLenBase, 1 << kLenNumHighBits, ref len, ref range, ref code, buf, ref bufPos);
                            len += kLenNumLowSymbols * 2;
                        }
                    }
                }

                if (state >= kNumStates)
                {
                    uint distance = 0;
                    probBase = Pi(PosSlot) + ((len < kNumLenToPosStates ? len : kNumLenToPosStates - 1) << kNumPosSlotBits);
                    RcTree6Decode(probs, ref probBase, ref distance, ref range, ref code, buf, ref bufPos);
                    if (distance >= kStartPosModelIndex)
                    {
                        int posSlot = (int)distance;
                        int numDirectBits = (int)((distance >> 1) - 1);
                        distance = (2u | (distance & 1u));
                        if (posSlot < kEndPosModelIndex)
                        {
                            distance <<= numDirectBits;
                            probBase = Pi(SpecPos);
                            uint m = 1;
                            distance++;
                            do
                            {
                                RcRevBitVar(probs, ref probBase, ref distance, ref m, ref range, ref code, buf, ref bufPos);
                            } while (--numDirectBits != 0);
                            distance -= m;
                        }
                        else
                        {
                            numDirectBits -= kNumAlignBits;
                            do
                            {
                                RcNormalize(ref range, ref code, buf, ref bufPos);
                                range >>= 1;
                                code -= range;
                                uint t = 0u - (code >> 31);
                                distance = (distance << 1) + (t + 1u);
                                code += range & t;
                            } while (--numDirectBits != 0);
                            probBase = Pi(Align);
                            distance <<= kNumAlignBits;
                            int i = 1;
                            RcRevBitConst(probs, ref probBase, ref i, 1, ref range, ref code, buf, ref bufPos);
                            RcRevBitConst(probs, ref probBase, ref i, 2, ref range, ref code, buf, ref bufPos);
                            RcRevBitConst(probs, ref probBase, ref i, 4, ref range, ref code, buf, ref bufPos);
                            RcRevBitLast(probs, ref probBase, ref i, 8, ref range, ref code, buf, ref bufPos);
                            distance |= (uint)i;
                            if (distance == 0xFFFFFFFFu)
                            {
                                len = kMatchSpecLenStart;
                                state -= kNumStates;
                                break;
                            }
                        }
                    }

                    rep3 = rep2;
                    rep2 = rep1;
                    rep1 = rep0;
                    rep0 = distance + 1u;
                    state = (state < kNumStates + kNumLitStates) ? (uint)kNumLitStates : (uint)(kNumLitStates + 3);
                    if (distance >= (checkDicSize == 0 ? processedPos : checkDicSize))
                    {
                        len += kMatchSpecLenErrorData + kMatchMinLen;
                        break;
                    }
                }

                len += kMatchMinLen;

                {
                    int rem = limit - dicPos;
                    if (rem == 0)
                        break;

                    int curLen = rem < len ? rem : len;
                    int pos = dicPos - (int)rep0 + (dicPos < rep0 ? dicBufSize : 0);

                    processedPos += (uint)curLen;
                    len -= curLen;
                    if (curLen <= dicBufSize - pos)
                    {
                        int destPos = dicPos;
                        int srcRel = pos - dicPos;
                        int limPos = destPos + curLen;
                        dicPos += curLen;
                        do
                        {
                            dic[destPos] = dic[destPos + srcRel];
                            destPos++;
                        } while (destPos != limPos);
                    }
                    else
                    {
                        do
                        {
                            dic[dicPos++] = dic[pos];
                            if (++pos == dicBufSize)
                                pos = 0;
                        } while (--curLen != 0);
                    }
                }
            } while (dicPos < limit && bufPos < bufLimitPos);

            RcNormalize(ref range, ref code, buf, ref bufPos);

            p.BufPos = bufPos;
            p.Range = range;
            p.Code = code;
            p.RemainLen = (uint)len;
            p.DicPos = dicPos;
            p.ProcessedPos = processedPos;
            p.Reps[0] = rep0;
            p.Reps[1] = rep1;
            p.Reps[2] = rep2;
            p.Reps[3] = rep3;
            p.State = state;
            if (len >= kMatchSpecLenErrorData)
                return SzRes.ErrorData;
            return SzRes.Ok;
        }

        private static void LzmaDec_WriteRem(CLzmaDec p, int limit)
        {
            int len = (int)p.RemainLen;
            if (len == 0)
                return;

            int dicPos = p.DicPos;
            int rem = limit - dicPos;
            if (rem < len)
            {
                len = rem;
                if (len == 0)
                    return;
            }

            if (p.CheckDicSize == 0 && p.Prop.DicSize - p.ProcessedPos <= (uint)len)
                p.CheckDicSize = p.Prop.DicSize;

            p.ProcessedPos += (uint)len;
            p.RemainLen -= (uint)len;
            byte[] dic = p.Dic;
            int rep0 = (int)p.Reps[0];
            int dicBufSize = p.DicBufSize;
            do
            {
                dic[dicPos] = dic[dicPos - rep0 + (dicPos < rep0 ? dicBufSize : 0)];
                dicPos++;
            } while (--len != 0);
            p.DicPos = dicPos;
        }

        private static int LzmaDec_DecodeReal2(CLzmaDec p, int limit, int bufLimitPos)
        {
            if (p.CheckDicSize == 0)
            {
                uint rem = p.Prop.DicSize - p.ProcessedPos;
                if (limit - p.DicPos > rem)
                    limit = p.DicPos + (int)rem;
            }
            int res = LzmaDec_DecodeReal_3(p, limit, bufLimitPos);
            if (p.CheckDicSize == 0 && p.ProcessedPos >= p.Prop.DicSize)
                p.CheckDicSize = p.Prop.DicSize;
            return res;
        }

        private static ELzmaDummy LzmaDec_TryDummy(CLzmaDec p, byte[] buf, int bufOffset, ref int bufPos, int bufLimitPos)
        {
            uint range = p.Range;
            uint code = p.Code;
            ushort[] probs = p.Probs;
            uint state = p.State;
            ELzmaDummy res;

            for (;;)
            {
                uint bound;
                int ttt;
                bool isBit0;
                uint pbMask = (1u << p.Prop.Pb) - 1u;
                int posState = CalcPosState(p.ProcessedPos, pbMask);
                int probIdx = Pi(IsMatch) + posState + (int)state;

                if (!RcBitCheck(probs, probIdx, ref range, ref code, buf, ref bufPos, bufLimitPos, out isBit0, out bound, out ttt))
                    return ELzmaDummy.DummyInputEof;
                if (isBit0)
                {
                    RcUpdate0Check(ref range, bound);
                    int probBase = Pi(Literal);
                    if (p.CheckDicSize != 0 || p.ProcessedPos != 0)
                    {
                        uint lc = p.Prop.Lc;
                        uint lpMask = (0x100u << p.Prop.Lp) - (0x100u >> (int)lc);
                        probBase += (int)(3u * ((((p.ProcessedPos << 8) + p.Dic[(p.DicPos == 0 ? p.DicBufSize : p.DicPos) - 1]) & lpMask) << (int)lc));
                    }

                    if (state < kNumLitStates)
                    {
                        int symbol = 1;
                        do
                        {
                            if (!RcGetBit2Check(probs, probBase + symbol, ref symbol, ref range, ref code, buf, ref bufPos, bufLimitPos))
                                return ELzmaDummy.DummyInputEof;
                        } while (symbol < 0x100);
                    }
                    else
                    {
                        int matchByte = p.Dic[p.DicPos - (int)p.Reps[0] + (p.DicPos < p.Reps[0] ? p.DicBufSize : 0)];
                        int offs = 0x100;
                        int symbol = 1;
                        do
                        {
                            matchByte += matchByte;
                            int bit = offs;
                            offs &= matchByte;
                            int probLitIdx = probBase + offs + bit + symbol;
                            uint boundLit;
                            int tttLit;
                            bool isBit0Lit;
                            if (!RcBitCheck(probs, probLitIdx, ref range, ref code, buf, ref bufPos, bufLimitPos, out isBit0Lit, out boundLit, out tttLit))
                                return ELzmaDummy.DummyInputEof;
                            if (isBit0Lit)
                            {
                                RcUpdate0Check(ref range, boundLit);
                                offs ^= bit;
                                symbol <<= 1;
                            }
                            else
                            {
                                RcUpdate1Check(ref range, ref code, boundLit);
                                symbol = (symbol << 1) + 1;
                            }
                        } while (symbol < 0x100);
                    }
                    res = ELzmaDummy.DummyLit;
                }
                else
                {
                    int len;
                    RcUpdate1Check(ref range, ref code, bound);
                    int probBase = Pi(IsRep) + (int)state;
                    if (!RcBitCheck(probs, probBase, ref range, ref code, buf, ref bufPos, bufLimitPos, out isBit0, out bound, out ttt))
                        return ELzmaDummy.DummyInputEof;
                    if (isBit0)
                    {
                        RcUpdate0Check(ref range, bound);
                        state = 0;
                        probBase = Pi(LenCoder);
                        res = ELzmaDummy.DummyMatch;
                    }
                    else
                    {
                        RcUpdate1Check(ref range, ref code, bound);
                        res = ELzmaDummy.DummyRep;
                        probBase = Pi(IsRepG0) + (int)state;
                        if (!RcBitCheck(probs, probBase, ref range, ref code, buf, ref bufPos, bufLimitPos, out isBit0, out bound, out ttt))
                            return ELzmaDummy.DummyInputEof;
                        if (isBit0)
                        {
                            RcUpdate0Check(ref range, bound);
                            probBase = Pi(IsRep0Long) + posState + (int)state;
                            if (!RcBitCheck(probs, probBase, ref range, ref code, buf, ref bufPos, bufLimitPos, out isBit0, out bound, out ttt))
                                return ELzmaDummy.DummyInputEof;
                            if (isBit0)
                            {
                                RcUpdate0Check(ref range, bound);
                                goto DummyDone;
                            }
                            RcUpdate1Check(ref range, ref code, bound);
                        }
                        else
                        {
                            RcUpdate1Check(ref range, ref code, bound);
                            probBase = Pi(IsRepG1) + (int)state;
                            if (!RcBitCheck(probs, probBase, ref range, ref code, buf, ref bufPos, bufLimitPos, out isBit0, out bound, out ttt))
                                return ELzmaDummy.DummyInputEof;
                            if (isBit0)
                            {
                                RcUpdate0Check(ref range, bound);
                            }
                            else
                            {
                                RcUpdate1Check(ref range, ref code, bound);
                                probBase = Pi(IsRepG2) + (int)state;
                                if (!RcBitCheck(probs, probBase, ref range, ref code, buf, ref bufPos, bufLimitPos, out isBit0, out bound, out ttt))
                                    return ELzmaDummy.DummyInputEof;
                                if (isBit0)
                                    RcUpdate0Check(ref range, bound);
                                else
                                    RcUpdate1Check(ref range, ref code, bound);
                            }
                        }
                        state = kNumStates;
                        probBase = Pi(RepLenCoder);
                    }

                    {
                        int limitLen;
                        int offset;
                        int probLenBase = probBase + LenChoice;
                        if (!RcBitCheck(probs, probLenBase, ref range, ref code, buf, ref bufPos, bufLimitPos, out isBit0, out bound, out ttt))
                            return ELzmaDummy.DummyInputEof;
                        if (isBit0)
                        {
                            RcUpdate0Check(ref range, bound);
                            probLenBase = probBase + LenLow + posState;
                            offset = 0;
                            limitLen = 1 << kLenNumLowBits;
                        }
                        else
                        {
                            RcUpdate1Check(ref range, ref code, bound);
                            probLenBase = probBase + LenChoice2;
                            if (!RcBitCheck(probs, probLenBase, ref range, ref code, buf, ref bufPos, bufLimitPos, out isBit0, out bound, out ttt))
                                return ELzmaDummy.DummyInputEof;
                            if (isBit0)
                            {
                                RcUpdate0Check(ref range, bound);
                                probLenBase = probBase + LenLow + posState + (1 << kLenNumLowBits);
                                offset = kLenNumLowSymbols;
                                limitLen = 1 << kLenNumLowBits;
                            }
                            else
                            {
                                RcUpdate1Check(ref range, ref code, bound);
                                probLenBase = probBase + LenHigh;
                                offset = kLenNumLowSymbols * 2;
                                limitLen = 1 << kLenNumHighBits;
                            }
                        }
                        len = 0;
                        if (!RcTreeDecodeCheck(probs, ref probLenBase, limitLen, ref len, ref range, ref code, buf, ref bufPos, bufLimitPos))
                            return ELzmaDummy.DummyInputEof;
                        len += offset;
                    }

                    if (state < 4)
                    {
                        int posSlot = 0;
                        probBase = Pi(PosSlot) +
                            ((len < kNumLenToPosStates - 1 ? len : kNumLenToPosStates - 1) << kNumPosSlotBits);
                        if (!RcTreeDecodeCheck(probs, ref probBase, 1 << kNumPosSlotBits, ref posSlot, ref range, ref code, buf, ref bufPos, bufLimitPos))
                            return ELzmaDummy.DummyInputEof;
                        if (posSlot >= kStartPosModelIndex)
                        {
                            int numDirectBits = (posSlot >> 1) - 1;
                            if (posSlot < kEndPosModelIndex)
                            {
                                probBase = Pi(SpecPos) + ((2 | (posSlot & 1)) << numDirectBits);
                            }
                            else
                            {
                                numDirectBits -= kNumAlignBits;
                                do
                                {
                                    if (!RcNormalizeCheck(ref range, ref code, buf, ref bufPos, bufLimitPos))
                                        return ELzmaDummy.DummyInputEof;
                                    range >>= 1;
                                    code -= range & (((code - range) >> 31) - 1u);
                                } while (--numDirectBits != 0);
                                probBase = Pi(Align);
                                numDirectBits = kNumAlignBits;
                            }
                            int i = 1;
                            int m = 1;
                            do
                            {
                                if (!RcRevBitCheck(probs, ref probBase, ref i, ref m, ref range, ref code, buf, ref bufPos, bufLimitPos))
                                    return ELzmaDummy.DummyInputEof;
                            } while (--numDirectBits != 0);
                        }
                    }
                }
                break;
            }

        DummyDone:
            if (!RcNormalizeCheck(ref range, ref code, buf, ref bufPos, bufLimitPos))
                return ELzmaDummy.DummyInputEof;
            return res;
        }

        public static int LzmaDec_DecodeToDic(CLzmaDec p, int dicLimit, byte[] src, int srcOffset, ref int srcLen,
            ELzmaFinishMode finishMode, out ELzmaStatus status)
        {
            int inSize = srcLen;
            srcLen = 0;
            status = ELzmaStatus.LzmaStatusNotSpecified;

            if (p.RemainLen > kMatchSpecLenStart)
            {
                if (p.RemainLen > kMatchSpecLenStart + 2)
                    return p.RemainLen == kMatchSpecLenErrorFail ? SzRes.ErrorFail : SzRes.ErrorData;

                int srcPos = srcOffset;
                for (; inSize > 0 && p.TempBufSize < RcInitSize; srcLen++, inSize--)
                    p.TempBuf[p.TempBufSize++] = src[srcPos++];
                if (p.TempBufSize != 0 && p.TempBuf[0] != 0)
                    return SzRes.ErrorData;
                if (p.TempBufSize < RcInitSize)
                {
                    status = ELzmaStatus.LzmaStatusNeedsMoreInput;
                    return SzRes.Ok;
                }
                p.Code = ((uint)p.TempBuf[1] << 24)
                    | ((uint)p.TempBuf[2] << 16)
                    | ((uint)p.TempBuf[3] << 8)
                    | p.TempBuf[4];

                if (p.CheckDicSize == 0 && p.ProcessedPos == 0 && p.Code >= kBadRepCode)
                    return SzRes.ErrorData;

                p.Range = 0xFFFFFFFFu;
                p.TempBufSize = 0;

                if (p.RemainLen > kMatchSpecLenStart + 1)
                {
                    uint numProbs = LzmaProps_GetNumProbs(p.Prop);
                    ushort[] probs = p.Probs;
                    for (uint i = 0; i < numProbs; i++)
                        probs[i] = (ushort)(kBitModelTotal >> 1);
                    p.Reps[0] = p.Reps[1] = p.Reps[2] = p.Reps[3] = 1;
                    p.State = 0;
                }

                p.RemainLen = 0;
            }

            for (;;)
            {
                if (p.RemainLen == kMatchSpecLenStart)
                {
                    if (p.Code != 0)
                        return SzRes.ErrorData;
                    status = ELzmaStatus.LzmaStatusFinishedWithMark;
                    return SzRes.Ok;
                }

                LzmaDec_WriteRem(p, dicLimit);

                int checkEndMarkNow = 0;

                if (p.DicPos >= dicLimit)
                {
                    if (p.RemainLen == 0 && p.Code == 0)
                    {
                        status = ELzmaStatus.LzmaStatusMaybeFinishedWithoutMark;
                        return SzRes.Ok;
                    }
                    if (finishMode == ELzmaFinishMode.LzmaFinishAny)
                    {
                        status = ELzmaStatus.LzmaStatusNotFinished;
                        return SzRes.Ok;
                    }
                    if (p.RemainLen != 0)
                    {
                        status = ELzmaStatus.LzmaStatusNotFinished;
                        return SzRes.ErrorData;
                    }
                    checkEndMarkNow = 1;
                }

                if (p.TempBufSize == 0)
                {
                    int bufLimitPos;
                    int dummyProcessed = -1;
                    int curSrc = srcOffset + srcLen;

                    if (inSize < LzmaRequiredInputMax || checkEndMarkNow != 0)
                    {
                        int bufPos = curSrc;
                        ELzmaDummy dummyRes = LzmaDec_TryDummy(p, src, curSrc, ref bufPos, curSrc + inSize);
                        if (dummyRes == ELzmaDummy.DummyInputEof)
                        {
                            if (inSize >= LzmaRequiredInputMax)
                                break;
                            srcLen += inSize;
                            p.TempBufSize = inSize;
                            for (int i = 0; i < inSize; i++)
                                p.TempBuf[i] = src[curSrc + i];
                            status = ELzmaStatus.LzmaStatusNeedsMoreInput;
                            return SzRes.Ok;
                        }

                        dummyProcessed = bufPos - curSrc;
                        if ((uint)dummyProcessed > LzmaRequiredInputMax)
                            break;

                        if (checkEndMarkNow != 0 && !IsDummyEndMarkerPossible(dummyRes))
                        {
                            srcLen += dummyProcessed;
                            p.TempBufSize = dummyProcessed;
                            for (int i = 0; i < dummyProcessed; i++)
                                p.TempBuf[i] = src[curSrc + i];
                            status = ELzmaStatus.LzmaStatusNotFinished;
                            return SzRes.ErrorData;
                        }

                        // One LZMA symbol per call; bufLimit equals current read position (7z2602-src/C/LzmaDec.c).
                        bufLimitPos = curSrc;
                    }
                    else
                        bufLimitPos = curSrc + inSize - LzmaRequiredInputMax;

                    p.Buf = src;
                    p.BufPos = curSrc;

                    {
                        int res = LzmaDec_DecodeReal2(p, dicLimit, bufLimitPos);
                        int processed = p.BufPos - curSrc;

                        if (dummyProcessed < 0)
                        {
                            if (processed > inSize)
                                break;
                        }
                        else if (dummyProcessed != processed)
                            break;

                        curSrc += processed;
                        inSize -= processed;
                        srcLen += processed;

                        if (res != SzRes.Ok)
                        {
                            p.RemainLen = kMatchSpecLenErrorData;
                            return SzRes.ErrorData;
                        }
                    }
                    continue;
                }

                {
                    int rem = p.TempBufSize;
                    int ahead = 0;
                    int dummyProcessed = -1;

                    while (rem < LzmaRequiredInputMax && ahead < inSize)
                        p.TempBuf[rem++] = src[srcOffset + srcLen + ahead++];

                    if (rem < LzmaRequiredInputMax || checkEndMarkNow != 0)
                    {
                        int bufPos = 0;
                        ELzmaDummy dummyRes = LzmaDec_TryDummy(p, p.TempBuf, 0, ref bufPos, rem);
                        if (dummyRes == ELzmaDummy.DummyInputEof)
                        {
                            if (rem >= LzmaRequiredInputMax)
                                break;
                            p.TempBufSize = rem;
                            srcLen += ahead;
                            status = ELzmaStatus.LzmaStatusNeedsMoreInput;
                            return SzRes.Ok;
                        }

                        dummyProcessed = bufPos;
                        if (dummyProcessed < p.TempBufSize)
                            break;

                        if (checkEndMarkNow != 0 && !IsDummyEndMarkerPossible(dummyRes))
                        {
                            srcLen += dummyProcessed - p.TempBufSize;
                            p.TempBufSize = dummyProcessed;
                            status = ELzmaStatus.LzmaStatusNotFinished;
                            return SzRes.ErrorData;
                        }
                    }

                    p.Buf = p.TempBuf;
                    p.BufPos = 0;

                    {
                        int res = LzmaDec_DecodeReal2(p, dicLimit, p.BufPos);
                        int processed = p.BufPos;
                        rem = p.TempBufSize;

                        if (dummyProcessed < 0)
                        {
                            if (processed > LzmaRequiredInputMax)
                                break;
                            if (processed < rem)
                                break;
                        }
                        else if (dummyProcessed != processed)
                            break;

                        processed -= rem;
                        inSize -= processed;
                        srcLen += processed;
                        p.TempBufSize = 0;

                        if (res != SzRes.Ok)
                        {
                            p.RemainLen = kMatchSpecLenErrorData;
                            return SzRes.ErrorData;
                        }
                    }
                }
            }

            p.RemainLen = kMatchSpecLenErrorFail;
            return SzRes.ErrorFail;
        }

        public static int DecodeToDic(CLzmaDec p, int dicLimit, byte[] src, ref int srcLen, int srcOffset,
            ELzmaFinishMode finishMode, out ELzmaStatus status) =>
            LzmaDec_DecodeToDic(p, dicLimit, src, srcOffset, ref srcLen, finishMode, out status);

        public static int LzmaDec_DecodeToBuf(CLzmaDec p, byte[] dest, int destOffset, ref int destLen,
            byte[] src, int srcOffset, ref int srcLen, ELzmaFinishMode finishMode, out ELzmaStatus status)
        {
            int outSize = destLen;
            int inSize = srcLen;
            srcLen = 0;
            destLen = 0;
            int curDest = destOffset;
            int curSrc = srcOffset;

            for (;;)
            {
                int inSizeCur = inSize;
                int outSizeCur;
                int dicPos;
                ELzmaFinishMode curFinishMode;
                if (p.DicPos == p.DicBufSize)
                    p.DicPos = 0;
                dicPos = p.DicPos;
                if (outSize > p.DicBufSize - dicPos)
                {
                    outSizeCur = p.DicBufSize;
                    curFinishMode = ELzmaFinishMode.LzmaFinishAny;
                }
                else
                {
                    outSizeCur = dicPos + outSize;
                    curFinishMode = finishMode;
                }

                int res = LzmaDec_DecodeToDic(p, outSizeCur, src, curSrc, ref inSizeCur, curFinishMode, out status);
                curSrc += inSizeCur;
                inSize -= inSizeCur;
                srcLen += inSizeCur;
                outSizeCur = p.DicPos - dicPos;
                Array.Copy(p.Dic, dicPos, dest, curDest, outSizeCur);
                curDest += outSizeCur;
                outSize -= outSizeCur;
                destLen += outSizeCur;
                if (res != SzRes.Ok)
                    return res;
                if (outSizeCur == 0 || outSize == 0)
                    return SzRes.Ok;
            }
        }

        public static int LzmaDecode(byte[] dest, ref int destLen, byte[] src, int srcOffset, ref int srcLen,
            byte[] propData, int propOffset, uint propSize, ELzmaFinishMode finishMode,
            out ELzmaStatus status, ISzAlloc alloc)
        {
            CLzmaDec p = new CLzmaDec();
            LzmaDec_Construct(p);
            int outSize = destLen;
            int inSize = srcLen;
            destLen = 0;
            srcLen = 0;
            status = ELzmaStatus.LzmaStatusNotSpecified;
            if (inSize < RcInitSize)
                return SzRes.ErrorInputEof;
            int res = LzmaDec_AllocateProbs(p, propData, propOffset, propSize, alloc);
            if (res != SzRes.Ok)
                return res;
            p.Dic = dest;
            p.DicBufSize = outSize;
            LzmaDec_Init(p);
            srcLen = inSize;
            res = LzmaDec_DecodeToDic(p, outSize, src, srcOffset, ref srcLen, finishMode, out status);
            destLen = p.DicPos;
            if (res == SzRes.Ok && status == ELzmaStatus.LzmaStatusNeedsMoreInput)
                res = SzRes.ErrorInputEof;
            LzmaDec_FreeProbs(p, alloc);
            return res;
        }

    }
}
