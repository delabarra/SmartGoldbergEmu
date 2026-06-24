using System;
using System.IO;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Helpers
{
    public static class GoldbergSavePathHelper
    {
        public static string FormatSteamUserdataDisplayPath(string accountSteamId, ulong appId = 0)
        {
            if (!SteamIdHelper.TryGetSteam3AccountId(accountSteamId, out string steam3AccountId))
                return string.Empty;

            string appSegment = appId != 0 ? appId.ToString() : "appId";
            return string.Format(ApplicationConstants.SteamUserdataPathDisplayFormat, steam3AccountId, appSegment);
        }

        public static bool TryEnsureSteamUserdataAccountDirectory(string accountSteamId)
        {
            if (!TryResolveSteamUserdataAccountDirectory(accountSteamId, out string accountDirectoryPath))
                return false;

            try
            {
                Directory.CreateDirectory(accountDirectoryPath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryResolveSteamUserdataAccountDirectory(string accountSteamId, out string accountDirectoryPath)
        {
            accountDirectoryPath = null;
            if (!SteamInstallationPathHelper.TryResolveSteamUserDataDirectoryForSteam64(accountSteamId, out string resolved))
                return false;

            try
            {
                accountDirectoryPath = Path.GetFullPath(resolved);
            }
            catch
            {
                accountDirectoryPath = resolved;
            }

            return !string.IsNullOrEmpty(accountDirectoryPath);
        }

        public static bool UsesSteamUserdataLayout(string localSavePath, string accountSteamId)
        {
            if (string.IsNullOrWhiteSpace(localSavePath) || string.IsNullOrWhiteSpace(accountSteamId))
                return false;

            if (!SteamInstallationPathHelper.TryResolveSteamUserDataDirectoryForSteam64(accountSteamId, out string expected))
                return false;

            return PathsEqual(localSavePath, expected);
        }

        public static bool PathsEqual(string a, string b)
        {
            try
            {
                return string.Equals(
                    Path.GetFullPath(a.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
                    Path.GetFullPath(b.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        public static bool IsPortableLocalSavePath(string localSavePath)
        {
            if (string.IsNullOrEmpty(localSavePath))
                return false;

            string normalizedPath = localSavePath.Trim();
            return normalizedPath == "." ||
                   normalizedPath == "./" ||
                   normalizedPath.StartsWith("./", StringComparison.Ordinal) ||
                   (!Path.IsPathRooted(normalizedPath) && !normalizedPath.Contains(":"));
        }

        public static ValidationResult ValidateCustomLocalSavePath(string customPath, string accountSteamId = null)
        {
            if (string.IsNullOrWhiteSpace(customPath))
                return ValidationResult.Failure("Custom Path requires a folder path. Enter a path or choose Browse.");

            string trimmed = customPath.Trim();

            if (IsPortableLocalSavePath(trimmed))
                return ValidationResult.Failure("Custom Path requires an absolute folder path. Use Portable for game-folder saves.");

            if (UsesSteamUserdataLayout(trimmed, accountSteamId))
                return ValidationResult.Failure("Use Steam userdata (Steam client) for Steam client save locations.");

            if (trimmed.StartsWith("%appdata%", StringComparison.OrdinalIgnoreCase))
                return ValidationResult.Failure("Enter a real folder path for Custom Path, or choose Default (AppData) instead.");

            if (!Path.IsPathRooted(trimmed))
                return ValidationResult.Failure("Custom Path requires an absolute folder path.");

            if (!PathValidationHelper.IsSafeFilePath(trimmed))
                return ValidationResult.Failure("The custom save path is not a valid folder path.");

            return ValidationResult.Success();
        }

        public static string ResolveLocalSavePathForGoldbergIni(string localSavePath, string accountSteamId)
        {
            if (string.IsNullOrWhiteSpace(localSavePath))
                return string.Empty;

            string trimmed = localSavePath.Trim();
            if (UsesSteamUserdataLayout(trimmed, accountSteamId))
            {
                if (TryResolveSteamUserdataAccountDirectory(accountSteamId, out string accountPath))
                    return accountPath;

                return string.Empty;
            }

            if (IsPortableLocalSavePath(trimmed))
            {
                if (trimmed == "." || string.Equals(trimmed, "./", StringComparison.Ordinal))
                    return "./";
                return trimmed.Replace('\\', '/').TrimEnd('/');
            }

            return trimmed.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        public static string NormalizePersistedLocalSavePath(string localSavePath, string accountSteamId)
        {
            return ResolveLocalSavePathForGoldbergIni(localSavePath, accountSteamId);
        }

        public static string ResolveGameSavesPath(UserSettings user, ulong appId, string relativeToGameDllDirectory)
        {
            if (appId == 0)
                return string.Empty;

            string savesFolderName = string.IsNullOrWhiteSpace(user?.SavesFolderName)
                ? ApplicationConstants.DefaultSavesFolderName
                : user.SavesFolderName.Trim();
            string localSavePath = user?.LocalSavePath?.Trim() ?? string.Empty;

            if (UsesSteamUserdataLayout(localSavePath, user?.AccountSteamId))
            {
                string steamId = user?.AccountSteamId;
                if (!string.IsNullOrWhiteSpace(steamId)
                    && SteamInstallationPathHelper.TryResolveSteamUserDataGamePath(steamId, appId, out string steamGamePath))
                    return steamGamePath;

                return PathConstants.GetUserSavesPath(savesFolderName, appId);
            }

            if (string.IsNullOrEmpty(localSavePath))
                return PathConstants.GetUserSavesPath(savesFolderName, appId);

            if (IsPortableLocalSavePath(localSavePath))
            {
                if (string.IsNullOrEmpty(relativeToGameDllDirectory))
                    return string.Empty;

                return Path.Combine(relativeToGameDllDirectory, savesFolderName, appId.ToString());
            }

            string basePath = Path.IsPathRooted(localSavePath)
                ? localSavePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                : Path.Combine(
                    !string.IsNullOrEmpty(relativeToGameDllDirectory)
                        ? relativeToGameDllDirectory
                        : PathConstants.AppBaseDirectory,
                    localSavePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            return Path.Combine(basePath, savesFolderName, appId.ToString());
        }
    }
}
