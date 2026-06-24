using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace SmartGoldbergEmu.Helpers
{
    // Resolves process image paths without relying on Process.MainModule (often blocked for other users' games).
    public static class ProcessImagePathHelper
    {
        public static bool TryGetProcessImagePath(Process process, out string imagePath)
        {
            imagePath = null;
            if (process == null)
                return false;

            try
            {
                if (process.HasExited)
                    return false;

                var buffer = new StringBuilder(1024);
                int size = buffer.Capacity;
                if (QueryFullProcessImageName(process.Handle, 0, buffer, ref size))
                {
                    imagePath = buffer.ToString(0, size);
                    return !string.IsNullOrEmpty(imagePath);
                }
            }
            catch
            {
            }

            try
            {
                imagePath = process.MainModule?.FileName;
                return !string.IsNullOrEmpty(imagePath);
            }
            catch
            {
                return false;
            }
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool QueryFullProcessImageName(
            IntPtr hProcess,
            int dwFlags,
            StringBuilder lpExeName,
            ref int lpdwSize);
    }
}
