using System;
using System.IO;
using Microsoft.Win32;
using SmartGoldbergEmu.Constants;

namespace SmartGoldbergEmu.Helpers
{
    // HKCU ActiveProcess for Goldberg (both registry views on WOW64 so 32- and 64-bit games see the same values).
    public static class SteamActiveProcessRegistryHelper
    {
        public static bool TrySetGoldbergClientDllPaths(string clientDirectory, out string errorMessage)
        {
            errorMessage = null;
            if (string.IsNullOrWhiteSpace(clientDirectory))
            {
                errorMessage = "ActiveProcess client directory is empty.";
                return false;
            }

            string directoryFull = Path.GetFullPath(clientDirectory.Trim());
            string client32Path = Path.Combine(directoryFull, PathConstants.GoldbergSteamClientDll32);
            string client64Path = Path.Combine(directoryFull, PathConstants.GoldbergSteamClientDll64);
            bool has32 = File.Exists(client32Path);
            bool has64 = File.Exists(client64Path);
            if (!has32 && !has64)
            {
                errorMessage = "No steamclient DLL was found in " + directoryFull;
                return false;
            }

            try
            {
                ForEachActiveProcessKey(key =>
                {
                    if (has32)
                    {
                        key.SetValue(
                            SteamClientRegistryValueNames.ActiveProcessSteamClientDll,
                            client32Path,
                            RegistryValueKind.String);
                    }

                    if (has64)
                    {
                        key.SetValue(
                            SteamClientRegistryValueNames.ActiveProcessSteamClientDll64,
                            client64Path,
                            RegistryValueKind.String);
                    }

                    key.Flush();
                });

                return true;
            }
            catch (UnauthorizedAccessException ex)
            {
                errorMessage = ex.Message;
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public static void SetActiveProcessPid(int processId)
        {
            ForEachActiveProcessKey(key =>
            {
                key.SetValue(SteamClientRegistryValueNames.ActiveProcessPid, processId, RegistryValueKind.DWord);
                key.Flush();
            });
        }

        public static void RestoreSteamClientDllPathsToSteamInstall()
        {
            try
            {
                string steamExePath = null;
                using (RegistryKey steamKey = Registry.CurrentUser.OpenSubKey(SteamClientRegistryKeyPaths.CurrentUserSteamClient))
                    steamExePath = steamKey?.GetValue(SteamClientRegistryValueNames.SteamExe)?.ToString();

                if (string.IsNullOrEmpty(steamExePath) || !File.Exists(steamExePath))
                    return;

                string steamDirectory = Path.GetDirectoryName(steamExePath);
                if (string.IsNullOrEmpty(steamDirectory))
                    return;

                string real32 = Path.Combine(steamDirectory, PathConstants.GoldbergSteamClientDll32);
                string real64 = Path.Combine(steamDirectory, PathConstants.GoldbergSteamClientDll64);

                ForEachActiveProcessKey(key =>
                {
                    if (File.Exists(real32))
                    {
                        key.SetValue(
                            SteamClientRegistryValueNames.ActiveProcessSteamClientDll,
                            real32,
                            RegistryValueKind.String);
                    }

                    if (File.Exists(real64))
                    {
                        key.SetValue(
                            SteamClientRegistryValueNames.ActiveProcessSteamClientDll64,
                            real64,
                            RegistryValueKind.String);
                    }

                    key.Flush();
                });
            }
            catch
            {
            }
        }

        private static void ForEachActiveProcessKey(Action<RegistryKey> action)
        {
            if (action == null)
                return;

            using (RegistryKey defaultKey = Registry.CurrentUser.CreateSubKey(SteamClientRegistryKeyPaths.CurrentUserSteamActiveProcess))
            {
                if (defaultKey != null)
                    action(defaultKey);
            }

            if (!Environment.Is64BitOperatingSystem)
                return;

            using (RegistryKey base32 = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry32))
            using (RegistryKey key32 = base32.CreateSubKey(SteamClientRegistryKeyPaths.CurrentUserSteamActiveProcess))
            {
                if (key32 != null)
                    action(key32);
            }

            using (RegistryKey base64 = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64))
            using (RegistryKey key64 = base64.CreateSubKey(SteamClientRegistryKeyPaths.CurrentUserSteamActiveProcess))
            {
                if (key64 != null)
                    action(key64);
            }
        }
    }
}
