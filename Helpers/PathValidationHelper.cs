using System;
using System.IO;
using SmartGoldbergEmu.Constants;

namespace SmartGoldbergEmu.Helpers
{
    /// <summary>
    /// Helper class for path validation and sanitization to prevent path traversal attacks.
    /// </summary>
    public static class PathValidationHelper
    {
        /// <summary>
        /// Validates that a resolved path is within the base directory (prevents path traversal).
        /// </summary>
        /// <param name="basePath">The base directory that the path must be within.</param>
        /// <param name="resolvedPath">The resolved path to validate.</param>
        /// <returns>True if the path is safe (within base directory), false otherwise.</returns>
        public static bool IsPathWithinBase(string basePath, string resolvedPath)
        {
            if (string.IsNullOrWhiteSpace(basePath) || string.IsNullOrWhiteSpace(resolvedPath))
                return false;

            try
            {
                // Normalize paths to full paths for comparison
                string normalizedBase = Path.GetFullPath(basePath);
                string normalizedResolved = Path.GetFullPath(resolvedPath);

                // Check if resolved path starts with base path
                return normalizedResolved.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                // If path is invalid, it's not safe
                return false;
            }
        }

        /// <summary>
        /// Resolves a relative path and validates it's within the base directory.
        /// </summary>
        /// <param name="basePath">The base directory.</param>
        /// <param name="relativePath">The relative path to resolve.</param>
        /// <param name="resolvedPath">Output parameter for the resolved path if valid.</param>
        /// <returns>True if the path is valid and within base directory, false otherwise.</returns>
        public static bool TryResolveAndValidatePath(string basePath, string relativePath, out string resolvedPath)
        {
            resolvedPath = null;

            if (string.IsNullOrWhiteSpace(basePath) || string.IsNullOrWhiteSpace(relativePath))
                return false;

            try
            {
                // Normalize path separators
                string normalizedRelative = relativePath.Replace('/', Path.DirectorySeparatorChar);
                normalizedRelative = normalizedRelative.Replace('\\', Path.DirectorySeparatorChar);

                // If it's already an absolute path, validate it's within base
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

                // Resolve relative path
                string combined = Path.Combine(basePath, normalizedRelative);
                string fullResolved = Path.GetFullPath(combined);

                // Validate it's within base directory
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

        /// <summary>
        /// Validates that a file path is safe to use with Process.Start.
        /// Checks for path traversal and ensures the file is within allowed directories.
        /// </summary>
        /// <param name="filePath">The file path to validate.</param>
        /// <param name="allowedBasePaths">Array of allowed base paths. If empty, only validates format.</param>
        /// <returns>True if the path is safe, false otherwise.</returns>
        public static bool IsSafeFilePath(string filePath, params string[] allowedBasePaths)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            try
            {
                // Check for invalid characters
                if (filePath.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
                    return false;

                // If no allowed base paths specified, just validate format
                if (allowedBasePaths == null || allowedBasePaths.Length == 0)
                {
                    // Basic format validation
                    string fullPath = Path.GetFullPath(filePath);
                    return !string.IsNullOrEmpty(fullPath);
                }

                // Check if path is within any allowed base path
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

        /// <summary>
        /// Validates that a URL is safe to open (basic validation).
        /// </summary>
        /// <param name="url">The URL to validate.</param>
        /// <returns>True if the URL appears safe, false otherwise.</returns>
        public static bool IsSafeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            string lowerUrl = url.ToLowerInvariant();
            return lowerUrl.StartsWith(ApplicationConstants.HttpUriSchemePrefix, StringComparison.Ordinal) ||
                   lowerUrl.StartsWith(ApplicationConstants.HttpsUriSchemePrefix, StringComparison.Ordinal);
        }

        /// <summary>
        /// Sanitizes a file name by removing invalid characters.
        /// </summary>
        /// <param name="fileName">The file name to sanitize.</param>
        /// <returns>Sanitized file name.</returns>
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

