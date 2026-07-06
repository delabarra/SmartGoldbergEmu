using System;
using System.IO;

namespace SmartGoldbergEmu.ExtractKit.Internal
{
    internal enum ESzSeek
    {
        Set = 0,
        Cur = 1,
        End = 2
    }

    internal interface ISzAlloc
    {
        byte[] Alloc(int size);
        void Free(byte[] address);
    }

    internal sealed class SzAlloc : ISzAlloc
    {
        public static readonly SzAlloc Instance = new SzAlloc();

        private SzAlloc()
        {
        }

        public byte[] Alloc(int size)
        {
            if (size == 0)
                return null;
            return new byte[size];
        }

        public void Free(byte[] address)
        {
        }
    }

    internal interface ISeqInStream
    {
        int Read(byte[] buf, ref int size);
    }

    internal interface ISeqOutStream
    {
        int Write(byte[] buf, int size);
    }

    internal interface ISeekInStream : ISeqInStream
    {
        int Seek(ref long pos, ESzSeek origin);
    }

    internal interface ILookInStream
    {
        // bufferOffset is the start index within buf for the returned look window.
        int Look(out byte[] buf, out int bufferOffset, ref int size);
        int Skip(int offset);
        int Read(byte[] buf, ref int size);
        int Seek(ref long pos, ESzSeek origin);
    }

    internal sealed class StreamSeekInStream : ISeekInStream
    {
        private readonly Stream _stream;

        public StreamSeekInStream(Stream stream)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        }

        public int Read(byte[] buf, ref int size)
        {
            if (size == 0)
                return SzRes.Ok;

            int read = _stream.Read(buf, 0, size);
            size = read;
            return SzRes.Ok;
        }

        public int Seek(ref long pos, ESzSeek origin)
        {
            try
            {
                switch (origin)
                {
                    case ESzSeek.Set:
                        _stream.Seek(pos, SeekOrigin.Begin);
                        break;
                    case ESzSeek.Cur:
                        _stream.Seek(pos, SeekOrigin.Current);
                        break;
                    case ESzSeek.End:
                        _stream.Seek(pos, SeekOrigin.End);
                        break;
                }

                pos = _stream.Position;
                return SzRes.Ok;
            }
            catch (IOException)
            {
                return SzRes.ErrorRead;
            }
        }
    }

    internal static class SzStreamMacros
    {
        public static int Rinok(int result)
        {
            return result != SzRes.Ok ? result : SzRes.Ok;
        }
    }
}
