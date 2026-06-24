using System;
using System.IO;
using Microsoft.Win32;
using SmartGoldbergEmu.Constants;

namespace SmartGoldbergEmu.Helpers
{
    // HKCU SourceModInstallPath session override (gbe_fork steamclient_experimental README).
    public static class SteamSourceModRegistryHelper
    {
        public sealed class RestoreToken
        {
            internal bool HadPreviousValue;
            internal string PreviousValue;
        }

        public static bool TryApplySourceModInstallPath(string folderContainingSteamClient, out RestoreToken restoreToken, out string error)
        {
            restoreToken = null;
            error = null;
            if (string.IsNullOrWhiteSpace(folderContainingSteamClient))
            {
                error = "SourceMod install folder is empty.";
                return false;
            }

            try
            {
                string normalized = Path.GetFullPath(folderContainingSteamClient.Trim());
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(SteamClientRegistryKeyPaths.CurrentUserSteamClient))
                {
                    if (key == null)
                    {
                        error = "Failed to open Steam registry key.";
                        return false;
                    }

                    object existing = key.GetValue(SteamClientRegistryValueNames.SourceModInstallPath);
                    restoreToken = new RestoreToken
                    {
                        HadPreviousValue = existing != null,
                        PreviousValue = existing?.ToString()
                    };
                    key.SetValue(SteamClientRegistryValueNames.SourceModInstallPath, normalized, RegistryValueKind.String);
                }

                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                error = ex.Message;
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static void RestoreSourceModInstallPath(bool hadPreviousValue, string previousValue)
        {
            RestoreSourceModInstallPath(new RestoreToken
            {
                HadPreviousValue = hadPreviousValue,
                PreviousValue = previousValue,
            });
        }

        public static void RestoreSourceModInstallPath(RestoreToken restoreToken)
        {
            if (restoreToken == null)
                return;

            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(SteamClientRegistryKeyPaths.CurrentUserSteamClient, writable: true))
                {
                    if (key == null)
                        return;

                    if (restoreToken.HadPreviousValue)
                        key.SetValue(SteamClientRegistryValueNames.SourceModInstallPath, restoreToken.PreviousValue ?? string.Empty, RegistryValueKind.String);
                    else
                        key.DeleteValue(SteamClientRegistryValueNames.SourceModInstallPath, throwOnMissingValue: false);
                }
            }
            catch
            {
            }
        }
    }
}
