using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.StubKit;

namespace SmartGoldbergEmu.Services
{
    public sealed class StubKitService
    {
        private static readonly object DetectCacheLock = new object();
        private static readonly Dictionary<string, DetectCacheEntry> DetectCache =
            new Dictionary<string, DetectCacheEntry>(StringComparer.OrdinalIgnoreCase);
        private const int DetectCacheMaxEntries = 64;

        private sealed class DetectCacheEntry
        {
            public long LastWriteUtcTicks;
            public DetectResult Result;
        }

        // Distinct existing exes in launcher order; settings Path appended only if not already listed.
        // Detection is deferred (IsDetectionPending) so the UI can list Loading… items first.
        public async Task<IReadOnlyList<StubExecutableTarget>> ResolveTargetsAsync(
            GameConfig game,
            CancellationToken cancellationToken = default)
        {
            var ordered = new List<StubExecutableTarget>();
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string settingsFullPath = null;

            if (game != null &&
                GameFolderPathHelper.TryResolveExecutableForStubRemoval(game, out string settingsPath) &&
                !string.IsNullOrWhiteSpace(settingsPath) &&
                File.Exists(settingsPath))
            {
                settingsFullPath = NormalizeFullPath(settingsPath);
            }

            if (game == null)
            {
                TryAppendSettingsTarget(game, settingsFullPath, ordered, seenPaths);
                return ordered;
            }

            cancellationToken.ThrowIfCancellationRequested();

            List<LaunchOption> allOptions = await ServiceLocator.LaunchOptionService
                .ExtractLaunchOptionsIncludingUserIniAsync(game, cancellationToken)
                .ConfigureAwait(false);

            List<LaunchOption> filtered = ServiceLocator.LaunchOptionService
                .FilterLaunchOptionsForCurrentSettings(allOptions);

            if (filtered != null)
            {
                foreach (LaunchOption option in filtered)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (option == null || string.IsNullOrWhiteSpace(option.Executable))
                        continue;

                    ResolvedLaunchCommand command;
                    try
                    {
                        command = ServiceLocator.GameLaunchService.GetResolvedLaunchCommand(game, option);
                    }
                    catch
                    {
                        continue;
                    }

                    if (command == null || string.IsNullOrWhiteSpace(command.ExecutablePath))
                        continue;

                    string resolvedFull = NormalizeFullPath(command.ExecutablePath);
                    if (string.IsNullOrEmpty(resolvedFull) || !File.Exists(resolvedFull))
                        continue;

                    // GetResolvedLaunchCommand falls back to game.Path when the option exe is missing;
                    // skip those so we do not attach the wrong launch-option label to the settings exe.
                    string optionFileName = Path.GetFileName(option.Executable.Trim());
                    string resolvedFileName = Path.GetFileName(resolvedFull);
                    if (!string.IsNullOrEmpty(optionFileName) &&
                        !string.Equals(optionFileName, resolvedFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!seenPaths.Add(resolvedFull))
                        continue;

                    string displayName = !string.IsNullOrWhiteSpace(option.Description)
                        ? option.Description.Trim()
                        : (!string.IsNullOrEmpty(resolvedFileName) ? resolvedFileName : resolvedFull);

                    ordered.Add(CreatePendingTarget(
                        game,
                        resolvedFull,
                        displayName,
                        isSettingsExecutable: !string.IsNullOrEmpty(settingsFullPath) &&
                            string.Equals(resolvedFull, settingsFullPath, StringComparison.OrdinalIgnoreCase)));
                }
            }

            TryAppendSettingsTarget(game, settingsFullPath, ordered, seenPaths);
            return ordered;
        }

        // Fills detection fields on a pending target (PE scan + backup check).
        public async Task DetectTargetAsync(
            StubExecutableTarget target,
            CancellationToken cancellationToken = default)
        {
            if (target == null || string.IsNullOrWhiteSpace(target.FullPath))
                return;

            DetectResult detect = await Task.Run(() => DetectExecutable(target.FullPath), cancellationToken)
                .ConfigureAwait(false);

            ApplyDetection(target, detect);
        }

        // Drop LOH pages left by prefix detect when a menu finishes scanning.
        public static void ReleaseTemporaryBuffers()
        {
            ReleaseLargeObjectHeapAfterStubKit();
        }

