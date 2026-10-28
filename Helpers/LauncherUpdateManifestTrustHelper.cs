using System;
using System.IO;
using System.Reflection;

namespace SmartGoldbergEmu.Helpers
{
    // Shared by the launcher (when writing the apply manifest) and SmartGoldbergEmu.LauncherUpdate (when applying).
    public static class LauncherUpdateManifestTrustHelper
    {
        public const string LauncherMainExecutableFileName = "SmartGoldbergEmu.exe";

        public static void ValidateOrThrow(
            string manifestPath,
            string installRoot,
            string stageRoot,
            string exePath,
            string workRoot,
            string updaterDirectory = null)
        {
            if (string.IsNullOrWhiteSpace(manifestPath))
                throw new InvalidOperationException("Launcher update manifest path is missing.");

            string manifestFull = Path.GetFullPath(manifestPath.Trim().Trim('"'));
            string manifestDir = Path.GetDirectoryName(manifestFull);
            if (string.IsNullOrEmpty(manifestDir))
                throw new InvalidOperationException("Launcher update manifest directory is invalid.");

            string updaterDir = string.IsNullOrWhiteSpace(updaterDirectory)
                ? ResolveUpdaterDirectory()
                : Path.GetFullPath(updaterDirectory);
            if (!string.Equals(manifestDir, updaterDir, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Launcher update manifest must be in the same folder as the updater executable.");
            }

            if (string.IsNullOrWhiteSpace(workRoot)
                || string.IsNullOrWhiteSpace(installRoot)
                || string.IsNullOrWhiteSpace(stageRoot)
                || string.IsNullOrWhiteSpace(exePath))
            {
                throw new InvalidOperationException("Launcher update manifest is missing required paths.");
            }

            string workFull = Path.GetFullPath(workRoot);
            string installFull = Path.GetFullPath(installRoot);
            string stageFull = Path.GetFullPath(stageRoot);
            string exeFull = Path.GetFullPath(exePath);

            if (!string.Equals(workFull, manifestDir, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Launcher update workRoot must match the manifest directory.");
            }

            if (!IsPathWithinBase(workFull, stageFull))
            {
                throw new InvalidOperationException(
                    "Launcher update stageRoot must be under workRoot.");
            }

            if (!IsPathWithinBase(installFull, workFull))
            {
                throw new InvalidOperationException(
                    "Launcher update workRoot must be under installRoot.");
            }

            string expectedExe = Path.Combine(installFull, LauncherMainExecutableFileName);
            if (!string.Equals(exeFull, expectedExe, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "Launcher update exePath must be SmartGoldbergEmu.exe under installRoot.");
            }

            if (!File.Exists(expectedExe))
            {
                throw new InvalidOperationException(
                    "Launcher update installRoot does not contain SmartGoldbergEmu.exe.");
            }
        }

        public static string ResolveUpdaterDirectory()
        {
            string location = Assembly.GetExecutingAssembly().Location;
            if (string.IsNullOrEmpty(location))
                throw new InvalidOperationException("Could not resolve updater assembly location.");

            string directory = Path.GetDirectoryName(location);
            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException("Could not resolve updater directory.");

            return Path.GetFullPath(directory);
        }

        public static bool IsPathWithinBase(string basePath, string resolvedPath)
        {
            if (string.IsNullOrWhiteSpace(basePath) || string.IsNullOrWhiteSpace(resolvedPath))
                return false;

            try
            {
                string normalizedBase = Path.GetFullPath(basePath);
                string normalizedResolved = Path.GetFullPath(resolvedPath);
                if (!normalizedBase.EndsWith(Path.DirectorySeparatorChar.ToString())
                    && !normalizedBase.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
                {
                    normalizedBase += Path.DirectorySeparatorChar;
                }

                return normalizedResolved.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(
                        Path.GetFullPath(resolvedPath),
                        Path.GetFullPath(basePath),
                        StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }
    }
}
