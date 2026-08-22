namespace SmartGoldbergEmu.ExtractKit.Internal
{
    internal static class CpuArch
    {
        public static ushort GetUi16(byte[] data, int offset)
        {
            return (ushort)(data[offset] | ((ushort)data[offset + 1] << 8));
        }

        public static ushort GetUi16a(byte[] data, int offset)
        {
            return GetUi16(data, offset);
        }

        public static uint GetUi32(byte[] data, int offset)
        {
            return (uint)data[offset]
                | ((uint)data[offset + 1] << 8)
                | ((uint)data[offset + 2] << 16)
                | ((uint)data[offset + 3] << 24);
        }

        public static uint GetUi32a(byte[] data, int offset)
        {
            return GetUi32(data, offset);
        }

        public static ulong GetUi64(byte[] data, int offset)
        {
            return GetUi32(data, offset) | ((ulong)GetUi32(data, offset + 4) << 32);
        }

        public static void SetUi32(byte[] data, int offset, uint v)
        {
            data[offset] = (byte)v;
            data[offset + 1] = (byte)(v >> 8);
            data[offset + 2] = (byte)(v >> 16);
            data[offset + 3] = (byte)(v >> 24);
        }

        public static void SetUi16(byte[] data, int offset, ushort v)
        {
            data[offset] = (byte)v;
            data[offset + 1] = (byte)(v >> 8);
        }

        public static void SetUi32a(byte[] data, int offset, uint v)
        {
            SetUi32(data, offset, v);
        }

        public static void SetUi16a(byte[] data, int offset, ushort v)
        {
            SetUi16(data, offset, v);
        }

        public static uint GetBe32a(byte[] data, int offset)
        {
            return (uint)data[offset] << 24
                | (uint)data[offset + 1] << 16
                | (uint)data[offset + 2] << 8
                | data[offset + 3];
        }

        public static void SetBe32a(byte[] data, int offset, uint v)
        {
            data[offset] = (byte)(v >> 24);
            data[offset + 1] = (byte)(v >> 16);
            data[offset + 2] = (byte)(v >> 8);
            data[offset + 3] = (byte)v;
        }
    }
}
