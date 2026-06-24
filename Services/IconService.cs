using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;

namespace SmartGoldbergEmu.Services
{
    public class IconService
    {
        private const uint SHGFI_ICON = 0x100;
        private const uint SHGFI_LARGEICON = 0x0;
        private const uint SHGFI_SMALLICON = 0x1;
        private const uint SHGFI_SHELLICONSIZE = 0x4;
        private const int MAX_PATH = 260;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public Icon ExtractLargeIcon(string filePath)
        {
            return ExtractIcon(filePath, SHGFI_LARGEICON | SHGFI_SHELLICONSIZE);
        }

        public Icon ExtractSmallIcon(string filePath)
        {
            return ExtractIcon(filePath, SHGFI_SMALLICON);
        }

        private static Icon ExtractIcon(string filePath, uint iconSizeFlags)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return null;

            try
            {
                SHFILEINFO shInfo = default;
                uint flags = SHGFI_ICON | iconSizeFlags;
                IntPtr hIcon = SHGetFileInfo(filePath, 0, ref shInfo, (uint)Marshal.SizeOf(typeof(SHFILEINFO)), flags);

                if (hIcon == IntPtr.Zero || shInfo.hIcon == IntPtr.Zero)
                    return null;

                Icon icon = (Icon)Icon.FromHandle(shInfo.hIcon).Clone();
                DestroyIcon(shInfo.hIcon);
                return icon;
            }
            catch
            {
                return null;
            }
        }

        public Bitmap IconToBitmap(Icon icon, Size size)
        {
            if (icon == null)
                return null;

            Bitmap bitmap = null;
            try
            {
                if (icon.Size == size)
                    return icon.ToBitmap();

                bitmap = new Bitmap(size.Width, size.Height);
                using (Graphics g = Graphics.FromImage(bitmap))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.CompositingQuality = CompositingQuality.HighQuality;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawIcon(icon, new Rectangle(Point.Empty, size));
                }

                var result = bitmap;
                bitmap = null;
                return result;
            }
            catch
            {
                return null;
            }
            finally
            {
                bitmap?.Dispose();
            }
        }
    }
}
