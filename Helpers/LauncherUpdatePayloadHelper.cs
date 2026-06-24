using System;
using System.IO;
using SmartGoldbergEmu.Constants;

namespace SmartGoldbergEmu.Helpers
{
    // Release zips from package-release.ps1 wrap files in SmartGoldbergEmu-{version}/; flatten before apply.
    public static class LauncherUpdatePayloadHelper
    {
        public static string ResolvePayloadRoot(string extractRoot, string launcherExeFileName)
        {
            if (string.IsNullOrWhiteSpace(extractRoot))
                throw new ArgumentException("Extract root is required.", nameof(extractRoot));

            if (string.IsNullOrWhiteSpace(launcherExeFileName))
                launcherExeFileName = PathConstants.LauncherMainExecutableFileName;

            if (File.Exists(Path.Combine(extractRoot, launcherExeFileName)))
                return extractRoot;

            string[] topLevelDirs = SafeGetDirectories(extractRoot);
            string[] topLevelFiles = SafeGetFiles(extractRoot);

            // Standard release layout: one version folder, no loose files at zip root.
            if (topLevelDirs.Length == 1 && topLevelFiles.Length == 0)
            {
                string nested = topLevelDirs[0];
                if (File.Exists(Path.Combine(nested, launcherExeFileName)))
                    return nested;
            }

            foreach (string dir in topLevelDirs)
            {
                if (File.Exists(Path.Combine(dir, launcherExeFileName)))
                    return dir;
            }

            return extractRoot;
        }

        private static string[] SafeGetDirectories(string path)
        {
            try
            {
                return Directory.GetDirectories(path);
            }
            catch (IOException)
            {
                return new string[0];
            }
            catch (UnauthorizedAccessException)
            {
                return new string[0];
            }
        }

        private static string[] SafeGetFiles(string path)
        {
            try
            {
                return Directory.GetFiles(path);
            }
            catch (IOException)
            {
                return new string[0];
            }
            catch (UnauthorizedAccessException)
            {
                return new string[0];
            }
        }
    }
}
