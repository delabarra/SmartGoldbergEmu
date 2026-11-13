using System;
using System.Drawing;
using System.IO;

namespace SmartGoldbergEmu.Helpers
{
    // Unlock LAN avatar files so cache JPEGs are not held open.
    public static class LanAvatarImageHelper
    {
        // displaySize > 0 scales to a square; 0 keeps the decoded pixel size.
        public static Image TryLoadAvatar(string path, int displaySize = 0)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                using (var ms = new MemoryStream(bytes))
                using (var img = Image.FromStream(ms))
                {
                    if (displaySize > 0)
                        return new Bitmap(img, new Size(displaySize, displaySize));
                    return new Bitmap(img);
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
