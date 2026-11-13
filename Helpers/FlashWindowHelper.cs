using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SmartGoldbergEmu.Helpers
{
    // Taskbar / caption flash without stealing focus (LAN chat attention).
    public static class FlashWindowHelper
    {
        private const uint FlashwStop = 0;
        private const uint FlashwCaption = 1;
        private const uint FlashwTray = 2;
        private const uint FlashwAll = FlashwCaption | FlashwTray;
        // Continuous until FLASHW_STOP. FLASHW_TIMERNOFG is a no-op when Windows
        // thinks the hwnd is foreground, and Win11 can spuriously activate a
        // minimized window during FlashWindowEx — so the flash never shows.
        private const uint FlashwTimer = 4;

        public static void FlashUntilForeground(Form form)
        {
            if (form == null || form.IsDisposed || !form.IsHandleCreated)
                return;

            try
            {
                var info = new FlashwInfo
                {
                    cbSize = (uint)Marshal.SizeOf(typeof(FlashwInfo)),
                    hwnd = form.Handle,
                    dwFlags = FlashwAll | FlashwTimer,
                    uCount = 0,
                    dwTimeout = 0
                };
                FlashWindowEx(ref info);
            }
            catch
            {
            }
        }

        public static void Stop(Form form)
        {
            if (form == null || form.IsDisposed || !form.IsHandleCreated)
                return;

            try
            {
                var info = new FlashwInfo
                {
                    cbSize = (uint)Marshal.SizeOf(typeof(FlashwInfo)),
                    hwnd = form.Handle,
                    dwFlags = FlashwStop,
                    uCount = 0,
                    dwTimeout = 0
                };
                FlashWindowEx(ref info);
            }
            catch
            {
            }
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FlashWindowEx(ref FlashwInfo pwfi);

        [StructLayout(LayoutKind.Sequential)]
        private struct FlashwInfo
        {
            public uint cbSize;
            public IntPtr hwnd;
            public uint dwFlags;
            public uint uCount;
            public uint dwTimeout;
        }
    }
}
