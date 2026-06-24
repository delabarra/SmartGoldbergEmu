using System;
using System.IO;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Validation;

namespace SmartGoldbergEmu.Helpers
{
    /// <summary>
    /// Resolves stored game executable paths relative to the game folder (StartFolder), matching Steam installdir semantics.
    /// </summary>
    public static class GameFolderPathHelper
    {
        /// <summary>
        /// Base folder for resolving stored Path and WorkingDirectory: StartFolder if it exists, otherwise the directory of an absolute Path.
        /// </summary>
        public static bool TryGetResolutionBaseFolder(GameConfig game, out string baseFolder)
        {
            baseFolder = null;
            if (game == null)
                return false;

            if (!string.IsNullOrWhiteSpace(game.StartFolder) && Directory.Exists(game.StartFolder))
            {
                baseFolder = Path.GetFullPath(game.StartFolder);
                return true;
            }

            string pathTrim = (game.Path ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(pathTrim) && Path.IsPathRooted(pathTrim))
            {
                string exeDir = Path.GetDirectoryName(pathTrim);
                if (!string.IsNullOrEmpty(exeDir) && Directory.Exists(exeDir))
                {
                    baseFolder = Path.GetFullPath(exeDir);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Base folder for launch-time path resolution (broader fallbacks than <see cref="TryGetResolutionBaseFolder"/>).
        /// </summary>
        public static string GetLaunchBaseGameFolder(GameConfig game)
        {
            if (game == null)
                return Environment.CurrentDirectory;

            if (TryGetResolutionBaseFolder(game, out string baseFolder))
                return baseFolder;

            string pathTrim = (game.Path ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(pathTrim) && Path.IsPathRooted(pathTrim))
                return Path.GetDirectoryName(pathTrim) ?? Environment.CurrentDirectory;

            if (!string.IsNullOrWhiteSpace(game.StartFolder))
                return Path.GetFullPath(game.StartFolder);

            return Path.GetDirectoryName(pathTrim) ?? Environment.CurrentDirectory;
        }

        /// <summary>
        /// Resolves a stored path relative to <paramref name="baseGameFolder"/>; returns trimmed stored path when unresolved.
        /// </summary>
        public static string ResolveStoredPathUnderBase(string baseGameFolder, string pathStored)
        {
            if (string.IsNullOrWhiteSpace(pathStored))
                return pathStored;
            string pathTrim = pathStored.Trim();
            if (!string.IsNullOrEmpty(baseGameFolder) &&
                PathValidationHelper.TryResolveAndValidatePath(baseGameFolder, pathTrim, out string resolved) &&
                File.Exists(resolved))
            {
                return resolved;
            }
            if (Path.IsPathRooted(pathTrim) && File.Exists(pathTrim))
                return Path.GetFullPath(pathTrim);
            return pathTrim;
        }

        /// <summary>
        /// Resolves the primary game executable (default launch option) to a full path when the file exists.
        /// </summary>
        public static bool TryResolvePrimaryExecutable(GameConfig game, out string fullExecutablePath)
        {
            fullExecutablePath = null;
            if (game == null || string.IsNullOrWhiteSpace(game.Path))
                return false;

            string baseGameFolder = GetLaunchBaseGameFolder(game);
            string candidate = ResolveStoredPathUnderBase(baseGameFolder, game.Path);
            if (string.IsNullOrWhiteSpace(candidate))
                return false;

            try
            {
                string full = Path.IsPathRooted(candidate)
                    ? Path.GetFullPath(candidate)
                    : Path.GetFullPath(Path.Combine(baseGameFolder ?? Environment.CurrentDirectory, candidate));

                if (!File.Exists(full))
                    return false;

                fullExecutablePath = full;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Resolves executable for Steamless: launch base, then settings fields, then library stored path.
        /// </summary>
        public static bool TryResolveExecutableForSteamless(GameConfig game, out string fullExecutablePath)
        {
            fullExecutablePath = null;
            if (game == null)
                return false;

            if (TryResolvePrimaryExecutable(game, out fullExecutablePath))
                return true;

            if (TryResolveExecutableFromGameFolderFields(game.StartFolder, game.Path, out fullExecutablePath))
                return true;

            return TryResolveStoredExecutable(game, out fullExecutablePath);
        }

        /// <summary>
        /// Resolves <see cref="GameConfig.Path"/> to a full path: relative to game folder, or legacy absolute path.
        /// </summary>
        public static bool TryResolveStoredExecutable(GameConfig game, out string fullExePath)
        {
            fullExePath = null;
            if (game == null || string.IsNullOrWhiteSpace(game.Path))
                return false;

            string pathTrim = game.Path.Trim();

            if (TryGetResolutionBaseFolder(game, out string baseFolder))
            {
                if (PathValidationHelper.TryResolveAndValidatePath(baseFolder, pathTrim, out string resolved) && File.Exists(resolved))
                {
                    fullExePath = resolved;
                    return true;
                }
            }

            if (Path.IsPathRooted(pathTrim) && File.Exists(pathTrim))
            {
                fullExePath = Path.GetFullPath(pathTrim);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Directory containing the game executable (resolved <see cref="GameConfig.Path"/> when possible).
        /// </summary>
        public static bool TryGetExecutableDirectory(GameConfig game, out string directory)
        {
            directory = null;
            if (game == null)
                return false;

            if (TryResolveStoredExecutable(game, out string fullExePath))
            {
                directory = Path.GetDirectoryName(fullExePath);
                return !string.IsNullOrEmpty(directory);
            }

            if (TryGetResolutionBaseFolder(game, out string baseFolder) &&
                TryGetDirectoryFromStoredPath(baseFolder, game.Path, out directory))
            {
                return true;
            }

            string pathTrim = (game.Path ?? string.Empty).Trim();
            if (Path.IsPathRooted(pathTrim))
            {
                directory = Path.GetDirectoryName(Path.GetFullPath(pathTrim));
                return !string.IsNullOrEmpty(directory);
            }

            return false;
        }

        private static bool TryGetDirectoryFromStoredPath(string baseFolder, string storedPath, out string directory)
        {
            directory = null;
            string pathTrim = (storedPath ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(pathTrim))
                return false;

            if (PathValidationHelper.TryResolveAndValidatePath(baseFolder, pathTrim, out string resolvedPath))
            {
                directory = Path.GetDirectoryName(resolvedPath);
                return !string.IsNullOrEmpty(directory);
            }

            directory = baseFolder;
            return true;
        }

        /// <summary>
        /// True if two games refer to the same executable (resolved paths when possible, else path + start folder text).
        /// </summary>
        public static bool ExecutablesReferToSameGame(GameConfig a, GameConfig b)
        {
            if (a == null || b == null)
                return false;

            if (TryResolveStoredExecutable(a, out string fullA) && TryResolveStoredExecutable(b, out string fullB))
                return string.Equals(fullA, fullB, StringComparison.OrdinalIgnoreCase);

            return string.Equals(CompositePathKey(a), CompositePathKey(b), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Resolves the file path used for shell icons (custom .ico/.exe or game executable): relative to the game folder when stored as relative, otherwise legacy absolute paths.
        /// </summary>
        public static bool TryResolveIconSourcePath(GameConfig game, out string fullPath)
        {
            fullPath = null;
            if (game == null)
                return false;

            string custom = (game.CustomIcon ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(custom))
            {
                if (TryGetResolutionBaseFolder(game, out string baseFolder))
                {
                    if (PathValidationHelper.TryResolveAndValidatePath(baseFolder, custom, out string resolved) && File.Exists(resolved))
                    {
                        fullPath = resolved;
                        return true;
                    }
                }

                if (Path.IsPathRooted(custom) && File.Exists(custom))
                {
                    fullPath = Path.GetFullPath(custom);
                    return true;
                }

                return false;
            }

            return TryResolveStoredExecutable(game, out fullPath);
        }

        public static string ResolveBaseFolderFromInputs(string startFolder, string executablePathOrName)
        {
            string folder = (startFolder ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                return Path.GetFullPath(folder);

            string exe = (executablePathOrName ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(exe))
                return null;

            try
            {
                string dir = Path.GetDirectoryName(exe);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    return Path.GetFullPath(dir);
            }
            catch
            {
            }

            return null;
        }

        /// <summary>
        /// If <paramref name="fullExecutablePath"/> contains a path segment equal to the Steam <c>installdir</c> folder name,
        /// returns that folder as the game root and the remainder as a relative executable path.
        /// Prefer the canonical Steam layout <c>...\steamapps\common\{installdir}\...</c> when present; otherwise first match wins.
        /// </summary>
        public static bool TrySplitExecutableAtSteamInstallDir(string fullExecutablePath, string installDirFolderName, out string gameRootFullPath, out string relativeExecutablePath)
        {
            gameRootFullPath = null;
            relativeExecutablePath = null;
            if (string.IsNullOrWhiteSpace(fullExecutablePath) || string.IsNullOrWhiteSpace(installDirFolderName))
                return false;

            string normalizedInstallDir = installDirFolderName.Trim().TrimEnd('\\', '/');
            if (normalizedInstallDir.Length == 0)
                return false;

            string full;
            try
            {
                full = Path.GetFullPath(fullExecutablePath.Trim());
            }
            catch
            {
                return false;
            }

            string root = Path.GetPathRoot(full);
            if (string.IsNullOrEmpty(root))
                return false;

            string tail = full.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.IsNullOrEmpty(tail))
                return false;

            char[] seps = { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar };
            string[] segs = tail.Split(seps, StringSplitOptions.RemoveEmptyEntries);
            if (segs.Length < 2)
                return false;

            int lastFolderIndex = segs.Length - 2;
            int chosenMatch = -1;
            int steamAppsCommonMatch = -1;
            for (int j = 0; j <= lastFolderIndex; j++)
            {
                if (string.Equals(segs[j], normalizedInstallDir, StringComparison.OrdinalIgnoreCase))
                {
                    if (chosenMatch < 0)
                        chosenMatch = j;

                    if (j >= 2 &&
                        string.Equals(segs[j - 2], PathConstants.SteamAppsDirectoryName, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(segs[j - 1], PathConstants.SteamAppsCommonDirectoryName, StringComparison.OrdinalIgnoreCase))
                    {
                        steamAppsCommonMatch = j;
                        break;
                    }
                }
            }

            int matchIndex = steamAppsCommonMatch >= 0 ? steamAppsCommonMatch : chosenMatch;
            if (matchIndex < 0)
                return false;

            string gameRoot = root;
            for (int i = 0; i <= matchIndex; i++)
                gameRoot = Path.Combine(gameRoot, segs[i]);
            gameRoot = Path.GetFullPath(gameRoot);

            var relParts = new System.Collections.Generic.List<string>();
            for (int i = matchIndex + 1; i < segs.Length; i++)
                relParts.Add(segs[i]);

            gameRootFullPath = gameRoot;
            relativeExecutablePath = string.Join(Path.DirectorySeparatorChar.ToString(), relParts.ToArray());
            return true;
        }

        /// <summary>
        /// Resolves the full path to the game executable from the folder + executable fields (as shown in settings).
        /// Uses a directory-prefix check stricter than <see cref="PathValidationHelper.IsPathWithinBase"/> so paths like
        /// <c>...\Game</c> vs <c>...\Game_DLC\file.exe</c> are not confused.
        /// </summary>
        public static bool TryResolveExecutableFromGameFolderFields(string startFolder, string executablePathOrName, out string fullExecutablePath)
        {
            fullExecutablePath = null;
            if (string.IsNullOrWhiteSpace(executablePathOrName))
                return false;

            string exeTrim = executablePathOrName.Trim();
            string folderTrim = (startFolder ?? string.Empty).Trim();

            try
            {
                if (Path.IsPathRooted(exeTrim))
                {
                    string abs = Path.GetFullPath(exeTrim);
                    if (!File.Exists(abs))
                        return false;
                    // Absolute path: use as-is so installdir checks match the real file even if the folder field is wrong/stale.
                    fullExecutablePath = abs;
                    return true;
                }

                if (string.IsNullOrEmpty(folderTrim) || !Directory.Exists(folderTrim))
                    return false;

                string baseFull = Path.GetFullPath(folderTrim);
                string combined = Path.GetFullPath(Path.Combine(baseFull, exeTrim));
                if (!File.Exists(combined))
                    return false;
                if (!IsStrictPathUnderDirectory(baseFull, combined))
                    return false;
                fullExecutablePath = combined;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsStrictPathUnderDirectory(string directoryFullPath, string candidateFileOrFolderPath)
        {
            string root = Path.GetFullPath(directoryFullPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string path = Path.GetFullPath(candidateFileOrFolderPath);
            string prefix = root + Path.DirectorySeparatorChar;
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
            if (Path.AltDirectorySeparatorChar != Path.DirectorySeparatorChar)
            {
                string altPrefix = root + Path.AltDirectorySeparatorChar;
                if (path.StartsWith(altPrefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        public static bool TryResolveExecutableDialogSeed(string gameFolder, string executableText, out string initialDirectory, out string fileName)
        {
            initialDirectory = null;
            fileName = null;

            string exeText = (executableText ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(exeText))
                return false;

            try
            {
                string gameBase = (gameFolder ?? string.Empty).Trim();
                if (!string.IsNullOrEmpty(gameBase) && Directory.Exists(gameBase) && !Path.IsPathRooted(exeText))
                {
                    string combined = Path.GetFullPath(Path.Combine(Path.GetFullPath(gameBase), exeText));
                    if (File.Exists(combined))
                    {
                        initialDirectory = Path.GetDirectoryName(combined);
                        fileName = Path.GetFileName(combined);
                        return !string.IsNullOrEmpty(initialDirectory);
                    }
                }
                else if (Path.IsPathRooted(exeText))
                {
                    initialDirectory = Path.GetDirectoryName(exeText);
                    fileName = Path.GetFileName(exeText);
                    return !string.IsNullOrEmpty(initialDirectory);
                }
            }
            catch
            {
            }

            return false;
        }

        private static string CompositePathKey(GameConfig g)
        {
            return (g.StartFolder ?? string.Empty).Trim() + "|" + (g.Path ?? string.Empty).Trim();
        }
    }
}
