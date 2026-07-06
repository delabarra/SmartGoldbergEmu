using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SmartGoldbergEmu.ExtractKit.Internal;

namespace SmartGoldbergEmu.ExtractKit.Internal
{
    internal sealed class ZipArchiveCore
    {
        private const uint LocalFileHeaderSignature = 0x04034B50;
        private const uint CentralDirectoryHeaderSignature = 0x02014B50;
        private const uint EndOfCentralDirectorySignature = 0x06054B50;
        private const uint Zip64EndOfCentralDirectorySignature = 0x06064B50;
        private const uint Zip64EndOfCentralDirectoryLocatorSignature = 0x07064B50;

        private const ushort Zip64ExtraFieldHeaderId = 0x0001;

        private const ushort MethodStore = 0;
        private const ushort MethodDeflate = 8;

        private const ushort GeneralPurposeFlagUtf8 = 0x0800;
        private const ushort GeneralPurposeFlagEncrypted = 0x0001;

        private const int MaxCommentLength = 0xFFFF;
        private const int EndOfCentralDirectorySize = 22;
        private const int Zip64LocatorSize = 20;

        private Stream _stream;
        private ZipEntry[] _entries;
        private Dictionary<string, int> _pathIndex;
        private bool _opened;

        private sealed class ZipEntry
        {
            public string Path;
            public long CompressedSize;
            public long UncompressedSize;
            public ushort Method;
            public long LocalHeaderOffset;
            public bool IsDirectory;
            public uint Crc32;
        }

        public int FileCount
        {
            get
            {
                EnsureOpen();
                return _entries.Length;
            }
        }

        public void Open(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead)
                throw new ArgumentException("Stream must be readable.", nameof(stream));
            if (!stream.CanSeek)
                throw new ArgumentException("Stream must be seekable.", nameof(stream));

            Close();

            _stream = stream;
            _entries = ReadCentralDirectory(stream);
            _pathIndex = BuildPathIndex(_entries);
            _opened = true;
        }

        public void Open(FileStream stream)
        {
            Open((Stream)stream);
        }

        public void Close()
        {
            _stream = null;
            _entries = null;
            _pathIndex = null;
            _opened = false;
        }

        public string GetEntryPath(int index)
        {
            EnsureOpen();
            if (index < 0 || index >= _entries.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _entries[index].Path;
        }

        public bool TryFindEntry(string path, out int index)
        {
            EnsureOpen();
            index = -1;
            if (string.IsNullOrEmpty(path))
                return false;

            string key = EntryPath.Normalize(path);
            return _pathIndex.TryGetValue(key, out index);
        }

        public void ExtractEntryToFile(int index, string destPath)
        {
            if (string.IsNullOrEmpty(destPath))
                throw new ArgumentException("Destination path is required.", nameof(destPath));

            EnsureOpen();
            if (index < 0 || index >= _entries.Length)
                throw new ArgumentOutOfRangeException(nameof(index));

            ZipEntry entry = _entries[index];
            string outputPath = ResolveEntryDestinationPath(entry, destPath);

            if (entry.IsDirectory)
            {
                Directory.CreateDirectory(outputPath);
                return;
            }

            string parent = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);

            ExtractEntryData(entry, outputPath);
        }

