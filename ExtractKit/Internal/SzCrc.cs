namespace SmartGoldbergEmu.ExtractKit.Internal
{
    internal static class SzCrc
    {
        private const uint CrcInitVal = 0xFFFFFFFF;
        private static readonly uint[] GCrcTable = new uint[256];
        private static bool _tableGenerated;

        static SzCrc()
        {
            CrcGenerateTable();
        }

        public static void CrcGenerateTable()
        {
            if (_tableGenerated)
                return;

            const uint kCrcPoly = 0xEDB88320;
            for (uint i = 0; i < 256; i++)
            {
                uint r = i;
                for (int j = 0; j < 8; j++)
                    r = (r >> 1) ^ (kCrcPoly & (0u - (r & 1)));
                GCrcTable[i] = r;
            }

            _tableGenerated = true;
        }

        public static uint CrcUpdate(uint crc, byte[] data, int offset, int size)
        {
            int lim = offset + size;
            for (int p = offset; p < lim; p++)
                crc = GCrcTable[(crc ^ data[p]) & 0xFF] ^ (crc >> 8);
            return crc;
        }

        public static uint CrcCalc(byte[] data, int offset, int size)
        {
            return CrcUpdate(CrcInitVal, data, offset, size) ^ CrcInitVal;
        }

        public static uint CrcCalc(byte[] data, int size)
        {
            return CrcCalc(data, 0, size);
        }
    }
}
