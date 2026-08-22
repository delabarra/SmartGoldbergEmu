using System;
using System.IO;
using System.IO.Compression;

namespace SmartGoldbergEmu.ExtractKit.Internal
{
    // ZIP method 8: zlib wrapper (7-Zip) or raw deflate (.NET Compress-Archive / release CI).
    internal sealed class ZipDeflateDecoder
    {
        private const int ZlibHeaderSize = 2;
        private const int ZlibFooterSize = 4;
        private const int MinZlibSize = ZlibHeaderSize + ZlibFooterSize;

        public void Inflate(Stream input, long offset, long compressedSize, byte[] output, int outputOffset, int outputCount)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));
            if (compressedSize <= 0)
                throw new InvalidDataException("Deflate payload is empty.");

            using (var compressed = new StreamSlice(input, offset, compressedSize))
            {
                if (compressedSize >= MinZlibSize)
                {
                    byte[] header = new byte[ZlibHeaderSize];
                    ReadExact(compressed, header, 0, ZlibHeaderSize);
                    if (IsZlibHeader(header))
                    {
                        InflateZlibWrapped(compressed, compressedSize, output, outputOffset, outputCount);
                        return;
                    }
                }

                compressed.Position = 0;
                InflateRaw(compressed, output, outputOffset, outputCount);
            }
        }

        private static void InflateZlibWrapped(
            Stream compressed,
            long totalCompressedSize,
            byte[] output,
            int outputOffset,
            int outputCount)
        {
            long deflateLength = totalCompressedSize - MinZlibSize;
            using (var deflatePayload = new StreamSlice(compressed, ZlibHeaderSize, deflateLength))
            using (var deflate = new DeflateStream(deflatePayload, CompressionMode.Decompress, leaveOpen: true))
            {
                ReadExact(deflate, output, outputOffset, outputCount);
            }

            byte[] footer = new byte[ZlibFooterSize];
            ReadExact(compressed, footer, 0, ZlibFooterSize);
            uint expectedAdler = ReadBigEndianUInt32(footer);
            uint actualAdler = Adler32.Update(Adler32.InitialValue, output, outputOffset, outputCount);
            if (expectedAdler != actualAdler)
                throw new InvalidDataException("Zlib adler32 mismatch.");
        }

        private static void InflateRaw(Stream compressed, byte[] output, int outputOffset, int outputCount)
        {
            using (var deflate = new DeflateStream(compressed, CompressionMode.Decompress, leaveOpen: true))
            {
                ReadExact(deflate, output, outputOffset, outputCount);
            }
        }

        private static bool IsZlibHeader(byte[] header)
        {
            byte b0 = header[0];
            byte b1 = header[1];
            if ((b0 & 0xF) != 8)
                return false;
            if ((b0 >> 4) > 7)
                return false;
            if ((b1 & 0x20) != 0)
                return false;

            uint check = ((uint)b0 << 8) + b1;
            return check % 31 == 0;
        }

        private static uint ReadBigEndianUInt32(byte[] bytes)
        {
            return ((uint)bytes[0] << 24)
                | ((uint)bytes[1] << 16)
                | ((uint)bytes[2] << 8)
                | bytes[3];
        }

        private static void ReadExact(Stream stream, byte[] buffer, int offset, int count)
        {
            int total = 0;
            while (total < count)
            {
                int read = stream.Read(buffer, offset + total, count - total);
                if (read == 0)
                    throw new InvalidDataException("Deflate decode failed.");
                total += read;
            }
        }

        private sealed class StreamSlice : Stream
        {
            private readonly Stream _base;
            private readonly long _start;
            private readonly long _length;
            private long _position;

            public StreamSlice(Stream baseStream, long offset, long length)
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
