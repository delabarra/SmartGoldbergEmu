using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;
using SmartGoldbergEmu.Constants;

namespace SmartGoldbergEmu.Helpers
{
    public static class SteamInstallationPathHelper
    {
        public static string GetLocalMachineSteamInstallPath()
        {
            try
            {
                foreach (string subKeyName in new[] { SteamClientRegistryKeyPaths.LocalMachineSteamClientWow64, SteamClientRegistryKeyPaths.LocalMachineSteamClient })
                {
                    using (RegistryKey key = Registry.LocalMachine.OpenSubKey(subKeyName))
                    {
                        string installPath = key?.GetValue(SteamClientRegistryValueNames.InstallPath) as string;
                        string normalized = NormalizeSteamRoot(installPath);
                        if (!string.IsNullOrEmpty(normalized))
                            return normalized;
                    }
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        // HKCU SteamPath only when that root directory exists (library / VDF discovery).
        public static string ResolveSteamRootFromCurrentUserIfPresent()
        {
            try
            {
                using (RegistryKey steamKey = Registry.CurrentUser.OpenSubKey(SteamClientRegistryKeyPaths.CurrentUserSteamClient))
                {
                    string steamPath = steamKey?.GetValue(SteamClientRegistryValueNames.SteamPath) as string;
                    string normalized = NormalizeSteamRoot(steamPath);
                    if (!string.IsNullOrEmpty(normalized) && Directory.Exists(normalized))
                        return normalized;
                }
            }
            catch
            {
            }

            return null;
        }

        public static string ResolveSteamClientUiSoundsDirectory()
        {
            try
            {
                using (RegistryKey steamKey = Registry.CurrentUser.OpenSubKey(SteamClientRegistryKeyPaths.CurrentUserSteamClient))
                {
                    string steamPath = steamKey?.GetValue(SteamClientRegistryValueNames.SteamPath) as string;
                    string candidate = PathConstants.CombineSteamClientUiSoundsPath(NormalizeSteamRoot(steamPath));
                    if (!string.IsNullOrEmpty(candidate) && Directory.Exists(candidate))
                        return candidate;
                }
            }
            catch
            {
            }

            string lmRoot = GetLocalMachineSteamInstallPath();
            string lmSounds = PathConstants.CombineSteamClientUiSoundsPath(lmRoot);
            if (!string.IsNullOrEmpty(lmSounds) && Directory.Exists(lmSounds))
                return lmSounds;

            return PathConstants.CombineSteamClientUiSoundsPath(PathConstants.GetProgramFilesX86DefaultSteamInstallationRoot());
        }

        // Copies Steam steamui\sounds WAVs into global sounds (original names) and overlay copies for the emulator.
        public static bool TryCopyOverlayNotificationSoundsFromSteam(string targetSoundsPath)
        {
            if (string.IsNullOrWhiteSpace(targetSoundsPath))
                return false;

            string steamUiSoundsPath = ResolveSteamClientUiSoundsDirectory();
            if (string.IsNullOrEmpty(steamUiSoundsPath) || !Directory.Exists(steamUiSoundsPath))
                return false;

            try
            {
                Directory.CreateDirectory(targetSoundsPath);

                CopySteamUiSoundIfPresent(
                    steamUiSoundsPath,
                    targetSoundsPath,
                    PathConstants.SteamClientUiAchievementSourceWav,
                    PathConstants.SteamClientUiAchievementNotificationWav);
                CopySteamUiSoundIfPresent(
                    steamUiSoundsPath,
                    targetSoundsPath,
                    PathConstants.SteamClientUiFriendSourceWav,
                    PathConstants.SteamClientUiFriendNotificationWav);

                return File.Exists(Path.Combine(targetSoundsPath, PathConstants.SteamClientUiAchievementNotificationWav))
                    && File.Exists(Path.Combine(targetSoundsPath, PathConstants.SteamClientUiFriendNotificationWav));
            }
            catch
            {
                return false;
            }
        }

        private static void CopySteamUiSoundIfPresent(
            string steamUiSoundsPath,
            string targetSoundsPath,
            string sourceFileName,
            string overlayFileName)
        {
            string sourcePath = Path.Combine(steamUiSoundsPath, sourceFileName);
            if (!File.Exists(sourcePath))
                return;

            string libraryPath = Path.Combine(targetSoundsPath, sourceFileName);
            if (!File.Exists(libraryPath))
                File.Copy(sourcePath, libraryPath);

            string overlayPath = Path.Combine(targetSoundsPath, overlayFileName);
            if (!File.Exists(overlayPath))
                File.Copy(sourcePath, overlayPath);
        }

        public static bool TryResolveSteamDllSourcePath(out string steamDllPath)
        {
            steamDllPath = null;
            foreach (string root in EnumerateSteamInstallationRootsInProbeOrder())
            {
                string candidate = Path.Combine(root, PathConstants.GoldbergSteamDllFileName);
                if (File.Exists(candidate))
                {
                    steamDllPath = candidate;
                    return true;
                }
            }

            return false;
        }

        public static bool TryRefreshSteamDllInGoldbergFolder()
        {
            TryEnsureSteamDllFromSteamClient(PathConstants.GoldbergSteamOldDirectory, out _);
            TryEnsureSteamClientOriginalBackup(PathConstants.GoldbergSteamOldDirectory, out _);
            return IsSteamDllPresentInGoldbergFolder();
        }

        // IfAbsent: copy Steam.dll from the local Steam client into goldberg\steam_old.
        // CDN bins_win32 extract is soft-fail in EnsureGlobalConfigFilesExistAsync; never use fork zip Steam.dll.
        public static bool TryEnsureSteamDllFromSteamClient(string targetDirectory, out string errorMessage)
        {
            errorMessage = null;
            if (string.IsNullOrWhiteSpace(targetDirectory))
            {
                errorMessage = "Target directory for Steam.dll is empty.";
                return false;
            }

            string dest = Path.Combine(targetDirectory, PathConstants.GoldbergSteamDllFileName);
            if (File.Exists(dest))
                return true;

            if (!TryResolveSteamDllSourcePath(out string steamClientDllPath))
            {
                errorMessage = "Steam.dll was not found in a local Steam installation.";
                return false;
            }

            try
            {
                Directory.CreateDirectory(targetDirectory);
                File.Copy(steamClientDllPath, dest, false);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Failed to copy Steam.dll from the Steam client: " + ex.Message;
                return false;
            }
        }

        public static bool IsSteamDllPresentInGoldbergFolder()
        {
            return File.Exists(PathConstants.CombineGoldbergSteamDllPath());
        }

        public static bool TryResolveSteamUserDataDirectoryForSteam64(string steamId64, out string userDataPath)
        {
            userDataPath = null;
            if (!SteamIdHelper.TryGetSteam3AccountId(steamId64, out string steam3AccountId))
                return false;

            string steamRoot = ResolveSteamRootFromCurrentUserIfPresent();
            if (string.IsNullOrEmpty(steamRoot))
                steamRoot = GetLocalMachineSteamInstallPath();
            if (string.IsNullOrEmpty(steamRoot))
                return false;

            userDataPath = PathConstants.CombineSteamUserDataAccountPath(steamRoot, steam3AccountId);
            return !string.IsNullOrEmpty(userDataPath);
        }

        public static bool TryResolveSteamUserDataGamePath(string steamId64, ulong appId, out string gameDataPath)
        {
            gameDataPath = null;
            if (appId == 0 || !SteamIdHelper.TryGetSteam3AccountId(steamId64, out string steam3AccountId))
                return false;

            string steamRoot = ResolveSteamRootFromCurrentUserIfPresent();
            if (string.IsNullOrEmpty(steamRoot))
                steamRoot = GetLocalMachineSteamInstallPath();
            if (string.IsNullOrEmpty(steamRoot))
                return false;

            gameDataPath = PathConstants.CombineSteamUserDataGamePath(steamRoot, steam3AccountId, appId);
            return !string.IsNullOrEmpty(gameDataPath);
        }

        // IfAbsent: prefer an existing goldberg\steam_old\Steam.dll; otherwise copy from the Steam client.
        public static bool TrySyncSteamDllToDirectory(string targetDirectory, out string errorMessage)
        {
            errorMessage = null;
            if (string.IsNullOrWhiteSpace(targetDirectory))
            {
                errorMessage = "Target directory for Steam.dll is empty.";
                return false;
            }

            string goldbergSteamDllPath = Path.Combine(targetDirectory, PathConstants.GoldbergSteamDllFileName);
            if (File.Exists(goldbergSteamDllPath))
            {
                TryEnsureSteamClientOriginalBackup(targetDirectory, out _);
                return true;
            }

            if (TryEnsureSteamDllFromSteamClient(targetDirectory, out errorMessage))
            {
                TryEnsureSteamClientOriginalBackup(targetDirectory, out _);
                return true;
            }

            if (string.IsNullOrEmpty(errorMessage))
                errorMessage = "Steam.dll is missing from goldberg\\steam_old and could not be copied from Steam.";
            return false;
        }

        public static bool TryEnsureSteamClientOriginalBackup(string targetDirectory, out string errorMessage)
        {
            errorMessage = null;
            if (string.IsNullOrWhiteSpace(targetDirectory))
            {
                errorMessage = "Target directory for Steam.dll backup is empty.";
                return false;
            }

            if (!TryResolveSteamDllSourcePath(out string steamClientDllPath))
                return false;

            try
            {
                Directory.CreateDirectory(targetDirectory);
                string backupPath = Path.Combine(targetDirectory, PathConstants.GoldbergSteamOriginalDllFileName);
                if (File.Exists(backupPath))
                    return true;

                File.Copy(steamClientDllPath, backupPath, false);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Failed to copy Steam.dll backup from the Steam client: " + ex.Message;
                return false;
            }
        }

        private static IEnumerable<string> EnumerateSteamInstallationRootsInProbeOrder()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string hkcuRoot = ResolveSteamRootFromCurrentUserIfPresent();
            if (TryAddSteamRoot(seen, hkcuRoot, out string normalizedHkcu))
                yield return normalizedHkcu;

            string lmRoot = GetLocalMachineSteamInstallPath();
            if (TryAddSteamRoot(seen, lmRoot, out string normalizedLm))
                yield return normalizedLm;

            string defaultRoot = PathConstants.GetProgramFilesX86DefaultSteamInstallationRoot();
            if (TryAddSteamRoot(seen, defaultRoot, out string normalizedDefault))
                yield return normalizedDefault;
        }

        private static bool TryAddSteamRoot(HashSet<string> seen, string root, out string normalized)
        {
            normalized = NormalizeSteamRoot(root);
            if (string.IsNullOrEmpty(normalized) || !Directory.Exists(normalized))
                return false;
            if (!seen.Add(normalized))
                return false;
            return true;
        }

        private static string NormalizeSteamRoot(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;
            return path.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }
}
