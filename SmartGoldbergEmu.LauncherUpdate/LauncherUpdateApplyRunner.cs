using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
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

                if (string.IsNullOrWhiteSpace(installRoot)
                    || string.IsNullOrWhiteSpace(stageRoot)
                    || string.IsNullOrWhiteSpace(exePath)
                    || string.IsNullOrWhiteSpace(workRoot))
                {
                    throw new InvalidOperationException("Launcher update manifest is missing required paths.");
                }

                Log(logPath, "Waiting for launcher process " + waitProcessId + " to exit.");
                WaitForProcessExit(waitProcessId);
                Thread.Sleep(PostExitSettleMs);

                Log(logPath, "Applying staged files from " + stageRoot + " to " + installRoot + ".");
                ApplyPayload(stageRoot, installRoot, manifest["skipDirectoryNames"] as JsonArray, logPath);

                RemoveLegacySideBySideDirectories(installRoot, logPath);

                Log(logPath, "Starting " + exePath + ".");
                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    WorkingDirectory = installRoot,
                    UseShellExecute = true
                });

                ScheduleTempCleanup(workRoot, logPath);
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

        private static void ApplyPayload(string stageRoot, string installRoot, JsonArray skipDirectoryNames, string logPath)
        {
            if (!Directory.Exists(stageRoot))
                throw new DirectoryNotFoundException("Staged update folder not found: " + stageRoot);

            Directory.CreateDirectory(installRoot);

            foreach (string entryPath in Directory.EnumerateFileSystemEntries(stageRoot))
            {
                string name = Path.GetFileName(entryPath);
                if (ShouldSkipEntry(name, entryPath, skipDirectoryNames))
                {
                    Log(logPath, "Skipping " + name + ".");
                    continue;
                }

                string destinationPath = Path.Combine(installRoot, name);
                if (Directory.Exists(entryPath))
                {
                    if (Directory.Exists(destinationPath))
                        Directory.Delete(destinationPath, true);

                    CopyDirectory(entryPath, destinationPath);
                }
                else
                {
                    File.Copy(entryPath, destinationPath, true);
                }
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

                File.Copy(filePath, destinationFile, true);
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

        private static void ScheduleTempCleanup(string workRoot, string logPath)
        {
            string tempRoot = Path.GetDirectoryName(workRoot);
            if (string.IsNullOrEmpty(tempRoot) || !Directory.Exists(tempRoot))
                return;

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
