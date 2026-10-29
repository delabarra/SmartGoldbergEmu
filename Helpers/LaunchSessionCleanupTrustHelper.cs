using System;
using System.IO;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Helpers
{
    // Constrains launch-cleanup manifests and deploy paths so orphaned/poisoned JSON cannot delete or overwrite arbitrary files.
    public static class LaunchSessionCleanupTrustHelper
    {
        private static readonly string[] AllowedDeployFileNames =
        {
            PathConstants.GoldbergStandardSteamApiDll32,
            PathConstants.GoldbergStandardSteamApiDll64,
            PathConstants.GoldbergSteamClientDll32,
            PathConstants.GoldbergSteamClientDll64,
            PathConstants.GoldbergGameOverlayRendererDll32,
            PathConstants.GoldbergGameOverlayRendererDll64,
            PathConstants.GoldbergSteamDllFileName,
        };

        public static bool IsAllowedManifestPath(string manifestPath)
        {
            if (string.IsNullOrWhiteSpace(manifestPath))
                return false;

            try
            {
                string full = Path.GetFullPath(manifestPath.Trim().Trim('"'));
                if (!PathValidationHelper.IsPathWithinBase(PathConstants.LaunchSessionManifestDirectory, full))
                    return false;

                string fileName = Path.GetFileName(full);
                if (string.IsNullOrEmpty(fileName)
                    || !fileName.EndsWith(PathConstants.LaunchSessionManifestFileExtension, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                string stem = fileName.Substring(
                    0,
                    fileName.Length - PathConstants.LaunchSessionManifestFileExtension.Length);
                return ulong.TryParse(stem, out ulong appId) && appId != 0;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryGetAppIdFromManifestPath(string manifestPath, out ulong appId)
        {
            appId = 0;
            if (!IsAllowedManifestPath(manifestPath))
                return false;

            try
            {
                string fileName = Path.GetFileName(Path.GetFullPath(manifestPath.Trim().Trim('"')));
                string stem = fileName.Substring(
                    0,
                    fileName.Length - PathConstants.LaunchSessionManifestFileExtension.Length);
                return ulong.TryParse(stem, out appId) && appId != 0;
            }
            catch
            {
                return false;
            }
        }

        public static bool SessionMatchesManifestPath(PersistedLaunchSession session, string manifestPath)
        {
            if (session == null || session.AppId == 0)
                return false;
            if (!TryGetAppIdFromManifestPath(manifestPath, out ulong pathAppId))
                return false;
            return pathAppId == session.AppId;
        }

        public static bool IsAllowedDeployedFile(PersistedFileDeployment file)
        {
            if (file == null || string.IsNullOrWhiteSpace(file.TargetPath))
                return false;

            try
            {
                string targetFull = Path.GetFullPath(file.TargetPath);
                string targetName = Path.GetFileName(targetFull);
                if (!IsAllowedDeployFileName(targetName))
                    return false;

                if (string.IsNullOrWhiteSpace(file.BackupPath))
                    return !file.HadOriginal;

                string backupFull = Path.GetFullPath(file.BackupPath);
                string expectedBackup = targetFull + PathConstants.SteamApiBackupSidecarExtension;
                if (!string.Equals(backupFull, expectedBackup, StringComparison.OrdinalIgnoreCase))
                    return false;

                string targetDir = Path.GetDirectoryName(targetFull);
                string backupDir = Path.GetDirectoryName(backupFull);
                return !string.IsNullOrEmpty(targetDir)
                    && string.Equals(targetDir, backupDir, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static bool IsAllowedSteamSettingsJunctionPath(string junctionPath)
        {
            if (string.IsNullOrWhiteSpace(junctionPath))
                return true;

            try
            {
                string full = Path.GetFullPath(junctionPath);
                return string.Equals(
                    Path.GetFileName(full),
                    PathConstants.SteamSettingsFolderName,
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static bool IsAllowedSteamSettingsBackupPath(string backupPath)
        {
            if (string.IsNullOrWhiteSpace(backupPath))
                return true;

            try
            {
                string full = Path.GetFullPath(backupPath);
                string expectedName = PathConstants.SteamSettingsFolderName + PathConstants.SteamApiBackupSidecarExtension;
                return string.Equals(Path.GetFileName(full), expectedName, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static bool IsAllowedSourceModPreviousValue(string previousValue)
        {
            if (string.IsNullOrWhiteSpace(previousValue))
                return true;

            try
            {
                string full = Path.GetFullPath(previousValue.Trim());
                string steamRoot = SteamInstallationPathHelper.GetLocalMachineSteamInstallPath();
                if (!string.IsNullOrEmpty(steamRoot) && PathValidationHelper.IsPathWithinBase(steamRoot, full))
                    return true;

                string cuRoot = SteamInstallationPathHelper.ResolveSteamRootFromCurrentUserIfPresent();
                if (!string.IsNullOrEmpty(cuRoot) && PathValidationHelper.IsPathWithinBase(cuRoot, full))
                    return true;

                return false;
            }
            catch
            {
                return false;
            }
        }

        // games/{appId} only — allows a watcher whose AppBaseDirectory differs from the launcher that wrote the session.
        public static bool IsAllowedGameLibraryFolder(ulong appId, string gameLibraryFolder)
        {
            if (appId == 0 || string.IsNullOrWhiteSpace(gameLibraryFolder))
                return false;

            try
            {
                string full = Path.GetFullPath(gameLibraryFolder.Trim());
                if (!string.Equals(Path.GetFileName(full), appId.ToString(), StringComparison.OrdinalIgnoreCase))
                    return false;

                string gamesParent = Path.GetDirectoryName(full);
                return !string.IsNullOrEmpty(gamesParent)
                    && string.Equals(
                        Path.GetFileName(gamesParent),
                        PathConstants.GamesDirectoryFolderName,
                        StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        // games/{appId}/steam_settings/load_dlls only.
        public static bool IsAllowedLoadDllsFolder(ulong appId, string loadDllsFolder)
        {
            if (appId == 0 || string.IsNullOrWhiteSpace(loadDllsFolder))
                return false;

            try
            {
                string full = Path.GetFullPath(loadDllsFolder.Trim());
                if (!string.Equals(
                        Path.GetFileName(full),
                        PathConstants.GoldbergLoadDllsFolderName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                string steamSettings = Path.GetDirectoryName(full);
                if (string.IsNullOrEmpty(steamSettings)
                    || !string.Equals(
                        Path.GetFileName(steamSettings),
                        PathConstants.SteamSettingsFolderName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                return IsAllowedGameLibraryFolder(appId, Path.GetDirectoryName(steamSettings));
            }
            catch
            {
                return false;
            }
        }

        private static bool IsAllowedDeployFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return false;

            foreach (string allowed in AllowedDeployFileNames)
            {
                if (string.Equals(fileName, allowed, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