        // Reads only the PE prefix StubClassifier needs (not the whole executable). Cached per path+mtime.
        public static DetectResult DetectExecutable(string executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            {
                return new DetectResult
                {
                    Variant = StubVariant.None,
                    Name = "none",
                    CanRemove = false
                };
            }

            string fullPath;
            long writeTicks;
            try
            {
                fullPath = Path.GetFullPath(executablePath.Trim());
                writeTicks = File.GetLastWriteTimeUtc(fullPath).Ticks;
            }
            catch
            {
                return SteamStub.DetectFile(executablePath);
            }

            lock (DetectCacheLock)
            {
                DetectCacheEntry cached;
                if (DetectCache.TryGetValue(fullPath, out cached) &&
                    cached != null &&
                    cached.Result != null &&
                    cached.LastWriteUtcTicks == writeTicks)
                {
                    return cached.Result;
                }
            }

            DetectResult detect = SteamStub.DetectFile(fullPath);

            lock (DetectCacheLock)
            {
                if (DetectCache.Count >= DetectCacheMaxEntries)
                    DetectCache.Clear();

                DetectCache[fullPath] = new DetectCacheEntry
                {
                    LastWriteUtcTicks = writeTicks,
                    Result = detect
                };
            }

            return detect;
        }

        public static void InvalidateDetectCache(string executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath))
                return;

