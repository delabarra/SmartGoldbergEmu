using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Services
{
    internal sealed class SteamlessCliRunResult
    {
        public int ExitCode { get; set; }
        public string StandardOutput { get; set; }
        public string StandardError { get; set; }

        public string GetCombinedOutput()
        {
            var parts = new[] { StandardOutput, StandardError };
            return string.Join(Environment.NewLine, parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()));
        }

        public string GetSummaryLine()
        {
            string combined = GetCombinedOutput();
            if (string.IsNullOrWhiteSpace(combined))
                return null;

            string[] lines = combined.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                string line = lines[i].Trim();
                if (IsIgnorableCliLine(line))
                    continue;

                if (line.IndexOf("failed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf("invalid", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    line.IndexOf("cannot", StringComparison.OrdinalIgnoreCase) >= 0)
                    return SteamlessFeedback.CleanCliLine(line);

                if (line.IndexOf("Successfully unpacked", StringComparison.OrdinalIgnoreCase) >= 0)
                    return SteamlessFeedback.CleanCliLine(line);
            }

            for (int i = lines.Length - 1; i >= 0; i--)
            {
                string line = lines[i].Trim();
                if (IsIgnorableCliLine(line))
                    continue;
                if (line.IndexOf("Steamless", StringComparison.OrdinalIgnoreCase) >= 0)
                    return SteamlessFeedback.CleanCliLine(line);
            }

            return SteamlessFeedback.CleanCliLine(lines[lines.Length - 1].Trim());
        }

        private static bool IsIgnorableCliLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return true;

            string text = line.Trim();
            if (text.Length < 2)
                return true;

            if (text.StartsWith("_", StringComparison.Ordinal) ||
                text.StartsWith("/", StringComparison.Ordinal) ||
                text.StartsWith("\\", StringComparison.Ordinal))
                return true;

            if (text.IndexOf("Donations", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("GitHub", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("Homepage", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("by atom0s", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("SteamStub DRM Remover", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return false;
        }
    }

    public sealed class SteamlessService
    {
        public bool TryGetConfiguredCli(out string cliPath, out string installRoot)
        {
            string savedPath = ServiceLocator.AppDataService.GetSteamlessCliPath();
            return TryValidateCliPath(savedPath, out cliPath, out installRoot);
        }

        public bool HasInvalidSavedCliPath()
        {
            string savedPath = ServiceLocator.AppDataService.GetSteamlessCliPath();
            if (string.IsNullOrEmpty(savedPath))
                return false;

            return !TryValidateCliPath(savedPath, out _, out _);
        }

        public bool TryValidateCliPath(string cliPath, out string validatedCliPath, out string installRoot)
        {
            validatedCliPath = null;
            installRoot = null;

            if (string.IsNullOrWhiteSpace(cliPath) || !File.Exists(cliPath.Trim()))
                return false;

            try
            {
                validatedCliPath = Path.GetFullPath(cliPath.Trim());
            }
            catch
            {
                return false;
            }

            if (!string.Equals(
                Path.GetFileName(validatedCliPath),
                PathConstants.SteamlessCliExecutableName,
                StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            installRoot = Path.GetDirectoryName(validatedCliPath);
            if (string.IsNullOrEmpty(installRoot))
                return false;

            string apiPluginPath = PathConstants.CombineSteamlessApiPluginPath(installRoot);
            if (!File.Exists(apiPluginPath))
                return false;

            string pluginsDir = PathConstants.CombineSteamlessPluginsDirectory(installRoot);
            return !string.IsNullOrEmpty(pluginsDir) && Directory.Exists(pluginsDir);
        }

        public string GetCliBrowseInitialDirectory()
        {
            string savedPath = ServiceLocator.AppDataService.GetSteamlessCliPath();
            if (!string.IsNullOrWhiteSpace(savedPath))
            {
                try
                {
                    string savedDir = Path.GetDirectoryName(Path.GetFullPath(savedPath.Trim()));
                    if (!string.IsNullOrEmpty(savedDir) && Directory.Exists(savedDir))
                        return savedDir;
                }
                catch
                {
                }
            }

            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrEmpty(programFiles) && Directory.Exists(programFiles))
                return programFiles;

            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (!string.IsNullOrEmpty(programFilesX86) && Directory.Exists(programFilesX86))
                return programFilesX86;

            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(userProfile) && Directory.Exists(userProfile))
                return userProfile;

            try
            {
                string systemRoot = Path.GetPathRoot(Environment.SystemDirectory);
                if (!string.IsNullOrEmpty(systemRoot) && Directory.Exists(systemRoot))
                    return systemRoot;
            }
            catch
            {
            }

            return null;
        }

        public ValidationResult TryPersistCliPath(string candidatePath, out string validatedCliPath)
        {
            validatedCliPath = null;
            if (!TryValidateCliPath(candidatePath, out validatedCliPath, out _))
                return ValidationResult.Failure(SteamlessFeedback.InvalidSteamlessInstallBody());

            return ServiceLocator.AppDataService.SetSteamlessCliPath(validatedCliPath);
        }

        public async Task<SteamlessApplyResult> ApplySteamlessAsync(
            string gameExecutablePath,
            SteamlessCliOptions cliOptions,
            ILogService log,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetConfiguredCli(out string cliPath, out string installRoot))
            {
                return new SteamlessApplyResult
                {
                    Outcome = SteamlessApplyOutcome.NotInstalled
                };
            }

            if (string.IsNullOrWhiteSpace(gameExecutablePath) || !File.Exists(gameExecutablePath))
            {
                return new SteamlessApplyResult
                {
                    Outcome = SteamlessApplyOutcome.ExecutablePathInvalid,
                    LogDetail = "Executable path is missing or the file does not exist."
                };
            }

            string executablePath;
            try
            {
                executablePath = Path.GetFullPath(gameExecutablePath.Trim());
            }
            catch (Exception ex)
            {
                log?.LogError("Steamless: invalid executable path.", ex);
                return new SteamlessApplyResult
                {
                    Outcome = SteamlessApplyOutcome.ExecutablePathInvalid,
                    LogDetail = ex.Message
                };
            }

            if (!PathValidationHelper.IsSafeFilePath(executablePath))
            {
                return new SteamlessApplyResult
                {
                    Outcome = SteamlessApplyOutcome.ExecutablePathInvalid,
                    LogDetail = "Executable path failed safety validation."
                };
            }

            string unpackedPath = executablePath + PathConstants.SteamlessUnpackedExecutableSuffix;
            string backupPath = PathConstants.BuildSteamlessOriginalBackupPath(executablePath);

            log?.LogMessage($"Steamless: unpacking {executablePath}");

            try
            {
                var options = cliOptions ?? SteamlessCliOptions.Default;
                log?.LogMessage("Steamless CLI: " + options.BuildArguments(executablePath));
                SteamlessCliRunResult runResult = await RunCliAsync(cliPath, installRoot, options, executablePath, cancellationToken).ConfigureAwait(false);
                LogCliOutput(runResult, log);

                bool unpackedExists = File.Exists(unpackedPath);
                if (!unpackedExists)
                {
                    if (runResult.ExitCode != 0)
                        return ClassifyCliFailure(runResult, backupPath);

                    log?.LogWarning("Steamless: unpacked file was not created. Expected: " + unpackedPath);
                    return new SteamlessApplyResult
                    {
                        Outcome = SteamlessApplyOutcome.UnpackedFileMissing,
                        LogDetail = "Expected: " + unpackedPath
                    };
                }

                if (runResult.ExitCode != 0)
                {
                    log?.LogWarning("Steamless exited with code " + runResult.ExitCode + " but unpacked file exists; replacing executable.");
                }

                try
                {
                    await Task.Run(() => ReplaceExecutableWithUnpacked(executablePath, unpackedPath, backupPath, log), cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    log?.LogError("Steamless: could not replace executable.", ex);
                    return new SteamlessApplyResult
                    {
                        Outcome = SteamlessApplyOutcome.FileReplaceFailed,
                        LogDetail = ex.Message
                    };
                }

                log?.LogMessage($"Steamless: replaced {executablePath} (original: {backupPath})");
                return new SteamlessApplyResult
                {
                    Outcome = SteamlessApplyOutcome.Success
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                log?.LogError("Steamless: run failed.", ex);
                return new SteamlessApplyResult
                {
                    Outcome = SteamlessApplyOutcome.Unexpected,
                    LogDetail = ex.Message
                };
            }
        }

        private static SteamlessApplyResult ClassifyCliFailure(SteamlessCliRunResult runResult, string backupPath)
        {
            string logDetail = BuildCliLogDetail(runResult);

            if (LooksLikeAlreadyApplied(runResult, backupPath))
            {
                return new SteamlessApplyResult
                {
                    Outcome = SteamlessApplyOutcome.AlreadyApplied,
                    LogDetail = logDetail
                };
            }

            return new SteamlessApplyResult
            {
                Outcome = SteamlessApplyOutcome.CannotProcess,
                LogDetail = logDetail
            };
        }

        private static bool LooksLikeAlreadyApplied(SteamlessCliRunResult runResult, string backupPath)
        {
            if (runResult == null || runResult.ExitCode == 0)
                return false;

            string combined = runResult.GetCombinedOutput() ?? string.Empty;
            if (combined.IndexOf("all unpackers failed", StringComparison.OrdinalIgnoreCase) < 0)
                return false;

            // game_o.exe is created when we successfully ran Steamless before; strongest signal for a second run.
            return File.Exists(backupPath);
        }

        private static string BuildCliLogDetail(SteamlessCliRunResult runResult)
        {
            if (runResult == null)
                return null;

            string summary = runResult.GetSummaryLine();
            if (!string.IsNullOrWhiteSpace(summary))
                return summary;

            string combined = runResult.GetCombinedOutput();
            if (!string.IsNullOrWhiteSpace(combined))
                return combined;

            return "Exit code " + runResult.ExitCode + ".";
        }

        private static void LogCliOutput(SteamlessCliRunResult runResult, ILogService log)
        {
            if (log == null || runResult == null)
                return;

            string combined = runResult.GetCombinedOutput();
            if (string.IsNullOrWhiteSpace(combined))
            {
                if (runResult.ExitCode != 0)
                    log.LogWarning("Steamless exited with code " + runResult.ExitCode + " but produced no output.");
                return;
            }

            if (runResult.ExitCode != 0)
                log.LogWarning("Steamless output (exit code " + runResult.ExitCode + "):" + Environment.NewLine + combined);
            else
                log.LogMessage("Steamless output:" + Environment.NewLine + combined);
        }

        private static Task<SteamlessCliRunResult> RunCliAsync(
            string cliPath,
            string installRoot,
            SteamlessCliOptions cliOptions,
            string executablePath,
            CancellationToken cancellationToken)
        {
            string arguments = (cliOptions ?? SteamlessCliOptions.Default).BuildArguments(executablePath);
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                var startInfo = new ProcessStartInfo
                {
                    FileName = cliPath,
                    Arguments = arguments,
                    WorkingDirectory = installRoot,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                        throw new InvalidOperationException("Failed to start Steamless.CLI.exe.");

                    string stdout = process.StandardOutput.ReadToEnd();
                    string stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    cancellationToken.ThrowIfCancellationRequested();

                    return new SteamlessCliRunResult
                    {
                        ExitCode = process.ExitCode,
                        StandardOutput = stdout?.Trim(),
                        StandardError = stderr?.Trim()
                    };
                }
            }, cancellationToken);
        }

        private static void ReplaceExecutableWithUnpacked(string executablePath, string unpackedPath, string backupPath, ILogService log)
        {
            if (File.Exists(backupPath))
            {
                log?.LogMessage("Steamless: removing existing original backup: " + backupPath);
                File.Delete(backupPath);
            }

            log?.LogMessage("Steamless: moving original to " + backupPath);
            File.Move(executablePath, backupPath);
            try
            {
                log?.LogMessage("Steamless: moving unpacked to " + executablePath);
                File.Move(unpackedPath, executablePath);
            }
            catch
            {
                try
                {
                    if (!File.Exists(executablePath) && File.Exists(backupPath))
                        File.Move(backupPath, executablePath);
                }
                catch (Exception rollbackEx)
                {
                    log?.LogError("Steamless: failed to restore original executable after swap error.", rollbackEx);
                }

                throw;
            }
        }
    }
}
