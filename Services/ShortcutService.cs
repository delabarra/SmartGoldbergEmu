using System;
using System.IO;
using System.Text;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Services
{
    public static class ShortcutService
    {
        public static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return "Shortcut";

            char[] invalidChars = Path.GetInvalidFileNameChars();
            StringBuilder sanitized = new StringBuilder(fileName.Length);

            foreach (char c in fileName)
            {
                if (Array.IndexOf(invalidChars, c) == -1)
                {
                    sanitized.Append(c);
                }
            }

            string result = sanitized.ToString().Trim();
            return string.IsNullOrWhiteSpace(result) ? "Shortcut" : result;
        }

        public static bool Create(string shortcutPath, ulong appId, string gameName = null, string iconPath = null, int iconIndex = 0)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(shortcutPath))
                    return false;

                string directory = Path.GetDirectoryName(shortcutPath);
                string fileName = Path.GetFileName(shortcutPath);
                string extension = Path.GetExtension(fileName);
                string nameWithoutExtension = string.IsNullOrEmpty(extension) ? fileName : fileName.Substring(0, fileName.Length - extension.Length);
                string sanitizedName = SanitizeFileName(nameWithoutExtension);
                shortcutPath = Path.Combine(directory, $"{sanitizedName}.url");

                if (!shortcutPath.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
                    shortcutPath = Path.ChangeExtension(shortcutPath, ".url");

                string runUri = UriProtocolService.CreateRunUri(appId);

                var content = new StringBuilder();
                content.AppendLine("[InternetShortcut]");
                content.AppendLine($"URL={runUri}");

                if (!string.IsNullOrWhiteSpace(iconPath) && File.Exists(iconPath))
                {
                    content.AppendLine($"IconFile={iconPath}");
                    content.AppendLine($"IconIndex={iconIndex}");
                }

                // Windows expects UTF-8 for .url files.
                File.WriteAllText(shortcutPath, content.ToString(), Encoding.UTF8);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static string CreateDesktopShortcut(ulong appId, string gameName, string iconPath = null)
        {
            try
            {
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                string sanitizedName = SanitizeFileName(gameName ?? $"Game_{appId}");
                string shortcutPath = Path.Combine(desktopPath, $"{sanitizedName}.url");

                int counter = 1;
                string originalPath = shortcutPath;
                while (File.Exists(shortcutPath))
                {
                    shortcutPath = Path.Combine(desktopPath, $"{sanitizedName} ({counter}).url");
                    counter++;
                }

                if (Create(shortcutPath, appId, gameName, iconPath))
                {
                    return shortcutPath;
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}