            try
            {
                string fullPath = Path.GetFullPath(executablePath.Trim());
                lock (DetectCacheLock)
                {
                    DetectCache.Remove(fullPath);
                }
            }
            catch
            {
            }
        }

        // True when a game_o.exe backup exists beside this executable.
        public static bool HasOriginalBackup(string executablePath)
        {
            if (string.IsNullOrWhiteSpace(executablePath))
                return false;

            string backupPath = PathConstants.BuildStubOriginalBackupPath(executablePath);
            return !string.IsNullOrEmpty(backupPath) && File.Exists(backupPath);
        }

        public async Task<StubKitApplyResult> RestoreAsync(
            string gameExecutablePath,
            ILogService log,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(gameExecutablePath) || !File.Exists(gameExecutablePath))
            {
                return new StubKitApplyResult
                {
                    Outcome = StubKitApplyOutcome.ExecutablePathInvalid,
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
                log?.LogError("StubKit restore: invalid executable path.", ex);
                return new StubKitApplyResult
                {
                    Outcome = StubKitApplyOutcome.ExecutablePathInvalid,
                    LogDetail = ex.Message
                };
            }

            if (!PathValidationHelper.IsSafeFilePath(executablePath))
            {
                return new StubKitApplyResult
                {
                    Outcome = StubKitApplyOutcome.ExecutablePathInvalid,
                    LogDetail = "Executable path failed safety validation."
                };
            }

            string backupPath = PathConstants.BuildStubOriginalBackupPath(executablePath);
            if (string.IsNullOrEmpty(backupPath) || !File.Exists(backupPath))
            {
                return new StubKitApplyResult
                {
                    Outcome = StubKitApplyOutcome.BackupMissing,
                    LogDetail = "Original backup not found: " + backupPath
                };
            }

            log?.LogMessage("StubKit: restoring original from " + backupPath);

            try
            {
                await Task.Run(() => RestoreExecutableFromBackup(executablePath, backupPath, log), cancellationToken)
                    .ConfigureAwait(false);

                log?.LogMessage("StubKit: restored " + executablePath + " from " + backupPath);
                InvalidateDetectCache(executablePath);
                ReleaseLargeObjectHeapAfterStubKit();
                return new StubKitApplyResult
                {
                    Outcome = StubKitApplyOutcome.Restored,
                    LogDetail = backupPath
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                log?.LogError("StubKit: restore failed.", ex);
                ReleaseLargeObjectHeapAfterStubKit();
                return new StubKitApplyResult
                {
                    Outcome = StubKitApplyOutcome.RestoreFailed,
                    LogDetail = ex.Message
                };
            }
        }

        public async Task<StubKitApplyResult> ApplyAsync(
            string gameExecutablePath,
            ILogService log,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(gameExecutablePath) || !File.Exists(gameExecutablePath))
            {
                return new StubKitApplyResult
                {
                    Outcome = StubKitApplyOutcome.ExecutablePathInvalid,
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
                log?.LogError("StubKit: invalid executable path.", ex);
                return new StubKitApplyResult
                {
                    Outcome = StubKitApplyOutcome.ExecutablePathInvalid,
                    LogDetail = ex.Message
                };
            }

            if (!PathValidationHelper.IsSafeFilePath(executablePath))
            {
                return new StubKitApplyResult
                {
                    Outcome = StubKitApplyOutcome.ExecutablePathInvalid,
                    LogDetail = "Executable path failed safety validation."
                };
            }

            string backupPath = PathConstants.BuildStubOriginalBackupPath(executablePath);
            log?.LogMessage("StubKit: unpacking " + executablePath);

            try
            {
                // Keep the full PE buffer inside this worker only — do not return it to the async
                // state machine (that pinned multi‑MB LOH arrays after patch/restore).
                StubKitApplyResult result = await Task.Run(
                        () => ApplyOnBackground(executablePath, backupPath, log),
                        cancellationToken)
                    .ConfigureAwait(false);

                InvalidateDetectCache(executablePath);
                ReleaseLargeObjectHeapAfterStubKit();
                return result;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                log?.LogError("StubKit: run failed.", ex);
                ReleaseLargeObjectHeapAfterStubKit();
                return new StubKitApplyResult
                {
                    Outcome = StubKitApplyOutcome.Unexpected,
                    LogDetail = ex.Message
                };
            }
        }

        private static StubKitApplyResult ApplyOnBackground(string executablePath, string backupPath, ILogService log)
        {
            byte[] peBytes = null;
            byte[] unpacked = null;
            try
            {
                peBytes = File.ReadAllBytes(executablePath);

                StubUnpackInfo info;
                bool success = SteamStub.TryUnpack(
                    peBytes,
                    UnpackOptions.Default,
                    mutateInPlace: true,
                    out unpacked,
                    out info);

                // In-place unpack aliases peBytes; drop the extra root before writing/failures return.
                peBytes = null;

                if (!success)
                {
                    unpacked = null;
                    StubKitApplyOutcome outcome = StubKitApplyOutcome.UnpackFailed;
                    if (info != null && info.Variant == StubVariant.None)
                        outcome = StubKitApplyOutcome.NoStubFound;
                    else if (info != null &&
                             !string.IsNullOrEmpty(info.ErrorMessage) &&
                             info.ErrorMessage.IndexOf("cannot be removed", StringComparison.OrdinalIgnoreCase) >= 0)
                        outcome = StubKitApplyOutcome.CannotRemove;

                    return new StubKitApplyResult
                    {
                        Outcome = outcome,
                        LogDetail = outcome == StubKitApplyOutcome.UnpackFailed
                            ? (info != null ? info.ErrorMessage : null)
                            : (info != null ? info.VariantName : null)
                    };
                }

                string unpackedPath = executablePath + PathConstants.StubUnpackedExecutableSuffix;
                try
                {
                    ReplaceExecutableWithUnpacked(executablePath, unpacked, unpackedPath, backupPath, log);
                }
                catch (Exception ex)
                {
                    log?.LogError("StubKit: could not replace executable.", ex);
                    return new StubKitApplyResult
                    {
                        Outcome = StubKitApplyOutcome.FileReplaceFailed,
                        LogDetail = ex.Message
                    };
                }

                log?.LogMessage("StubKit: replaced " + executablePath + " (original: " + backupPath + ")");
                string summary = info != null ? info.Summary : null;
                if (!string.IsNullOrWhiteSpace(summary))
                    log?.LogMessage("StubKit: " + summary);

                return new StubKitApplyResult
                {
                    Outcome = StubKitApplyOutcome.Success,
                    Summary = summary,
                    LogDetail = summary
                };
            }
            finally
            {
                unpacked = null;
                peBytes = null;
            }
        }

        // Full PE images land on the LOH; without a compact, Working Set often stays elevated after refs die.
        private static void ReleaseLargeObjectHeapAfterStubKit()
        {
            try
            {
                GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
            }
            catch
            {
            }
        }

        private static void TryAppendSettingsTarget(
            GameConfig game,
            string settingsFullPath,
            List<StubExecutableTarget> ordered,
            HashSet<string> seenPaths)
        {
            if (string.IsNullOrEmpty(settingsFullPath) || !seenPaths.Add(settingsFullPath))
                return;

            ordered.Add(CreatePendingTarget(
                game,
                settingsFullPath,
                "Settings executable",
                isSettingsExecutable: true));
        }

        private static StubExecutableTarget CreatePendingTarget(
            GameConfig game,
            string fullPath,
            string displayName,
            bool isSettingsExecutable)
        {
            return new StubExecutableTarget
            {
                FullPath = fullPath,
                DisplayName = displayName,
                RelativeOrExeHint = BuildRelativeOrExeHint(game, fullPath),
                IsSettingsExecutable = isSettingsExecutable,
                IsDetectionPending = true,
                HasOriginalBackup = HasOriginalBackup(fullPath)
            };
        }

        private static void ApplyDetection(StubExecutableTarget target, DetectResult detect)
        {
            bool hasKnownStub = detect != null && detect.Variant != StubVariant.None;
            // Unknown .bind still surfaces as non-removable rather than a clean "no stub".
            bool looksProtected = hasKnownStub
                || (detect != null
                    && !string.IsNullOrWhiteSpace(detect.Name)
                    && !string.Equals(detect.Name, "none", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(detect.Name, "unreadable", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(detect.Name, "invalid PE", StringComparison.OrdinalIgnoreCase));

            target.HasSteamStub = looksProtected;
            target.CanRemove = detect != null && detect.CanRemove;
            target.HasOriginalBackup = HasOriginalBackup(target.FullPath);
            target.StubName = detect != null ? detect.Name : "none";
            target.IsDetectionPending = false;
        }

        private static string NormalizeFullPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            try
            {
                return Path.GetFullPath(path.Trim());
            }
            catch
            {
                return null;
            }
        }

        private static string BuildRelativeOrExeHint(GameConfig game, string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath))
                return null;

            string fileName = Path.GetFileName(fullPath);
            if (game == null || string.IsNullOrWhiteSpace(game.StartFolder))
                return fileName;

            try
            {
                string baseFolder = Path.GetFullPath(game.StartFolder.Trim());
                if (!baseFolder.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) &&
                    !baseFolder.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                {
                    baseFolder += Path.DirectorySeparatorChar;
                }

                string full = Path.GetFullPath(fullPath);
                if (full.StartsWith(baseFolder, StringComparison.OrdinalIgnoreCase))
                    return full.Substring(baseFolder.Length);

                Uri baseUri = new Uri(baseFolder);
                Uri fileUri = new Uri(full);
                string relative = Uri.UnescapeDataString(baseUri.MakeRelativeUri(fileUri).ToString())
                    .Replace('/', Path.DirectorySeparatorChar);
                if (!string.IsNullOrEmpty(relative) && !relative.StartsWith("..", StringComparison.Ordinal))
                    return relative;
            }
            catch
            {
            }

            return fileName;
        }

        private static void RestoreExecutableFromBackup(string executablePath, string backupPath, ILogService log)
        {
            string tempPath = executablePath + ".restore.tmp";
            if (File.Exists(tempPath))
                File.Delete(tempPath);

            log?.LogMessage("StubKit: moving current executable aside: " + tempPath);
            File.Move(executablePath, tempPath);
            try
            {
                log?.LogMessage("StubKit: moving backup to " + executablePath);
                File.Move(backupPath, executablePath);
            }
            catch
            {
                try
                {
                    if (!File.Exists(executablePath) && File.Exists(tempPath))
                        File.Move(tempPath, executablePath);
                }
                catch (Exception rollbackEx)
                {
                    log?.LogError("StubKit: failed to put current executable back after restore error.", rollbackEx);
                }

                throw;
            }

            try
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch (Exception ex)
            {
                log?.LogWarning("StubKit: restored executable but could not delete temporary file: " + tempPath + " (" + ex.Message + ")");
            }
        }

        private static void ReplaceExecutableWithUnpacked(
            string executablePath,
            byte[] unpackedBytes,
            string unpackedPath,
            string backupPath,
            ILogService log)
        {
            if (File.Exists(unpackedPath))
                File.Delete(unpackedPath);

            File.WriteAllBytes(unpackedPath, unpackedBytes);

            if (File.Exists(backupPath))
            {
                log?.LogMessage("StubKit: removing existing original backup: " + backupPath);
                File.Delete(backupPath);
            }

            log?.LogMessage("StubKit: moving original to " + backupPath);
            File.Move(executablePath, backupPath);
            try
            {
                log?.LogMessage("StubKit: moving unpacked to " + executablePath);
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
                    log?.LogError("StubKit: failed to restore original executable after swap error.", rollbackEx);
                }

                throw;
            }
        }
    }
}
