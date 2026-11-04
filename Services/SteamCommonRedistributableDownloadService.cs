using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Services
{
    // Downloads missing Steamworks Common Redistributable depots (app 228980) via DepotDownloader (anonymous).
    public class SteamCommonRedistributableDownloadService
    {
        private static readonly object ToolGate = new object();
        private static readonly object DepotGate = new object();

        private const int ToolDownloadTimeoutSeconds = 180;
        private const int DepotDownloadTimeoutMinutes = 30;

        private static readonly Regex DepotPercentRegex = new Regex(
            @"(\d{1,3}(?:\.\d+)?)\s*%",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private readonly ILogService _logger;

        public SteamCommonRedistributableDownloadService()
            : this(ServiceLocator.LogService)
        {
        }

        public SteamCommonRedistributableDownloadService(ILogService logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string GetSgeSharedRoot()
        {
            string root = PathConstants.GoldbergCommonRedistDirectory;
            Directory.CreateDirectory(root);
            return root;
        }

        // Ensures the depot's installscript (and installers) exist under targetSharedRoot.
        // progress is (message, percent 0–100) for this download only.
        public ValidationResult EnsureDepotFiles(
            uint depotId,
            string relativeInstallScriptPath,
            string targetSharedRoot,
            bool dryRun = false,
            Action<string, int> progress = null,
            Func<bool> isCancelled = null)
        {
            if (depotId == 0)
                return ValidationResult.Failure("Depot id is required.");
            if (string.IsNullOrWhiteSpace(relativeInstallScriptPath))
                return ValidationResult.Failure("Install script relative path is required.");
            if (string.IsNullOrWhiteSpace(targetSharedRoot))
                return ValidationResult.Failure("Target Steamworks Shared root is required.");

            string scriptPath = Path.Combine(
                targetSharedRoot,
                relativeInstallScriptPath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(scriptPath))
            {
                progress?.Invoke("Redistributable files ready.", 100);
                return ValidationResult.Success();
            }

            if (dryRun)
            {
                _logger?.LogMessage(
                    $"Install scripts: dry-run would download common-redist depot {depotId} into {targetSharedRoot}");
                return ValidationResult.Success();
            }

            lock (DepotGate)
            {
                if (File.Exists(scriptPath))
                {
                    progress?.Invoke("Redistributable files ready.", 100);
                    return ValidationResult.Success();
                }

                try
                {
                    return EnsureDepotFilesCore(depotId, scriptPath, targetSharedRoot, progress, isCancelled);
                }
                catch (Exception ex)
                {
                    return ValidationResult.Failure(
                        $"Failed to download common redistributable depot {depotId}: {ex.Message}");
                }
            }
        }

        private ValidationResult EnsureDepotFilesCore(
            uint depotId,
            string scriptPath,
            string targetSharedRoot,
            Action<string, int> progress,
            Func<bool> isCancelled)
        {
            progress?.Invoke("Preparing download tools…", 2);
            ValidationResult tool = EnsureDepotDownloaderTool();
            if (!tool.IsValid)
                return tool;

            if (isCancelled != null && isCancelled())
                return ValidationResult.Failure("Dependency install cancelled.");

            string toolExe = GetDepotDownloaderExecutablePath();
            Directory.CreateDirectory(targetSharedRoot);

            _logger?.LogMessage(
                $"Install scripts: downloading Steam common redistributable depot {depotId} (app {SteamCommonRedistributableConstants.AppId})…");
            progress?.Invoke("Downloading redistributable depot " + depotId + "…", 5);

            string args = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "-app {0} -depot {1} -dir \"{2}\"",
                SteamCommonRedistributableConstants.AppId,
                depotId,
                targetSharedRoot);

            var startInfo = new ProcessStartInfo
            {
                FileName = toolExe,
                Arguments = args,
                WorkingDirectory = Path.GetDirectoryName(toolExe) ?? targetSharedRoot,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (var process = new Process { StartInfo = startInfo })
            {
                var stdout = new System.Text.StringBuilder();
                var stderr = new System.Text.StringBuilder();
                int lastReportedPct = 5;
                process.OutputDataReceived += (s, e) =>
                {
                    if (e.Data == null)
                        return;
                    stdout.AppendLine(e.Data);
                    if (e.Data.IndexOf("Downloading", StringComparison.OrdinalIgnoreCase) >= 0
                        || e.Data.IndexOf("Got depot key", StringComparison.OrdinalIgnoreCase) >= 0
                        || e.Data.IndexOf("%", StringComparison.OrdinalIgnoreCase) >= 0)
                        _logger?.LogDebug("DepotDownloader: " + e.Data);

                    int? parsed = TryParseDepotDownloadPercent(e.Data);
                    if (!parsed.HasValue)
                        return;

                    // Keep a little headroom for promote/verify after the process exits.
                    int mapped = 5 + (parsed.Value * 85) / 100;
                    if (mapped < lastReportedPct)
                        return;
                    lastReportedPct = mapped;
                    progress?.Invoke(
                        "Downloading redistributable depot " + depotId + " (" + parsed.Value + "%)…",
                        mapped);
                };
                process.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data == null)
                        return;
                    stderr.AppendLine(e.Data);
                    _logger?.LogDebug("DepotDownloader stderr: " + e.Data);
                };

                if (!process.Start())
                    return ValidationResult.Failure("Could not start DepotDownloader.");

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                int timeoutMs = DepotDownloadTimeoutMinutes * 60 * 1000;
                int waited = 0;
                const int pollMs = 250;
                while (!process.WaitForExit(pollMs))
                {
                    waited += pollMs;
                    if (waited >= timeoutMs)
                    {
                        try { process.Kill(); } catch { }
                        return ValidationResult.Failure(
                            $"DepotDownloader timed out after {DepotDownloadTimeoutMinutes} minutes for depot {depotId}.");
                    }
                    if (isCancelled != null && isCancelled())
                    {
                        try { process.Kill(); } catch { }
                        return ValidationResult.Failure("Dependency install cancelled.");
                    }
                }

                // Ensure async readers finish.
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    string detail = stderr.ToString().Trim();
                    if (string.IsNullOrEmpty(detail))
                        detail = stdout.ToString().Trim();
                    if (detail.Length > 500)
                        detail = detail.Substring(detail.Length - 500);
                    return ValidationResult.Failure(
                        $"DepotDownloader exited {process.ExitCode} for depot {depotId}"
                        + (string.IsNullOrEmpty(detail) ? "." : ": " + detail));
                }
            }

            progress?.Invoke("Verifying redistributable files…", 92);

            // DepotDownloader may nest under depots\{id}\{manifest}\ — flatten if needed.
            if (!File.Exists(scriptPath))
                TryPromoteNestedDepotOutput(targetSharedRoot, relativeHint: Path.GetFileName(scriptPath));

            if (!File.Exists(scriptPath))
            {
                return ValidationResult.Failure(
                    $"Depot {depotId} downloaded but install script is still missing: {scriptPath}");
            }

            _logger?.LogMessage($"Install scripts: common-redist depot {depotId} ready.");
            progress?.Invoke("Redistributable files ready.", 100);
            return ValidationResult.Success();
        }

        private static int? TryParseDepotDownloadPercent(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return null;
            Match match = DepotPercentRegex.Match(line);
            if (!match.Success)
                return null;
            if (!double.TryParse(
                    match.Groups[1].Value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out double value))
                return null;
            if (value < 0 || value > 100)
                return null;
            return (int)Math.Round(value);
        }

        private ValidationResult EnsureDepotDownloaderTool()
        {
            lock (ToolGate)
            {
                string exePath = GetDepotDownloaderExecutablePath();
                if (File.Exists(exePath))
                    return ValidationResult.Success();

                string toolRoot = GetDepotDownloaderToolRoot();
                Directory.CreateDirectory(toolRoot);

                string url = SteamCommonRedistributableConstants.DepotDownloaderWindowsX64DownloadUrl;
                if (!PathValidationHelper.IsTrustedGitHubReleaseDownloadUrl(url))
                    return ValidationResult.Failure("DepotDownloader download URL is not a trusted GitHub release URL.");

                string zipPath = Path.Combine(toolRoot, SteamCommonRedistributableConstants.DepotDownloaderWindowsX64ZipFileName);
                string extractDir = Path.Combine(toolRoot, "extract");

                try
                {
                    _logger?.LogMessage("Install scripts: downloading DepotDownloader tool…");
                    DownloadFileSync(url, zipPath, ToolDownloadTimeoutSeconds);

                    if (Directory.Exists(extractDir))
                        Directory.Delete(extractDir, recursive: true);
                    Directory.CreateDirectory(extractDir);

                    global::SmartGoldbergEmu.ExtractKit.ExtractKit.ExtractAll(zipPath, extractDir);

                    string foundExe = FindFileRecursive(
                        extractDir,
                        SteamCommonRedistributableConstants.DepotDownloaderExecutableFileName);
                    if (string.IsNullOrEmpty(foundExe) || !File.Exists(foundExe))
                        return ValidationResult.Failure("DepotDownloader.zip did not contain DepotDownloader.exe.");

                    string destExe = exePath;
                    Directory.CreateDirectory(Path.GetDirectoryName(destExe) ?? toolRoot);
                    File.Copy(foundExe, destExe, overwrite: true);

                    // Keep companion runtime files beside the exe (self-contained publish).
                    string foundDir = Path.GetDirectoryName(foundExe);
                    if (!string.IsNullOrEmpty(foundDir))
                    {
                        foreach (string file in Directory.GetFiles(foundDir))
                        {
                            string name = Path.GetFileName(file);
                            if (string.IsNullOrEmpty(name))
                                continue;
                            string dest = Path.Combine(Path.GetDirectoryName(destExe) ?? toolRoot, name);
                            if (!File.Exists(dest))
                                File.Copy(file, dest, overwrite: false);
                        }
                    }

                    TryDeleteQuiet(zipPath);
                    TryDeleteDirectoryQuiet(extractDir);

                    if (!File.Exists(destExe))
                        return ValidationResult.Failure("DepotDownloader.exe is missing after extract.");

                    _logger?.LogDebug("Install scripts: DepotDownloader tool ready at " + destExe);
                    return ValidationResult.Success();
                }
                catch (Exception ex)
                {
                    return ValidationResult.Failure("Failed to bootstrap DepotDownloader: " + ex.Message);
                }
            }
        }

        private static void DownloadFileSync(string url, string destinationPath, int timeoutSeconds)
        {
            using (var http = HttpServiceFactory.Create(TimeSpan.FromSeconds(timeoutSeconds)))
            {
                Task download = http.DownloadFileAsync(url, destinationPath);
                download.ConfigureAwait(false).GetAwaiter().GetResult();
            }
        }

        private static void TryPromoteNestedDepotOutput(string sharedRoot, string relativeHint)
        {
            // Some DepotDownloader layouts use depots\<depotId>\<manifestId>\_CommonRedist\...
            string depotsRoot = Path.Combine(sharedRoot, "depots");
            if (!Directory.Exists(depotsRoot))
                return;

            try
            {
                foreach (string manifestDir in Directory.GetDirectories(depotsRoot, "*", SearchOption.AllDirectories))
                {
                    string nestedCommon = Path.Combine(manifestDir, PathConstants.SteamCommonRedistFolderName);
                    if (!Directory.Exists(nestedCommon))
                        continue;

                    string destCommon = Path.Combine(sharedRoot, PathConstants.SteamCommonRedistFolderName);
                    CopyDirectoryMerge(nestedCommon, destCommon);
                    return;
                }

                // Fallback: search for the installscript file name and copy its _CommonRedist ancestor.
                if (string.IsNullOrEmpty(relativeHint))
                    return;
                foreach (string hit in Directory.GetFiles(depotsRoot, relativeHint, SearchOption.AllDirectories))
                {
                    DirectoryInfo dir = new DirectoryInfo(Path.GetDirectoryName(hit) ?? string.Empty);
                    while (dir != null && !string.Equals(dir.Name, PathConstants.SteamCommonRedistFolderName, StringComparison.OrdinalIgnoreCase))
                        dir = dir.Parent;
                    if (dir == null || !dir.Exists)
                        continue;
                    string destCommon = Path.Combine(sharedRoot, PathConstants.SteamCommonRedistFolderName);
                    CopyDirectoryMerge(dir.FullName, destCommon);
                    return;
                }
            }
            catch
            {
            }
        }

        private static void CopyDirectoryMerge(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);
            foreach (string file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string rel = file.Substring(sourceDir.Length).TrimStart('\\', '/');
                string dest = Path.Combine(destDir, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(dest) ?? destDir);
                File.Copy(file, dest, overwrite: true);
            }
        }

        private static string FindFileRecursive(string root, string fileName)
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                return null;
            try
            {
                foreach (string path in Directory.GetFiles(root, fileName, SearchOption.AllDirectories))
                    return path;
            }
            catch
            {
            }
            return null;
        }

        private static string GetDepotDownloaderToolRoot()
        {
            return Path.Combine(
                PathConstants.LocalAppDataPerUserDirectory,
                "tools",
                SteamCommonRedistributableConstants.DepotDownloaderToolsFolderName,
                SteamCommonRedistributableConstants.DepotDownloaderVersionTag);
        }

        private static string GetDepotDownloaderExecutablePath()
        {
            return Path.Combine(
                GetDepotDownloaderToolRoot(),
                "bin",
                SteamCommonRedistributableConstants.DepotDownloaderExecutableFileName);
        }

        private static void TryDeleteQuiet(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
            }
        }

        private static void TryDeleteDirectoryQuiet(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, recursive: true);
            }
            catch
            {
            }
        }
    }
}
