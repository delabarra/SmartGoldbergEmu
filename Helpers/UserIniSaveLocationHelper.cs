using System;
using System.IO;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;

namespace SmartGoldbergEmu.Helpers
{
    // [user::saves] in configs.user.ini (gbe_fork); global configs.user.ini only — not per-game.
    public static class UserIniSaveLocationHelper
    {
        public const string SavesSection = "user::saves";
        public const string LocalSavePathKey = "local_save_path";
        public const string SavesFolderNameKey = "saves_folder_name";

        public static void ResolveGlobalSaveFields(
            out string localSavePath,
            out string savesFolderName,
            string uiLocalSavePath,
            string uiSavesFolderName,
            bool isSteamUserdataMode,
            string accountSteamId)
        {
            localSavePath = GoldbergSavePathHelper.ResolveLocalSavePathForGoldbergIni(
                uiLocalSavePath ?? string.Empty,
                accountSteamId ?? string.Empty);

            if (isSteamUserdataMode)
            {
                savesFolderName = string.Empty;
                return;
            }

            savesFolderName = (uiSavesFolderName ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(savesFolderName))
                savesFolderName = ApplicationConstants.DefaultSavesFolderName;
        }

        public static void ApplySaveLocationToIni(IniFileService iniService, IniFile iniFile, UserSettings settings)
        {
            if (iniService == null || iniFile == null || settings == null)
                return;

            string resolvedPath = GoldbergSavePathHelper.ResolveLocalSavePathForGoldbergIni(
                settings.LocalSavePath ?? string.Empty,
                settings.AccountSteamId ?? string.Empty);

            resolvedPath = ExpandPortableLocalSavePathForIni(resolvedPath, settings);

            iniService.SetValue(iniFile, SavesSection, LocalSavePathKey, resolvedPath, skipIfDefault: true);

            if (!string.IsNullOrEmpty(resolvedPath))
            {
                iniService.RemoveValue(iniFile, SavesSection, SavesFolderNameKey);
                return;
            }

            var defaults = new UserSettings();
            string folderName = string.IsNullOrWhiteSpace(settings.SavesFolderName)
                ? defaults.SavesFolderName
                : settings.SavesFolderName.Trim();

            if (string.Equals(folderName, defaults.SavesFolderName, StringComparison.Ordinal))
                iniService.RemoveValue(iniFile, SavesSection, SavesFolderNameKey);
            else
                iniService.SetValue(iniFile, SavesSection, SavesFolderNameKey, folderName);
        }

        public static bool FileContainsSaveLocationKeys(string filePath, IniFileService iniService)
        {
            if (iniService == null || string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return false;

            var iniFile = iniService.ParseFile(filePath);
            return HasSaveLocationKeys(iniFile, iniService);
        }

        public static bool HasSaveLocationKeys(IniFile iniFile, IniFileService iniService)
        {
            if (iniService == null || iniFile == null)
                return false;

            return !string.IsNullOrEmpty(iniService.GetValue(iniFile, SavesSection, LocalSavePathKey))
                || !string.IsNullOrEmpty(iniService.GetValue(iniFile, SavesSection, SavesFolderNameKey));
        }

        public static bool TryRemoveSaveLocationKeysFromFile(string filePath, IniFileService iniService)
        {
            if (iniService == null || string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return true;

            try
            {
                var iniFile = iniService.ParseFile(filePath);
                if (!TryRemoveSaveLocationKeys(iniFile, iniService))
                    return true;

                iniService.WriteFile(iniFile, filePath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // gbe_fork appends {appId} under local_save_path; bare ./ beside games/{appId} DLLs yields games/{appId}/{appId}.
        public static string ExpandPortableLocalSavePathForIni(string resolvedPath, UserSettings settings)
        {
            if (settings == null || string.IsNullOrEmpty(resolvedPath))
                return resolvedPath ?? string.Empty;

            if (resolvedPath != "." && !string.Equals(resolvedPath, "./", StringComparison.Ordinal))
                return resolvedPath;

            string folderName = string.IsNullOrWhiteSpace(settings.SavesFolderName)
                ? ApplicationConstants.DefaultSavesFolderName
                : settings.SavesFolderName.Trim();
            if (string.IsNullOrEmpty(folderName))
                return resolvedPath;

            return "./" + folderName.Replace('\\', '/').Trim('/');
        }

        public static bool TryRemoveSaveLocationKeys(IniFile iniFile, IniFileService iniService)
        {
            if (iniService == null || iniFile == null)
                return false;

            bool removed = iniService.RemoveValue(iniFile, SavesSection, LocalSavePathKey);
            removed |= iniService.RemoveValue(iniFile, SavesSection, SavesFolderNameKey);
            return removed;
        }
    }
}
