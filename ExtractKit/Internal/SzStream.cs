namespace SmartGoldbergEmu.ExtractKit.Internal
{
    internal static class SzStream
    {
        public static int SeqInStream_ReadMax(ISeqInStream stream, byte[] buf, ref int processedSize)
        {
            int size = processedSize;
            processedSize = 0;
            int bufOffset = 0;
            while (size != 0)
            {
                int cur = size;
                int res = stream.Read(buf, ref cur);
                processedSize += cur;
                bufOffset += cur;
                size -= cur;
                if (res != SzRes.Ok)
                    return res;
                if (cur == 0)
                    return SzRes.Ok;
            }

            return SzRes.Ok;
        }

        public static int SeqInStream_ReadByte(ISeqInStream stream, out byte b)
        {
            b = 0;
            int processed = 1;
            byte[] one = new byte[1];
            int res = stream.Read(one, ref processed);
            if (res != SzRes.Ok)
                return res;
            if (processed != 1)
                return SzRes.ErrorInputEof;
            b = one[0];
            return SzRes.Ok;
        }

        public static int LookInStream_SeekTo(ILookInStream stream, ulong offset)
        {
            long t = (long)offset;
            return stream.Seek(ref t, ESzSeek.Set);
        }

        public static int LookInStream_LookRead(ILookInStream stream, byte[] buf, ref int size)
        {
            if (size == 0)
                return SzRes.Ok;

            byte[] lookBuf;
            int lookOffset;
            int res = stream.Look(out lookBuf, out lookOffset, ref size);
            if (res != SzRes.Ok)
                return res;
            System.Array.Copy(lookBuf, lookOffset, buf, 0, size);
            return stream.Skip(size);
        }

        public static int LookInStream_Read2(ILookInStream stream, byte[] buf, int size, int errorType)
        {
            int offset = 0;
            while (size != 0)
            {
                int processed = size;
                int res = stream.Read(buf, ref processed);
                if (res != SzRes.Ok)
                    return res;
                if (processed == 0)
                    return errorType;
                offset += processed;
                size -= processed;
            }

            return SzRes.Ok;
        }

        public static int LookInStream_Read(ILookInStream stream, byte[] buf, int size)
        {
            return LookInStream_Read2(stream, buf, size, SzRes.ErrorInputEof);
        }
    }

    internal sealed class LookToRead2 : ILookInStream
    {
        private readonly ISeekInStream _realStream;
        private readonly bool _lookahead;
        private int _pos;
        private int _size;
        public byte[] Buf;
        public int BufSize;

        public LookToRead2(ISeekInStream realStream, bool lookahead)
        {
            _realStream = realStream;
            _lookahead = lookahead;
            _pos = 0;
            _size = 0;
        }

        public void Init()
        {
            _pos = 0;
            _size = 0;
        }

        public int Look(out byte[] buf, out int bufferOffset, ref int size)
        {
            return _lookahead ? Look_Lookahead(out buf, out bufferOffset, ref size) : Look_Exact(out buf, out bufferOffset, ref size);
        }

        private int Look_Lookahead(out byte[] buf, out int bufferOffset, ref int size)
        {
            int res = SzRes.Ok;
            int size2 = _size - _pos;
            if (size2 == 0 && size != 0)
            {
                _pos = 0;
                _size = 0;
                size2 = BufSize;
                res = _realStream.Read(Buf, ref size2);
                _size = size2;
            }

            if (size > size2)
                size = size2;
            buf = Buf;
            bufferOffset = _pos;
            return res;
        }

        private int Look_Exact(out byte[] buf, out int bufferOffset, ref int size)
        {
            int res = SzRes.Ok;
            int size2 = _size - _pos;
            if (size2 == 0 && size != 0)
            {
                _pos = 0;
                _size = 0;
                if (size > BufSize)
                    size = BufSize;
                res = _realStream.Read(Buf, ref size);
                size2 = _size = size;
            }

            if (size > size2)
                size = size2;
            buf = Buf;
            bufferOffset = _pos;
            return res;
        }

        public int Skip(int offset)
        {
            _pos += offset;
            return SzRes.Ok;
        }

        public int Read(byte[] buf, ref int size)
        {
            int rem = _size - _pos;
            if (rem == 0)
                return _realStream.Read(buf, ref size);

            if (rem > size)
                rem = size;
            System.Array.Copy(Buf, _pos, buf, 0, rem);
            _pos += rem;
            size = rem;
            return SzRes.Ok;
        }

        public int Seek(ref long pos, ESzSeek origin)
        {
            _pos = 0;
            _size = 0;
            return _realStream.Seek(ref pos, origin);
        }
    }

    internal sealed class SecToLook : ISeqInStream
    {
        private readonly ILookInStream _realStream;

        public SecToLook(ILookInStream realStream)
        {
            _realStream = realStream;
        }

        public int Read(byte[] buf, ref int size)
        {
            return SzStream.LookInStream_LookRead(_realStream, buf, ref size);
        }
    }

    internal sealed class SecToRead : ISeqInStream
    {
        private readonly ILookInStream _realStream;

        public SecToRead(ILookInStream realStream)
        {
            _realStream = realStream;
        }

        public int Read(byte[] buf, ref int size)
        {
            return _realStream.Read(buf, ref size);
        }
    }
}
