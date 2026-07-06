using System;
using System.IO;
using System.Text;

namespace SmartGoldbergEmu.ExtractKit.Internal
{
    public sealed class BlockCache
    {
        public uint BlockIndex = uint.MaxValue;
        public byte[] Buffer;
        public int BufferSize;
    }

    internal sealed class SevenZipArchiveCore
    {
        private const int LookBufSize = 1 << 18;

        private readonly CSzArEx _archive = new CSzArEx();
        private readonly ISzAlloc _allocMain;
        private readonly ISzAlloc _allocTemp;
        private LookToRead2 _lookStream;
        private bool _opened;

        public SevenZipArchiveCore()
            : this(SzAlloc.Instance, SzAlloc.Instance)
        {
        }

        internal SevenZipArchiveCore(ISzAlloc allocMain, ISzAlloc allocTemp)
        {
            _allocMain = allocMain ?? SzAlloc.Instance;
            _allocTemp = allocTemp ?? SzAlloc.Instance;
            Sz7zArcIn.SzArEx_Init(_archive);
        }

        public int Open(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead)
                throw new ArgumentException("Stream must be readable.", nameof(stream));
            if (!stream.CanSeek)
                throw new ArgumentException("Stream must be seekable.", nameof(stream));

            Close();

            ISeekInStream seekStream = new StreamSeekInStream(stream);
            _lookStream = new LookToRead2(seekStream, true)
            {
                Buf = new byte[LookBufSize],
                BufSize = LookBufSize
            };
            _lookStream.Init();

            int res = Sz7zArcIn.SzArEx_Open(_archive, _lookStream, _allocMain, _allocTemp);
            if (res != SzRes.Ok)
            {
                Close();
                return res;
            }

            _opened = true;
            return SzRes.Ok;
        }

        public void Close()
        {
            if (_opened || _archive.NumFiles != 0 || _archive.Db.NumFolders != 0)
                Sz7zArcIn.SzArEx_Free(_archive, _allocMain);
            _lookStream = null;
            _opened = false;
        }

        public int FileCount
        {
            get
            {
                EnsureOpen();
                return (int)_archive.NumFiles;
            }
        }

        public string GetEntryPath(int fileIndex)
        {
            EnsureOpen();
            if (fileIndex < 0 || fileIndex >= (int)_archive.NumFiles)
                throw new ArgumentOutOfRangeException(nameof(fileIndex));

            int len = Sz7zArcIn.SzArEx_GetFileNameUtf16(_archive, fileIndex, null);
            if (len <= 0)
                return string.Empty;

            ushort[] name = new ushort[len];
            Sz7zArcIn.SzArEx_GetFileNameUtf16(_archive, fileIndex, name);

            // SzArEx_GetFileNameUtf16 reports the length including the trailing UTF-16 NUL terminator.
            int charCount = len;
            if (charCount > 0 && name[charCount - 1] == 0)
                charCount--;
            return Encoding.Unicode.GetString(ToByteArray(name), 0, charCount * 2);
        }

        // Decodes the folder that contains fileIndex (cached in blockCache across files that
        // share a folder) and returns the file's slice as [offset, offset + outSize) inside
        // blockCache.Buffer. The cache buffer and its size are left intact so the next file in
        // the same folder reuses the decoded block; callers must read the returned slice.
        public int ExtractFile(int fileIndex, BlockCache blockCache, out int offset, out int outSize)
        {
            offset = 0;
            outSize = 0;
            if (blockCache == null)
                throw new ArgumentNullException(nameof(blockCache));
            EnsureOpen();
            if (fileIndex < 0 || fileIndex >= (int)_archive.NumFiles)
                throw new ArgumentOutOfRangeException(nameof(fileIndex));

            int res = Sz7zArcIn.SzArEx_Extract(
                _archive,
                _lookStream,
                (uint)fileIndex,
                ref blockCache.BlockIndex,
                ref blockCache.Buffer,
                ref blockCache.BufferSize,
                out offset,
                out outSize,
                _allocMain,
                _allocTemp);

            if (res != SzRes.Ok)
                return res;

            if (outSize != 0 && (blockCache.Buffer == null || offset < 0 ||
                offset + outSize > blockCache.BufferSize))
                return SzRes.ErrorFail;

            return SzRes.Ok;
        }

        private void EnsureOpen()
        {
            if (!_opened)
                throw new InvalidOperationException("Archive is not open. Call Open(Stream) first.");
        }

        private static byte[] ToByteArray(ushort[] chars)
        {
            byte[] bytes = new byte[chars.Length * 2];
            Buffer.BlockCopy(chars, 0, bytes, 0, bytes.Length);
            return bytes;
        }
    }
}
