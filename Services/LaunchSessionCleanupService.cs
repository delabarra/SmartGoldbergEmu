using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.JsonKit;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Services
{
    // Manifest I/O, detached watcher, and Goldberg deploy teardown for GameLaunchService.
    public class LaunchSessionCleanupService
    {
        private const int CleanupMutexWaitMs = 60_000;

        private readonly ILogService _logger;
        private readonly EmulatorConfigService _emulatorConfigService;

        public LaunchSessionCleanupService()
            : this(ServiceLocator.LogService, ServiceLocator.EmulatorConfigService)
        {
        }

        public LaunchSessionCleanupService(ILogService logger, EmulatorConfigService emulatorConfigService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _emulatorConfigService = emulatorConfigService ?? throw new ArgumentNullException(nameof(emulatorConfigService));
        }

        public static bool TryRunWatcherFromCommandLine(string[] args, ILogService logger)
        {
            if (logger == null)
                return false;

            var service = new LaunchSessionCleanupService(logger, new EmulatorConfigService());
            return service.TryHandleWatcherCommandLine(args);
        }

        public string GetManifestPathForAppId(ulong appId)
        {
            if (appId == 0)
                return null;
            return PathConstants.CombineLaunchSessionManifestPath(appId);
        }

        public string WriteSession(PersistedLaunchSession session)
        {
            if (session == null || session.AppId == 0)
                return null;

            if (string.IsNullOrEmpty(session.SessionGeneration))
                session.SessionGeneration = Guid.NewGuid().ToString("D");

            session.GameLibraryFolder = PathConstants.GetGameFolder(session.AppId);
            session.LoadDllsFolder = PathConstants.CombineGameSteamSettingsLoadDllsDirectory(session.AppId);

            string directory = PathConstants.LaunchSessionManifestDirectory;
            Directory.CreateDirectory(directory);
            string path = PathConstants.CombineLaunchSessionManifestPath(session.AppId);

            // Keep unfinished cleanup from an earlier session (process id 0); appended so this session's restores run first.
            if (TryLoad(path, out PersistedLaunchSession pendingRetry) && pendingRetry.GameProcessId == 0)
            {
                if (pendingRetry.DeploySites != null && pendingRetry.DeploySites.Count > 0)
                {
                    if (session.DeploySites == null)
                        session.DeploySites = new List<PersistedDeploySite>();
                    session.DeploySites.AddRange(pendingRetry.DeploySites);
                }

                // Prefer this session's stub restore; keep a pending one only when the new session has none.
                if (session.StubExeRestore == null && pendingRetry.StubExeRestore != null)
                    session.StubExeRestore = pendingRetry.StubExeRestore;
            }

            string json = JsonConvert.SerializeObject(session, JsonFormatting.Indented);
            File.WriteAllText(path, json);
            return path;
        }

        public bool TryLoad(string manifestPath, out PersistedLaunchSession session)
        {
            session = null;
            if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
                return false;

            if (!LaunchSessionCleanupTrustHelper.IsAllowedManifestPath(manifestPath))
            {
                _logger.LogWarning(
                    $"Refusing launch-session manifest outside {PathConstants.LaunchSessionManifestFolderName}: {manifestPath}");
                return false;
            }

            try
            {
                string json = File.ReadAllText(manifestPath);
                session = JsonConvert.DeserializeObject<PersistedLaunchSession>(json);
                if (session == null || session.AppId == 0)
                    return false;

                if (!LaunchSessionCleanupTrustHelper.SessionMatchesManifestPath(session, manifestPath))
                {
                    _logger.LogWarning(
                        $"Refusing launch-session manifest with AppId mismatch (file vs JSON): {manifestPath}");
                    session = null;
                    return false;
                }

                // Prefer launcher-written absolute games/{appId} paths when shape-valid; otherwise resolve from this process.
                // A detached watcher can have a different AppBaseDirectory (shadow-copy, alternate exe) than the launcher.
                string jsonLibrary = session.GameLibraryFolder;
                string jsonLoadDlls = session.LoadDllsFolder;
                if (LaunchSessionCleanupTrustHelper.IsAllowedGameLibraryFolder(session.AppId, jsonLibrary))
                    session.GameLibraryFolder = Path.GetFullPath(jsonLibrary.Trim());
                else
                    session.GameLibraryFolder = PathConstants.GetGameFolder(session.AppId);

                if (LaunchSessionCleanupTrustHelper.IsAllowedLoadDllsFolder(session.AppId, jsonLoadDlls))
                    session.LoadDllsFolder = Path.GetFullPath(jsonLoadDlls.Trim());
                else
                    session.LoadDllsFolder = Path.Combine(
                        session.GameLibraryFolder,
                        PathConstants.SteamSettingsFolderName,
                        PathConstants.GoldbergLoadDllsFolderName);
                return true;
            }
            catch
            {
                session = null;
                return false;
            }
        }

        public void TryDeleteManifest(string manifestPath)
        {
            if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
                return;

            try
            {
                File.Delete(manifestPath);
            }
            catch
            {
            }
        }

        public static bool SessionNeedsRegistryRestore(PersistedLaunchSession session)
        {
            return session != null && (session.RestoreActiveProcessRegistry || session.SourceModRestore != null);
        }

        public void TryRestoreRegistryAfterLoadWindow(string manifestPath, string expectedGeneration, int expectedProcessId)
        {
            if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
                return;

            if (!TryLoad(manifestPath, out PersistedLaunchSession session))
                return;

            if (!string.Equals(session.SessionGeneration, expectedGeneration, StringComparison.Ordinal)
                || session.GameProcessId != expectedProcessId)
            {
                return;
            }

            if (!SessionNeedsRegistryRestore(session))
                return;

            if (session.DeferRegistryRestoreUntilExit)
            {
                _logger.LogDebug(
                    $"Deferring registry restore until game exit for AppId {session.AppId} (PID {session.GameProcessId}).");
                return;
            }

            TryExecuteRegistryRestore(manifestPath);
        }

        public static long TryGetProcessStartTimeUtcTicks(Process process)
        {
            try
            {
                return process.StartTime.ToUniversalTime().Ticks;
            }
            catch
            {
                return 0;
            }
        }

        public bool IsSessionProcessAlive(PersistedLaunchSession session)
        {
            if (session == null || session.GameProcessId <= 0)
                return false;

            try
            {
                using (Process process = Process.GetProcessById(session.GameProcessId))
                {
                    if (process.HasExited)
                        return false;
                    if (session.GameProcessStartTimeUtcTicks == 0)
                        return true;

                    long startTicks = TryGetProcessStartTimeUtcTicks(process);
                    // Unreadable start time (e.g. elevated game): cannot prove reuse, so treat as the same process.
                    if (startTicks == 0)
                        return true;
                    return Math.Abs(startTicks - session.GameProcessStartTimeUtcTicks) <= TimeSpan.TicksPerSecond;
                }
            }
            catch
            {
                return false;
            }
        }

        public bool HasLiveSessionForAppId(ulong appId)
        {
            string path = GetManifestPathForAppId(appId);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            if (!TryLoad(path, out PersistedLaunchSession session))
                return false;

            return IsSessionProcessAlive(session);
        }

        // PID for a live launch-session process (LAN join relaunch when Accept while already running).
        public bool TryGetLiveSessionProcessId(ulong appId, out int processId)
        {
            processId = 0;
            string path = GetManifestPathForAppId(appId);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            if (!TryLoad(path, out PersistedLaunchSession session))
                return false;

            if (!IsSessionProcessAlive(session) || session.GameProcessId <= 0)
                return false;

            processId = session.GameProcessId;
            return true;
        }

        public bool HasAnyLiveLaunchSession()
        {
            ulong unused;
            return TryGetPrimaryLiveLaunchAppId(out unused);
        }

        // First live launch-session AppId (for LAN Players "playing" presence).
        public bool TryGetPrimaryLiveLaunchAppId(out ulong appId)
        {
            appId = 0;
            string directory = PathConstants.LaunchSessionManifestDirectory;
            if (!Directory.Exists(directory))
                return false;

            foreach (string manifestPath in Directory.GetFiles(directory, "*" + PathConstants.LaunchSessionManifestFileExtension))
            {
                if (!TryLoad(manifestPath, out PersistedLaunchSession session))
                    continue;

                if (!IsSessionProcessAlive(session) || session.AppId == 0)
                    continue;

                appId = session.AppId;
                return true;
            }

            return false;
        }

        public void ReconcileStaleSessionForAppId(ulong appId)
        {
            if (appId == 0)
                return;

            string manifestPath = GetManifestPathForAppId(appId);
            if (string.IsNullOrEmpty(manifestPath) || !File.Exists(manifestPath))
                return;

            if (!TryLoad(manifestPath, out PersistedLaunchSession session))
                return;

            if (IsSessionProcessAlive(session))
                return;

            _logger.LogDebug(
                $"Cleaning stale launch session for AppId {appId} (PID {session.GameProcessId} is not running) before deploy.");
            TryExecuteCleanup(manifestPath, null);
        }

        public void ReconcileAllOrphanedSessions()
        {
            string directory = PathConstants.LaunchSessionManifestDirectory;
            if (!Directory.Exists(directory))
                return;

            foreach (string manifestPath in Directory.GetFiles(directory, "*" + PathConstants.LaunchSessionManifestFileExtension))
            {
                if (!TryLoad(manifestPath, out PersistedLaunchSession session))
                    continue;

                if (IsSessionProcessAlive(session))
                    continue;

                _logger.LogDebug(
                    $"Cleaning orphaned launch session for AppId {session.AppId} (PID {session.GameProcessId} is not running).");
                TryExecuteCleanup(manifestPath, null);
            }
        }

        public void TryExecuteCleanup(string manifestPath, PersistedLaunchSession inlineSession)
        {
            PersistedLaunchSession session = inlineSession;
            if (session == null && !TryLoad(manifestPath, out session))
                return;

            string mutexName = @"Global\SGE_LaunchCleanup_" + session.AppId;
            using (var mutex = new Mutex(false, mutexName))
            {
                bool acquired = false;
                try
                {
                    acquired = mutex.WaitOne(CleanupMutexWaitMs);
                    if (!acquired)
                    {
                        _logger.LogWarning(
                            $"Launch session cleanup mutex timed out for AppId {session.AppId}; skipping duplicate cleanup.");
                        return;
                    }

                    PersistedLaunchSession pendingRetry = RunCleanup(session);
                    if (pendingRetry != null)
                        WritePendingCleanupRetry(manifestPath, pendingRetry);
                    else
                        TryDeleteManifest(manifestPath);
                }
                finally
                {
                    if (acquired)
                    {
                        try
                        {
                            mutex.ReleaseMutex();
                        }
                        catch
                        {
                        }
                    }
                }
            }
        }

        public bool SpawnDetachedWatcher(string manifestPath)
        {
            if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
                return false;

            try
            {
                string exePath = ResolveHostExecutablePath();
                if (string.IsNullOrEmpty(exePath))
                {
                    _logger.LogWarning("Launch cleanup watcher could not resolve host executable path.");
                    return false;
                }

                string quotedManifest = "\"" + manifestPath + "\"";
                var startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = ApplicationConstants.LaunchCleanupWatcherCliFlag + " " + quotedManifest,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                };

                using (Process.Start(startInfo))
                {
                }
                _logger.LogDebug($"Spawned detached launch cleanup watcher for manifest {manifestPath}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to spawn launch cleanup watcher: {ex.Message}");
                return false;
            }
        }

        public bool TryHandleWatcherCommandLine(string[] args)
        {
            if (args == null || args.Length == 0)
                return false;

            string manifestPath = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (!IsWatcherFlag(args[i]))
                    continue;
                if (i + 1 >= args.Length)
                {
                    _logger.LogWarning("Launch cleanup watcher flag present but manifest path is missing.");
                    return true;
                }

                manifestPath = args[i + 1].Trim().Trim('"');
                break;
            }

            if (string.IsNullOrEmpty(manifestPath))
                return false;

            if (!LaunchSessionCleanupTrustHelper.IsAllowedManifestPath(manifestPath))
            {
                _logger.LogWarning(
                    $"Launch cleanup watcher refused manifest path outside {PathConstants.LaunchSessionManifestFolderName}: {manifestPath}");
                return true;
            }

            RunWatcher(manifestPath);
            return true;
        }

        private void RunWatcher(string manifestPath)
        {
            if (!TryLoad(manifestPath, out PersistedLaunchSession session))
            {
                _logger.LogDebug($"Launch cleanup watcher: manifest missing or invalid ({manifestPath}).");
                return;
            }

            int watchedProcessId = session.GameProcessId;
            string watchedGeneration = session.SessionGeneration;
            _logger.LogDebug(
                $"Launch cleanup watcher waiting for game PID {watchedProcessId} (AppId {session.AppId}, generation {watchedGeneration}).");

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(ApplicationConstants.LaunchRegistryRedirectDurationMs).ConfigureAwait(false);
                    TryRestoreRegistryAfterLoadWindow(manifestPath, watchedGeneration, watchedProcessId);
                }
                catch
                {
                }
            });

            WaitForGameProcessExit(session);

            if (!File.Exists(manifestPath))
            {
                _logger.LogDebug("Launch cleanup watcher: manifest already removed.");
                return;
            }

            if (!TryLoad(manifestPath, out PersistedLaunchSession current))
            {
                _logger.LogDebug("Launch cleanup watcher: manifest unreadable after game exit.");
                return;
            }

            if (!string.Equals(current.SessionGeneration, watchedGeneration, StringComparison.Ordinal)
                || current.GameProcessId != watchedProcessId)
            {
                _logger.LogDebug(
                    "Launch cleanup watcher: session was superseded by a newer launch; skipping cleanup.");
                return;
            }

            _logger.LogDebug(
                $"Launch cleanup watcher running file and registry cleanup for AppId {current.AppId} (library: {current.GameLibraryFolder}).");
            TryExecuteCleanup(manifestPath, null);
        }

        private void WaitForGameProcessExit(PersistedLaunchSession session)
        {
            int processId = session.GameProcessId;
            if (!IsSessionProcessAlive(session))
            {
                _logger.LogDebug($"Launch cleanup watcher: PID {processId} is not running.");
                return;
            }

            try
            {
                using (Process process = Process.GetProcessById(processId))
                {
                    if (!process.HasExited)
                        process.WaitForExit();
                }
            }
            catch (ArgumentException)
            {
                _logger.LogDebug($"Launch cleanup watcher: PID {processId} is not running.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Launch cleanup watcher wait failed for PID {processId}: {ex.Message}");
            }
        }

        // Process id 0 keeps the retry manifest out of live-session checks, so startup or the next launch reconciles it.
        private void WritePendingCleanupRetry(string manifestPath, PersistedLaunchSession pending)
        {
            if (pending == null || pending.AppId == 0)
                return;

            ulong appId = pending.AppId;
            try
            {
                string path = GetManifestPathForAppId(appId);
                if (string.IsNullOrEmpty(path))
                    return;

                // Prefer the canonical per-appId path; ignore caller paths outside the launch_sessions folder.
                if (!string.IsNullOrEmpty(manifestPath)
                    && LaunchSessionCleanupTrustHelper.IsAllowedManifestPath(manifestPath)
                    && string.Equals(
                        Path.GetFullPath(manifestPath),
                        Path.GetFullPath(path),
                        StringComparison.OrdinalIgnoreCase))
                {
                    path = Path.GetFullPath(manifestPath);
                }

                PersistedLaunchSession retry;
                if (TryLoad(path, out PersistedLaunchSession existing) && existing.GameProcessId == 0)
                {
                    retry = existing;
                    if (pending.DeploySites != null && pending.DeploySites.Count > 0)
                    {
                        if (retry.DeploySites == null)
                            retry.DeploySites = new List<PersistedDeploySite>();
                        retry.DeploySites.AddRange(pending.DeploySites);
                    }

                    if (retry.StubExeRestore == null && pending.StubExeRestore != null)
                        retry.StubExeRestore = pending.StubExeRestore;
                }
                else
                {
                    retry = new PersistedLaunchSession
                    {
                        AppId = appId,
                        GameProcessId = 0,
                        SessionGeneration = Guid.NewGuid().ToString("D"),
                        GameLibraryFolder = PathConstants.GetGameFolder(appId),
                        LoadDllsFolder = PathConstants.CombineGameSteamSettingsLoadDllsDirectory(appId),
                        DeploySites = pending.DeploySites,
                        StubExeRestore = pending.StubExeRestore,
                    };
                }

                WriteSession(retry);
                _logger.LogWarning(
                    $"Launch session cleanup for AppId {appId} could not restore every file; kept {path} to retry at next startup or launch.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to save pending launch cleanup for AppId {appId}", ex);
            }
        }

        // Returns a process-id-0 retry session when deploy or stub restore is still pending; otherwise null.
        private PersistedLaunchSession RunCleanup(PersistedLaunchSession session)
        {
            if (session == null)
                return null;

            int deployFileCount = 0;
            if (session.DeploySites != null)
            {
                foreach (PersistedDeploySite site in session.DeploySites)
                {
                    if (site?.Files != null)
                        deployFileCount += site.Files.Count;
                }
            }

            _logger.LogDebug(
                $"Launch session cleanup for AppId {session.AppId}: "
                + $"{deployFileCount} beside-exe file(s), library={session.GameLibraryFolder}, load_dlls={session.LoadDllsFolder}, "
                + $"restoreActiveProcess={session.RestoreActiveProcessRegistry}, "
                + $"stubExeRestore={(session.StubExeRestore != null)}.");

            RestoreSourceMod(session);
            List<PersistedDeploySite> pendingSites = CleanupDeploySites(session.DeploySites);
            PersistedStubExeRestore pendingStub = null;
            if (!TryRestoreStubExe(session.StubExeRestore))
                pendingStub = session.StubExeRestore;

            if (session.RestoreActiveProcessRegistry)
                RestoreActiveProcessRegistry();
            CleanupGameLibraryFiles(session);
            RestoreBranch(session);

            bool hasPendingSites = pendingSites != null && pendingSites.Count > 0;
            if (!hasPendingSites && pendingStub == null)
                return null;

            return new PersistedLaunchSession
            {
                AppId = session.AppId,
                GameProcessId = 0,
                SessionGeneration = Guid.NewGuid().ToString("D"),
                GameLibraryFolder = session.GameLibraryFolder,
                LoadDllsFolder = session.LoadDllsFolder,
                DeploySites = hasPendingSites ? pendingSites : null,
                StubExeRestore = pendingStub,
            };
        }

        private bool TryRestoreStubExe(PersistedStubExeRestore stubRestore)
        {
            if (stubRestore == null)
                return true;

            if (!LaunchSessionCleanupTrustHelper.IsAllowedStubExeRestore(stubRestore))
            {
                _logger.LogWarning(
                    $"Skipping SteamStub exe restore; untrusted path pair: {stubRestore.ExecutablePath} / {stubRestore.BackupPath}");
                return true;
            }

            string executablePath;
            string backupPath;
            try
            {
                executablePath = Path.GetFullPath(stubRestore.ExecutablePath.Trim());
                backupPath = Path.GetFullPath(stubRestore.BackupPath.Trim());
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Skipping SteamStub exe restore; invalid paths: {ex.Message}");
                return true;
            }

            string unpackedLeftover = executablePath + PathConstants.StubUnpackedExecutableSuffix;
            try
            {
                if (File.Exists(unpackedLeftover))
                {
                    File.Delete(unpackedLeftover);
                    _logger.LogDebug("Deleted leftover StubKit unpacked file: " + unpackedLeftover);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    $"Could not delete leftover StubKit unpacked file {unpackedLeftover}: {ex.Message}");
            }

            if (!File.Exists(backupPath))
            {
                _logger.LogDebug(
                    $"SteamStub original backup already gone; assuming prior restore for {executablePath}.");
                return true;
            }

            try
            {
                string tempPath = executablePath + ".restore.tmp";
                if (File.Exists(tempPath))
                    File.Delete(tempPath);

                if (File.Exists(executablePath))
                    File.Move(executablePath, tempPath);

                try
                {
                    File.Move(backupPath, executablePath);
                }
                catch
                {
                    if (!File.Exists(executablePath) && File.Exists(tempPath))
                        File.Move(tempPath, executablePath);
                    throw;
                }

                try
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        $"Restored SteamStub original but could not delete temporary file {tempPath}: {ex.Message}");
                }

                _logger.LogDebug($"Restored SteamStub original executable: {executablePath}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to restore SteamStub original executable {executablePath}: {ex.Message}");
                return false;
            }
        }

        private void RestoreSourceMod(PersistedLaunchSession session)
        {
            if (session.SourceModRestore == null)
                return;

            if (session.SourceModRestore.HadPreviousValue
                && !LaunchSessionCleanupTrustHelper.IsAllowedSourceModPreviousValue(
                    session.SourceModRestore.PreviousValue))
            {
                _logger.LogWarning(
                    "Skipping SourceModInstallPath restore: previous value is not under a known Steam install path.");
                return;
            }

            SteamSourceModRegistryHelper.RestoreSourceModInstallPath(
                session.SourceModRestore.HadPreviousValue,
                session.SourceModRestore.PreviousValue);
            _logger.LogDebug("Restored SourceModInstallPath registry value after launch session cleanup.");
        }

        private void RestoreActiveProcessRegistry()
        {
            SteamActiveProcessRegistryHelper.RestoreSteamClientDllPathsToSteamInstall();
            _logger.LogDebug("Restored ActiveProcess steamclient paths to the Steam installation.");
        }

        private void TryExecuteRegistryRestore(string manifestPath)
        {
            if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
                return;

            if (!TryLoad(manifestPath, out PersistedLaunchSession session))
                return;

            if (!SessionNeedsRegistryRestore(session))
                return;

            string mutexName = @"Global\SGE_LaunchCleanup_" + session.AppId;
            using (var mutex = new Mutex(false, mutexName))
            {
                bool acquired = false;
                try
                {
                    acquired = mutex.WaitOne(CleanupMutexWaitMs);
                    if (!acquired)
                    {
                        _logger.LogWarning(
                            $"Launch session registry restore mutex timed out for AppId {session.AppId}; skipping early restore.");
                        return;
                    }

                    if (!TryLoad(manifestPath, out PersistedLaunchSession current))
                        return;

                    if (!SessionNeedsRegistryRestore(current))
                        return;

                    if (current.RestoreActiveProcessRegistry)
                        RestoreActiveProcessRegistry();

                    RestoreSourceMod(current);

                    current.RestoreActiveProcessRegistry = false;
                    current.SourceModRestore = null;

                    string json = JsonConvert.SerializeObject(current, JsonFormatting.Indented);
                    File.WriteAllText(manifestPath, json);

                    _logger.LogDebug(
                        $"Restored Steam registry after {ApplicationConstants.LaunchRegistryRedirectDurationMs / 1000}s load window "
                        + $"for AppId {current.AppId} (PID {current.GameProcessId}).");
                }
                finally
                {
                    if (acquired)
                    {
                        try
                        {
                            mutex.ReleaseMutex();
                        }
                        catch
                        {
                        }
                    }
                }
            }
        }

        private void RestoreBranch(PersistedLaunchSession session)
        {
            PersistedBranchRestore branch = session.BranchRestore;
            if (branch == null || branch.AppId == 0)
                return;

            try
            {
                var snap = _emulatorConfigService.LoadGameSettingsSnapshot(branch.AppId);
                snap.App.BranchName = branch.BranchName ?? SteamPicsKeyNames.SteamDefaultBranchName;
                snap.App.IsBetaBranch = branch.IsBetaBranch;
                _emulatorConfigService.SaveConfigsAppIni(branch.AppId, snap.App);
                _logger.LogDebug(
                    $"Restored {PathConstants.GoldbergAppIniFileName} branch: is_beta_branch={branch.IsBetaBranch}, branch_name={snap.App.BranchName}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to restore branch settings after launch session cleanup: {ex.Message}");
            }
        }

        // Returns the sites that still need work (locked files, junction in use), or null when everything was restored.
        private List<PersistedDeploySite> CleanupDeploySites(List<PersistedDeploySite> sites)
        {
            if (sites == null)
                return null;

            List<PersistedDeploySite> pendingSites = null;
            foreach (PersistedDeploySite site in sites)
            {
                PersistedDeploySite pending = CleanupDeploySite(site);
                if (pending == null)
                    continue;
                if (pendingSites == null)
                    pendingSites = new List<PersistedDeploySite>();
                pendingSites.Add(pending);
            }

            return pendingSites;
        }

        private PersistedDeploySite CleanupDeploySite(PersistedDeploySite site)
        {
            if (site == null)
                return null;

            var pending = new PersistedDeploySite();
            bool hasPending = false;

            if (!string.IsNullOrEmpty(site.MirroredSteamSettingsPath)
                && !LaunchSessionCleanupTrustHelper.IsAllowedSteamSettingsJunctionPath(site.MirroredSteamSettingsPath))
            {
                _logger.LogWarning(
                    $"Skipping steam_settings junction cleanup; path is not a steam_settings folder: {site.MirroredSteamSettingsPath}");
            }
            else if (!TryRemoveSteamSettingsJunction(site.MirroredSteamSettingsPath))
            {
                pending.MirroredSteamSettingsPath = site.MirroredSteamSettingsPath;
                hasPending = true;
            }

            if (!string.IsNullOrEmpty(site.SteamSettingsBackupPath)
                && !LaunchSessionCleanupTrustHelper.IsAllowedSteamSettingsBackupPath(site.SteamSettingsBackupPath))
            {
                _logger.LogWarning(
                    $"Skipping steam_settings backup restore; path is not a steam_settings.sge folder: {site.SteamSettingsBackupPath}");
            }
            else if (!TryRestoreSteamSettingsBackup(site.SteamSettingsBackupPath))
            {
                pending.SteamSettingsBackupPath = site.SteamSettingsBackupPath;
                hasPending = true;
            }

            if (site.Files != null)
            {
                foreach (PersistedFileDeployment file in site.Files)
                {
                    if (file == null)
                        continue;
                    if (!LaunchSessionCleanupTrustHelper.IsAllowedDeployedFile(file))
                    {
                        _logger.LogWarning(
                            $"Skipping deploy cleanup for untrusted target/backup pair: {file.TargetPath}");
                        continue;
                    }

                    if (TryCleanupDeployedFile(file))
                        continue;
                    if (pending.Files == null)
                        pending.Files = new List<PersistedFileDeployment>();
                    pending.Files.Add(file);
                    hasPending = true;
                }
            }

            return hasPending ? pending : null;
        }

        private bool TryRemoveSteamSettingsJunction(string junctionPath)
        {
            if (string.IsNullOrEmpty(junctionPath) || !Directory.Exists(junctionPath))
                return true;

            if (!DirectoryJunctionHelper.IsDirectoryReparsePoint(junctionPath))
            {
                _logger.LogWarning(
                    $"Expected a junction at {junctionPath} but found a real folder; leaving it in place.");
                return true;
            }

            try
            {
                Directory.Delete(junctionPath, recursive: false);
                _logger.LogDebug($"Removed {PathConstants.SteamSettingsFolderName} junction: {junctionPath}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    $"Failed to remove {PathConstants.SteamSettingsFolderName} junction at {junctionPath}: {ex.Message}");
                return false;
            }
        }

        private bool TryRestoreSteamSettingsBackup(string backupPath)
        {
            if (string.IsNullOrEmpty(backupPath) || !Directory.Exists(backupPath))
                return true;

            string ext = PathConstants.SteamApiBackupSidecarExtension;
            if (!backupPath.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                return true;

            string originalPath = backupPath.Substring(0, backupPath.Length - ext.Length);
            if (Directory.Exists(originalPath) || File.Exists(originalPath))
            {
                _logger.LogWarning(
                    $"Cannot restore {backupPath}: {originalPath} still exists.");
                return false;
            }

            try
            {
                Directory.Move(backupPath, originalPath);
                _logger.LogDebug($"Restored original {PathConstants.SteamSettingsFolderName} folder: {originalPath}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to restore {PathConstants.SteamSettingsFolderName} folder from {backupPath}: {ex.Message}");
                return false;
            }
        }

        private bool TryCleanupDeployedFile(PersistedFileDeployment file)
        {
            try
            {
                if (file.HadOriginal)
                {
                    // Restore from backup when present. If the backup is already gone, a prior cleanup
                    // likely restored the original — do not delete the target.
                    if (!string.IsNullOrEmpty(file.BackupPath) && File.Exists(file.BackupPath))
                    {
                        File.Copy(file.BackupPath, file.TargetPath, overwrite: true);
                        File.Delete(file.BackupPath);
                        _logger.LogDebug($"Restored original file: {file.TargetPath}");
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(file.TargetPath) && File.Exists(file.TargetPath))
                    {
                        File.Delete(file.TargetPath);
                        _logger.LogDebug($"Cleaned deployed file: {file.TargetPath}");
                    }

                    DeleteLegacySteamApiDeploymentBackup(file.TargetPath);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to cleanup deployed file {file.TargetPath}: {ex.Message}");
                return false;
            }
        }

        private void CleanupGameLibraryFiles(PersistedLaunchSession session)
        {
            if (session == null || session.AppId == 0)
                return;

            // Shape-validated session paths (or PathConstants fallback) so poisoned JSON cannot delete arbitrary folders.
            string gameDirectory = PathConstants.GetGameFolder(session.AppId);
            if (LaunchSessionCleanupTrustHelper.IsAllowedGameLibraryFolder(session.AppId, session.GameLibraryFolder))
                gameDirectory = Path.GetFullPath(session.GameLibraryFolder.Trim());

            string loadDllsFolder = Path.Combine(
                gameDirectory,
                PathConstants.SteamSettingsFolderName,
                PathConstants.GoldbergLoadDllsFolderName);
            if (LaunchSessionCleanupTrustHelper.IsAllowedLoadDllsFolder(session.AppId, session.LoadDllsFolder))
                loadDllsFolder = Path.GetFullPath(session.LoadDllsFolder.Trim());

            try
            {
                if (Directory.Exists(gameDirectory))
                {
                    foreach (string dllFile in new[]
                    {
                        PathConstants.GoldbergSteamClientDll32,
                        PathConstants.GoldbergSteamClientDll64,
                        PathConstants.GoldbergGameOverlayRendererDll32,
                        PathConstants.GoldbergGameOverlayRendererDll64,
                    })
                    {
                        string dllPath = Path.Combine(gameDirectory, dllFile);
                        if (!File.Exists(dllPath))
                            continue;
                        try
                        {
                            File.Delete(dllPath);
                            _logger.LogDebug($"Cleaned up DLL: {dllPath}");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning($"Failed to delete DLL {dllPath}: {ex.Message}");
                        }
                    }
                }

                // Per-game steam_settings/load_dlls is durable user content — do not delete on session cleanup.
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    $"Failed to cleanup game library files for AppId {session.AppId} "
                    + $"(library={session.GameLibraryFolder}, load_dlls={session.LoadDllsFolder})",
                    ex);
            }
        }

        // Older builds left *.sge.bak beside deployed DLLs inside game folders, which startup migration cannot enumerate.
        internal static void DeleteLegacySteamApiDeploymentBackup(string targetPath)
        {
            if (string.IsNullOrEmpty(targetPath))
                return;

            string legacyPath = targetPath + PathConstants.SteamApiDllDeploymentLegacyBackupExtension;
            if (!File.Exists(legacyPath))
                return;

            try
            {
                File.Delete(legacyPath);
            }
            catch
            {
            }
        }

        // Prefer the launcher beside AppBaseDirectory. Assembly.Location can be a shadow-copy path
        // (e.g. under testhost); starting that exe makes the watcher's BaseDirectory miss games/ staging.
        private static string ResolveHostExecutablePath()
        {
            try
            {
                string besideBase = Path.Combine(
                    PathConstants.AppBaseDirectory,
                    PathConstants.LauncherMainExecutableFileName);
                if (File.Exists(besideBase))
                    return Path.GetFullPath(besideBase);
            }
            catch
            {
            }

            try
            {
                string location = Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(location)
                    && location.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    && File.Exists(location))
                {
                    return location;
                }
            }
            catch
            {
            }

            try
            {
                using (Process current = Process.GetCurrentProcess())
                {
                    string mainModulePath = current.MainModule?.FileName;
                    if (!string.IsNullOrEmpty(mainModulePath)
                        && mainModulePath.EndsWith(
                            PathConstants.LauncherMainExecutableFileName,
                            StringComparison.OrdinalIgnoreCase)
                        && File.Exists(mainModulePath))
                    {
                        return mainModulePath;
                    }
                }
            }
            catch
            {
            }

            string candidate = PathConstants.LauncherMainExecutablePath;
            return File.Exists(candidate) ? candidate : null;
        }

        private static bool IsWatcherFlag(string arg)
        {
            if (string.IsNullOrWhiteSpace(arg))
                return false;

            string trimmed = arg.Trim();
            return trimmed.Equals(ApplicationConstants.LaunchCleanupWatcherCliFlag, StringComparison.OrdinalIgnoreCase)
                || trimmed.Equals("--" + ApplicationConstants.LaunchCleanupWatcherCliFlag, StringComparison.OrdinalIgnoreCase);
        }
    }
}
