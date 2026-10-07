using System;
using System.IO;

namespace SmartGoldbergEmu.ExtractKit.Internal
{
    internal static class EntryPath
    {
        internal static string Normalize(string path)
        {
            if (string.IsNullOrEmpty(path))
                return string.Empty;

            string normalized = path.Replace('\\', '/');
            while (normalized.StartsWith("./", StringComparison.Ordinal))
                normalized = normalized.Substring(2);
            if (normalized.StartsWith("/", StringComparison.Ordinal))
                normalized = normalized.Substring(1);
            return normalized;
        }

        // Every on-disk write derived from an archive entry name must go through here (zip-slip guard):
        // the result is a full path strictly below destinationRoot.
        internal static string ResolveDestinationPath(string destinationRoot, string entryPath)
        {
            if (string.IsNullOrEmpty(destinationRoot))
                throw new ArgumentException("Destination root is required.", nameof(destinationRoot));

            string relative = Normalize(entryPath).Replace('/', Path.DirectorySeparatorChar);
            if (relative.Length == 0
                || relative.IndexOf(Path.VolumeSeparatorChar) >= 0
                || Path.IsPathRooted(relative))
                throw new ExtractKitException("Archive entry path is not allowed: " + entryPath);

            string root = Path.GetFullPath(destinationRoot);
            if (!root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                root += Path.DirectorySeparatorChar;

            string fullPath = Path.GetFullPath(Path.Combine(root, relative));
            if (fullPath.Length <= root.Length || !fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new ExtractKitException("Archive entry path escapes the destination directory: " + entryPath);

            return fullPath;
        }
    }
}
