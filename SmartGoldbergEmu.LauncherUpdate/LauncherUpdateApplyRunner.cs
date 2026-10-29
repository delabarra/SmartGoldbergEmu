using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.JsonKit;

namespace SmartGoldbergEmu.LauncherUpdate
{
    internal static class LauncherUpdateApplyRunner
    {
        private const int WaitForExitTimeoutMs = 120000;
        private const int PostExitSettleMs = 500;
        private const int TempCleanupDelaySeconds = 3;
        private static readonly string[] SkipFileExtensions = { ".cfg", ".ini", ".log" };
        private const string LegacySideBySideDirectoryPrefix = "SmartGoldbergEmu-";
        private const string RollbackFolderPrefix = "_update_rollback_";
        private const int IoRetryAttempts = 5;
        private const int IoRetryDelayMs = 500;

        public static void Run(string manifestPath)
        {
            if (string.IsNullOrWhiteSpace(manifestPath))
                throw new ArgumentException("Manifest path is required.", nameof(manifestPath));

            string logPath = Path.Combine(Path.GetDirectoryName(manifestPath) ?? string.Empty, "apply.log");
            try
            {
                JsonObject manifest = JsonObject.Parse(File.ReadAllText(manifestPath));
                string installRoot = manifest["installRoot"]?.ToString();
                string stageRoot = manifest["stageRoot"]?.ToString();
                string exePath = manifest["exePath"]?.ToString();
                string workRoot = manifest["workRoot"]?.ToString();
                int waitProcessId = manifest["waitProcessId"]?.ToInt32() ?? 0;

                LauncherUpdateManifestTrustHelper.ValidateOrThrow(
                    manifestPath,
                    installRoot,
                    stageRoot,
                    exePath,
                    workRoot);

                installRoot = Path.GetFullPath(installRoot);
                stageRoot = Path.GetFullPath(stageRoot);
                exePath = Path.GetFullPath(exePath);
                workRoot = Path.GetFullPath(workRoot);

                Log(logPath, "Waiting for launcher process " + waitProcessId + " to exit.");
                WaitForProcessExit(waitProcessId);
                Thread.Sleep(PostExitSettleMs);

                Log(logPath, "Applying staged files from " + stageRoot + " to " + installRoot + ".");
                try
                {
                    ApplyPayload(stageRoot, installRoot, manifest["skipDirectoryNames"] as JsonArray, logPath);
                }
                catch (Exception ex)
                {
                    // ApplyPayload rolled back to the previous version; restart it so the launcher does not just vanish.
                    Log(logPath, "Apply failed and was rolled back: " + ex.Message);
                    TryStartLauncher(exePath, installRoot, logPath);
                    throw;
                }

                RemoveLegacySideBySideDirectories(installRoot, logPath);

                Log(logPath, "Starting " + exePath + ".");
                TryStartLauncher(exePath, installRoot, logPath);

                ScheduleTempCleanup(workRoot, installRoot, logPath);
            }
            catch (Exception ex)
            {
                Log(logPath, "Apply failed: " + ex.Message);
                throw;
            }
        }

        private static void WaitForProcessExit(int processId)
        {
            if (processId <= 0)
                return;

            try
            {
                using (var process = Process.GetProcessById(processId))
                {
                    if (!process.WaitForExit(WaitForExitTimeoutMs))
                        throw new TimeoutException("Timed out waiting for launcher to exit.");
                }
            }
            catch (ArgumentException)
            {
                // Process already exited.
            }
        }

        private sealed class AppliedEntry
        {
            public string DestinationPath;
            public string BackupPath;
        }

        // Existing entries are moved into a rollback folder inside installRoot (same volume, so moves are cheap and atomic)
        // before each copy; any failure restores them so the install is never left half old, half new.
        private static void ApplyPayload(string stageRoot, string installRoot, JsonArray skipDirectoryNames, string logPath)
        {
            if (!Directory.Exists(stageRoot))
                throw new DirectoryNotFoundException("Staged update folder not found: " + stageRoot);

            Directory.CreateDirectory(installRoot);
            string rollbackRoot = Path.Combine(installRoot, RollbackFolderPrefix + DateTime.Now.Ticks);
            var applied = new System.Collections.Generic.List<AppliedEntry>();

            try
            {
                foreach (string entryPath in Directory.EnumerateFileSystemEntries(stageRoot))
                {
                    string name = Path.GetFileName(entryPath);
                    if (ShouldSkipEntry(name, entryPath, skipDirectoryNames))
                    {
                        Log(logPath, "Skipping " + name + ".");
                        continue;
                    }

                    string destinationPath = Path.Combine(installRoot, name);
                    var entry = new AppliedEntry { DestinationPath = destinationPath };
                    if (Directory.Exists(destinationPath) || File.Exists(destinationPath))
                    {
                        Directory.CreateDirectory(rollbackRoot);
                        entry.BackupPath = Path.Combine(rollbackRoot, name);
                        RetryOnIoError(() => MoveEntry(destinationPath, entry.BackupPath));
                    }

                    applied.Add(entry);
                    if (Directory.Exists(entryPath))
                        CopyDirectory(entryPath, destinationPath);
                    else
                        RetryOnIoError(() => File.Copy(entryPath, destinationPath, true));
                }
            }
            catch
            {
                Rollback(applied, logPath);
                TryDeleteDirectory(rollbackRoot, logPath);
                throw;
            }

            TryDeleteDirectory(rollbackRoot, logPath);
        }

