namespace SmartGoldbergEmu.ExtractKit.Internal
{
    internal static class SzAllocImpl
    {
        public static byte[] Alloc(ISzAlloc alloc, int size)
        {
            return alloc.Alloc(size);
        }

        public static void Free(ISzAlloc alloc, byte[] address)
        {
            alloc.Free(address);
        }
    }
}
