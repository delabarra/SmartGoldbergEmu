using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;

namespace SmartGoldbergEmu.Validation
{
    /// <summary>
    /// Service for validating Steam API DLL files.
    /// Detects and verifies whether DLL files are original Valve releases or have been modified.
    /// </summary>
    public static class SteamApiValidator
    {
        /// <summary>32-bit Steam API DLL filename.</summary>
        public const string SteamApiDll32 = "steam_api.dll";

        /// <summary>64-bit Steam API DLL filename.</summary>
        public const string SteamApiDll64 = "steam_api64.dll";

        /// <summary>
        /// Determines if a file is an original, unmodified Steam API DLL by comparing its SHA256 hash
        /// against a database of known-good versions.
        /// </summary>
        /// <param name="path">The full path to the DLL file to validate.</param>
        /// <returns>True if the file hash matches a known original Steam API DLL version; otherwise, false.</returns>
        public static bool IsOriginalSteamApi(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            try
            {
                string fileName = Path.GetFileName(path);
                HashSet<string> knownHashes = SteamApiHashes.GetHashesForFile(fileName);
                if (knownHashes == null || knownHashes.Count == 0)
                    return false;

                string fileHash = ComputeSha256Hex(path);
                if (fileHash == null)
                    return false;

                return knownHashes.Contains(fileHash);
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService.LogError($"IsOriginalSteamApi: Error computing hash for {path}", ex);
            }

            return false;
        }

        // Filename-specific or cross-name hash match against the Valve steam_api catalog.
        public static bool IsKnownGoodValveSteamApi(string path)
        {
            if (IsOriginalSteamApi(path))
                return true;

            return TryGetKnownGoodWindowsSteamApiBitness(path, out _);
        }

