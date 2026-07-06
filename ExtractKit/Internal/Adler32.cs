namespace SmartGoldbergEmu.ExtractKit.Internal
{
    // 7z2602-src/CPP/7zip/Compress/ZlibDecoder.cpp Adler32_Update
    internal static class Adler32
    {
        public const uint InitialValue = 1;
        private const uint Mod = 65521;
        private const int LoopMax = 5550;

        public static uint Update(uint adler, byte[] data, int offset, int count)
        {
            if (count == 0)
                return adler;

            uint a = adler & 0xffff;
            uint b = adler >> 16;
            int size = count;
            int index = offset;

            while (size > 0)
            {
                int cur = size > LoopMax ? LoopMax : size;
                size -= cur;
                int end = index + cur;

                if (cur >= 4)
                {
                    int lim = end - 3;
                    while (index < lim)
                    {
                        a += data[index]; b += a;
                        a += data[index + 1]; b += a;
                        a += data[index + 2]; b += a;
                        a += data[index + 3]; b += a;
                        index += 4;
                    }
                }

                while (index < end)
                {
                    a += data[index++];
                    b += a;
                }

                a %= Mod;
                b %= Mod;
            }

            return (b << 16) | a;
        }
    }
}
