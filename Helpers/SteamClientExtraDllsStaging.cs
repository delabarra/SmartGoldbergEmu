using System;
using System.IO;
using SmartGoldbergEmu.Constants;

namespace SmartGoldbergEmu.Helpers
{
    // Copies goldberg/steamclient_experimental/extra_dlls into per-game steam_settings/load_dlls before launch.
    // Flat top-level only: Goldberg load_dlls scans that folder non-recursively (LoadLibraryW).
    // Skips upstream extra_dlls inject samples; those must not go in load_dlls.
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

            try
            {
                foreach (string sourcePath in Directory.GetFiles(extraDllsSourceDirectory, "*.dll", SearchOption.TopDirectoryOnly))
                {
                    if (GoldbergInstallLayout.IsShippedSteamClientExtraDll(sourcePath))
                        continue;
                    if (!LoadDllsArchFilter.MatchesProcessArchitecture(sourcePath, useX64))
                        continue;

                    Directory.CreateDirectory(loadDllsDestinationDirectory);
                    string destPath = Path.Combine(loadDllsDestinationDirectory, Path.GetFileName(sourcePath));
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