        /// <summary>
        /// Computes SHA256 hash of a file and returns it as lowercase hex string.
        /// </summary>
        private static string ComputeSha256Hex(string path)
        {
            using (SHA256 checksum = SHA256.Create())
            using (FileStream fileStream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                byte[] hashBytes = checksum.ComputeHash(fileStream);
                StringBuilder sb = new StringBuilder(hashBytes.Length * 2);
                foreach (byte b in hashBytes)
                    sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>
        /// Computes SHA256 hash of a file and returns lowercase hex, or null on error.
        /// </summary>
        public static string TryComputeSha256Hex(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return null;

            try
            {
                return ComputeSha256Hex(path);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Tries to resolve "Steamworks vX.XX" label for a known Windows steam_api / steam_api64 file.
        /// </summary>
        public static bool TryGetWindowsSteamworksVersionLabel(string path, out string steamworksVersionLabel)
        {
            steamworksVersionLabel = null;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            try
            {
                string fileHash = ComputeSha256Hex(path);
                if (string.IsNullOrEmpty(fileHash) ||
                    !SteamApiHashes.TryMatchWindowsSteamApiHash(fileHash, out _))
                {
                    return false;
                }

                return SteamApiHashes.TryGetWindowsSteamworksVersionByHash(fileHash, out steamworksVersionLabel);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Returns file ProductName metadata, or empty if unavailable.
        /// </summary>
        public static string GetFileProductName(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return string.Empty;

            try
            {
                FileVersionInfo info = FileVersionInfo.GetVersionInfo(path);
                return info?.ProductName ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// True when the file is a Valve steam_api binary suitable for scanning interface version strings (not emulator builds).
        /// </summary>
        public static bool IsAcceptableSteamApiForInterfaceGeneration(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            string fileName = Path.GetFileName(path);
            if (string.IsNullOrEmpty(fileName))
                return false;

            if (fileName.EndsWith(PathConstants.SteamApiBackupSidecarExtension, StringComparison.OrdinalIgnoreCase))
                fileName = fileName.Substring(0, fileName.Length - PathConstants.SteamApiBackupSidecarExtension.Length);

            if (fileName.IndexOf("steam_api", StringComparison.OrdinalIgnoreCase) < 0 ||
                !fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (IsIgnoredSteamApiFile(path))
                return false;

            if (IsKnownGoodValveSteamApi(path))
                return true;

            string productName = GetFileProductName(path);
            if (string.IsNullOrWhiteSpace(productName))
                return false;

            if (productName.IndexOf("GSE", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;

            return productName.Equals("Steam Client API", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// True if the file SHA256 is listed in <see cref="SteamApiHashes.IgnoredSteamApiDetectionHashes"/>.
        /// </summary>
        private static bool IsIgnoredSteamApiFile(string path)
        {
            try
            {
                string fileHash = ComputeSha256Hex(path);
                return fileHash != null && SteamApiHashes.IsIgnoredSteamApiDetectionHash(fileHash);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// True if the file bytes match a known Windows steam_api / steam_api64 hash (any filename).
        /// </summary>
        public static bool TryGetKnownGoodWindowsSteamApiBitness(string path, out bool is64Bit)
        {
            is64Bit = false;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;
            if (IsIgnoredSteamApiFile(path))
                return false;

            try
            {
                string fileHash = ComputeSha256Hex(path);
                if (fileHash == null)
                    return false;
                return SteamApiHashes.TryMatchWindowsSteamApiHash(fileHash, out is64Bit);
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService.LogError($"TryGetKnownGoodWindowsSteamApiBitness: {path}", ex);
                return false;
            }
        }

        /// <summary>
        /// Replaces dirty steam_api / steam_api64 with a clean file from <see cref="SteamApiStatus.CleanBackups"/> (move current to .sge backup, copy clean backup).
        /// </summary>
        /// <returns>Number of DLLs restored.</returns>
        public static int TryRestoreSteamApiFromCleanBackups(SteamApiStatus status, out string errorMessage)
        {
            errorMessage = null;
            if (status == null || status.CleanBackups == null || status.CleanBackups.Count == 0)
            {
                errorMessage = "No clean backup DLLs found to restore.";
                return 0;
            }

            int restoredCount = 0;
            try
            {
                if (status.X32Found && !status.X32IsClean && !string.IsNullOrEmpty(status.X32Path))
                {
                    string backupPath = FindCleanBackupPathForBitness(status.CleanBackups, targetIs64Bit: false);

                    if (!string.IsNullOrEmpty(backupPath))
                    {
                        if (File.Exists(status.X32Path))
                        {
                            string modPath = status.X32Path + PathConstants.SteamApiBackupSidecarExtension;
                            if (File.Exists(modPath))
                                File.Delete(modPath);
                            File.Move(status.X32Path, modPath);
                        }
                        File.Copy(backupPath, status.X32Path, true);
                        restoredCount++;
                    }
                }

                if (status.X64Found && !status.X64IsClean && !string.IsNullOrEmpty(status.X64Path))
                {
                    string backupPath = FindCleanBackupPathForBitness(status.CleanBackups, targetIs64Bit: true);

                    if (!string.IsNullOrEmpty(backupPath))
                    {
                        if (File.Exists(status.X64Path))
                        {
                            string modPath = status.X64Path + PathConstants.SteamApiBackupSidecarExtension;
                            if (File.Exists(modPath))
                                File.Delete(modPath);
                            File.Move(status.X64Path, modPath);
                        }
                        File.Copy(backupPath, status.X64Path, true);
                        restoredCount++;
                    }
                }
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                ServiceLocator.LogService.LogError("TryRestoreSteamApiFromCleanBackups", ex);
                return 0;
            }

            if (restoredCount == 0 && string.IsNullOrEmpty(errorMessage))
                errorMessage = "Could not find matching backup DLLs to restore.";
            return restoredCount;
        }

        /// <summary>
        /// Scans a game folder recursively to find Steam API DLLs and validates each one.
        /// </summary>
        /// <param name="gameFolder">The path to the game folder to scan.</param>
        /// <returns>A SteamApiStatus object containing validation results for all found DLLs.</returns>
        public static SteamApiStatus DetectAndValidateSteamApi(string gameFolder)
        {
            SteamApiStatus status = new SteamApiStatus();

            if (string.IsNullOrEmpty(gameFolder) || !Directory.Exists(gameFolder))
                return status;

            try
            {
                List<string> allFiles = EnumerateSteamApiNamedFilesSafe(gameFolder);

                if (TrySelectBestPrimaryCandidate(allFiles, gameFolder, targetIs64Bit: false, out string x32Path))
                {
                    status.X32Found = true;
                    status.X32Path = x32Path;
                    status.X32IsClean = IsKnownGoodValveSteamApi(x32Path);
                }

                if (TrySelectBestPrimaryCandidate(allFiles, gameFolder, targetIs64Bit: true, out string x64Path))
                {
                    status.X64Found = true;
                    status.X64Path = x64Path;
                    status.X64IsClean = IsKnownGoodValveSteamApi(x64Path);
                }

                if ((status.X32Found && !status.X32IsClean) || (status.X64Found && !status.X64IsClean))
                {
                    status.CleanBackups = FindCleanBackupDlls(gameFolder, status.X32Path, status.X64Path);
                }
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService.LogError($"DetectAndValidateSteamApi: Error detecting Steam API in {gameFolder}", ex);
            }

            return status;
        }

        /// <summary>
        /// Full paths to existing Steam API DLLs under <paramref name="gameFolder"/> matching <paramref name="useX64"/>.
        /// Used to deploy standard Goldberg builds beside every copy (launcher + nested game binaries).
        /// </summary>
        public static List<string> GetDeployTargetPathsForBitness(string gameFolder, bool useX64)
        {
            var paths = new List<string>();
            if (string.IsNullOrEmpty(gameFolder) || !Directory.Exists(gameFolder))
                return paths;

            foreach (string filePath in EnumerateSteamApiNamedFilesSafe(gameFolder))
            {
                if (IsPrimarySteamApiCandidate(filePath, useX64))
                    paths.Add(filePath);
            }

            return paths;
        }

        /// <summary>
        /// Returns true if <paramref name="filePath"/> is a candidate primary Steam API DLL for the requested bitness.
        /// Accepts exact canonical names and steam_api*.dll variants when bitness can be inferred.
        /// </summary>
        private static bool TrySelectBestPrimaryCandidate(
            IEnumerable<string> allFiles,
            string gameFolder,
            bool targetIs64Bit,
            out string bestPath)
        {
            bestPath = null;
            int bestScore = int.MinValue;
            string normalizedRoot = TryGetNormalizedDirectoryPrefix(gameFolder);

            foreach (string filePath in allFiles)
            {
                if (!IsPrimarySteamApiCandidate(filePath, targetIs64Bit))
                    continue;

                int score = ScorePrimarySteamApiCandidate(filePath, targetIs64Bit, normalizedRoot);
                if (score <= bestScore)
                    continue;

                bestScore = score;
                bestPath = filePath;
            }

            return bestPath != null;
        }

        private static int ScorePrimarySteamApiCandidate(string filePath, bool targetIs64Bit, string normalizedRootPrefix)
        {
            int score = 0;
            string fileName = Path.GetFileName(filePath);
            if (targetIs64Bit && fileName.Equals(SteamApiDll64, StringComparison.OrdinalIgnoreCase))
                score += 1000;
            else if (!targetIs64Bit && fileName.Equals(SteamApiDll32, StringComparison.OrdinalIgnoreCase))
                score += 1000;

            if (IsKnownGoodValveSteamApi(filePath))
                score += 500;
            else if (IsLikelyOfficialSteamClientApi(filePath))
                score += 100;

            if (!string.IsNullOrEmpty(normalizedRootPrefix))
            {
                try
                {
                    string fullPath = Path.GetFullPath(filePath);
                    if (fullPath.StartsWith(normalizedRootPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        string relative = fullPath.Substring(normalizedRootPrefix.Length);
                        int depth = relative.Split(
                            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                            StringSplitOptions.RemoveEmptyEntries).Length;
                        score += Math.Max(0, 200 - (depth * 25));
                    }
                }
                catch
                {
                }
            }

            return score;
        }

        private static string TryGetNormalizedDirectoryPrefix(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
                return null;

            try
            {
                string full = Path.GetFullPath(directoryPath.Trim());
                if (!full.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                    full += Path.DirectorySeparatorChar;
                return full;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsExcludedFromPrimarySteamApiDetection(string filePath)
        {
            string fileName = Path.GetFileName(filePath);
            if (string.IsNullOrEmpty(fileName))
                return true;

            if (fileName.EndsWith(PathConstants.SteamApiBackupSidecarExtension, StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(PathConstants.SteamApiDllDeploymentLegacyBackupExtension, StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".backup", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return IsUnderGoldbergSubfolderInPath(filePath);
        }

        private static bool IsUnderGoldbergSubfolderInPath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return false;

            foreach (string segment in filePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            {
                if (string.Equals(segment, PathConstants.GoldbergDirectoryFolderName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static bool IsPrimarySteamApiCandidate(string filePath, bool targetIs64Bit)
        {
            if (IsExcludedFromPrimarySteamApiDetection(filePath))
                return false;

            string fileName = Path.GetFileName(filePath);
            if (string.IsNullOrEmpty(fileName))
                return false;

            if (targetIs64Bit && fileName.Equals(SteamApiDll64, StringComparison.OrdinalIgnoreCase))
                return true;
            if (!targetIs64Bit && fileName.Equals(SteamApiDll32, StringComparison.OrdinalIgnoreCase))
                return true;

            if (!fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                fileName.IndexOf("steam_api", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            if (TryGetKnownGoodWindowsSteamApiBitness(filePath, out bool detectedIs64Bit))
                return detectedIs64Bit == targetIs64Bit;

            if (TryInferWindowsSteamApiBitnessFromFileName(filePath, out bool inferredIs64Bit))
                return inferredIs64Bit == targetIs64Bit;

            return false;
        }

        /// <summary>
        /// Lists files under <paramref name="root"/> whose name contains "steam_api", skipping directories that cannot be read (common under game install folders).
        /// </summary>
        private static List<string> EnumerateSteamApiNamedFilesSafe(string root)
        {
            var paths = new List<string>();
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                return paths;

            void Walk(string dir)
            {
                try
                {
                    foreach (string f in Directory.GetFiles(dir, PathConstants.SteamApiRedistributableDllSearchPattern))
                        paths.Add(f);
                }
                catch
                {
                }
                try
                {
                    foreach (string f in Directory.GetFiles(dir))
                    {
                        string fn = Path.GetFileName(f);
                        if (fn.IndexOf("steam_api", StringComparison.OrdinalIgnoreCase) >= 0)
                            paths.Add(f);
                    }
                }
                catch
                {
                }
                try
                {
                    foreach (string sub in Directory.GetDirectories(dir))
                        Walk(sub);
                }
                catch
                {
                }
            }

            Walk(root);

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var unique = new List<string>();
            foreach (string p in paths)
            {
                if (seen.Add(p))
                    unique.Add(p);
            }
            return unique;
        }

        /// <summary>
        /// Searches for backup files that might contain clean (original) Steam API DLL versions.
        /// </summary>
        public static List<string> FindCleanBackupDlls(string gameFolder, string mainX32Path = null, string mainX64Path = null)
        {
            List<string> cleanBackups = new List<string>();

            if (string.IsNullOrEmpty(gameFolder) || !Directory.Exists(gameFolder))
                return cleanBackups;

            try
            {
                List<string> allFiles = EnumerateSteamApiNamedFilesSafe(gameFolder);
                Regex steamApiPattern = new Regex(@"steam_api", RegexOptions.IgnoreCase);

                foreach (string filePath in allFiles)
                {
                    string fileName = Path.GetFileName(filePath);

                    if ((!string.IsNullOrEmpty(mainX32Path) && filePath.Equals(mainX32Path, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrEmpty(mainX64Path) && filePath.Equals(mainX64Path, StringComparison.OrdinalIgnoreCase)) ||
                        IsExcludedFromPrimarySteamApiDetection(filePath))
                    {
                        continue;
                    }

                    if (steamApiPattern.IsMatch(fileName))
                    {
                        if (IsIgnoredSteamApiFile(filePath))
                            continue;

                        if (IsKnownGoodValveSteamApi(filePath) ||
                            TryGetKnownGoodWindowsSteamApiBitness(filePath, out _) ||
                            IsLikelyOfficialSteamClientApi(filePath))
                        {
                            cleanBackups.Add(filePath);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService.LogError($"FindCleanBackupDlls: Error finding backup DLLs in {gameFolder}", ex);
            }

            return cleanBackups;
        }

        public static string FindCleanBackupPathForBitness(IEnumerable<string> cleanBackups, bool targetIs64Bit)
        {
            if (cleanBackups == null)
                return null;

            return cleanBackups.FirstOrDefault(b =>
                !string.IsNullOrEmpty(b) && File.Exists(b) && IsCandidateCleanBackupForBitness(b, targetIs64Bit));
        }

        private static bool IsCandidateCleanBackupForBitness(string backupPath, bool targetIs64Bit)
        {
            if (TryGetKnownGoodWindowsSteamApiBitness(backupPath, out bool detectedIs64))
                return detectedIs64 == targetIs64Bit;

            if (!IsLikelyOfficialSteamClientApi(backupPath))
                return false;

            if (!TryInferWindowsSteamApiBitnessFromFileName(backupPath, out bool inferredIs64))
                return false;

            return inferredIs64 == targetIs64Bit;
        }

        private static bool IsLikelyOfficialSteamClientApi(string path)
        {
            try
            {
                string productName = GetFileProductName(path);
                return !string.IsNullOrEmpty(productName) &&
                       productName.Equals("Steam Client API", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static bool TryInferWindowsSteamApiBitnessFromFileName(string path, out bool is64Bit)
        {
            is64Bit = false;
            string fileName = Path.GetFileName(path);
            if (string.IsNullOrEmpty(fileName))
                return false;

            if (fileName.IndexOf("steam_api64", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                is64Bit = true;
                return true;
            }

            if (fileName.IndexOf("steam_api", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                is64Bit = false;
                return true;
            }

            return false;
        }
    }
}
