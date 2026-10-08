using System;
using System.IO;
using SmartGoldbergEmu.Constants;

namespace SmartGoldbergEmu.Helpers
{
    public static class PathValidationHelper
    {
        public static bool IsPathWithinBase(string basePath, string resolvedPath)
        {
            if (string.IsNullOrWhiteSpace(basePath) || string.IsNullOrWhiteSpace(resolvedPath))
                return false;

            try
            {
                string normalizedBase = Path.GetFullPath(basePath);
                string normalizedResolved = Path.GetFullPath(resolvedPath);

                return normalizedResolved.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static bool TryResolveAndValidatePath(string basePath, string relativePath, out string resolvedPath)
        {
            resolvedPath = null;

            if (string.IsNullOrWhiteSpace(basePath) || string.IsNullOrWhiteSpace(relativePath))
                return false;

            try
            {
                string normalizedRelative = relativePath.Replace('/', Path.DirectorySeparatorChar);
                normalizedRelative = normalizedRelative.Replace('\\', Path.DirectorySeparatorChar);

                if (Path.IsPathRooted(normalizedRelative))
                {
                    string fullPath = Path.GetFullPath(normalizedRelative);
                    if (IsPathWithinBase(basePath, fullPath))
                    {
                        resolvedPath = fullPath;
                        return true;
                    }
                    return false;
                }

                string combined = Path.Combine(basePath, normalizedRelative);
                string fullResolved = Path.GetFullPath(combined);

                if (IsPathWithinBase(basePath, fullResolved))
                {
                    resolvedPath = fullResolved;
                    return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        // Guards paths passed to Process.Start. With no allowedBasePaths, only the path format is checked.
        public static bool IsSafeFilePath(string filePath, params string[] allowedBasePaths)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            try
            {
                if (filePath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                    return false;

                if (allowedBasePaths == null || allowedBasePaths.Length == 0)
                {
                    string fullPath = Path.GetFullPath(filePath);
                    return !string.IsNullOrEmpty(fullPath);
                }

                string normalizedFilePath = Path.GetFullPath(filePath);
                foreach (string allowedBase in allowedBasePaths)
                {
                    if (string.IsNullOrWhiteSpace(allowedBase))
                        continue;

                    if (IsPathWithinBase(allowedBase, normalizedFilePath))
                        return true;
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsSafeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            string lowerUrl = url.ToLowerInvariant();
            return lowerUrl.StartsWith(ApplicationConstants.HttpUriSchemePrefix, StringComparison.Ordinal) ||
                   lowerUrl.StartsWith(ApplicationConstants.HttpsUriSchemePrefix, StringComparison.Ordinal);
        }

        public static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return string.Empty;

            char[] invalidChars = Path.GetInvalidFileNameChars();
            foreach (char c in invalidChars)
            {
                fileName = fileName.Replace(c, '_');
            }

            return fileName;
        }

        public static string TryResolveWorkingDirectoryTextToFullPath(string raw, string gameBase)
        {
            if (string.IsNullOrWhiteSpace(raw) || string.IsNullOrEmpty(gameBase))
                return null;
            try
            {
                raw = raw.Trim();
                string full = Path.IsPathRooted(raw)
                    ? Path.GetFullPath(raw)
                    : Path.GetFullPath(Path.Combine(gameBase, raw));
                return full;
            }
            catch
            {
                return null;
            }
        }

        public static string ToDisplayPathRelativeToGameFolder(string storedPath, string gameFolder)
        {
            if (string.IsNullOrWhiteSpace(storedPath))
                return string.Empty;
            string trim = storedPath.Trim();
            if (string.IsNullOrWhiteSpace(gameFolder) || !Directory.Exists(gameFolder))
                return trim;
            if (!Path.IsPathRooted(trim))
                return trim;
            string fullGameFolder = Path.GetFullPath(gameFolder);
            if (TryMakePathRelativeToDirectory(fullGameFolder, trim, out string rel))
                return rel ?? string.Empty;
            return trim;
        }

        public static bool TryMakePathRelativeToDirectory(string rootDirectory, string targetDirectory, out string relativePath)
        {
            relativePath = null;
            if (string.IsNullOrEmpty(rootDirectory) || string.IsNullOrEmpty(targetDirectory))
                return false;
            try
            {
                string root = Path.GetFullPath(rootDirectory);
                string target = Path.GetFullPath(targetDirectory);
                string rootTrim = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string targetTrim = target.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (string.Equals(rootTrim, targetTrim, StringComparison.OrdinalIgnoreCase))
                {
                    relativePath = string.Empty;
                    return true;
                }
                string rootPrefix = rootTrim + Path.DirectorySeparatorChar;
                if (!target.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                    return false;
                relativePath = target.Substring(rootPrefix.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string ToRelativePathOrOriginal(string rootDirectory, string targetPath)
        {
            if (string.IsNullOrWhiteSpace(targetPath))
                return string.Empty;
            if (string.IsNullOrWhiteSpace(rootDirectory))
                return targetPath;

            try
            {
                string root = Path.GetFullPath(rootDirectory);
                string target = Path.GetFullPath(targetPath);
                if (TryMakePathRelativeToDirectory(root, target, out string rel))
                    return rel ?? string.Empty;
            }
            catch
            {
            }

            return targetPath;
        }

        public static string ToRelativePathOrFileNameOrOriginal(string rootDirectory, string targetPath)
        {
            if (string.IsNullOrWhiteSpace(targetPath))
                return string.Empty;

            string relativeOrOriginal = ToRelativePathOrOriginal(rootDirectory, targetPath);
            if (string.IsNullOrEmpty(relativeOrOriginal))
                return Path.GetFileName(targetPath);
            return relativeOrOriginal;
        }
    }
}

