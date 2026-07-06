namespace SmartGoldbergEmu.ExtractKit.Internal
{
    internal static class Sz7zArcIn
    {
        private const int KNumCodersStreamsInFolderMax =
            Sz7zConstants.SzNumBondsInFolderMax + Sz7zConstants.SzNumPackStreamsInFolderMax;

        private const int KScanNumCodersMax = 64;
        private const int KScanNumCodersStreamsInFolderMax = 64;
        private const int NumAdditionalStreamsMax = 8;

        private enum EIdEnum
        {
            K7zIdEnd,
            K7zIdHeader,
            K7zIdArchiveProperties,
            K7zIdAdditionalStreamsInfo,
            K7zIdMainStreamsInfo,
            K7zIdFilesInfo,
            K7zIdPackInfo,
            K7zIdUnpackInfo,
            K7zIdSubStreamsInfo,
            K7zIdSize,
            K7zIdCrc,
            K7zIdFolder,
            K7zIdCodersUnpackSize,
            K7zIdNumUnpackStream,
            K7zIdEmptyStream,
            K7zIdEmptyFile,
            K7zIdAnti,
            K7zIdName,
            K7zIdCTime,
            K7zIdATime,
            K7zIdMTime,
            K7zIdWinAttrib,
            K7zIdComment,
            K7zIdEncodedHeader,
            K7zIdStartPos,
            K7zIdDummy
        }

        private static void SzBitUi32sInit(CSzBitUi32s p)
        {
            p.Defs = null;
            p.Vals = null;
        }

        private static int SzBitUi32sAlloc(CSzBitUi32s p, int num, ISzAlloc alloc)
        {
            if (num == 0)
            {
                p.Defs = null;
                p.Vals = null;
            }
            else
            {
                int res = AllocBytes(ref p.Defs, (num + 7) >> 3, alloc);
                if (res != SzRes.Ok)
                    return res;
                res = AllocUInt32(ref p.Vals, num, alloc);
                if (res != SzRes.Ok)
                    return res;
            }

            return SzRes.Ok;
        }

        private static void SzBitUi32sFree(CSzBitUi32s p, ISzAlloc alloc)
        {
            SzAllocImpl.Free(alloc, p.Defs);
            p.Defs = null;
            p.Vals = null;
        }

        private static void SzBitUi64sInit(CSzBitUi64s p)
        {
            p.Defs = null;
            p.Vals = null;
        }

        private static void SzBitUi64sFree(CSzBitUi64s p, ISzAlloc alloc)
        {
            SzAllocImpl.Free(alloc, p.Defs);
            p.Defs = null;
            p.Vals = null;
        }

        private static void SzArInit(CSzAr p)
        {
            p.NumPackStreams = 0;
            p.NumFolders = 0;

            p.PackPositions = null;
            SzBitUi32sInit(p.FolderCrcs);

            p.FoCodersOffsets = null;
            p.FoStartPackStreamIndex = null;
            p.FoToCoderUnpackSizes = null;
            p.FoToMainUnpackSizeIndex = null;
            p.CoderUnpackSizes = null;

            p.CodersData = null;

            p.RangeLimit = 0;
        }

        private static void SzArFree(CSzAr p, ISzAlloc alloc)
        {
            p.PackPositions = null;
            SzBitUi32sFree(p.FolderCrcs, alloc);
            p.FoCodersOffsets = null;
            p.FoStartPackStreamIndex = null;
            p.FoToCoderUnpackSizes = null;
            p.FoToMainUnpackSizeIndex = null;
            p.CoderUnpackSizes = null;
            SzAllocImpl.Free(alloc, p.CodersData);
            p.CodersData = null;

            SzArInit(p);
        }

        public static void SzArEx_Init(CSzArEx p)
        {
            SzArInit(p.Db);

            p.NumFiles = 0;
            p.DataPos = 0;

            p.UnpackPositions = null;
            p.IsDirs = null;

            p.FolderToFile = null;
            p.FileToFolder = null;

            p.FileNameOffsets = null;
            p.FileNames = null;

            SzBitUi32sInit(p.Crcs);
            SzBitUi32sInit(p.Attribs);
            SzBitUi64sInit(p.MTime);
            SzBitUi64sInit(p.CTime);
        }

        public static void SzArEx_Free(CSzArEx p, ISzAlloc alloc)
        {
            p.UnpackPositions = null;
            SzAllocImpl.Free(alloc, p.IsDirs);
            p.IsDirs = null;

            p.FolderToFile = null;
            p.FileToFolder = null;

            p.FileNameOffsets = null;
            SzAllocImpl.Free(alloc, p.FileNames);
            p.FileNames = null;

            SzBitUi32sFree(p.Crcs, alloc);
            SzBitUi32sFree(p.Attribs, alloc);
            SzBitUi64sFree(p.MTime, alloc);
            SzBitUi64sFree(p.CTime, alloc);

            SzArFree(p.Db, alloc);
            SzArEx_Init(p);
        }

        private static int ReadByteSd(CSzData sd, out byte dest)
        {
            if (sd.Size == 0)
            {
                dest = 0;
                return SzRes.ErrorArchive;
            }

            dest = sd.Data[sd.Offset++];
            sd.Size--;
            return SzRes.Ok;
        }

        private static int ReadByteSdNoCheck(CSzData sd, out byte dest)
        {
            sd.Size--;
            dest = sd.Data[sd.Offset++];
            return SzRes.Ok;
        }

        private static void SkipData(CSzData sd, int size)
        {
            sd.Size -= size;
            sd.Offset += size;
        }

        private static int ReadNumber(CSzData sd, out ulong value)
        {
            byte firstByte;
            int res = ReadByteSd(sd, out firstByte);
            if (res != SzRes.Ok)
            {
                value = 0;
                return res;
            }

            if ((firstByte & 0x80) == 0)
            {
                value = firstByte;
                return SzRes.Ok;
            }

            byte v;
            res = ReadByteSd(sd, out v);
            if (res != SzRes.Ok)
            {
                value = 0;
                return res;
            }

            if ((firstByte & 0x40) == 0)
            {
                value = (((uint)firstByte & 0x3Fu) << 8) | v;
                return SzRes.Ok;
            }

            byte mask;
            res = ReadByteSd(sd, out mask);
            if (res != SzRes.Ok)
            {
                value = 0;
                return res;
            }

            value = v | ((ulong)mask << 8);
            mask = 0x20;
            for (int i = 2 * 8; i < 8 * 8; i += 8)
            {
                if ((firstByte & mask) == 0)
                {
                    ulong highPart = (uint)firstByte & (uint)(mask - 1);
                    value |= highPart << i;
                    return SzRes.Ok;
                }

                mask >>= 1;
                byte b;
                res = ReadByteSd(sd, out b);
                if (res != SzRes.Ok)
                {
                    value = 0;
                    return res;
                }

                value |= (ulong)b << i;
            }

            return SzRes.Ok;
        }

        private static int SzReadNumber32(CSzData sd, out uint value)
        {
            value = 0;
            if (sd.Size == 0)
                return SzRes.ErrorArchive;

            byte firstByte = sd.Data[sd.Offset];
            if ((firstByte & 0x80) == 0)
            {
                value = firstByte;
                sd.Offset++;
                sd.Size--;
                return SzRes.Ok;
            }

            ulong value64;
            int res = ReadNumber(sd, out value64);
            if (res != SzRes.Ok)
                return res;

            if (value64 >= 0x80000000u - 1)
                return SzRes.ErrorUnsupported;
            if (value64 >= (1UL << ((System.IntPtr.Size - 1) * 8 + 4)))
                return SzRes.ErrorUnsupported;

            value = (uint)value64;
            return SzRes.Ok;
        }

        private static int ReadId(CSzData sd, out ulong value)
        {
            return ReadNumber(sd, out value);
        }

        private static int SkipDataBlock(CSzData sd)
        {
            ulong size;
            int res = ReadNumber(sd, out size);
            if (res != SzRes.Ok)
                return res;
            if (size > (ulong)sd.Size)
                return SzRes.ErrorArchive;
            SkipData(sd, (int)size);
            return SzRes.Ok;
        }

        private static int WaitId(CSzData sd, uint id)
        {
            for (; ; )
            {
                ulong type;
                int res = ReadId(sd, out type);
                if (res != SzRes.Ok)
                    return res;
                if (type == id)
                    return SzRes.Ok;
                if (type == (uint)EIdEnum.K7zIdEnd)
                    return SzRes.ErrorArchive;
                res = SkipDataBlock(sd);
                if (res != SzRes.Ok)
                    return res;
            }
        }

        private static uint CountDefinedBits(byte[] bits, int bitsOffset, uint numItems)
        {
            uint b = 0;
            int m = 0;
            uint sum = 0;
            int bitIndex = bitsOffset;
            for (; numItems != 0; numItems--)
            {
                if (m == 0)
                {
                    b = bits[bitIndex++];
                    m = 8;
                }

                m--;
                sum += (uint)((b >> m) & 1);
            }

            return sum;
        }

        private static int ReadBitVector(CSzData sd, int numItems, ref byte[] v, ISzAlloc alloc)
        {
            byte allAreDefined;
            int res = ReadByteSd(sd, out allAreDefined);
            if (res != SzRes.Ok)
                return res;

            int numBytes = (numItems + 7) >> 3;
            v = null;
            if (numBytes == 0)
                return SzRes.Ok;

            if (allAreDefined == 0)
            {
                if (numBytes > sd.Size)
                    return SzRes.ErrorArchive;
                res = AllocAndCopy(ref v, numBytes, sd.Data, sd.Offset, alloc);
                if (res != SzRes.Ok)
                    return res;
                SkipData(sd, numBytes);
                return SzRes.Ok;
            }

            res = AllocBytes(ref v, numBytes, alloc);
            if (res != SzRes.Ok)
                return res;

            for (int j = 0; j < numBytes; j++)
                v[j] = 0xFF;

            int numBits = numItems & 7;
            if (numBits != 0)
                v[numBytes - 1] = (byte)(0xff00 >> numBits);

            return SzRes.Ok;
        }

        private static int ReadUi32s(CSzData sd2, int numItems, CSzBitUi32s crcs, ISzAlloc alloc)
        {
            int data = sd2.Offset;
            int size = sd2.Size;
            uint[] vals = null;
            int res = AllocUInt32Ze(ref vals, numItems, alloc);
            if (res != SzRes.Ok)
                return res;

            crcs.Vals = vals;
            byte[] defs = crcs.Defs;
            for (int i = 0; i < numItems; i++)
            {
                if (Sz7zBitArray.Check(defs, (uint)i))
                {
                    if (size < 4)
                        return SzRes.ErrorArchive;
                    size -= 4;
                    vals[i] = CpuArch.GetUi32(sd2.Data, data);
                    data += 4;
                }
                else
                {
                    vals[i] = 0;
                }
            }

            sd2.Offset = data;
            sd2.Size = size;
            return SzRes.Ok;
        }

        private static int ReadBitUi32s(CSzData sd, int numItems, CSzBitUi32s crcs, ISzAlloc alloc)
        {
            if (crcs.Defs != null)
                return SzRes.ErrorArchive;
            int res = ReadBitVector(sd, numItems, ref crcs.Defs, alloc);
            if (res != SzRes.Ok)
                return res;
            return ReadUi32s(sd, numItems, crcs, alloc);
        }

        private static int SkipBitUi32s(CSzData sd, uint numItems)
        {
            byte allAreDefined;
            int res = ReadByteSd(sd, out allAreDefined);
            if (res != SzRes.Ok)
                return res;

            uint numDefined = numItems;
            if (allAreDefined == 0)
            {
                int numBytes = (int)((numItems + 7) >> 3);
                if (numBytes > sd.Size)
                    return SzRes.ErrorArchive;
                numDefined = CountDefinedBits(sd.Data, sd.Offset, numItems);
                SkipData(sd, numBytes);
            }

            if (numDefined > (uint)(sd.Size >> 2))
                return SzRes.ErrorArchive;
            SkipData(sd, (int)numDefined * 4);
            return SzRes.Ok;
        }

        private static int ReadPackInfo(CSzAr p, CSzData sd, ISzAlloc alloc)
        {
            int res = SzReadNumber32(sd, out p.NumPackStreams);
            if (res != SzRes.Ok)
                return res;

            res = WaitId(sd, (uint)EIdEnum.K7zIdSize);
            if (res != SzRes.Ok)
                return res;

            ulong sum = 0;
            int num = (int)p.NumPackStreams + 1;
            res = AllocUInt64(ref p.PackPositions, num, alloc);
            if (res != SzRes.Ok)
                return res;

            int packIndex = 0;
            for (; ; )
            {
                p.PackPositions[packIndex++] = sum;
                if (--num == 0)
                    break;

                ulong packSize;
                res = ReadNumber(sd, out packSize);
                if (res != SzRes.Ok)
                    return res;
                sum += packSize;
                if (sum < packSize)
                    return SzRes.ErrorArchive;
            }

            for (; ; )
            {
                ulong type;
                res = ReadId(sd, out type);
                if (res != SzRes.Ok)
                    return res;
                if (type == (uint)EIdEnum.K7zIdEnd)
                    return SzRes.Ok;
                if (type == (uint)EIdEnum.K7zIdCrc)
                {
                    res = SkipBitUi32s(sd, p.NumPackStreams);
                    if (res != SzRes.Ok)
                        return res;
                    continue;
                }

                res = SkipDataBlock(sd);
                if (res != SzRes.Ok)
                    return res;
            }
        }

        public static int SzGetNextFolderItem(CSzFolder f, CSzData sd)
        {
            int dataStart = sd.Offset;
            uint numCoders;
            int res = SzReadNumber32(sd, out numCoders);
            if (res != SzRes.Ok)
                return res;

            f.UnpackStream = 0;

            if (numCoders == 0 || numCoders > Sz7zConstants.SzNumCodersInFolderMax)
                return SzRes.ErrorUnsupported;

            f.NumCoders = numCoders;
            uint numInStreams = 0;

            for (uint i = 0; i < numCoders; i++)
            {
                byte mainByte;
                if (sd.Size == 0)
                    return SzRes.ErrorArchive;

                sd.Size--;
                int data = sd.Offset;
                mainByte = sd.Data[data++];
                sd.Offset = data;

                if ((mainByte & 0xC0) != 0)
                    return SzRes.ErrorUnsupported;

                int idSize = mainByte & 0xF;
                if (idSize > 8)
                    return SzRes.ErrorUnsupported;
                if (idSize > sd.Size)
                    return SzRes.ErrorArchive;

                sd.Size -= idSize;
                ulong id = 0;
                for (int j = 0; j < idSize; j++)
                    id = (id << 8) | sd.Data[data++];

                sd.Offset = data;
                if (id > 0xFFFFFFFFu)
                    return SzRes.ErrorUnsupported;

                CSzCoderInfo coder = f.Coders[i];
                coder.MethodId = (uint)id;
                coder.NumStreams = 1;
                coder.PropsOffset = 0;
                coder.PropsSize = 0;

                if ((mainByte & 0x10) != 0)
                {
                    uint numStreams;
                    res = SzReadNumber32(sd, out numStreams);
                    if (res != SzRes.Ok)
                        return res;
                    if (numStreams > KNumCodersStreamsInFolderMax)
                        return SzRes.ErrorUnsupported;
                    coder.NumStreams = (byte)numStreams;
                    res = SzReadNumber32(sd, out numStreams);
                    if (res != SzRes.Ok)
                        return res;
                    if (numStreams != 1)
                        return SzRes.ErrorUnsupported;
                }

                numInStreams += coder.NumStreams;
                if (numInStreams > KNumCodersStreamsInFolderMax)
                    return SzRes.ErrorUnsupported;

                if ((mainByte & 0x20) != 0)
                {
                    uint propsSize;
                    res = SzReadNumber32(sd, out propsSize);
                    if (res != SzRes.Ok)
                        return res;
                    if (propsSize > (uint)sd.Size)
                        return SzRes.ErrorArchive;
                    if (propsSize >= 0x80)
                        return SzRes.ErrorUnsupported;
                    coder.PropsOffset = sd.Offset - dataStart;
                    coder.PropsSize = (byte)propsSize;
                    sd.Offset += (int)propsSize;
                    sd.Size -= (int)propsSize;
                }
            }

            byte[] streamUsed = new byte[KNumCodersStreamsInFolderMax];
            uint numBonds = numCoders - 1;
            if (numInStreams < numBonds)
                return SzRes.ErrorArchive;
            if (numBonds > Sz7zConstants.SzNumBondsInFolderMax)
                return SzRes.ErrorUnsupported;

            f.NumBonds = numBonds;

            uint numPackStreams = numInStreams - numBonds;
            if (numPackStreams > Sz7zConstants.SzNumPackStreamsInFolderMax)
                return SzRes.ErrorUnsupported;

            f.NumPackStreams = numPackStreams;

            for (int i = 0; i < streamUsed.Length; i++)
                streamUsed[i] = 0;

            if (numBonds != 0)
            {
                byte[] coderUsed = new byte[Sz7zConstants.SzNumCodersInFolderMax];
                for (int i = 0; i < coderUsed.Length; i++)
                    coderUsed[i] = 0;

                for (uint i = 0; i < numBonds; i++)
                {
                    CSzBond bp = f.Bonds[i];
                    res = SzReadNumber32(sd, out bp.InIndex);
                    if (res != SzRes.Ok)
                        return res;
                    if (bp.InIndex >= numInStreams || streamUsed[bp.InIndex] != 0)
                        return SzRes.ErrorArchive;
                    streamUsed[bp.InIndex] = 1;

                    res = SzReadNumber32(sd, out bp.OutIndex);
                    if (res != SzRes.Ok)
                        return res;
                    if (bp.OutIndex >= numCoders || coderUsed[bp.OutIndex] != 0)
                        return SzRes.ErrorArchive;
                    coderUsed[bp.OutIndex] = 1;
                }

                uint ci = 0;
                for (; ; )
                {
                    if (coderUsed[ci] == 0)
                        break;
                    if (++ci == numCoders)
                        return SzRes.ErrorArchive;
                }

                f.UnpackStream = ci;
            }

            if (numPackStreams == 1)
            {
                uint i = 0;
                for (; ; i++)
                {
                    if (i == numInStreams)
                        return SzRes.ErrorArchive;
                    if (streamUsed[i] == 0)
                        break;
                }

                f.PackStreams[0] = i;
            }
            else
            {
                for (uint i = 0; i < numPackStreams; i++)
                {
                    uint index;
                    res = SzReadNumber32(sd, out index);
                    if (res != SzRes.Ok)
                        return res;
                    if (index >= numInStreams || streamUsed[index] != 0)
                        return SzRes.ErrorArchive;
                    streamUsed[index] = 1;
                    f.PackStreams[i] = index;
                }
            }

            return SzRes.Ok;
        }

        public static int GetNextFolderItem(CSzFolder f, CSzData sd)
        {
            return SzGetNextFolderItem(f, sd);
        }

        private static int SkipNumbers(CSzData sd2, uint num)
        {
            int data = sd2.Offset;
            int size = sd2.Size;
            for (; num != 0; num--)
            {
                if (size == 0)
                    return SzRes.ErrorArchive;

                byte firstByte = sd2.Data[data];
                int i = 1;
                byte fb = firstByte;
                for (; (fb & 0x80) != 0; i++)
                    fb <<= 1;
                if (size < i)
                    return SzRes.ErrorArchive;
                size -= i;
                data += i;
            }

            sd2.Offset = data;
            sd2.Size = size;
            return SzRes.Ok;
        }

        private static int ReadUnpackInfo(
            CSzAr p,
            CSzData sd2,
            uint numFoldersMax,
            CBuf[] tempBufs,
            uint numTempBufs,
            ISzAlloc alloc)
        {
            int res = WaitId(sd2, (uint)EIdEnum.K7zIdFolder);
            if (res != SzRes.Ok)
                return res;

            uint numFolders;
            res = SzReadNumber32(sd2, out numFolders);
            if (res != SzRes.Ok)
                return res;
            if (numFolders > numFoldersMax)
                return SzRes.ErrorUnsupported;

            p.NumFolders = numFolders;

            byte external;
            res = ReadByteSd(sd2, out external);
            if (res != SzRes.Ok)
                return res;

            CSzData sd;
            if (external == 0)
            {
                sd = sd2.Copy();
            }
            else
            {
                uint index;
                res = SzReadNumber32(sd2, out index);
                if (res != SzRes.Ok)
                    return res;
                if (index >= numTempBufs)
                    return SzRes.ErrorArchive;
                sd = new CSzData
                {
                    Data = tempBufs[index].Data,
                    Offset = 0,
                    Size = tempBufs[index].Size
                };
            }

            res = AllocInt32(ref p.FoCodersOffsets, (int)numFolders + 1, alloc);
            if (res != SzRes.Ok)
                return res;
            res = AllocUInt32(ref p.FoStartPackStreamIndex, (int)numFolders + 1, alloc);
            if (res != SzRes.Ok)
                return res;
            res = AllocUInt32(ref p.FoToCoderUnpackSizes, (int)numFolders + 1, alloc);
            if (res != SzRes.Ok)
                return res;
            res = AllocBytesZe(ref p.FoToMainUnpackSizeIndex, (int)numFolders, alloc);
            if (res != SzRes.Ok)
                return res;

            int startBufPtr = sd.Offset;
            uint packStreamIndex = 0;
            uint numCodersOutStreams = 0;

            for (uint fo = 0; fo < numFolders; fo++)
            {
                p.FoCodersOffsets[fo] = sd.Offset - startBufPtr;

                uint numCoders;
                res = SzReadNumber32(sd, out numCoders);
                if (res != SzRes.Ok)
                    return res;
                if (numCoders == 0 || numCoders > KScanNumCodersMax)
                    return SzRes.ErrorUnsupported;

                uint numInStreams = 0;
                for (uint ci = 0; ci < numCoders; ci++)
                {
                    if (sd.Size == 0)
                        return SzRes.ErrorArchive;

                    byte mainByte = sd.Data[sd.Offset++];
                    sd.Size--;

                    if ((mainByte & 0xC0) != 0)
                        return SzRes.ErrorUnsupported;

                    int idSize = mainByte & 0xF;
                    if (idSize > 8)
                        return SzRes.ErrorUnsupported;
                    if (idSize > sd.Size)
                        return SzRes.ErrorArchive;
                    SkipData(sd, idSize);

                    uint coderInStreams = 1;
                    if ((mainByte & 0x10) != 0)
                    {
                        uint coderOutStreams;
                        res = SzReadNumber32(sd, out coderInStreams);
                        if (res != SzRes.Ok)
                            return res;
                        res = SzReadNumber32(sd, out coderOutStreams);
                        if (res != SzRes.Ok)
                            return res;
                        if (coderInStreams > KScanNumCodersStreamsInFolderMax || coderOutStreams != 1)
                            return SzRes.ErrorUnsupported;
                    }

                    numInStreams += coderInStreams;

                    if ((mainByte & 0x20) != 0)
                    {
                        uint propsSize;
                        res = SzReadNumber32(sd, out propsSize);
                        if (res != SzRes.Ok)
                            return res;
                        if (propsSize > (uint)sd.Size)
                            return SzRes.ErrorArchive;
                        SkipData(sd, (int)propsSize);
                    }
                }

                uint indexOfMainStream = 0;
                uint numPackStreams = 1;

                if (numCoders != 1 || numInStreams != 1)
                {
                    byte[] streamUsed = new byte[KScanNumCodersStreamsInFolderMax];
                    byte[] coderUsed = new byte[KScanNumCodersMax];
                    uint numBonds = numCoders - 1;
                    if (numInStreams < numBonds)
                        return SzRes.ErrorArchive;
                    if (numInStreams > KScanNumCodersStreamsInFolderMax)
                        return SzRes.ErrorUnsupported;

                    for (uint i = 0; i < numInStreams; i++)
                        streamUsed[i] = 0;
                    for (uint i = 0; i < numCoders; i++)
                        coderUsed[i] = 0;

                    for (uint i = 0; i < numBonds; i++)
                    {
                        uint index;
                        res = SzReadNumber32(sd, out index);
                        if (res != SzRes.Ok)
                            return res;
                        if (index >= numInStreams || streamUsed[index] != 0)
                            return SzRes.ErrorArchive;
                        streamUsed[index] = 1;

                        res = SzReadNumber32(sd, out index);
                        if (res != SzRes.Ok)
                            return res;
                        if (index >= numCoders || coderUsed[index] != 0)
                            return SzRes.ErrorArchive;
                        coderUsed[index] = 1;
                    }

                    numPackStreams = numInStreams - numBonds;

                    if (numPackStreams != 1)
                    {
                        for (uint i = 0; i < numPackStreams; i++)
                        {
                            uint index;
                            res = SzReadNumber32(sd, out index);
                            if (res != SzRes.Ok)
                                return res;
                            if (index >= numInStreams || streamUsed[index] != 0)
                                return SzRes.ErrorArchive;
                            streamUsed[index] = 1;
                        }
                    }

                    uint i2 = 0;
                    for (; ; )
                    {
                        if (coderUsed[i2] == 0)
                            break;
                        if (++i2 == numCoders)
                            return SzRes.ErrorArchive;
                    }

                    indexOfMainStream = i2;
                }

                p.FoStartPackStreamIndex[fo] = packStreamIndex;
                p.FoToCoderUnpackSizes[fo] = numCodersOutStreams;
                p.FoToMainUnpackSizeIndex[fo] = (byte)indexOfMainStream;
                numCodersOutStreams += numCoders;
                if (numCodersOutStreams < numCoders)
                    return SzRes.ErrorUnsupported;
                if (numPackStreams > p.NumPackStreams - packStreamIndex)
                    return SzRes.ErrorArchive;
                packStreamIndex += numPackStreams;
            }

            int kNumCodersOutStreamsLimit = 1 << (System.IntPtr.Size * 8 - 4);
            if (numCodersOutStreams >= (uint)kNumCodersOutStreamsLimit)
                return SzRes.ErrorUnsupported;

            p.FoToCoderUnpackSizes[numFolders] = numCodersOutStreams;
            p.FoStartPackStreamIndex[numFolders] = packStreamIndex;

            int dataSize = sd.Offset - startBufPtr;
            p.FoCodersOffsets[numFolders] = dataSize;
            res = AllocZeAndCopy(ref p.CodersData, dataSize, sd.Data, startBufPtr, alloc);
            if (res != SzRes.Ok)
                return res;

            if (external != 0)
            {
                if (sd.Size != 0)
                    return SzRes.ErrorArchive;
                sd.Assign(sd2);
            }

            res = WaitId(sd, (uint)EIdEnum.K7zIdCodersUnpackSize);
            if (res != SzRes.Ok)
                return res;

            res = AllocUInt64Ze(ref p.CoderUnpackSizes, (int)numCodersOutStreams, alloc);
            if (res != SzRes.Ok)
                return res;

            if (numCodersOutStreams != 0)
            {
                int sizesIndex = 0;
                uint remaining = numCodersOutStreams;
                do
                {
                    ulong sizeVal;
                    res = ReadNumber(sd, out sizeVal);
                    if (res != SzRes.Ok)
                        return res;
                    p.CoderUnpackSizes[sizesIndex++] = sizeVal;
                }
                while (--remaining != 0);
            }

            for (; ; )
            {
                ulong type;
                res = ReadId(sd, out type);
                if (res != SzRes.Ok)
                    return res;
                if (type == (uint)EIdEnum.K7zIdEnd)
                    break;
                if (type == (uint)EIdEnum.K7zIdCrc)
                {
                    res = ReadBitUi32s(sd, (int)numFolders, p.FolderCrcs, alloc);
                    if (res != SzRes.Ok)
                        return res;
                    continue;
                }

                res = SkipDataBlock(sd);
                if (res != SzRes.Ok)
                    return res;
            }

            sd2.Assign(sd);
            return SzRes.Ok;
        }

        public static ulong SzAr_GetFolderUnpackSize(CSzAr p, uint folderIndex)
        {
            return p.CoderUnpackSizes[p.FoToCoderUnpackSizes[folderIndex] + p.FoToMainUnpackSizeIndex[folderIndex]];
        }

        public static ulong GetFolderUnpackSize(CSzAr p, uint folderIndex)
        {
            return SzAr_GetFolderUnpackSize(p, folderIndex);
        }

        private sealed class CSubStreamInfo
        {
            public uint NumTotalSubStreams;
            public uint NumSubDigests;
            public CSzData SdNumSubStreams = new CSzData();
            public CSzData SdSizes = new CSzData();
            public CSzData SdCrcs = new CSzData();
        }

        private static int ReadSubStreamsInfo(CSzAr p, CSzData sd, CSubStreamInfo ssi)
        {
            ulong type = 0;
            uint numFolders = p.NumFolders;
            uint numUnpackStreams = numFolders;
            uint numSubDigests = numFolders;
            uint numUnpackSizesInData = 0;

            for (; ; )
            {
                int res = ReadId(sd, out type);
                if (res != SzRes.Ok)
                    return res;

                if (type == (uint)EIdEnum.K7zIdNumUnpackStream)
                {
                    if (ssi.SdNumSubStreams.Data != null)
                        return SzRes.ErrorUnsupported;

                    ssi.SdNumSubStreams.Data = sd.Data;
                    ssi.SdNumSubStreams.Offset = sd.Offset;
                    numUnpackStreams = 0;
                    numSubDigests = 0;

                    for (uint i = 0; i < numFolders; i++)
                    {
                        uint numStreams;
                        res = SzReadNumber32(sd, out numStreams);
                        if (res != SzRes.Ok)
                            return res;
                        numUnpackStreams += numStreams;
                        if (numUnpackStreams < numStreams)
                            return SzRes.ErrorUnsupported;
                        if (numStreams != 0)
                            numUnpackSizesInData += numStreams - 1;
                        if (numStreams != 1 || !Sz7zBitArray.WithValsCheck(p.FolderCrcs, i))
                            numSubDigests += numStreams;
                    }

                    ssi.SdNumSubStreams.Size = sd.Offset - ssi.SdNumSubStreams.Offset;
                    continue;
                }

                if (type == (uint)EIdEnum.K7zIdCrc || type == (uint)EIdEnum.K7zIdSize || type == (uint)EIdEnum.K7zIdEnd)
                    break;

                res = SkipDataBlock(sd);
                if (res != SzRes.Ok)
                    return res;
            }

            if (ssi.SdNumSubStreams.Data == null && p.FolderCrcs.Defs != null)
                numSubDigests = numFolders - CountDefinedBits(p.FolderCrcs.Defs, 0, numFolders);

            ssi.NumTotalSubStreams = numUnpackStreams;
            ssi.NumSubDigests = numSubDigests;

            if (type == (uint)EIdEnum.K7zIdSize)
            {
                ssi.SdSizes.Data = sd.Data;
                ssi.SdSizes.Offset = sd.Offset;
                int res = SkipNumbers(sd, numUnpackSizesInData);
                if (res != SzRes.Ok)
                    return res;
                ssi.SdSizes.Size = sd.Offset - ssi.SdSizes.Offset;
                res = ReadId(sd, out type);
                if (res != SzRes.Ok)
                    return res;
            }

            for (; ; )
            {
                if (type == (uint)EIdEnum.K7zIdEnd)
                    return SzRes.Ok;
                if (type == (uint)EIdEnum.K7zIdCrc)
                {
                    if (ssi.SdCrcs.Data != null)
                        return SzRes.ErrorUnsupported;
                    ssi.SdCrcs.Data = sd.Data;
                    ssi.SdCrcs.Offset = sd.Offset;
                    int res = SkipBitUi32s(sd, numSubDigests);
                    if (res != SzRes.Ok)
                        return res;
                    ssi.SdCrcs.Size = sd.Offset - ssi.SdCrcs.Offset;
                }
                else
                {
                    int res = SkipDataBlock(sd);
                    if (res != SzRes.Ok)
                        return res;
                }

                int res2 = ReadId(sd, out type);
                if (res2 != SzRes.Ok)
                    return res2;
            }
        }

        private static int SzReadStreamsInfo(
            CSzAr p,
            CSzData sd,
            uint numFoldersMax,
            CBuf[] tempBufs,
            uint numTempBufs,
            out ulong dataOffset,
            CSubStreamInfo ssi,
            ISzAlloc alloc)
        {
            ssi.SdSizes.Clear();
            ssi.SdCrcs.Clear();
            ssi.SdNumSubStreams.Clear();

            dataOffset = 0;
            ulong type;
            int res = ReadId(sd, out type);
            if (res != SzRes.Ok)
                return res;

            if (type == (uint)EIdEnum.K7zIdPackInfo)
            {
                res = ReadNumber(sd, out dataOffset);
                if (res != SzRes.Ok)
                    return res;
                if (dataOffset > p.RangeLimit)
                    return SzRes.ErrorArchive;
                res = ReadPackInfo(p, sd, alloc);
                if (res != SzRes.Ok)
                    return res;
                if (p.PackPositions[p.NumPackStreams] > p.RangeLimit - dataOffset)
                    return SzRes.ErrorArchive;
                res = ReadId(sd, out type);
                if (res != SzRes.Ok)
                    return res;
            }

            if (type == (uint)EIdEnum.K7zIdUnpackInfo)
            {
                res = ReadUnpackInfo(p, sd, numFoldersMax, tempBufs, numTempBufs, alloc);
                if (res != SzRes.Ok)
                    return res;
                res = ReadId(sd, out type);
                if (res != SzRes.Ok)
                    return res;
            }

            if (type == (uint)EIdEnum.K7zIdSubStreamsInfo)
            {
                res = ReadSubStreamsInfo(p, sd, ssi);
                if (res != SzRes.Ok)
                    return res;
                res = ReadId(sd, out type);
                if (res != SzRes.Ok)
                    return res;
            }
            else
            {
                ssi.NumTotalSubStreams = p.NumFolders;
            }

            return type == (uint)EIdEnum.K7zIdEnd ? SzRes.Ok : SzRes.ErrorUnsupported;
        }

        private static int SzReadAndDecodePackedStreams(
            ILookInStream inStream,
            CSzData sd,
            CBuf[] tempBufs,
            uint numFoldersMax,
            ulong baseOffset,
            CSzAr p,
            ISzAlloc allocTemp)
        {
            CSubStreamInfo ssi = new CSubStreamInfo();
            ulong dataStartPos;
            int res = SzReadStreamsInfo(p, sd, numFoldersMax, null, 0, out dataStartPos, ssi, allocTemp);
            if (res != SzRes.Ok)
                return res;

            dataStartPos += baseOffset;
            if (p.NumFolders == 0)
                return SzRes.ErrorArchive;

            for (uint fo = 0; fo < p.NumFolders; fo++)
                tempBufs[fo].Init();

            for (uint fo = 0; fo < p.NumFolders; fo++)
            {
                CBuf tempBuf = tempBufs[fo];
                ulong unpackSize = SzAr_GetFolderUnpackSize(p, fo);
                if ((ulong)unpackSize > int.MaxValue)
                    return SzRes.ErrorMem;
                if (!tempBuf.Create((int)unpackSize, allocTemp))
                    return SzRes.ErrorMem;
            }

            for (uint fo = 0; fo < p.NumFolders; fo++)
            {
                CBuf tempBuf = tempBufs[fo];
                res = SzLookInStream.SeekTo(inStream, dataStartPos);
                if (res != SzRes.Ok)
                    return res;
                res = Sz7zDec.SzAr_DecodeFolder(p, fo, inStream, dataStartPos, tempBuf.Data, tempBuf.Size, allocTemp);
                if (res != SzRes.Ok)
                    return res;
            }

            return SzRes.Ok;
        }

        private static int SzReadFileNames(byte[] data, int size, uint numFiles, int[] offsets)
        {
            int offsetIndex = 0;
            offsets[offsetIndex++] = 0;
            if (numFiles == 0)
                return size == 0 ? SzRes.Ok : SzRes.ErrorArchive;
            if (size < 2)
                return SzRes.ErrorArchive;

            int lim = size;
            if (CpuArch.GetUi16(data, lim - 2) != 0)
                return SzRes.ErrorArchive;

            int p = 0;
            do
            {
                if (p >= lim)
                    return SzRes.ErrorArchive;
                for (; CpuArch.GetUi16(data, p) != 0; p += 2) ;
                p += 2;
                offsets[offsetIndex++] = p >> 1;
            }
            while (--numFiles != 0);

            return p == lim ? SzRes.Ok : SzRes.ErrorArchive;
        }

        private static int ReadTime(
            CSzBitUi64s bitTimes,
            int num,
            CSzData sd2,
            CBuf[] tempBufs,
            uint numTempBufs,
            ISzAlloc alloc)
        {
            if (bitTimes.Defs != null)
                return SzRes.ErrorArchive;

            int res = ReadBitVector(sd2, num, ref bitTimes.Defs, alloc);
            if (res != SzRes.Ok)
                return res;

            byte external;
            res = ReadByteSd(sd2, out external);
            if (res != SzRes.Ok)
                return res;

            byte[] data;
            int dataOffset;
            int size;
            if (external == 0)
            {
                data = sd2.Data;
                dataOffset = sd2.Offset;
                size = sd2.Size;
            }
            else
            {
                uint index;
                res = SzReadNumber32(sd2, out index);
                if (res != SzRes.Ok)
                    return res;
                if (index >= numTempBufs || sd2.Size != 0)
                    return SzRes.ErrorArchive;
                data = tempBufs[index].Data;
                dataOffset = 0;
                size = tempBufs[index].Size;
            }

            res = AllocNtfsFileTimeZe(ref bitTimes.Vals, num, alloc);
            if (res != SzRes.Ok)
                return res;

            CNtfsFileTime[] vals = bitTimes.Vals;
            byte[] defs = bitTimes.Defs;
            for (int i = 0; i < num; i++)
            {
                if (Sz7zBitArray.Check(defs, (uint)i))
                {
                    if (size < 8)
                        return SzRes.ErrorArchive;
                    size -= 8;
                    vals[i].Low = CpuArch.GetUi32(data, dataOffset);
                    vals[i].High = CpuArch.GetUi32(data, dataOffset + 4);
                    dataOffset += 8;
                }
                else
                {
                    vals[i].High = 0;
                    vals[i].Low = 0;
                }
            }

            return size != 0 ? SzRes.ErrorArchive : SzRes.Ok;
        }

        private static int SzReadHeader2(
            CSzArEx p,
            CSzData sd,
            ILookInStream inStream,
            CBuf[] tempBufs,
            ISzAlloc allocMain,
            ISzAlloc allocTemp)
        {
            CSubStreamInfo ssi = new CSubStreamInfo();
            uint numTempBufs = 0;

            ssi.SdSizes.Clear();
            ssi.SdCrcs.Clear();
            ssi.SdNumSubStreams.Clear();
            ssi.NumSubDigests = 0;
            ssi.NumTotalSubStreams = 0;

            ulong type;
            int res = ReadId(sd, out type);
            if (res != SzRes.Ok)
                return res;

            if (type == (uint)EIdEnum.K7zIdArchiveProperties)
            {
                for (; ; )
                {
                    ulong type2;
                    res = ReadId(sd, out type2);
                    if (res != SzRes.Ok)
                        return res;
                    if (type2 == (uint)EIdEnum.K7zIdEnd)
                        break;
                    res = SkipDataBlock(sd);
                    if (res != SzRes.Ok)
                        return res;
                }

                res = ReadId(sd, out type);
                if (res != SzRes.Ok)
                    return res;
            }

            if (type == (uint)EIdEnum.K7zIdAdditionalStreamsInfo)
            {
                CSzAr tempAr = new CSzAr();
                SzArInit(tempAr);
                tempAr.RangeLimit = p.Db.RangeLimit;

                res = SzReadAndDecodePackedStreams(inStream, sd, tempBufs, NumAdditionalStreamsMax,
                    p.StartPosAfterHeader, tempAr, allocTemp);
                numTempBufs = tempAr.NumFolders;
                SzArFree(tempAr, allocTemp);

                if (res != SzRes.Ok)
                    return res;
                res = ReadId(sd, out type);
                if (res != SzRes.Ok)
                    return res;
            }

            if (type == (uint)EIdEnum.K7zIdMainStreamsInfo)
            {
                const uint kNumFoldersMax = 1u << 30;
                ulong dataPos;
                res = SzReadStreamsInfo(p.Db, sd, kNumFoldersMax, tempBufs, numTempBufs, out dataPos, ssi, allocMain);
                if (res != SzRes.Ok)
                    return res;
                p.DataPos = dataPos + p.StartPosAfterHeader;
                res = ReadId(sd, out type);
                if (res != SzRes.Ok)
                    return res;
            }

            if (type == (uint)EIdEnum.K7zIdEnd)
                return SzRes.Ok;
            if (type != (uint)EIdEnum.K7zIdFilesInfo)
                return SzRes.ErrorArchive;

            uint numFiles;
            res = SzReadNumber32(sd, out numFiles);
            if (res != SzRes.Ok)
                return res;
            p.NumFiles = numFiles;

            byte[] emptyStreams = null;
            int emptyStreamsOffset = 0;
            byte[] emptyFiles = null;
            int emptyFilesOffset = 0;
            uint numEmptyStreams = 0;

            for (; ; )
            {
                ulong propType;
                res = ReadId(sd, out propType);
                if (res != SzRes.Ok)
                    return res;
                if (propType == (uint)EIdEnum.K7zIdEnd)
                    break;

                ulong propSize;
                res = ReadNumber(sd, out propSize);
                if (res != SzRes.Ok)
                    return res;
                if (propSize > (ulong)sd.Size)
                    return SzRes.ErrorArchive;

                CSzData sdSwitch = new CSzData
                {
                    Data = sd.Data,
                    Offset = sd.Offset,
                    Size = (int)propSize
                };
                SkipData(sd, (int)propSize);

                if (propType >= 64)
                    continue;

                switch ((int)propType)
                {
                    case (int)EIdEnum.K7zIdEmptyStream:
                        if (emptyStreams != null || emptyFiles != null)
                            return SzRes.ErrorArchive;
                        if (((int)numFiles + 7) >> 3 != sdSwitch.Size)
                            return SzRes.ErrorArchive;
                        emptyStreams = sdSwitch.Data;
                        emptyStreamsOffset = sdSwitch.Offset;
                        numEmptyStreams = CountDefinedBits(emptyStreams, emptyStreamsOffset, numFiles);
                        break;

                    case (int)EIdEnum.K7zIdEmptyFile:
                        if (emptyFiles != null)
                            return SzRes.ErrorArchive;
                        if (((int)numEmptyStreams + 7) >> 3 != sdSwitch.Size)
                            return SzRes.ErrorArchive;
                        emptyFiles = sdSwitch.Data;
                        emptyFilesOffset = sdSwitch.Offset;
                        break;

                    case (int)EIdEnum.K7zIdName:
                    {
                        if (p.FileNameOffsets != null)
                            return SzRes.ErrorArchive;

                        byte external;
                        res = ReadByteSd(sdSwitch, out external);
                        if (res != SzRes.Ok)
                            return res;

                        byte[] namesData;
                        int namesSize;
                        int namesOffset;
                        if (external == 0)
                        {
                            namesData = sdSwitch.Data;
                            namesOffset = sdSwitch.Offset;
                            namesSize = sdSwitch.Size;
                        }
                        else
                        {
                            uint index;
                            res = SzReadNumber32(sdSwitch, out index);
                            if (res != SzRes.Ok)
                                return res;
                            if (index >= numTempBufs || sdSwitch.Size != 0)
                                return SzRes.ErrorArchive;
                            namesData = tempBufs[index].Data;
                            namesOffset = 0;
                            namesSize = tempBufs[index].Size;
                        }

                        if ((namesSize & 1) != 0)
                            return SzRes.ErrorArchive;

                        res = AllocInt32(ref p.FileNameOffsets, (int)numFiles + 1, allocMain);
                        if (res != SzRes.Ok)
                            return res;

                        res = AllocZeAndCopy(ref p.FileNames, namesSize, namesData, namesOffset, allocMain);
                        if (res != SzRes.Ok)
                            return res;

                        res = SzReadFileNames(p.FileNames, namesSize, numFiles, p.FileNameOffsets);
                        if (res != SzRes.Ok)
                            return res;
                        break;
                    }

                    case (int)EIdEnum.K7zIdWinAttrib:
                    {
                        if (p.Attribs.Defs != null)
                            return SzRes.ErrorArchive;
                        res = ReadBitVector(sdSwitch, (int)numFiles, ref p.Attribs.Defs, allocMain);
                        if (res != SzRes.Ok)
                            return res;

                        byte external;
                        res = ReadByteSd(sdSwitch, out external);
                        if (res != SzRes.Ok)
                            return res;

                        if (external != 0)
                        {
                            uint index;
                            res = SzReadNumber32(sdSwitch, out index);
                            if (res != SzRes.Ok)
                                return res;
                            if (index >= numTempBufs || sdSwitch.Size != 0)
                                return SzRes.ErrorArchive;
                            sdSwitch.Data = tempBufs[index].Data;
                            sdSwitch.Offset = 0;
                            sdSwitch.Size = tempBufs[index].Size;
                        }

                        res = ReadUi32s(sdSwitch, (int)numFiles, p.Attribs, allocMain);
                        if (res != SzRes.Ok)
                            return res;
                        if (sdSwitch.Size != 0)
                            return SzRes.ErrorArchive;
                        break;
                    }

                    case (int)EIdEnum.K7zIdMTime:
                    case (int)EIdEnum.K7zIdCTime:
                    {
                        CSzBitUi64s target = propType == (uint)EIdEnum.K7zIdMTime ? p.MTime : p.CTime;
                        res = ReadTime(target, (int)numFiles, sdSwitch, tempBufs, numTempBufs, allocMain);
                        if (res != SzRes.Ok)
                            return res;
                        break;
                    }
                }
            }

            if (numFiles - numEmptyStreams != ssi.NumTotalSubStreams)
                return SzRes.ErrorArchive;

            for (; ; )
            {
                ulong endType;
                res = ReadId(sd, out endType);
                if (res != SzRes.Ok)
                    return res;
                if (endType == (uint)EIdEnum.K7zIdEnd)
                    break;
                res = SkipDataBlock(sd);
                if (res != SzRes.Ok)
                    return res;
            }

            uint emptyFileIndex = 0;
            uint folderIndex = 0;
            uint remSubStreams = 0;
            uint numSubStreams = 0;
            ulong unpackPos = 0;
            byte[] digestsDefs = null;
            int digestsDefsOffset = 0;
            byte[] digestsValsData = null;
            int digestsValsOffset = 0;
            uint digestIndex = 0;
            byte isDirMask = 0;
            byte crcMask = 0;
            byte mask = 0x80;

            res = AllocUInt32(ref p.FolderToFile, (int)p.Db.NumFolders + 1, allocMain);
            if (res != SzRes.Ok)
                return res;
            res = AllocUInt32Ze(ref p.FileToFolder, (int)p.NumFiles, allocMain);
            if (res != SzRes.Ok)
                return res;
            res = AllocUInt64(ref p.UnpackPositions, (int)p.NumFiles + 1, allocMain);
            if (res != SzRes.Ok)
                return res;
            res = AllocBytesZe(ref p.IsDirs, (int)(p.NumFiles + 7) >> 3, allocMain);
            if (res != SzRes.Ok)
                return res;
            res = SzBitUi32sAlloc(p.Crcs, (int)p.NumFiles, allocMain);
            if (res != SzRes.Ok)
                return res;

            if (ssi.SdCrcs.Size != 0)
            {
                CSzData sdCrcs = ssi.SdCrcs.Copy();
                byte allDigestsDefined;
                res = ReadByteSdNoCheck(sdCrcs, out allDigestsDefined);
                if (res != SzRes.Ok)
                    return res;

                if (allDigestsDefined != 0)
                {
                    digestsValsData = sdCrcs.Data;
                    digestsValsOffset = sdCrcs.Offset;
                }
                else
                {
                    int numBytes = (int)((ssi.NumSubDigests + 7) >> 3);
                    digestsDefs = sdCrcs.Data;
                    digestsDefsOffset = sdCrcs.Offset;
                    digestsValsData = sdCrcs.Data;
                    digestsValsOffset = sdCrcs.Offset + numBytes;
                }
            }

            uint i;
            for (i = 0; i < numFiles; i++, mask >>= 1)
            {
                if (mask == 0)
                {
                    uint byteIndex = (i - 1) >> 3;
                    p.IsDirs[byteIndex] = isDirMask;
                    p.Crcs.Defs[byteIndex] = crcMask;
                    isDirMask = 0;
                    crcMask = 0;
                    mask = 0x80;
                }

                p.UnpackPositions[i] = unpackPos;
                p.Crcs.Vals[i] = 0;

                if (emptyStreams != null && Sz7zBitArray.CheckAt(emptyStreams, emptyStreamsOffset, i))
                {
                    if (emptyFiles != null)
                    {
                        if (!Sz7zBitArray.CheckAt(emptyFiles, emptyFilesOffset, emptyFileIndex))
                            isDirMask |= mask;
                        emptyFileIndex++;
                    }
                    else
                    {
                        isDirMask |= mask;
                    }

                    if (remSubStreams == 0)
                    {
                        p.FileToFolder[i] = Sz7zConstants.InvalidFolderIndex;
                        continue;
                    }
                }

                if (remSubStreams == 0)
                {
                    for (; ; )
                    {
                        if (folderIndex >= p.Db.NumFolders)
                            return SzRes.ErrorArchive;
                        p.FolderToFile[folderIndex] = i;
                        numSubStreams = 1;
                        if (ssi.SdNumSubStreams.Data != null)
                        {
                            CSzData sdNum = ssi.SdNumSubStreams.Copy();
                            res = SzReadNumber32(sdNum, out numSubStreams);
                            if (res != SzRes.Ok)
                                return res;
                            ssi.SdNumSubStreams.Offset = sdNum.Offset;
                            ssi.SdNumSubStreams.Size = sdNum.Size;
                        }

                        remSubStreams = numSubStreams;
                        if (numSubStreams != 0)
                            break;

                        ulong folderUnpackSize = SzAr_GetFolderUnpackSize(p.Db, folderIndex);
                        unpackPos += folderUnpackSize;
                        if (unpackPos < folderUnpackSize)
                            return SzRes.ErrorArchive;
                        folderIndex++;
                    }
                }

                p.FileToFolder[i] = folderIndex;

                if (emptyStreams != null && Sz7zBitArray.CheckAt(emptyStreams, emptyStreamsOffset, i))
                    continue;

                if (--remSubStreams == 0)
                {
                    ulong folderUnpackSize = SzAr_GetFolderUnpackSize(p.Db, folderIndex);
                    ulong startFolderUnpackPos = p.UnpackPositions[p.FolderToFile[folderIndex]];
                    if (folderUnpackSize < unpackPos - startFolderUnpackPos)
                        return SzRes.ErrorArchive;
                    unpackPos = startFolderUnpackPos + folderUnpackSize;
                    if (unpackPos < folderUnpackSize)
                        return SzRes.ErrorArchive;

                    if (numSubStreams == 1 && Sz7zBitArray.WithValsCheck(p.Db.FolderCrcs, folderIndex))
                    {
                        p.Crcs.Vals[i] = p.Db.FolderCrcs.Vals[folderIndex];
                        crcMask |= mask;
                    }

                    folderIndex++;
                }
                else
                {
                    CSzData sdSizes = ssi.SdSizes.Copy();
                    ulong v;
                    res = ReadNumber(sdSizes, out v);
                    if (res != SzRes.Ok)
                        return res;
                    ssi.SdSizes.Offset = sdSizes.Offset;
                    ssi.SdSizes.Size = sdSizes.Size;
                    unpackPos += v;
                    if (unpackPos < v)
                        return SzRes.ErrorArchive;
                }

                if ((crcMask & mask) == 0 && digestsValsData != null)
                {
                    if (digestsDefs == null || Sz7zBitArray.CheckAt(digestsDefs, digestsDefsOffset, digestIndex))
                    {
                        p.Crcs.Vals[i] = CpuArch.GetUi32(digestsValsData, digestsValsOffset);
                        digestsValsOffset += 4;
                        crcMask |= mask;
                    }

                    digestIndex++;
                }
            }

            if (mask != 0x80)
            {
                uint byteIndex = (i - 1) >> 3;
                p.IsDirs[byteIndex] = isDirMask;
                p.Crcs.Defs[byteIndex] = crcMask;
            }

            p.UnpackPositions[i] = unpackPos;

            if (remSubStreams != 0)
                return SzRes.ErrorArchive;

            for (; ; )
            {
                p.FolderToFile[folderIndex] = i;
                if (folderIndex >= p.Db.NumFolders)
                    break;
                if (ssi.SdNumSubStreams.Data == null)
                    return SzRes.ErrorArchive;

                CSzData sdNum = ssi.SdNumSubStreams.Copy();
                res = SzReadNumber32(sdNum, out numSubStreams);
                if (res != SzRes.Ok)
                    return res;
                ssi.SdNumSubStreams.Offset = sdNum.Offset;
                ssi.SdNumSubStreams.Size = sdNum.Size;

                if (numSubStreams != 0)
                    return SzRes.ErrorArchive;
                folderIndex++;
            }

            if (ssi.SdNumSubStreams.Data != null && ssi.SdNumSubStreams.Size != 0)
                return SzRes.ErrorArchive;

            return SzRes.Ok;
        }

        private static int SzReadHeader(
            CSzArEx p,
            CSzData sd,
            ILookInStream inStream,
            ISzAlloc allocMain,
            ISzAlloc allocTemp)
        {
            CBuf[] tempBufs = new CBuf[NumAdditionalStreamsMax];
            for (int i = 0; i < NumAdditionalStreamsMax; i++)
            {
                tempBufs[i] = new CBuf();
                tempBufs[i].Init();
            }

            int res = SzReadHeader2(p, sd, inStream, tempBufs, allocMain, allocTemp);

            for (int i = 0; i < NumAdditionalStreamsMax; i++)
                tempBufs[i].Free(allocTemp);

            if (res == SzRes.Ok && sd.Size != 0)
                return SzRes.ErrorArchive;
            return res;
        }

        private static int SzArEx_Open2(
            CSzArEx p,
            ILookInStream inStream,
            ISzAlloc allocMain,
            ISzAlloc allocTemp)
        {
            byte[] header = new byte[Sz7zConstants.K7zStartHeaderSize];
            int res = SzLookInStream.Read2(inStream, header, Sz7zConstants.K7zStartHeaderSize, SzRes.ErrorNoArchive);
            if (res != SzRes.Ok)
                return res;

            long startPosAfterHeader = 0;
            res = inStream.Seek(ref startPosAfterHeader, ESzSeek.Cur);
            if (res != SzRes.Ok)
                return res;
            p.StartPosAfterHeader = (ulong)startPosAfterHeader;

            for (int i = 0; i < Sz7zConstants.K7zSignatureSize; i++)
            {
                if (header[i] != Sz7zConstants.K7zSignature[i])
                    return SzRes.ErrorNoArchive;
            }

            if (header[6] != 0)
                return SzRes.ErrorUnsupported;

            if (SzCrc.CrcCalc(header, 12, 20) != CpuArch.GetUi32(header, 8))
                return SzRes.ErrorCrc;

            ulong nextHeaderOffset = CpuArch.GetUi64(header, 12);
            ulong nextHeaderSize = CpuArch.GetUi64(header, 20);
            uint nextHeaderCrc = CpuArch.GetUi32(header, 28);

            p.Db.RangeLimit = nextHeaderOffset;
            if (nextHeaderOffset >= 1UL << 62 || nextHeaderSize >= 1UL << 48)
                return SzRes.ErrorNoArchive;

            int nextHeaderSizeT = (int)nextHeaderSize;
            if ((ulong)nextHeaderSizeT != nextHeaderSize)
                return SzRes.ErrorMem;

            if (nextHeaderSizeT == 0)
            {
                if (nextHeaderOffset != 0 || nextHeaderCrc != 0)
                    return SzRes.ErrorNoArchive;
                return SzRes.Ok;
            }

            long pos = 0;
            res = inStream.Seek(ref pos, ESzSeek.End);
            if (res != SzRes.Ok)
                return res;
            if ((ulong)(pos - (long)startPosAfterHeader) < nextHeaderOffset + nextHeaderSize)
                return SzRes.ErrorInputEof;

            res = SzLookInStream.SeekTo(inStream, p.StartPosAfterHeader + nextHeaderOffset);
            if (res != SzRes.Ok)
                return res;

            CBuf buf = new CBuf();
            buf.Init();
            if (!buf.Create(nextHeaderSizeT, allocTemp))
                return SzRes.ErrorMem;

            res = SzLookInStream.Read(inStream, buf.Data, nextHeaderSizeT);

            if (res == SzRes.Ok)
            {
                res = SzRes.ErrorArchive;
                if (SzCrc.CrcCalc(buf.Data, nextHeaderSizeT) == nextHeaderCrc)
                {
                    CSzData sd = new CSzData
                    {
                        Data = buf.Data,
                        Offset = 0,
                        Size = buf.Size
                    };

                    ulong type;
                    res = ReadId(sd, out type);

                    if (res == SzRes.Ok && type == (uint)EIdEnum.K7zIdEncodedHeader)
                    {
                        CSzAr tempAr = new CSzAr();
                        CBuf tempBuf = new CBuf();
                        tempBuf.Init();
                        SzArInit(tempAr);
                        tempAr.RangeLimit = p.Db.RangeLimit;

                        CBuf[] singleBuf = { tempBuf };
                        res = SzReadAndDecodePackedStreams(inStream, sd, singleBuf, 1, p.StartPosAfterHeader, tempAr, allocTemp);
                        SzArFree(tempAr, allocTemp);

                        if (res != SzRes.Ok)
                        {
                            tempBuf.Free(allocTemp);
                        }
                        else
                        {
                            buf.Free(allocTemp);
                            buf.Data = tempBuf.Data;
                            buf.Size = tempBuf.Size;
                            sd.Data = buf.Data;
                            sd.Offset = 0;
                            sd.Size = buf.Size;
                            res = ReadId(sd, out type);
                        }
                    }

                    if (res == SzRes.Ok)
                    {
                        if (type == (uint)EIdEnum.K7zIdHeader)
                            res = SzReadHeader(p, sd, inStream, allocMain, allocTemp);
                        else
                            res = SzRes.ErrorUnsupported;
                    }
                }
            }

            buf.Free(allocTemp);
            return res;
        }

        public static int SzArEx_Open(CSzArEx p, ILookInStream inStream, ISzAlloc allocMain, ISzAlloc allocTemp)
        {
            int res = SzArEx_Open2(p, inStream, allocMain, allocTemp);
            if (res != SzRes.Ok)
                SzArEx_Free(p, allocMain);
            return res;
        }

        public static int SzArEx_Extract(
            CSzArEx p,
            ILookInStream inStream,
            uint fileIndex,
            ref uint blockIndex,
            ref byte[] tempBuf,
            ref int outBufferSize,
            out int offset,
            out int outSizeProcessed,
            ISzAlloc allocMain,
            ISzAlloc allocTemp)
        {
            uint folderIndex = p.FileToFolder[fileIndex];
            int res = SzRes.Ok;

            offset = 0;
            outSizeProcessed = 0;

            if (folderIndex == Sz7zConstants.InvalidFolderIndex)
            {
                SzAllocImpl.Free(allocMain, tempBuf);
                blockIndex = folderIndex;
                tempBuf = null;
                outBufferSize = 0;
                return SzRes.Ok;
            }

            if (tempBuf == null || blockIndex != folderIndex)
            {
                ulong unpackSizeSpec = SzAr_GetFolderUnpackSize(p.Db, folderIndex);
                int unpackSize = (int)unpackSizeSpec;

                if ((ulong)unpackSize != unpackSizeSpec)
                    return SzRes.ErrorMem;

                blockIndex = folderIndex;
                SzAllocImpl.Free(allocMain, tempBuf);
                tempBuf = null;

                outBufferSize = unpackSize;
                if (unpackSize != 0)
                {
                    tempBuf = SzAllocImpl.Alloc(allocMain, unpackSize);
                    if (tempBuf == null)
                        res = SzRes.ErrorMem;
                }

                if (res == SzRes.Ok)
                {
                    res = Sz7zDec.SzAr_DecodeFolder(p.Db, folderIndex, inStream, p.DataPos, tempBuf, unpackSize, allocTemp);
                }
            }

            if (res == SzRes.Ok)
            {
                ulong unpackPos = p.UnpackPositions[fileIndex];
                offset = (int)(unpackPos - p.UnpackPositions[p.FolderToFile[folderIndex]]);
                outSizeProcessed = (int)(p.UnpackPositions[fileIndex + 1] - unpackPos);
                if (offset + outSizeProcessed > outBufferSize)
                    return SzRes.ErrorFail;
                if (Sz7zBitArray.WithValsCheck(p.Crcs, fileIndex))
                {
                    if (SzCrc.CrcCalc(tempBuf, offset, outSizeProcessed) != p.Crcs.Vals[fileIndex])
                        res = SzRes.ErrorCrc;
                }
            }

            return res;
        }

        public static int SzArEx_GetFileNameUtf16(CSzArEx p, int fileIndex, ushort[] dest)
        {
            int[] offsets = p.FileNameOffsets;
            if (offsets == null)
            {
                if (dest != null)
                    dest[0] = 0;
                return 1;
            }

            int offs = offsets[fileIndex];
            int len = offsets[fileIndex + 1] - offs;
            if (dest != null)
            {
                byte[] src = p.FileNames;
                int srcOffset = offs * 2;
                for (int i = 0; i < len; i++)
                    dest[i] = CpuArch.GetUi16a(src, srcOffset + i * 2);
            }

            return len;
        }

        private static int AllocBytes(ref byte[] p, int size, ISzAlloc alloc)
        {
            if (size == 0)
            {
                p = null;
                return SzRes.Ok;
            }

            p = SzAllocImpl.Alloc(alloc, size);
            return p != null ? SzRes.Ok : SzRes.ErrorMem;
        }

        private static int AllocBytesZe(ref byte[] p, int size, ISzAlloc alloc)
        {
            if (size == 0)
            {
                p = null;
                return SzRes.Ok;
            }

            return AllocBytes(ref p, size, alloc);
        }

        private static int AllocUInt32(ref uint[] p, int size, ISzAlloc alloc)
        {
            if (size == 0)
            {
                p = null;
                return SzRes.Ok;
            }

            p = new uint[size];
            return SzRes.Ok;
        }

        private static int AllocUInt32Ze(ref uint[] p, int size, ISzAlloc alloc)
        {
            if (size == 0)
            {
                p = null;
                return SzRes.Ok;
            }

            return AllocUInt32(ref p, size, alloc);
        }

        private static int AllocUInt64(ref ulong[] p, int size, ISzAlloc alloc)
        {
            if (size == 0)
            {
                p = null;
                return SzRes.Ok;
            }

            p = new ulong[size];
            return SzRes.Ok;
        }

        private static int AllocUInt64Ze(ref ulong[] p, int size, ISzAlloc alloc)
        {
            if (size == 0)
            {
                p = null;
                return SzRes.Ok;
            }

            return AllocUInt64(ref p, size, alloc);
        }

        private static int AllocInt32(ref int[] p, int size, ISzAlloc alloc)
        {
            if (size == 0)
            {
                p = null;
                return SzRes.Ok;
            }

            p = new int[size];
            return SzRes.Ok;
        }

        private static int AllocNtfsFileTimeZe(ref CNtfsFileTime[] p, int size, ISzAlloc alloc)
        {
            if (size == 0)
            {
                p = null;
                return SzRes.Ok;
            }

            p = new CNtfsFileTime[size];
            return SzRes.Ok;
        }

        private static int AllocAndCopy(ref byte[] to, int size, byte[] from, int fromOffset, ISzAlloc alloc)
        {
            int res = AllocBytes(ref to, size, alloc);
            if (res != SzRes.Ok)
                return res;
            System.Buffer.BlockCopy(from, fromOffset, to, 0, size);
            return SzRes.Ok;
        }

        private static int AllocZeAndCopy(ref byte[] to, int size, byte[] from, int fromOffset, ISzAlloc alloc)
        {
            if (size == 0)
            {
                to = null;
                return SzRes.Ok;
            }

            return AllocAndCopy(ref to, size, from, fromOffset, alloc);
        }
    }
}
