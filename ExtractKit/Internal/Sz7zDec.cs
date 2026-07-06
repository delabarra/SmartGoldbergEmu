namespace SmartGoldbergEmu.ExtractKit.Internal
{
    internal static class Sz7zDec
    {
        private const uint KCopy = 0;
        private const uint KLzma2 = 0x21;
        private const uint KLzma = 0x30101;
        private const uint KBcj2 = 0x303011B;
        private const uint KDelta = 3;
        private const uint KRiscv = 0xb;
        private const uint KBcj = 0x3030103;
        private const uint KPpc = 0x3030205;
        private const uint KIa64 = 0x3030401;
        private const uint KArm = 0x3030501;
        private const uint KSparc = 0x3030805;
        private const uint KArm64 = 0xa;
        private const uint KArmt = 0x3030701;

        private static int DecodeLzma(byte[] props, int propsOffset, int propsSize, ulong inSize,
            ILookInStream inStream, byte[] outBuffer, int outSize, ISzAlloc allocMain)
        {
            CLzmaDec state = new CLzmaDec();
            state.Construct();
            int res = LzmaDec.AllocateProbs(state, props, propsOffset, propsSize, allocMain);
            if (res != SzRes.Ok)
                return res;

            state.Dic = outBuffer;
            state.DicBufSize = outSize;
            LzmaDec.Init(state);

            while (true)
            {
                byte[] inBuf;
                int lookOffset;
                int lookahead = 1 << 18;
                if ((ulong)lookahead > inSize)
                    lookahead = (int)inSize;
                res = inStream.Look(out inBuf, out lookOffset, ref lookahead);
                if (res != SzRes.Ok)
                    break;

                int inProcessed = lookahead;
                int dicPos = state.DicPos;
                ELzmaStatus status;
                res = LzmaDec.DecodeToDic(state, outSize, inBuf, ref inProcessed, lookOffset,
                    ELzmaFinishMode.LzmaFinishEnd, out status);
                lookahead -= inProcessed;
                inSize -= (ulong)inProcessed;
                if (res != SzRes.Ok)
                    break;

                if (status == ELzmaStatus.LzmaStatusFinishedWithMark)
                {
                    if (outSize != state.DicPos || inSize != 0)
                        res = SzRes.ErrorData;
                    break;
                }

                if (outSize == state.DicPos && inSize == 0 &&
                    status == ELzmaStatus.LzmaStatusMaybeFinishedWithoutMark)
                    break;

                if (inProcessed == 0 && dicPos == state.DicPos)
                {
                    res = SzRes.ErrorData;
                    break;
                }

                res = inStream.Skip(inProcessed);
                if (res != SzRes.Ok)
                    break;
            }

            LzmaDec.FreeProbs(state, allocMain);
            return res;
        }

        private static int DecodeLzma2(byte[] props, int propsOffset, int propsSize, ulong inSize,
            ILookInStream inStream, byte[] outBuffer, int outSize, ISzAlloc allocMain)
        {
            CLzma2Dec state = new CLzma2Dec();
            state.Construct();
            if (propsSize != 1)
                return SzRes.ErrorData;
            int res = Lzma2Dec.AllocateProbs(state, props[propsOffset], allocMain);
            if (res != SzRes.Ok)
                return res;

            state.Decoder.Dic = outBuffer;
            state.Decoder.DicBufSize = outSize;
            Lzma2Dec.Init(state);

            while (true)
            {
                byte[] inBuf;
                int lookOffset;
                int lookahead = 1 << 18;
                if ((ulong)lookahead > inSize)
                    lookahead = (int)inSize;
                res = inStream.Look(out inBuf, out lookOffset, ref lookahead);
                if (res != SzRes.Ok)
                    break;

                int inProcessed = lookahead;
                int dicPos = state.Decoder.DicPos;
                ELzmaStatus status;
                res = Lzma2Dec.DecodeToDic(state, outSize, inBuf, ref inProcessed, lookOffset,
                    ELzmaFinishMode.LzmaFinishEnd, out status);
                lookahead -= inProcessed;
                inSize -= (ulong)inProcessed;
                if (res != SzRes.Ok)
                    break;

                if (status == ELzmaStatus.LzmaStatusFinishedWithMark)
                {
                    if (outSize != state.Decoder.DicPos || inSize != 0)
                        res = SzRes.ErrorData;
                    break;
                }

                if (inProcessed == 0 && dicPos == state.Decoder.DicPos)
                {
                    res = SzRes.ErrorData;
                    break;
                }

                res = inStream.Skip(inProcessed);
                if (res != SzRes.Ok)
                    break;
            }

            Lzma2Dec.FreeProbs(state, allocMain);
            return res;
        }

        private static int DecodeCopy(ulong inSize, ILookInStream inStream, byte[] outBuffer, int outOffset)
        {
            while (inSize > 0)
            {
                byte[] inBuf;
                int lookOffset;
                int curSize = 1 << 18;
                if ((ulong)curSize > inSize)
                    curSize = (int)inSize;
                int res = inStream.Look(out inBuf, out lookOffset, ref curSize);
                if (res != SzRes.Ok)
                    return res;
                if (curSize == 0)
                    return SzRes.ErrorInputEof;
                System.Array.Copy(inBuf, lookOffset, outBuffer, outOffset, curSize);
                outOffset += curSize;
                inSize -= (ulong)curSize;
                res = inStream.Skip(curSize);
                if (res != SzRes.Ok)
                    return res;
            }

            return SzRes.Ok;
        }

        private static bool IsMainMethod(uint m)
        {
            switch (m)
            {
                case KCopy:
                case KLzma:
                case KLzma2:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsSupportedCoder(CSzCoderInfo c)
        {
            return c.NumStreams == 1 && IsMainMethod(c.MethodId);
        }

        private static bool IsBcj2(CSzCoderInfo c)
        {
            return c.MethodId == KBcj2 && c.NumStreams == 4;
        }

        private static int CheckSupportedFolder(CSzFolder f)
        {
            if (f.NumCoders < 1 || f.NumCoders > 4)
                return SzRes.ErrorUnsupported;
            if (!IsSupportedCoder(f.Coders[0]))
                return SzRes.ErrorUnsupported;
            if (f.NumCoders == 1)
            {
                if (f.NumPackStreams != 1 || f.PackStreams[0] != 0 || f.NumBonds != 0)
                    return SzRes.ErrorUnsupported;
                return SzRes.Ok;
            }

            if (f.NumCoders == 2)
            {
                CSzCoderInfo c = f.Coders[1];
                if (c.NumStreams != 1 || f.NumPackStreams != 1 || f.PackStreams[0] != 0 ||
                    f.NumBonds != 1 || f.Bonds[0].InIndex != 1 || f.Bonds[0].OutIndex != 0)
                    return SzRes.ErrorUnsupported;
                switch (c.MethodId)
                {
                    case KDelta:
                    case KBcj:
                    case KPpc:
                    case KIa64:
                    case KSparc:
                    case KArm:
                    case KRiscv:
                    case KArm64:
                    case KArmt:
                        break;
                    default:
                        return SzRes.ErrorUnsupported;
                }

                return SzRes.Ok;
            }

            if (f.NumCoders == 4)
            {
                if (!IsSupportedCoder(f.Coders[1]) || !IsSupportedCoder(f.Coders[2]) || !IsBcj2(f.Coders[3]))
                    return SzRes.ErrorUnsupported;
                if (f.NumPackStreams != 4 || f.PackStreams[0] != 2 || f.PackStreams[1] != 6 ||
                    f.PackStreams[2] != 1 || f.PackStreams[3] != 0 || f.NumBonds != 3 ||
                    f.Bonds[0].InIndex != 5 || f.Bonds[0].OutIndex != 0 ||
                    f.Bonds[1].InIndex != 4 || f.Bonds[1].OutIndex != 1 ||
                    f.Bonds[2].InIndex != 3 || f.Bonds[2].OutIndex != 2)
                    return SzRes.ErrorUnsupported;
                return SzRes.Ok;
            }

            return SzRes.ErrorUnsupported;
        }

        private static int FolderDecode2(CSzFolder folder, byte[] propsData, int propsDataOffset, ulong[] unpackSizes,
            int unpackIndex, ulong[] packPositions, int packIndex, ILookInStream inStream, ulong startPos,
            byte[] outBuffer, int outSize, ISzAlloc allocMain, byte[][] tempBuf)
        {
            int res = CheckSupportedFolder(folder);
            if (res != SzRes.Ok)
                return res;

            int[] tempSizes = new int[3];
            int tempSize3 = 0;

            for (uint ci = 0; ci < folder.NumCoders; ci++)
            {
                CSzCoderInfo coder = folder.Coders[ci];
                int coderPropsOffset = propsDataOffset + coder.PropsOffset;
                if (IsMainMethod(coder.MethodId))
                {
                    uint si = 0;
                    ulong offset;
                    ulong packInSize;
                    byte[] outBufCur = outBuffer;
                    int outOffsetCur = 0;
                    int outSizeCur = outSize;
                    if (folder.NumCoders == 4)
                    {
                        uint[] indices = { 3, 2, 0 };
                        ulong unpackSize = unpackSizes[unpackIndex + ci];
                        si = indices[ci];
                        if (ci < 2)
                        {
                            outSizeCur = (int)unpackSize;
                            if (outSizeCur != (int)unpackSize)
                                return SzRes.ErrorMem;
                            byte[] temp = SzAllocImpl.Alloc(allocMain, outSizeCur);
                            if (temp == null && outSizeCur != 0)
                                return SzRes.ErrorMem;
                            outBufCur = tempBuf[1 - ci] = temp;
                            tempSizes[1 - (int)ci] = outSizeCur;
                        }
                        else if (ci == 2)
                        {
                            if (unpackSize > (ulong)outSize)
                                return SzRes.ErrorParam;
                            tempSize3 = outSizeCur = (int)unpackSize;
                            // Decode the BCJ2 main stream into its own 0-based buffer rather than in-place
                            // at the tail of outBuffer: the upstream in-place trick needs a dic base offset
                            // the C# LZMA decoder cannot express without breaking its wrap and prev-byte
                            // literal-context math (dic[dicPos - 1] / dicBufSize wrap assume a 0-based dic).
                            byte[] mainTemp = SzAllocImpl.Alloc(allocMain, outSizeCur);
                            if (mainTemp == null && outSizeCur != 0)
                                return SzRes.ErrorMem;
                            tempBuf[3] = mainTemp;
                            outBufCur = mainTemp;
                            outOffsetCur = 0;
                        }
                        else
                        {
                            return SzRes.ErrorUnsupported;
                        }
                    }

                    offset = packPositions[packIndex + si];
                    packInSize = packPositions[packIndex + si + 1] - offset;
                    res = SzLookInStream.SeekTo(inStream, startPos + offset);
                    if (res != SzRes.Ok)
                        return res;

                    if (coder.MethodId == KCopy)
                    {
                        if (packInSize != (ulong)outSizeCur)
                            return SzRes.ErrorData;
                        res = DecodeCopy(packInSize, inStream, outBufCur, outOffsetCur);
                    }
                    else if (coder.MethodId == KLzma)
                    {
                        res = DecodeLzma(propsData, coderPropsOffset, coder.PropsSize, packInSize,
                            inStream, outBufCur, outSizeCur, allocMain);
                    }
                    else if (coder.MethodId == KLzma2)
                    {
                        res = DecodeLzma2(propsData, coderPropsOffset, coder.PropsSize, packInSize,
                            inStream, outBufCur, outSizeCur, allocMain);
                    }
                    else
                    {
                        return SzRes.ErrorUnsupported;
                    }

                    if (res != SzRes.Ok)
                        return res;
                }
                else if (coder.MethodId == KBcj2)
                {
                    ulong offset = packPositions[packIndex + 1];
                    ulong s3Size = packPositions[packIndex + 2] - offset;
                    if (ci != 3)
                        return SzRes.ErrorUnsupported;

                    tempSizes[2] = (int)s3Size;
                    if (tempSizes[2] != (int)s3Size)
                        return SzRes.ErrorMem;
                    tempBuf[2] = SzAllocImpl.Alloc(allocMain, tempSizes[2]);
                    if (tempBuf[2] == null && tempSizes[2] != 0)
                        return SzRes.ErrorMem;

                    res = SzLookInStream.SeekTo(inStream, startPos + offset);
                    if (res != SzRes.Ok)
                        return res;
                    res = DecodeCopy(s3Size, inStream, tempBuf[2], 0);
                    if (res != SzRes.Ok)
                        return res;

                    if ((tempSizes[0] & 3) != 0 || (tempSizes[1] & 3) != 0 ||
                        tempSize3 + tempSizes[0] + tempSizes[1] != outSize)
                        return SzRes.ErrorData;

                    CBcj2Dec bcj = new CBcj2Dec();
                    bcj.Bufs[Bcj2.StreamMain] = tempBuf[3];
                    bcj.BufPos[Bcj2.StreamMain] = 0;
                    bcj.LimPos[Bcj2.StreamMain] = tempSize3;
                    bcj.Bufs[Bcj2.StreamCall] = tempBuf[0];
                    bcj.BufPos[Bcj2.StreamCall] = 0;
                    bcj.LimPos[Bcj2.StreamCall] = tempSizes[0];
                    bcj.Bufs[Bcj2.StreamJump] = tempBuf[1];
                    bcj.BufPos[Bcj2.StreamJump] = 0;
                    bcj.LimPos[Bcj2.StreamJump] = tempSizes[1];
                    bcj.Bufs[Bcj2.StreamRc] = tempBuf[2];
                    bcj.BufPos[Bcj2.StreamRc] = 0;
                    bcj.LimPos[Bcj2.StreamRc] = tempSizes[2];
                    bcj.Dest = outBuffer;
                    bcj.DestPos = 0;
                    bcj.DestLim = outSize;
                    Bcj2.DecInit(bcj);
                    res = Bcj2.DecDecode(bcj);
                    if (res != SzRes.Ok)
                        return res;

                    for (int i = 0; i < 4; i++)
                    {
                        if (bcj.BufPos[i] != bcj.LimPos[i])
                            return SzRes.ErrorData;
                    }

                    if (bcj.DestPos != bcj.DestLim || !Bcj2.IsMaybeFinished(bcj))
                        return SzRes.ErrorData;
                }
                else if (ci == 1)
                {
                    if (coder.MethodId == KDelta)
                    {
                        if (coder.PropsSize != 1)
                            return SzRes.ErrorUnsupported;
                        byte[] state = new byte[Delta.StateSize];
                        Delta.Init(state);
                        Delta.Decode(state, (uint)propsData[coderPropsOffset] + 1, outBuffer, outSize);
                        continue;
                    }

                    if (coder.MethodId == KArm64)
                    {
                        uint pc = 0;
                        if (coder.PropsSize == 4)
                        {
                            pc = CpuArch.GetUi32(propsData, coderPropsOffset);
                            if ((pc & 3) != 0)
                                return SzRes.ErrorUnsupported;
                        }
                        else if (coder.PropsSize != 0)
                        {
                            return SzRes.ErrorUnsupported;
                        }

                        Bra.BranchConvArm64Dec(outBuffer, 0, outSize, pc);
                        continue;
                    }

                    if (coder.MethodId == KRiscv)
                    {
                        uint pc = 0;
                        if (coder.PropsSize == 4)
                        {
                            pc = CpuArch.GetUi32(propsData, coderPropsOffset);
                            if ((pc & 1) != 0)
                                return SzRes.ErrorUnsupported;
                        }
                        else if (coder.PropsSize != 0)
                        {
                            return SzRes.ErrorUnsupported;
                        }

                        Bra.BranchConvRiscvDec(outBuffer, 0, outSize, pc);
                        continue;
                    }

                    if (coder.PropsSize != 0)
                        return SzRes.ErrorUnsupported;

                    switch (coder.MethodId)
                    {
                        case KBcj:
                        {
                            uint state = 0;
                            Bra86.BranchConvStX86Dec(outBuffer, 0, outSize, 0, ref state);
                            break;
                        }
                        case KPpc:
                            Bra.BranchConvPpcDec(outBuffer, 0, outSize, 0);
                            break;
                        case KIa64:
                            Bra.BranchConvIa64Dec(outBuffer, 0, outSize, 0);
                            break;
                        case KSparc:
                            Bra.BranchConvSparcDec(outBuffer, 0, outSize, 0);
                            break;
                        case KArm:
                            Bra.BranchConvArmDec(outBuffer, 0, outSize, 0);
                            break;
                        case KArmt:
                            Bra.BranchConvArmtDec(outBuffer, 0, outSize, 0);
                            break;
                        default:
                            return SzRes.ErrorUnsupported;
                    }
                }
                else
                {
                    return SzRes.ErrorUnsupported;
                }
            }

            return SzRes.Ok;
        }

        public static int SzAr_DecodeFolder(CSzAr p, uint folderIndex, ILookInStream inStream, ulong startPos,
            byte[] outBuffer, int outSize, ISzAlloc allocMain)
        {
            CSzFolder folder = new CSzFolder();
            int dataOffset = p.FoCodersOffsets[folderIndex];
            int dataSize = p.FoCodersOffsets[folderIndex + 1] - dataOffset;
            CSzData sd = new CSzData
            {
                Data = p.CodersData,
                Offset = dataOffset,
                Size = dataSize
            };

            int res = Sz7zArcIn.GetNextFolderItem(folder, sd);
            if (res != SzRes.Ok)
                return res;

            if (sd.Size != 0 ||
                folder.UnpackStream != p.FoToMainUnpackSizeIndex[folderIndex] ||
                outSize != (int)Sz7zArcIn.GetFolderUnpackSize(p, folderIndex))
                return SzRes.ErrorFail;

            byte[][] tempBuf = new byte[4][];
            try
            {
                res = FolderDecode2(folder, p.CodersData, dataOffset,
                    p.CoderUnpackSizes, (int)p.FoToCoderUnpackSizes[folderIndex],
                    p.PackPositions, (int)p.FoStartPackStreamIndex[folderIndex],
                    inStream, startPos, outBuffer, outSize, allocMain, tempBuf);

                if (res == SzRes.Ok && Sz7zBitArray.WithValsCheck(p.FolderCrcs, folderIndex))
                {
                    if (SzCrc.CrcCalc(outBuffer, outSize) != p.FolderCrcs.Vals[folderIndex])
                        res = SzRes.ErrorCrc;
                }
            }
            finally
            {
                for (int i = 0; i < tempBuf.Length; i++)
                    SzAllocImpl.Free(allocMain, tempBuf[i]);
            }

            return res;
        }
    }
}