        private static void Rollback(System.Collections.Generic.List<AppliedEntry> applied, string logPath)
        {
            for (int i = applied.Count - 1; i >= 0; i--)
            {
                AppliedEntry entry = applied[i];
                try
                {
                    if (Directory.Exists(entry.DestinationPath))
                        Directory.Delete(entry.DestinationPath, true);
                    else if (File.Exists(entry.DestinationPath))
                        File.Delete(entry.DestinationPath);

                    if (entry.BackupPath != null)
                        MoveEntry(entry.BackupPath, entry.DestinationPath);
                }
                catch (Exception ex)
                {
                    Log(logPath, "Rollback failed for " + entry.DestinationPath + ": " + ex.Message);
                }
            }
        }

        private static void MoveEntry(string sourcePath, string destinationPath)
        {
            if (Directory.Exists(sourcePath))
                Directory.Move(sourcePath, destinationPath);
            else
                File.Move(sourcePath, destinationPath);
        }

        // Antivirus scanners briefly lock freshly written executables.
        private static void RetryOnIoError(Action action)
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    action();
                    return;
                }
                catch (IOException) when (attempt < IoRetryAttempts)
                {
                    Thread.Sleep(IoRetryDelayMs);
                }
                catch (UnauthorizedAccessException) when (attempt < IoRetryAttempts)
                {
                    Thread.Sleep(IoRetryDelayMs);
                }
            }
        }

        private static void TryDeleteDirectory(string path, string logPath)
        {
            if (!Directory.Exists(path))
                return;

            try
            {
                Directory.Delete(path, true);
            }
            catch (Exception ex)
            {
                Log(logPath, "Could not remove " + path + ": " + ex.Message);
            }
        }

        private static void TryStartLauncher(string exePath, string installRoot, string logPath)
        {
            string expectedExe = Path.Combine(
                Path.GetFullPath(installRoot),
                LauncherUpdateManifestTrustHelper.LauncherMainExecutableFileName);
            if (!string.Equals(Path.GetFullPath(exePath), expectedExe, StringComparison.OrdinalIgnoreCase))
            {
                Log(logPath, "Refusing to start launcher; exePath is not SmartGoldbergEmu.exe under installRoot.");
                return;
            }

            if (!File.Exists(exePath))
                return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    WorkingDirectory = installRoot,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Log(logPath, "Could not restart " + exePath + ": " + ex.Message);
            }
        }

        private static bool ShouldSkipEntry(string name, string entryPath, JsonArray skipDirectoryNames)
        {
            if (Directory.Exists(entryPath) && skipDirectoryNames != null)
            {
                foreach (JsonValue skipName in skipDirectoryNames)
                {
                    if (string.Equals(name, skipName?.ToString(), StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            if (!Directory.Exists(entryPath))
            {
                string extension = Path.GetExtension(name);
                foreach (string skipExtension in SkipFileExtensions)
                {
                    if (string.Equals(extension, skipExtension, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }

        private static void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            foreach (string filePath in Directory.EnumerateFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string relativePath = filePath.Substring(sourceDir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string destinationFile = Path.Combine(targetDir, relativePath);
                string destinationDirectory = Path.GetDirectoryName(destinationFile);
                if (!string.IsNullOrEmpty(destinationDirectory))
                    Directory.CreateDirectory(destinationDirectory);

                RetryOnIoError(() => File.Copy(filePath, destinationFile, true));
            }
        }

        private static void RemoveLegacySideBySideDirectories(string installRoot, string logPath)
        {
            foreach (string directoryPath in Directory.GetDirectories(installRoot))
            {
                string directoryName = Path.GetFileName(directoryPath);
                if (!directoryName.StartsWith(LegacySideBySideDirectoryPrefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    Directory.Delete(directoryPath, true);
                    Log(logPath, "Removed legacy folder " + directoryName + ".");
                }
                catch (Exception ex)
                {
                    Log(logPath, "Failed to remove legacy folder " + directoryName + ": " + ex.Message);
                }
            }
        }

        private static void ScheduleTempCleanup(string workRoot, string installRoot, string logPath)
        {
            string tempRoot = Path.GetDirectoryName(workRoot);
            if (string.IsNullOrEmpty(tempRoot) || !Directory.Exists(tempRoot))
                return;

            // Only delete the install temp folder (parent of workRoot), never an unrelated path from a crafted manifest.
            if (!LauncherUpdateManifestTrustHelper.IsPathWithinBase(installRoot, tempRoot))
            {
                Log(logPath, "Skipping temp cleanup; folder is not under installRoot: " + tempRoot);
                return;
            }

            Log(logPath, "Scheduling cleanup of " + tempRoot + ".");
            string cleanupArguments =
                "/c ping -n " + TempCleanupDelaySeconds + " 127.0.0.1 >nul & rmdir /s /q \"" + tempRoot + "\"";

            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = cleanupArguments,
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }

        private static void Log(string logPath, string message)
        {
            if (string.IsNullOrEmpty(logPath))
                return;

            try
            {
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + message + Environment.NewLine;
                File.AppendAllText(logPath, line);
            }
            catch
            {
                // Best-effort logging only.
            }
        }
    }
}
