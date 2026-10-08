using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;

namespace SmartGoldbergEmu.Validation
{
    public static class SteamApiValidator
    {
        public const string SteamApiDll32 = "steam_api.dll";

        public const string SteamApiDll64 = "steam_api64.dll";

        // SHA256 match against the known-good Valve hashes for this file name only.
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

        // Lowercase hex, or null on any error.
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

        // Label is "Steamworks vX.XX"; only resolved for hashes in the Windows steam_api catalog.
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

        // PE FileVersion, or ProductVersion if FileVersion is empty.
        public static string GetFileVersion(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return string.Empty;

            try
            {
                FileVersionInfo info = FileVersionInfo.GetVersionInfo(path);
                if (info == null)
                    return string.Empty;

                if (!string.IsNullOrWhiteSpace(info.FileVersion))
                    return info.FileVersion.Trim();
                if (!string.IsNullOrWhiteSpace(info.ProductVersion))
                    return info.ProductVersion.Trim();
                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        // Valve steam_api binaries only (never emulator builds); used to scan interface version strings.
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

        // Matches the Windows steam_api / steam_api64 hash catalog regardless of file name.
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

        // Moves each dirty DLL aside to a .sge sidecar and copies the matching clean backup over it; returns the count restored.
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
                List<SteamApiFinding> dirtyFindings = GetDirtyFindings(status);
                foreach (SteamApiFinding finding in dirtyFindings)
                {
                    if (finding == null || string.IsNullOrEmpty(finding.Path))
                        continue;

                    string backupPath = FindCleanBackupPathForBitness(status.CleanBackups, finding.Is64Bit);
                    if (string.IsNullOrEmpty(backupPath))
                        continue;
                    if (backupPath.Equals(finding.Path, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (File.Exists(finding.Path))
                    {
                        string modPath = finding.Path + PathConstants.SteamApiBackupSidecarExtension;
                        if (File.Exists(modPath))
                            File.Delete(modPath);
                        File.Move(finding.Path, modPath);
                    }
                    File.Copy(backupPath, finding.Path, true);
                    restoredCount++;
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

        // Dirty live steam_api paths that Restore can replace from CleanBackups.
        public static List<SteamApiFinding> GetDirtyFindings(SteamApiStatus status)
        {
            var dirty = new List<SteamApiFinding>();
            if (status == null)
                return dirty;

            if (status.Findings != null && status.Findings.Count > 0)
            {
                foreach (SteamApiFinding finding in status.Findings)
                {
                    if (finding != null && !finding.IsClean && !string.IsNullOrEmpty(finding.Path))
                        dirty.Add(finding);
                }
                return dirty;
            }

            if (status.X32Found && !status.X32IsClean && !string.IsNullOrEmpty(status.X32Path))
                dirty.Add(new SteamApiFinding { Path = status.X32Path, Is64Bit = false, IsClean = false });
            if (status.X64Found && !status.X64IsClean && !string.IsNullOrEmpty(status.X64Path))
                dirty.Add(new SteamApiFinding { Path = status.X64Path, Is64Bit = true, IsClean = false });
            return dirty;
        }

        public static bool HasDirtySteamApi(SteamApiStatus status)
        {
            return GetDirtyFindings(status).Count > 0;
        }

        // Recursive scan of the game folder.
        public static SteamApiStatus DetectAndValidateSteamApi(string gameFolder)
        {
            SteamApiStatus status = new SteamApiStatus();

            if (string.IsNullOrEmpty(gameFolder) || !Directory.Exists(gameFolder))
                return status;

            try
            {
                List<string> allFiles = EnumerateSteamApiNamedFilesSafe(gameFolder);
                status.Findings = CollectSteamApiFindings(allFiles, gameFolder);

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

                if (HasDirtySteamApi(status))
                {
                    var excludeDirty = new List<string>();
                    foreach (SteamApiFinding finding in GetDirtyFindings(status))
                        excludeDirty.Add(finding.Path);
                    status.CleanBackups = FindCleanBackupDlls(gameFolder, excludeDirty);
                }
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService.LogError($"DetectAndValidateSteamApi: Error detecting Steam API in {gameFolder}", ex);
            }

            return status;
        }

        // Exact steam_api.dll / steam_api64.dll only (renamed copies stay restore sources via FindCleanBackupDlls).
        public static List<SteamApiFinding> CollectSteamApiFindings(IEnumerable<string> allFiles, string gameFolder)
        {
            var findings = new List<SteamApiFinding>();
            if (allFiles == null)
                return findings;

            string normalizedRoot = TryGetNormalizedDirectoryPrefix(gameFolder);
            var scored = new List<ScoredSteamApiFinding>();

            foreach (string filePath in allFiles)
            {
                if (string.IsNullOrEmpty(filePath) || IsExcludedFromPrimarySteamApiDetection(filePath))
                    continue;

                if (!IsExactSteamApiDllFileName(filePath))
                    continue;

                if (IsPrimarySteamApiCandidate(filePath, targetIs64Bit: false))
                {
                    scored.Add(new ScoredSteamApiFinding
                    {
                        Path = filePath,
                        Is64Bit = false,
                        Score = ScorePrimarySteamApiCandidate(filePath, targetIs64Bit: false, normalizedRoot)
                    });
                }
                else if (IsPrimarySteamApiCandidate(filePath, targetIs64Bit: true))
                {
                    scored.Add(new ScoredSteamApiFinding
                    {
                        Path = filePath,
                        Is64Bit = true,
                        Score = ScorePrimarySteamApiCandidate(filePath, targetIs64Bit: true, normalizedRoot)
                    });
                }
            }

            scored.Sort((a, b) =>
            {
                int arch = a.Is64Bit.CompareTo(b.Is64Bit);
                if (arch != 0)
                    return arch;
                int byScore = b.Score.CompareTo(a.Score);
                if (byScore != 0)
                    return byScore;
                // Prefer normal game folders (e.g. x64\) over side trees like compat\.
                int prefA = GetSteamApiPathDisplayPreference(a.Path);
                int prefB = GetSteamApiPathDisplayPreference(b.Path);
                int byPref = prefB.CompareTo(prefA);
                if (byPref != 0)
                    return byPref;
                return string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase);
            });

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (ScoredSteamApiFinding item in scored)
            {
                if (!seen.Add(item.Path))
                    continue;
                findings.Add(new SteamApiFinding
                {
                    Path = item.Path,
                    Is64Bit = item.Is64Bit,
                    IsClean = IsKnownGoodValveSteamApi(item.Path)
                });
            }

            return findings;
        }

        // Higher = listed earlier within the same arch/score (main x64 before compat).
        private static int GetSteamApiPathDisplayPreference(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return 0;

            string normalized = filePath.Replace('/', '\\');
            if (normalized.IndexOf("\\compat\\", StringComparison.OrdinalIgnoreCase) >= 0)
                return 0;
            if (normalized.IndexOf("\\x64\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("\\bin\\x64\\", StringComparison.OrdinalIgnoreCase) >= 0)
                return 2;
            if (normalized.IndexOf("\\x86\\", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("\\bin\\Win32\\", StringComparison.OrdinalIgnoreCase) >= 0)
                return 2;
            return 1;
        }

        private sealed class ScoredSteamApiFinding
        {
            public string Path;
            public bool Is64Bit;
            public int Score;
        }

        // Presence-only scan used when choosing a default launch mode for a newly added game.
        public static bool HasPrimarySteamApiDll(string gameFolder)
        {
            if (string.IsNullOrEmpty(gameFolder) || !Directory.Exists(gameFolder))
                return false;

            List<string> allFiles = EnumerateSteamApiNamedFilesSafe(gameFolder);
            return TrySelectBestPrimaryCandidate(allFiles, gameFolder, targetIs64Bit: false, out _)
                || TrySelectBestPrimaryCandidate(allFiles, gameFolder, targetIs64Bit: true, out _);
        }

        // Every same-bitness copy (launcher and nested game binaries) receives the standard Goldberg build.
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

        // Search order: exe directory, nearest ancestor under startFolder, then the best primary under startFolder.
        public static bool TryResolveSteamApiForExecutable(
            string startFolder,
            string exePath,
            bool useX64,
            out string steamApiPath)
        {
            steamApiPath = null;
            string dllName = useX64 ? SteamApiDll64 : SteamApiDll32;

            string exeDirectory = TryGetExecutableDirectory(exePath);
            if (!string.IsNullOrEmpty(exeDirectory))
            {
                string besideExe = Path.Combine(exeDirectory, dllName);
                if (File.Exists(besideExe) && IsPrimarySteamApiCandidate(besideExe, useX64))
                {
                    steamApiPath = besideExe;
                    return true;
                }

                if (TryFindCanonicalSteamApiInAncestors(startFolder, exeDirectory, dllName, useX64, out steamApiPath))
                    return true;
            }

            if (string.IsNullOrWhiteSpace(startFolder) || !Directory.Exists(startFolder))
                return false;

            List<string> allFiles = EnumerateSteamApiNamedFilesSafe(startFolder);
            if (!TrySelectBestPrimaryCandidate(allFiles, startFolder, useX64, out string bestPath))
                return false;

            steamApiPath = bestPath;
            return true;
        }

        // Returns the live file or its .sge sidecar, whichever is the original Valve binary.
        public static bool TryResolveSteamApiSourceForInterfaces(
            string startFolder,
            string exePath,
            bool useX64,
            out string sourcePath)
        {
            sourcePath = null;

            if (TryResolveSteamApiForExecutable(startFolder, exePath, useX64, out string preferredPath))
            {
                if (TryPickAcceptableInterfaceSource(preferredPath, out sourcePath))
                    return true;
            }

            if (string.IsNullOrWhiteSpace(startFolder) || !Directory.Exists(startFolder))
                return false;

            List<string> allFiles = EnumerateSteamApiNamedFilesSafe(startFolder);
            if (!TrySelectBestPrimaryCandidate(allFiles, startFolder, useX64, out string bestPath))
                return false;

            return TryPickAcceptableInterfaceSource(bestPath, out sourcePath);
        }

        // preferredApiPath plus same-bitness primaries with the same original hash
        // (live Valve bytes, or the .sge backup when the live file was already swapped).
        public static List<string> GetSameHashDeployTargetPaths(
            string gameRoot,
            bool useX64,
            string preferredApiPath)
        {
            var paths = new List<string>();
            if (string.IsNullOrWhiteSpace(preferredApiPath))
                return paths;

            string preferredFull;
            try
            {
                preferredFull = Path.GetFullPath(preferredApiPath);
            }
            catch
            {
                preferredFull = preferredApiPath;
            }

            paths.Add(preferredFull);

            string preferredHash = TryGetComparableOriginalSteamApiHash(preferredFull);
            if (string.IsNullOrEmpty(preferredHash) || string.IsNullOrWhiteSpace(gameRoot) || !Directory.Exists(gameRoot))
                return paths;

            foreach (string peer in GetDeployTargetPathsForBitness(gameRoot, useX64))
            {
                string peerFull;
                try
                {
                    peerFull = Path.GetFullPath(peer);
                }
                catch
                {
                    peerFull = peer;
                }

                if (paths.Any(p => string.Equals(p, peerFull, StringComparison.OrdinalIgnoreCase)))
                    continue;

                string peerHash = TryGetComparableOriginalSteamApiHash(peerFull);
                if (peerHash != null &&
                    string.Equals(peerHash, preferredHash, StringComparison.OrdinalIgnoreCase))
                {
                    paths.Add(peerFull);
                }
            }

            return paths;
        }

        // Reads the PE Machine field; false when the file is missing or not a valid PE.
        public static bool TryDetectExecutableIsX64(string executablePath, out bool isX64)
        {
            isX64 = true;
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
                return false;

            try
            {
                using (var fs = new FileStream(executablePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var reader = new BinaryReader(fs))
                {
                    if (fs.Length < 64)
                        return false;

                    if (reader.ReadUInt16() != 0x5A4D)
                        return false;

                    fs.Position = 0x3C;
                    uint peHeaderOffset = reader.ReadUInt32();
                    if (peHeaderOffset >= fs.Length || peHeaderOffset == 0)
                        return false;

                    fs.Position = peHeaderOffset;
                    if (reader.ReadUInt32() != 0x00004550)
                        return false;

                    ushort machine = reader.ReadUInt16();
                    isX64 = machine == 0x8664 || machine == 0xAA64;
                    return true;
                }
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService?.LogError($"TryDetectExecutableIsX64: {executablePath}", ex);
                return false;
            }
        }

        private static bool TryPickAcceptableInterfaceSource(string preferredLivePath, out string sourcePath)
        {
            sourcePath = null;
            if (string.IsNullOrWhiteSpace(preferredLivePath))
                return false;

            if (IsAcceptableSteamApiForInterfaceGeneration(preferredLivePath))
            {
                sourcePath = preferredLivePath;
                return true;
            }

            string sidecar = preferredLivePath + PathConstants.SteamApiBackupSidecarExtension;
            if (IsAcceptableSteamApiForInterfaceGeneration(sidecar))
            {
                sourcePath = sidecar;
                return true;
            }

            return false;
        }

        private static string TryGetComparableOriginalSteamApiHash(string livePath)
        {
            if (string.IsNullOrWhiteSpace(livePath))
                return null;

            if (File.Exists(livePath) &&
                (IsKnownGoodValveSteamApi(livePath) || IsAcceptableSteamApiForInterfaceGeneration(livePath)))
            {
                return TryComputeSha256Hex(livePath);
            }

            string sidecar = livePath + PathConstants.SteamApiBackupSidecarExtension;
            if (File.Exists(sidecar) &&
                (IsKnownGoodValveSteamApi(sidecar) || IsAcceptableSteamApiForInterfaceGeneration(sidecar)))
            {
                return TryComputeSha256Hex(sidecar);
            }

            return null;
        }

        private static string TryGetExecutableDirectory(string exePath)
        {
            if (string.IsNullOrWhiteSpace(exePath))
                return null;

            try
            {
                string full = Path.IsPathRooted(exePath)
                    ? Path.GetFullPath(exePath)
                    : Path.GetFullPath(exePath);
                string dir = Path.GetDirectoryName(full);
                return string.IsNullOrEmpty(dir) ? null : dir;
            }
            catch
            {
                try
                {
                    return Path.GetDirectoryName(exePath);
                }
                catch
                {
                    return null;
                }
            }
        }

        private static bool TryFindCanonicalSteamApiInAncestors(
            string startFolder,
            string exeDirectory,
            string dllName,
            bool useX64,
            out string steamApiPath)
        {
            steamApiPath = null;
            if (string.IsNullOrWhiteSpace(startFolder) || string.IsNullOrWhiteSpace(exeDirectory))
                return false;

            string rootFull;
            try
            {
                rootFull = Path.GetFullPath(startFolder.Trim());
            }
            catch
            {
                return false;
            }

            string walk;
            try
            {
                walk = Directory.GetParent(exeDirectory)?.FullName;
            }
            catch
            {
                return false;
            }

            while (!string.IsNullOrEmpty(walk))
            {
                string walkFull;
                try
                {
                    walkFull = Path.GetFullPath(walk);
                }
                catch
                {
                    break;
                }

                if (!IsPathUnderOrEqualDirectory(walkFull, rootFull))
                    break;

                string candidate = Path.Combine(walkFull, dllName);
                if (File.Exists(candidate) && IsPrimarySteamApiCandidate(candidate, useX64))
                {
                    steamApiPath = candidate;
                    return true;
                }

                if (string.Equals(walkFull, rootFull, StringComparison.OrdinalIgnoreCase))
                    break;

                try
                {
                    walk = Directory.GetParent(walkFull)?.FullName;
                }
                catch
                {
                    break;
                }
            }

            return false;
        }

        private static bool IsPathUnderOrEqualDirectory(string path, string rootDirectory)
        {
            if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(rootDirectory))
                return false;

            try
            {
                string pathFull = Path.GetFullPath(path);
                string rootFull = Path.GetFullPath(rootDirectory);
                if (string.Equals(pathFull, rootFull, StringComparison.OrdinalIgnoreCase))
                    return true;

                string rootPrefix = rootFull;
                if (!rootPrefix.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) &&
                    !rootPrefix.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                {
                    rootPrefix += Path.DirectorySeparatorChar;
                }

                return pathFull.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

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

        private static bool IsExactSteamApiDllFileName(string filePath)
        {
            string fileName = Path.GetFileName(filePath);
            if (string.IsNullOrEmpty(fileName))
                return false;

            return fileName.Equals(SteamApiDll32, StringComparison.OrdinalIgnoreCase) ||
                   fileName.Equals(SteamApiDll64, StringComparison.OrdinalIgnoreCase);
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

        // Exact canonical names, or steam_api*.dll variants whose bitness can be determined.
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

        // Skips unreadable directories, which are common under game install folders.
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

        public static List<string> FindCleanBackupDlls(string gameFolder, string mainX32Path = null, string mainX64Path = null)
        {
            var exclude = new List<string>();
            if (!string.IsNullOrEmpty(mainX32Path))
                exclude.Add(mainX32Path);
            if (!string.IsNullOrEmpty(mainX64Path))
                exclude.Add(mainX64Path);
            return FindCleanBackupDlls(gameFolder, exclude);
        }

        public static List<string> FindCleanBackupDlls(string gameFolder, IEnumerable<string> excludePaths)
        {
            List<string> cleanBackups = new List<string>();

            if (string.IsNullOrEmpty(gameFolder) || !Directory.Exists(gameFolder))
                return cleanBackups;

            var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (excludePaths != null)
            {
                foreach (string path in excludePaths)
                {
                    if (!string.IsNullOrEmpty(path))
                        excluded.Add(path);
                }
            }

            try
            {
                List<string> allFiles = EnumerateSteamApiNamedFilesSafe(gameFolder);

                foreach (string filePath in allFiles)
                {
                    string fileName = Path.GetFileName(filePath);

                    if (excluded.Contains(filePath) || IsExcludedFromPrimarySteamApiDetection(filePath))
                        continue;

                    if (!IsSteamApiDllCandidateFileName(fileName))
                        continue;

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

        // Filename matches *steam_api*.dll* (steam_api before .dll; trailing suffix allowed).
        private static bool IsSteamApiDllCandidateFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return false;

            int steamApiIndex = fileName.IndexOf("steam_api", StringComparison.OrdinalIgnoreCase);
            if (steamApiIndex < 0)
                return false;

            return fileName.IndexOf(".dll", steamApiIndex, StringComparison.OrdinalIgnoreCase) >= 0;
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
