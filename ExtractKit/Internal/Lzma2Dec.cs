namespace SmartGoldbergEmu.ExtractKit.Internal
{
    internal enum ELzma2ParseStatus
    {
        NewBlock = ELzmaStatus.LzmaStatusMaybeFinishedWithoutMark + 1,
        NewChunk
    }

    internal sealed class CLzma2Dec
    {
        public int State;
        public byte Control;
        public byte NeedInitLevel;
        public byte IsExtraMode;
        public uint PackSize;
        public uint UnpackSize;
        public readonly CLzmaDec Decoder = new CLzmaDec();

        public void Construct()
        {
            Decoder.Construct();
        }
    }

    internal static class Lzma2Dec
    {
        private const int LclpMax = 4;
        private const int ControlCopyResetDic = 1;
        private const int PropsSize = LzmaDec.PropsSize;

        private enum ELzma2State
        {
            Control,
            Unpack0,
            Unpack1,
            Pack0,
            Pack1,
            Prop,
            Data,
            DataCont,
            Finished,
            Error
        }

        private static bool IsUncompressedState(CLzma2Dec p)
        {
            return (p.Control & (1 << 7)) == 0;
        }

        private static uint DicSizeFromProp(byte prop)
        {
            return (uint)(2 | (prop & 1)) << (prop / 2 + 11);
        }

        private static int GetOldProps(byte prop, byte[] props)
        {
            if (prop > 40)
                return SzRes.ErrorUnsupported;
            uint dicSize = prop == 40 ? 0xFFFFFFFFu : DicSizeFromProp(prop);
            props[0] = (byte)LclpMax;
            props[1] = (byte)dicSize;
            props[2] = (byte)(dicSize >> 8);
            props[3] = (byte)(dicSize >> 16);
            props[4] = (byte)(dicSize >> 24);
            return SzRes.Ok;
        }

        public static int AllocateProbs(CLzma2Dec p, byte prop, ISzAlloc alloc)
        {
            byte[] props = new byte[PropsSize];
            int res = GetOldProps(prop, props);
            if (res != SzRes.Ok)
                return res;
            return LzmaDec.AllocateProbs(p.Decoder, props, PropsSize, alloc);
        }

        public static int Allocate(CLzma2Dec p, byte prop, ISzAlloc alloc)
        {
            byte[] props = new byte[PropsSize];
            int res = GetOldProps(prop, props);
            if (res != SzRes.Ok)
                return res;
            return LzmaDec.Allocate(p.Decoder, props, PropsSize, alloc);
        }

        public static void Init(CLzma2Dec p)
        {
            p.State = (int)ELzma2State.Control;
            p.NeedInitLevel = 0xE0;
            p.IsExtraMode = 0;
            p.UnpackSize = 0;
            LzmaDec.Init(p.Decoder);
        }

        private static int UpdateState(CLzma2Dec p, byte b)
        {
            switch ((ELzma2State)p.State)
            {
                case ELzma2State.Control:
                    p.IsExtraMode = 0;
                    p.Control = b;
                    if (b == 0)
                        return (int)ELzma2State.Finished;
                    if (IsUncompressedState(p))
                    {
                        if (b == ControlCopyResetDic)
                            p.NeedInitLevel = 0xC0;
                        else if (b > 2 || p.NeedInitLevel == 0xE0)
                            return (int)ELzma2State.Error;
                    }
                    else
                    {
                        if (b < p.NeedInitLevel)
                            return (int)ELzma2State.Error;
                        p.NeedInitLevel = 0;
                        p.UnpackSize = (uint)(b & 0x1F) << 16;
                    }
                    return (int)ELzma2State.Unpack0;

                case ELzma2State.Unpack0:
                    p.UnpackSize |= (uint)b << 8;
                    return (int)ELzma2State.Unpack1;

                case ELzma2State.Unpack1:
                    p.UnpackSize |= b;
                    p.UnpackSize++;
                    return IsUncompressedState(p) ? (int)ELzma2State.Data : (int)ELzma2State.Pack0;

                case ELzma2State.Pack0:
                    p.PackSize = (uint)b << 8;
                    return (int)ELzma2State.Pack1;

                case ELzma2State.Pack1:
                    p.PackSize |= b;
                    p.PackSize++;
                    return (p.Control & 0x40) != 0 ? (int)ELzma2State.Prop : (int)ELzma2State.Data;

                case ELzma2State.Prop:
                {
                    if (b >= 9 * 5 * 5)
                        return (int)ELzma2State.Error;
                    uint lc = (uint)(b % 9);
                    b /= 9;
                    p.Decoder.Prop.Pb = (byte)(b / 5);
                    uint lp = (uint)(b % 5);
                    if (lc + lp > LclpMax)
                        return (int)ELzma2State.Error;
                    p.Decoder.Prop.Lc = (byte)lc;
                    p.Decoder.Prop.Lp = (byte)lp;
                    return (int)ELzma2State.Data;
                }

                default:
                    return (int)ELzma2State.Error;
            }
        }

        private static void UpdateWithUncompressed(CLzmaDec p, byte[] src, int srcOffset, int size)
        {
            System.Array.Copy(src, srcOffset, p.Dic, p.DicPos, size);
            p.DicPos += size;
            if (p.CheckDicSize == 0 && p.Prop.DicSize - p.ProcessedPos <= size)
                p.CheckDicSize = p.Prop.DicSize;
            p.ProcessedPos += (uint)size;
        }

        public static void FreeProbs(CLzma2Dec p, ISzAlloc alloc)
        {
            LzmaDec.FreeProbs(p.Decoder, alloc);
        }

        public static int DecodeToDic(CLzma2Dec p, int dicLimit, byte[] src, ref int srcLen, int srcOffset,
            ELzmaFinishMode finishMode, out ELzmaStatus status)
        {
            int inSize = srcLen;
            srcLen = 0;
            status = ELzmaStatus.LzmaStatusNotSpecified;
            int curOffset = srcOffset;

            while (p.State != (int)ELzma2State.Error)
            {
                int dicPos;
                if (p.State == (int)ELzma2State.Finished)
                {
                    status = ELzmaStatus.LzmaStatusFinishedWithMark;
                    return SzRes.Ok;
                }

                dicPos = p.Decoder.DicPos;
                if (dicPos == dicLimit && finishMode == ELzmaFinishMode.LzmaFinishAny)
                {
                    status = ELzmaStatus.LzmaStatusNotFinished;
                    return SzRes.Ok;
                }

                if (p.State != (int)ELzma2State.Data && p.State != (int)ELzma2State.DataCont)
                {
                    if (srcLen == inSize)
                    {
                        status = ELzmaStatus.LzmaStatusNeedsMoreInput;
                        return SzRes.Ok;
                    }

                    srcLen++;
                    p.State = UpdateState(p, src[curOffset++]);
                    if (dicPos == dicLimit && p.State != (int)ELzma2State.Finished)
                        break;
                    continue;
                }

                int inCur = inSize - srcLen;
                int outCur = dicLimit - dicPos;
                ELzmaFinishMode curFinishMode = ELzmaFinishMode.LzmaFinishAny;
                if (outCur >= p.UnpackSize)
                {
                    outCur = (int)p.UnpackSize;
                    curFinishMode = ELzmaFinishMode.LzmaFinishEnd;
                }

                if (IsUncompressedState(p))
                {
                    if (inCur == 0)
                    {
                        status = ELzmaStatus.LzmaStatusNeedsMoreInput;
                        return SzRes.Ok;
                    }

                    if (p.State == (int)ELzma2State.Data)
                    {
                        bool initDic = p.Control == ControlCopyResetDic;
                        LzmaDec.InitDicAndState(p.Decoder, initDic, false);
                    }

                    if (inCur > outCur)
                        inCur = outCur;
                    if (inCur == 0)
                        break;

                    UpdateWithUncompressed(p.Decoder, src, curOffset, inCur);
                    curOffset += inCur;
                    srcLen += inCur;
                    p.UnpackSize -= (uint)inCur;
                    p.State = p.UnpackSize == 0 ? (int)ELzma2State.Control : (int)ELzma2State.DataCont;
                }
                else
                {
                    if (p.State == (int)ELzma2State.Data)
                    {
                        bool initDic = p.Control >= 0xE0;
                        bool initState = p.Control >= 0xA0;
                        LzmaDec.InitDicAndState(p.Decoder, initDic, initState);
                        p.State = (int)ELzma2State.DataCont;
                    }

                    if (inCur > p.PackSize)
                        inCur = (int)p.PackSize;

                    int res = LzmaDec.DecodeToDic(p.Decoder, dicPos + outCur, src, ref inCur, curOffset,
                        curFinishMode, out status);
                    curOffset += inCur;
                    srcLen += inCur;
                    p.PackSize -= (uint)inCur;
                    outCur = p.Decoder.DicPos - dicPos;
                    p.UnpackSize -= (uint)outCur;

                    if (res != SzRes.Ok)
                        return res;

                    if (status == ELzmaStatus.LzmaStatusNeedsMoreInput)
                    {
                        if (p.PackSize == 0)
                            break;
                        return SzRes.Ok;
                    }

                    if (inCur == 0 && outCur == 0)
                    {
                        if (status != ELzmaStatus.LzmaStatusMaybeFinishedWithoutMark || p.UnpackSize != 0 || p.PackSize != 0)
                            break;
                        p.State = (int)ELzma2State.Control;
                    }

                    status = ELzmaStatus.LzmaStatusNotSpecified;
                }
            }

            status = ELzmaStatus.LzmaStatusNotSpecified;
            p.State = (int)ELzma2State.Error;
            return SzRes.ErrorData;
        }

        public static int DecodeToBuf(CLzma2Dec p, byte[] dest, ref int destLen, byte[] src, ref int srcLen,
            ELzmaFinishMode finishMode, out ELzmaStatus status)
        {
            int outSize = destLen;
            int inSize = srcLen;
            srcLen = 0;
            destLen = 0;
            int destOffset = 0;
            int srcOffset = 0;

            for (;;)
            {
                int inCur = inSize - srcLen;
                int outCur;
                int dicPos;
                ELzmaFinishMode curFinishMode;
                if (p.Decoder.DicPos == p.Decoder.DicBufSize)
                    p.Decoder.DicPos = 0;
                dicPos = p.Decoder.DicPos;
                if (outSize > p.Decoder.DicBufSize - dicPos)
                {
                    outCur = p.Decoder.DicBufSize;
                    curFinishMode = ELzmaFinishMode.LzmaFinishAny;
                }
                else
                {
                    outCur = dicPos + outSize;
                    curFinishMode = finishMode;
                }

                int processed = inCur;
                int res = DecodeToDic(p, outCur, src, ref processed, srcOffset, curFinishMode, out status);
                srcOffset += processed;
                srcLen += processed;
                outCur = p.Decoder.DicPos - dicPos;
                System.Array.Copy(p.Decoder.Dic, dicPos, dest, destOffset, outCur);
                destOffset += outCur;
                outSize -= outCur;
                destLen += outCur;
                if (res != SzRes.Ok)
                    return res;
                if (outCur == 0 || outSize == 0)
                    return SzRes.Ok;
            }
        }
    }
}
