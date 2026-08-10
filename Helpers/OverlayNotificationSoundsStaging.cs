using System;
using System.IO;
using SmartGoldbergEmu.Constants;

namespace SmartGoldbergEmu.Helpers
{
    // Copies global overlay notification WAVs into per-game steam_settings/sounds before launch.
    // Goldberg checks local steam_settings/sounds first; with local_save_path set it ignores AppData global settings.
    public static class OverlayNotificationSoundsStaging
    {
        private static readonly string[] OverlaySoundFileNames =
        {
            PathConstants.SteamClientUiAchievementNotificationWav,
            PathConstants.SteamClientUiFriendNotificationWav,
        };

        public static bool TryStageIntoGameSoundsFolder(
            string globalSoundsSourceDirectory,
            string gameSoundsDestinationDirectory,
            out int copiedFileCount)
        {
            copiedFileCount = 0;
            if (string.IsNullOrWhiteSpace(globalSoundsSourceDirectory) || !Directory.Exists(globalSoundsSourceDirectory))
                return false;
            if (string.IsNullOrWhiteSpace(gameSoundsDestinationDirectory))
                return false;

            Directory.CreateDirectory(gameSoundsDestinationDirectory);

            try
            {
                foreach (string fileName in OverlaySoundFileNames)
                {
                    string sourcePath = Path.Combine(globalSoundsSourceDirectory, fileName);
                    if (!File.Exists(sourcePath))
                        continue;

                    string destPath = Path.Combine(gameSoundsDestinationDirectory, fileName);
                    File.Copy(sourcePath, destPath, overwrite: true);
                    copiedFileCount++;
                }
            }
            catch
            {
                return copiedFileCount > 0;
            }

            return copiedFileCount > 0;
        }

        // Settings combo lists library names and hides overlay_*.wav. CDN/older installs may only
        // have overlay files — mirror them to the Steam source names so the list is not empty.
        public static void EnsureLibraryAliasesInSoundsFolder(string soundsDirectory)
        {
            if (string.IsNullOrWhiteSpace(soundsDirectory) || !Directory.Exists(soundsDirectory))
                return;

            TryCopyAliasIfMissing(
                soundsDirectory,
                PathConstants.SteamClientUiAchievementNotificationWav,
                PathConstants.SteamClientUiAchievementSourceWav);
            TryCopyAliasIfMissing(
                soundsDirectory,
                PathConstants.SteamClientUiFriendNotificationWav,
                PathConstants.SteamClientUiFriendSourceWav);
        }

        private static void TryCopyAliasIfMissing(string soundsDirectory, string overlayFileName, string libraryFileName)
        {
            try
            {
                string libraryPath = Path.Combine(soundsDirectory, libraryFileName);
                if (File.Exists(libraryPath))
                    return;

                string overlayPath = Path.Combine(soundsDirectory, overlayFileName);
                if (!File.Exists(overlayPath))
                    return;

                File.Copy(overlayPath, libraryPath);
            }
            catch
            {
            }
        }
    }
}
