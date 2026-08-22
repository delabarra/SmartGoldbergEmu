using System;
using System.IO;

namespace SmartGoldbergEmu.ExtractKit.Internal
{
    internal static class ArchivePath
    {
        public static string RequireSupportedArchive(string path, string paramName)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path is required.", paramName);

            string fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException("Archive file was not found.", fullPath);
            if (!IsSupportedPath(fullPath))
                throw new UnsupportedArchiveFormatException(fullPath);

            return fullPath;
        }

        public static bool IsSupportedPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".7z":
                case ".zip":
                    return true;
                default:
                    return false;
            }
        }
    }
}