        public void ExtractAll(string destRoot)
        {
            if (string.IsNullOrEmpty(destRoot))
                throw new ArgumentException("Destination root is required.", nameof(destRoot));

            EnsureOpen();
            Directory.CreateDirectory(destRoot);

            for (int i = 0; i < _entries.Length; i++)
            {
                ZipEntry entry = _entries[i];
                string outputPath = Path.Combine(destRoot, entry.Path.Replace('/', Path.DirectorySeparatorChar));

                if (entry.IsDirectory)
                {
                    Directory.CreateDirectory(outputPath);
                    continue;
                }

                string parent = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(parent))
                    Directory.CreateDirectory(parent);

                ExtractEntryData(entry, outputPath);
            }
        }

        private static string ResolveEntryDestinationPath(ZipEntry entry, string destPath)
        {
            bool destIsDirectory = destPath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                || destPath.EndsWith("/", StringComparison.Ordinal)
                || destPath.EndsWith("\\", StringComparison.Ordinal)
                || Directory.Exists(destPath);

            if (destIsDirectory)
            {
                string relative = entry.Path.Replace('/', Path.DirectorySeparatorChar);
                return Path.Combine(destPath, relative);
            }

            return destPath;
        }

        private void ExtractEntryData(ZipEntry entry, string outputPath)
        {
            long dataOffset = ResolveLocalFileDataOffset(entry.LocalHeaderOffset, out ushort method, out ushort flags);
            if ((flags & GeneralPurposeFlagEncrypted) != 0)
                throw new ExtractKitException("Encrypted zip entries are not supported.");

            if (method != MethodStore && method != MethodDeflate)
                throw new ExtractKitException("Unsupported zip compression method " + method + ".");

            if (entry.UncompressedSize == 0)
            {
                using (File.Create(outputPath))
                {
                }
                return;
            }

            if (entry.UncompressedSize > int.MaxValue)
                throw new ExtractKitException("Entry is too large: " + entry.Path);

            byte[] buffer = new byte[(int)entry.UncompressedSize];
            _stream.Position = dataOffset;

            if (method == MethodStore)
            {
                if (entry.CompressedSize > int.MaxValue)
                    throw new ExtractKitException("Entry compressed size is too large: " + entry.Path);

                byte[] compressed = new byte[(int)entry.CompressedSize];
                ReadExact(_stream, compressed, 0, compressed.Length);
                if (compressed.Length != buffer.Length)
                    throw new InvalidDataException("Stored entry size mismatch: " + entry.Path);
                Buffer.BlockCopy(compressed, 0, buffer, 0, buffer.Length);
            }
            else
            {
                var decoder = new ZipDeflateDecoder();
                decoder.Inflate(_stream, dataOffset, entry.CompressedSize, buffer, 0, buffer.Length);
            }

            // The central directory CRC is authoritative; reject a wrong decode loudly instead of
            // writing a corrupt file (mirrors the per-file CRC check on the 7z extraction path).
            uint actualCrc = SzCrc.CrcCalc(buffer, buffer.Length);
            if (actualCrc != entry.Crc32)
                throw new ExtractKitException("Zip entry CRC mismatch: " + entry.Path);

            using (var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None))
                output.Write(buffer, 0, buffer.Length);
        }

        private long ResolveLocalFileDataOffset(long localHeaderOffset, out ushort method, out ushort flags)
        {
            _stream.Position = localHeaderOffset;
            if (ReadUInt32(_stream) != LocalFileHeaderSignature)
                throw new InvalidDataException("Invalid local file header.");

            ReadUInt16(_stream); // version needed
            flags = ReadUInt16(_stream);
            method = ReadUInt16(_stream);
            ReadUInt16(_stream); // mod time
            ReadUInt16(_stream); // mod date
            ReadUInt32(_stream); // crc32
            uint compressedSize32 = ReadUInt32(_stream);
            uint uncompressedSize32 = ReadUInt32(_stream);
            ushort nameLength = ReadUInt16(_stream);
            ushort extraLength = ReadUInt16(_stream);

            long dataOffset = localHeaderOffset + 30 + nameLength + extraLength;

            if (compressedSize32 == uint.MaxValue || uncompressedSize32 == uint.MaxValue)
            {
                _stream.Position = localHeaderOffset + 30 + nameLength;
                ReadZip64SizesFromExtra(_stream, extraLength, compressedSize32 == uint.MaxValue, uncompressedSize32 == uint.MaxValue, out _, out _);
            }

            return dataOffset;
        }

        private static ZipEntry[] ReadCentralDirectory(Stream stream)
        {
            long length = stream.Length;
            if (length < EndOfCentralDirectorySize)
                throw new InvalidDataException("Zip archive is too small.");

            int searchSize = (int)Math.Min(length, MaxCommentLength + EndOfCentralDirectorySize + Zip64LocatorSize);
            byte[] tail = new byte[searchSize];
            stream.Position = length - searchSize;
            ReadExact(stream, tail, 0, searchSize);

            int eocdOffset = FindSignatureBackward(tail, EndOfCentralDirectorySignature);
            if (eocdOffset < 0)
                throw new InvalidDataException("End of central directory record not found.");

            int eocdPos = eocdOffset;
            stream.Position = length - searchSize + eocdPos;

            if (ReadUInt32(stream) != EndOfCentralDirectorySignature)
                throw new InvalidDataException("Invalid end of central directory signature.");

            ReadUInt16(stream); // disk number
            ReadUInt16(stream); // disk with CD
            ushort entriesOnDisk = ReadUInt16(stream);
            ushort totalEntries = ReadUInt16(stream);
            uint centralDirectorySize32 = ReadUInt32(stream);
            uint centralDirectoryOffset32 = ReadUInt32(stream);
            ushort commentLength = ReadUInt16(stream);

            long centralDirectoryOffset = centralDirectoryOffset32;
            long centralDirectorySize = centralDirectorySize32;
            long entryCount = totalEntries;

            if (entriesOnDisk == ushort.MaxValue || totalEntries == ushort.MaxValue
                || centralDirectorySize32 == uint.MaxValue || centralDirectoryOffset32 == uint.MaxValue)
            {
                long locatorPos = FindZip64Locator(stream, length, searchSize);
                ReadZip64EndOfCentralDirectory(stream, locatorPos, ref entryCount, ref centralDirectorySize, ref centralDirectoryOffset);
            }

            if (entryCount < 0 || centralDirectorySize < 0 || centralDirectoryOffset < 0
                || centralDirectoryOffset + centralDirectorySize > length)
                throw new InvalidDataException("Invalid central directory bounds.");

            stream.Position = centralDirectoryOffset;
            var entries = new List<ZipEntry>((int)Math.Min(entryCount, int.MaxValue));
            long end = centralDirectoryOffset + centralDirectorySize;

            while (stream.Position < end)
            {
                if (ReadUInt32(stream) != CentralDirectoryHeaderSignature)
                    throw new InvalidDataException("Invalid central directory header.");

                ReadUInt16(stream); // version made by
                ReadUInt16(stream); // version needed
                ushort flags = ReadUInt16(stream);
                ushort method = ReadUInt16(stream);
                ReadUInt16(stream); // mod time
                ReadUInt16(stream); // mod date
                uint crc32 = ReadUInt32(stream);

                uint compressedSize32 = ReadUInt32(stream);
                uint uncompressedSize32 = ReadUInt32(stream);
                ushort nameLength = ReadUInt16(stream);
                ushort extraLength = ReadUInt16(stream);
                ushort entryCommentLength = ReadUInt16(stream);
                ReadUInt16(stream); // disk start
                ReadUInt16(stream); // internal attrs
                ReadUInt32(stream); // external attrs
                uint localHeaderOffset32 = ReadUInt32(stream);

                byte[] nameBytes = new byte[nameLength];
                ReadExact(stream, nameBytes, 0, nameLength);

                long compressedSize = compressedSize32;
                long uncompressedSize = uncompressedSize32;
                long localHeaderOffset = localHeaderOffset32;

                if (extraLength > 0)
                {
                    long extraStart = stream.Position;
                    ReadZip64Extra(
                        stream,
                        extraLength,
                        compressedSize32 == uint.MaxValue,
                        uncompressedSize32 == uint.MaxValue,
                        localHeaderOffset32 == uint.MaxValue,
                        ref compressedSize,
                        ref uncompressedSize,
                        ref localHeaderOffset);
                    stream.Position = extraStart + extraLength;
                }

                if (entryCommentLength > 0)
                    stream.Position += entryCommentLength;

                string path = DecodeEntryPath(nameBytes, flags);
                bool isDirectory = path.EndsWith("/", StringComparison.Ordinal)
                    || path.EndsWith("\\", StringComparison.Ordinal);

                entries.Add(new ZipEntry
                {
                    Path = EntryPath.Normalize(path),
                    CompressedSize = compressedSize,
                    UncompressedSize = uncompressedSize,
                    Method = method,
                    LocalHeaderOffset = localHeaderOffset,
                    IsDirectory = isDirectory,
                    Crc32 = crc32
                });
            }

            return entries.ToArray();
        }

        private static long FindZip64Locator(Stream stream, long length, int searchSize)
        {
            byte[] tail = new byte[searchSize];
            stream.Position = length - searchSize;
            ReadExact(stream, tail, 0, searchSize);

            int locatorOffset = FindSignatureBackward(tail, Zip64EndOfCentralDirectoryLocatorSignature);
            if (locatorOffset < 0)
                throw new InvalidDataException("Zip64 end of central directory locator not found.");

            return length - searchSize + locatorOffset;
        }

        private static void ReadZip64EndOfCentralDirectory(
            Stream stream,
            long locatorPos,
            ref long entryCount,
            ref long centralDirectorySize,
            ref long centralDirectoryOffset)
        {
            stream.Position = locatorPos;
            if (ReadUInt32(stream) != Zip64EndOfCentralDirectoryLocatorSignature)
                throw new InvalidDataException("Invalid Zip64 locator signature.");

            ReadUInt32(stream); // disk with zip64 eocd
            long zip64EocdOffset = ReadInt64(stream);
            ReadUInt32(stream); // total disks

            stream.Position = zip64EocdOffset;
            if (ReadUInt32(stream) != Zip64EndOfCentralDirectorySignature)
                throw new InvalidDataException("Invalid Zip64 end of central directory signature.");

            long recordSize = ReadInt64(stream);
            long recordEnd = stream.Position + recordSize;

            ReadUInt16(stream); // version made by
            ReadUInt16(stream); // version needed
            ReadUInt32(stream); // disk number
            ReadUInt32(stream); // disk with CD
            entryCount = ReadInt64(stream);
            ReadInt64(stream); // entries on disk
            centralDirectorySize = ReadInt64(stream);
            centralDirectoryOffset = ReadInt64(stream);

            stream.Position = recordEnd;
        }

        private static void ReadZip64Extra(
            Stream stream,
            int extraLength,
            bool needCompressedSize,
            bool needUncompressedSize,
            bool needLocalHeaderOffset,
            ref long compressedSize,
            ref long uncompressedSize,
            ref long localHeaderOffset)
        {
            int remaining = extraLength;
            while (remaining >= 4)
            {
                ushort headerId = ReadUInt16(stream);
                ushort dataSize = ReadUInt16(stream);
                if (dataSize > remaining - 4)
                    throw new InvalidDataException("Invalid zip extra field.");
                remaining -= 4 + dataSize;

                if (headerId == Zip64ExtraFieldHeaderId)
                {
                    if (needUncompressedSize)
                    {
                        if (dataSize < 8)
                            throw new InvalidDataException("Invalid Zip64 extra field.");
                        uncompressedSize = ReadInt64(stream);
                        dataSize -= 8;
                    }

                    if (needCompressedSize)
                    {
                        if (dataSize < 8)
                            throw new InvalidDataException("Invalid Zip64 extra field.");
                        compressedSize = ReadInt64(stream);
                        dataSize -= 8;
                    }

                    if (needLocalHeaderOffset)
                    {
                        if (dataSize < 8)
                            throw new InvalidDataException("Invalid Zip64 extra field.");
                        localHeaderOffset = ReadInt64(stream);
                        dataSize -= 8;
                    }

                    if (dataSize > 0)
                        stream.Position += dataSize;
                }
                else
                {
                    stream.Position += dataSize;
                }
            }
        }

        private static void ReadZip64SizesFromExtra(
            Stream stream,
            int extraLength,
            bool needCompressedSize,
            bool needUncompressedSize,
            out long compressedSize,
            out long uncompressedSize)
        {
            compressedSize = 0;
            uncompressedSize = 0;
            long localHeaderOffset = 0;
            ReadZip64Extra(stream, extraLength, needCompressedSize, needUncompressedSize, false, ref compressedSize, ref uncompressedSize, ref localHeaderOffset);
        }

        private static Dictionary<string, int> BuildPathIndex(ZipEntry[] entries)
        {
            var map = new Dictionary<string, int>(entries.Length, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < entries.Length; i++)
            {
                string path = entries[i].Path;
                if (!map.ContainsKey(path))
                    map.Add(path, i);
            }

            return map;
        }

        private static string DecodeEntryPath(byte[] nameBytes, ushort flags)
        {
            if ((flags & GeneralPurposeFlagUtf8) != 0)
                return Encoding.UTF8.GetString(nameBytes);

            try
            {
                return Encoding.GetEncoding(437).GetString(nameBytes);
            }
            catch (ArgumentException)
            {
                return Encoding.UTF8.GetString(nameBytes);
            }
        }

        private static int FindSignatureBackward(byte[] buffer, uint signature)
        {
            byte b0 = (byte)signature;
            byte b1 = (byte)(signature >> 8);
            byte b2 = (byte)(signature >> 16);
            byte b3 = (byte)(signature >> 24);

            for (int i = buffer.Length - 4; i >= 0; i--)
            {
                if (buffer[i] == b0 && buffer[i + 1] == b1 && buffer[i + 2] == b2 && buffer[i + 3] == b3)
                    return i;
            }

            return -1;
        }

        private void EnsureOpen()
        {
            if (!_opened)
                throw new InvalidOperationException("Zip archive is not open.");
        }

        private static void ReadExact(Stream stream, byte[] buffer, int offset, int count)
        {
            int readTotal = 0;
            while (readTotal < count)
            {
                int read = stream.Read(buffer, offset + readTotal, count - readTotal);
                if (read == 0)
                    throw new EndOfStreamException();
                readTotal += read;
            }
        }

        private static ushort ReadUInt16(Stream stream)
        {
            int b0 = stream.ReadByte();
            int b1 = stream.ReadByte();
            if (b0 < 0 || b1 < 0)
                throw new EndOfStreamException();
            return (ushort)(b0 | (b1 << 8));
        }

        private static uint ReadUInt32(Stream stream)
        {
            int b0 = stream.ReadByte();
            int b1 = stream.ReadByte();
            int b2 = stream.ReadByte();
            int b3 = stream.ReadByte();
            if (b0 < 0 || b1 < 0 || b2 < 0 || b3 < 0)
                throw new EndOfStreamException();
            return (uint)(b0 | (b1 << 8) | (b2 << 16) | (b3 << 24));
        }

        private static long ReadInt64(Stream stream)
        {
            uint lo = ReadUInt32(stream);
            uint hi = ReadUInt32(stream);
            return (long)((ulong)lo | ((ulong)hi << 32));
        }

        private sealed class SubStream : Stream
        {
            private readonly Stream _base;
            private readonly long _start;
            private readonly long _length;
            private long _position;

            public SubStream(Stream baseStream, long offset, long length)
            {
                if (!baseStream.CanSeek)
                    throw new ArgumentException("Base stream must be seekable.", nameof(baseStream));
                if (offset < 0 || length < 0)
                    throw new ArgumentOutOfRangeException();
                _base = baseStream;
                _start = offset;
                _length = length;
                _position = 0;
            }

            public override bool CanRead => true;
            public override bool CanSeek => true;
            public override bool CanWrite => false;
            public override long Length => _length;

            public override long Position
            {
                get => _position;
                set
                {
                    if (value < 0 || value > _length)
                        throw new ArgumentOutOfRangeException(nameof(value));
                    _position = value;
                }
            }

            public override void Flush()
            {
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (_position >= _length)
                    return 0;

                long remaining = _length - _position;
                if (count > remaining)
                    count = (int)remaining;

                _base.Position = _start + _position;
                int read = _base.Read(buffer, offset, count);
                _position += read;
                return read;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                long newPos;
                switch (origin)
                {
                    case SeekOrigin.Begin:
                        newPos = offset;
                        break;
                    case SeekOrigin.Current:
                        newPos = _position + offset;
                        break;
                    case SeekOrigin.End:
                        newPos = _length + offset;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(origin));
                }

                if (newPos < 0 || newPos > _length)
                    throw new IOException("Seek outside stream bounds.");
                _position = newPos;
                return _position;
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }
        }
    }
}
