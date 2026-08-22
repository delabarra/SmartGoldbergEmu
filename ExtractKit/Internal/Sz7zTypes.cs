namespace SmartGoldbergEmu.ExtractKit.Internal
{
    internal static class Sz7zConstants
    {
        public const int K7zStartHeaderSize = 0x20;
        public const int K7zSignatureSize = 6;

        public static readonly byte[] K7zSignature = { (byte)'7', (byte)'z', 0xBC, 0xAF, 0x27, 0x1C };

        // C name alias from 7z.h
        public static readonly byte[] k7zSignature = K7zSignature;

        public const int SzNumCodersInFolderMax = 4;
        public const int SzNumBondsInFolderMax = 3;
        public const int SzNumPackStreamsInFolderMax = 4;

        public const uint InvalidFolderIndex = uint.MaxValue;
    }

    internal sealed class CSzData
    {
        public byte[] Data;
        public int Offset;
        public int Size;

        public void Clear()
        {
            Data = null;
            Offset = 0;
            Size = 0;
        }

        public void Assign(CSzData other)
        {
            Data = other.Data;
            Offset = other.Offset;
            Size = other.Size;
        }

        public CSzData Copy()
        {
            return new CSzData
            {
                Data = Data,
                Offset = Offset,
                Size = Size
            };
        }
    }

    internal sealed class CSzCoderInfo
    {
        public int PropsOffset;
        public uint MethodId;
        public byte NumStreams;
        public byte PropsSize;
    }

    internal sealed class CSzBond
    {
        public uint InIndex;
        public uint OutIndex;
    }

    internal sealed class CSzFolder
    {
        public uint NumCoders;
        public uint NumBonds;
        public uint NumPackStreams;
        public uint UnpackStream;
        public readonly uint[] PackStreams = new uint[Sz7zConstants.SzNumPackStreamsInFolderMax];
        public readonly CSzBond[] Bonds = new CSzBond[Sz7zConstants.SzNumBondsInFolderMax];
        public readonly CSzCoderInfo[] Coders = new CSzCoderInfo[Sz7zConstants.SzNumCodersInFolderMax];

        public CSzFolder()
        {
            for (int i = 0; i < Bonds.Length; i++)
                Bonds[i] = new CSzBond();
            for (int i = 0; i < Coders.Length; i++)
                Coders[i] = new CSzCoderInfo();
        }
    }

    internal struct CNtfsFileTime
    {
        public uint Low;
        public uint High;
    }

    internal sealed class CSzBitUi32s
    {
        public byte[] Defs;
        public uint[] Vals;
    }

    internal sealed class CSzBitUi64s
    {
        public byte[] Defs;
        public CNtfsFileTime[] Vals;
    }

    internal sealed class CSzAr
    {
        public uint NumPackStreams;
        public uint NumFolders;

        public ulong[] PackPositions;
        public CSzBitUi32s FolderCrcs = new CSzBitUi32s();

        public int[] FoCodersOffsets;
        public uint[] FoStartPackStreamIndex;
        public uint[] FoToCoderUnpackSizes;
        public byte[] FoToMainUnpackSizeIndex;
        public ulong[] CoderUnpackSizes;

        public byte[] CodersData;

        public ulong RangeLimit;
    }

    internal sealed class CSzArEx
    {
        public CSzAr Db = new CSzAr();

        public ulong StartPosAfterHeader;
        public ulong DataPos;

        public uint NumFiles;

        public ulong[] UnpackPositions;
        public byte[] IsDirs;
        public CSzBitUi32s Crcs = new CSzBitUi32s();

        public CSzBitUi32s Attribs = new CSzBitUi32s();
        public CSzBitUi64s MTime = new CSzBitUi64s();
        public CSzBitUi64s CTime = new CSzBitUi64s();

        public uint[] FolderToFile;
        public uint[] FileToFolder;

        public int[] FileNameOffsets;
        public byte[] FileNames;
    }

    internal static class Sz7zBitArray
    {
        public static bool Check(byte[] p, uint i)
        {
            return CheckAt(p, 0, i);
        }

        public static bool CheckAt(byte[] p, int offset, uint i)
        {
            if (p == null)
                return false;
            return (p[offset + (int)(i >> 3)] & (0x80 >> (int)(i & 7))) != 0;
        }

        public static bool WithValsCheck(CSzBitUi32s p, uint i)
        {
            return p.Defs != null && (p.Defs[i >> 3] & (0x80 >> (int)(i & 7))) != 0;
        }
    }
}
