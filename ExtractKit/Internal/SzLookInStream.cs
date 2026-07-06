namespace SmartGoldbergEmu.ExtractKit.Internal
{
    internal static class SzLookInStream
    {
        public static int SeekTo(ILookInStream stream, ulong offset)
        {
            long t = (long)offset;
            return stream.Seek(ref t, ESzSeek.Set);
        }

        public static int Read2(ILookInStream stream, byte[] buf, int size, int errorType)
        {
            return Read2(stream, buf, 0, size, errorType);
        }

        public static int Read2(ILookInStream stream, byte[] buf, int bufOffset, int size, int errorType)
        {
            byte[] temp = null;
            while (size != 0)
            {
                int processed = size;
                int res;
                if (bufOffset == 0)
                {
                    res = stream.Read(buf, ref processed);
                }
                else
                {
                    if (temp == null || temp.Length < processed)
                        temp = new byte[processed];
                    res = stream.Read(temp, ref processed);
                    if (res == SzRes.Ok && processed != 0)
                        System.Array.Copy(temp, 0, buf, bufOffset, processed);
                }

                if (res != SzRes.Ok)
                    return res;
                if (processed == 0)
                    return errorType;
                bufOffset += processed;
                size -= processed;
            }

            return SzRes.Ok;
        }

        public static int Read(ILookInStream stream, byte[] buf, int size)
        {
            return Read2(stream, buf, size, SzRes.ErrorInputEof);
        }
    }
}
