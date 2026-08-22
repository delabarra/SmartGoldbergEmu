namespace SmartGoldbergEmu.ExtractKit.Internal
{
    internal static class SzRes
    {
        public const int Ok = 0;

        public const int ErrorData = 1;
        public const int ErrorMem = 2;
        public const int ErrorCrc = 3;
        public const int ErrorUnsupported = 4;
        public const int ErrorParam = 5;
        public const int ErrorInputEof = 6;
        public const int ErrorOutputEof = 7;
        public const int ErrorRead = 8;
        public const int ErrorWrite = 9;
        public const int ErrorProgress = 10;
        public const int ErrorFail = 11;
        public const int ErrorThread = 12;

        public const int ErrorArchive = 16;
        public const int ErrorNoArchive = 17;
    }
}
