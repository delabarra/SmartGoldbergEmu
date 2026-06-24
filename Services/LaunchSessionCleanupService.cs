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

            string directory = PathConstants.LaunchSessionManifestDirectory;
            Directory.CreateDirectory(directory);
            string path = PathConstants.CombineLaunchSessionManifestPath(session.AppId);
            string json = JsonConvert.SerializeObject(session, JsonFormatting.Indented);
            File.WriteAllText(path, json);
            return path;
        }

        public bool TryLoad(string manifestPath, out PersistedLaunchSession session)
        {
            session = null;
            if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
                return false;

            try
            {
                string json = File.ReadAllText(manifestPath);
                session = JsonConvert.DeserializeObject<PersistedLaunchSession>(json);
                if (session == null || session.AppId == 0)
                    return false;

                return true;
            }
            catch
            {
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

            TryExecuteRegistryRestore(manifestPath);
        }

        public bool IsProcessAlive(int processId)
        {
            if (processId <= 0)
                return false;

            try
            {
                using (Process process = Process.GetProcessById(processId))
                    return !process.HasExited;
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

            return IsProcessAlive(session.GameProcessId);
        }

        public bool HasAnyLiveLaunchSession()
        {
            string directory = PathConstants.LaunchSessionManifestDirectory;
            if (!Directory.Exists(directory))
                return false;

            foreach (string manifestPath in Directory.GetFiles(directory, "*" + PathConstants.LaunchSessionManifestFileExtension))
            {
                if (!TryLoad(manifestPath, out PersistedLaunchSession session))
                    continue;

                if (IsProcessAlive(session.GameProcessId))
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

            if (IsProcessAlive(session.GameProcessId))
                return;

            _logger.LogMessage(
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

                if (IsProcessAlive(session.GameProcessId))
                    continue;

                _logger.LogMessage(
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

                    RunCleanup(session);
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

                Process.Start(startInfo);
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
            _logger.LogMessage(
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

            WaitForGameProcessExit(watchedProcessId);

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

            _logger.LogMessage(
                $"Launch cleanup watcher running file and registry cleanup for AppId {current.AppId} (library: {current.GameLibraryFolder}).");
            TryExecuteCleanup(manifestPath, null);
        }

        private void WaitForGameProcessExit(int processId)
        {
            if (processId <= 0)
                return;

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

        private void RunCleanup(PersistedLaunchSession session)
        {
            if (session == null)
                return;

            int deployFileCount = 0;
            if (session.DeploySites != null)
            {
                foreach (PersistedDeploySite site in session.DeploySites)
                {
                    if (site?.Files != null)
                        deployFileCount += site.Files.Count;
                }
            }

            _logger.LogMessage(
                $"Launch session cleanup for AppId {session.AppId}: "
                + $"{deployFileCount} beside-exe file(s), library={session.GameLibraryFolder}, load_dlls={session.LoadDllsFolder}, "
                + $"restoreActiveProcess={session.RestoreActiveProcessRegistry}.");

            RestoreSourceMod(session);
            CleanupDeploySites(session.DeploySites);
            if (session.RestoreActiveProcessRegistry)
                RestoreActiveProcessRegistry();
            CleanupGameLibraryFiles(session);
            RestoreBranch(session);
        }

        private void RestoreSourceMod(PersistedLaunchSession session)
        {
            if (session.SourceModRestore == null)
                return;

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

                    _logger.LogMessage(
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

        private void CleanupDeploySites(List<PersistedDeploySite> sites)
        {
            if (sites == null)
                return;

            foreach (PersistedDeploySite site in sites)
                CleanupDeploySite(site);
        }

        private void CleanupDeploySite(PersistedDeploySite site)
        {
            if (site == null)
                return;

            if (!string.IsNullOrEmpty(site.MirroredSteamSettingsPath) && Directory.Exists(site.MirroredSteamSettingsPath))
            {
                try
                {
                    if (!DirectoryJunctionHelper.IsDirectoryReparsePoint(site.MirroredSteamSettingsPath))
                    {
                        _logger.LogWarning(
                            $"Expected a junction at {site.MirroredSteamSettingsPath}; removing folder recursively.");
                        Directory.Delete(site.MirroredSteamSettingsPath, recursive: true);
                    }
                    else
                    {
                        Directory.Delete(site.MirroredSteamSettingsPath, recursive: false);
                        _logger.LogDebug($"Removed {PathConstants.SteamSettingsFolderName} junction: {site.MirroredSteamSettingsPath}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        $"Failed to remove {PathConstants.SteamSettingsFolderName} at {site.MirroredSteamSettingsPath}: {ex.Message}");
                }
            }

            if (site.Files == null || site.Files.Count == 0)
                return;

            foreach (PersistedFileDeployment file in site.Files)
            {
                try
                {
                    if (file.HadOriginal && !string.IsNullOrEmpty(file.BackupPath) && File.Exists(file.BackupPath))
                    {
                        File.Copy(file.BackupPath, file.TargetPath, overwrite: true);
                        File.Delete(file.BackupPath);
                        _logger.LogDebug($"Restored original file: {file.TargetPath}");
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(file.TargetPath) && File.Exists(file.TargetPath))
                        {
                            File.Delete(file.TargetPath);
                            _logger.LogDebug($"Cleaned deployed file: {file.TargetPath}");
                        }

                        if (!string.IsNullOrEmpty(file.BackupPath) && File.Exists(file.BackupPath))
                            File.Delete(file.BackupPath);
                        DeleteLegacySteamApiDeploymentBackup(file.TargetPath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to cleanup deployed file {file.TargetPath}: {ex.Message}");
                }
            }
        }

        private void CleanupGameLibraryFiles(PersistedLaunchSession session)
        {
            if (session == null || session.AppId == 0)
                return;

            string gameDirectory = !string.IsNullOrWhiteSpace(session.GameLibraryFolder)
                ? session.GameLibraryFolder
                : PathConstants.GetGameFolder(session.AppId);
            string loadDllsFolder = !string.IsNullOrWhiteSpace(session.LoadDllsFolder)
                ? session.LoadDllsFolder
                : PathConstants.CombineGameSteamSettingsLoadDllsDirectory(session.AppId);

            try
            {
                if (!Directory.Exists(gameDirectory))
                    return;

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

                CleanupLoadDllsFolder(loadDllsFolder);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to cleanup game library files for AppId {session.AppId}: {ex.Message}", ex);
            }
        }

        private void CleanupLoadDllsFolder(string loadDllsFolder)
        {
            if (string.IsNullOrEmpty(loadDllsFolder) || !Directory.Exists(loadDllsFolder))
                return;

            try
            {
                Directory.Delete(loadDllsFolder, recursive: true);
                _logger.LogDebug($"Cleaned up load_dlls folder: {loadDllsFolder}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to delete load_dlls folder {loadDllsFolder}: {ex.Message}");
            }
        }

        private static void DeleteLegacySteamApiDeploymentBackup(string targetPath)
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

        private static string ResolveHostExecutablePath()
        {
            try
            {
                string location = Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(location) && File.Exists(location))
                    return location;
            }
            catch
            {
            }

            try
            {
                using (Process current = Process.GetCurrentProcess())
                {
                    string mainModulePath = current.MainModule?.FileName;
                    if (!string.IsNullOrEmpty(mainModulePath) && File.Exists(mainModulePath))
                        return mainModulePath;
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
