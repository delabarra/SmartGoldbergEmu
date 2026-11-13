using System;
using System.Runtime.InteropServices;

namespace SmartGoldbergEmu.Helpers
{
    // System-wide keyboard/mouse idle via GetLastInputInfo.
    public static class WindowsUserIdleHelper
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }

        [DllImport("user32.dll")]
        private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        // Milliseconds since last keyboard/mouse input; 0 if the API fails.
        public static uint GetIdleMilliseconds()
        {
            var info = new LASTINPUTINFO();
            info.cbSize = (uint)Marshal.SizeOf(info);
            if (!GetLastInputInfo(ref info))
                return 0;

            // Both are GetTickCount-based; unsigned subtract handles ~49-day wrap.
            return unchecked((uint)Environment.TickCount - info.dwTime);
        }
    }
}
