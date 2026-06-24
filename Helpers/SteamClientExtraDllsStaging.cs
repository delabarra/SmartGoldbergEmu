using System;
using System.IO;
using SmartGoldbergEmu.Constants;

namespace SmartGoldbergEmu.Helpers
{
    // Copies goldberg/steamclient_extra_dlls into per-game steam_settings/load_dlls before launch.
    public static class SteamClientExtraDllsStaging
    {
        public static bool TryStageIntoLoadDllsFolder(
            string extraDllsSourceDirectory,
            string loadDllsDestinationDirectory,
            bool useX64,
            out int copiedFileCount)
        {
            copiedFileCount = 0;
            if (string.IsNullOrWhiteSpace(extraDllsSourceDirectory) || !Directory.Exists(extraDllsSourceDirectory))
                return false;
            if (string.IsNullOrWhiteSpace(loadDllsDestinationDirectory))
                return false;

            Directory.CreateDirectory(loadDllsDestinationDirectory);

            string loadOrderSource = Path.Combine(extraDllsSourceDirectory, PathConstants.GoldbergLoadDllsLoadOrderFileName);
            string loadOrderDest = Path.Combine(loadDllsDestinationDirectory, PathConstants.GoldbergLoadDllsLoadOrderFileName);
            if (File.Exists(loadOrderSource) && !File.Exists(loadOrderDest))
            {
                File.Copy(loadOrderSource, loadOrderDest);
                copiedFileCount++;
            }

            string sourceRoot = Path.GetFullPath(extraDllsSourceDirectory);
            if (!sourceRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                sourceRoot += Path.DirectorySeparatorChar;

            try
            {
                foreach (string sourcePath in Directory.GetFiles(extraDllsSourceDirectory, "*.dll", SearchOption.AllDirectories))
                {
                    if (!LoadDllsArchFilter.MatchesProcessArchitecture(sourcePath, useX64))
                        continue;

                    string fullSource = Path.GetFullPath(sourcePath);
                    if (!fullSource.StartsWith(sourceRoot, StringComparison.OrdinalIgnoreCase))
                        continue;

                    string relative = fullSource.Substring(sourceRoot.Length);
                    string destPath = Path.Combine(loadDllsDestinationDirectory, relative);
                    string destDir = Path.GetDirectoryName(destPath);
                    if (!string.IsNullOrEmpty(destDir))
                        Directory.CreateDirectory(destDir);

                    if (File.Exists(destPath))
                        continue;

                    File.Copy(sourcePath, destPath);
                    copiedFileCount++;
                }
            }
            catch
            {
                return copiedFileCount > 0;
            }

            return copiedFileCount > 0;
        }
    }
}
