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
    }
}
