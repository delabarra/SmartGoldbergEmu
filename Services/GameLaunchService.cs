using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Validation;

namespace SmartGoldbergEmu.Services
{
    public class GameLaunchService
    {
        private readonly ILogService _logger;
        private readonly EmulatorConfigService _emulatorConfigService;
        private readonly LaunchSessionCleanupService _launchSessionCleanup;
        private readonly Dictionary<ulong, Process> _launchedProcesses;
        private readonly object _branchRestoreLock = new object();
        private readonly Dictionary<int, BranchRestoreInfo> _branchRestoreByProcessId = new Dictionary<int, BranchRestoreInfo>();
        private readonly object _sourceModRestoreLock = new object();
        private readonly Dictionary<int, SteamSourceModRegistryHelper.RestoreToken> _sourceModRestoreByProcessId =
            new Dictionary<int, SteamSourceModRegistryHelper.RestoreToken>();
        private readonly object _deployLock = new object();
        private readonly Dictionary<int, Win32DllDeploymentState> _win32DeploymentByProcessId = new Dictionary<int, Win32DllDeploymentState>();
        private readonly object _activeProcessRegistryLock = new object();
        private readonly HashSet<int> _processIdsWithActiveProcessRegistry = new HashSet<int>();
        private readonly object _launchInProgressLock = new object();
        private readonly HashSet<ulong> _launchInProgressAppIds = new HashSet<ulong>();
        private readonly object _manifestPathLock = new object();
        private readonly Dictionary<ulong, string> _manifestPathByAppId = new Dictionary<ulong, string>();
        private readonly object _watcherActiveLock = new object();
        private readonly HashSet<ulong> _watcherActiveAppIds = new HashSet<ulong>();
        private readonly object _supersededCleanupLock = new object();
        private readonly Dictionary<int, SupersededLaunchCleanup> _supersededCleanupByProcessId =
            new Dictionary<int, SupersededLaunchCleanup>();
        private readonly object _registryRestoreTimerLock = new object();
        private readonly Dictionary<ulong, CancellationTokenSource> _registryRestoreCancellationByAppId =
            new Dictionary<ulong, CancellationTokenSource>();

        private sealed class BranchRestoreInfo
        {
            public ulong AppId;
            public string BranchName;
            public bool IsBetaBranch;
        }

        private sealed class StandardDeploySite
        {
            public List<FileDeployment> Files = new List<FileDeployment>();
            public string MirroredSteamSettingsPath;
        }

        private sealed class Win32DllDeploymentState
        {
            public ulong AppId;
            public List<StandardDeploySite> Sites = new List<StandardDeploySite>();
        }

        private sealed class FileDeployment
        {
            public string TargetPath;
            public string BackupPath;
            public bool HadOriginal;
        }

        private sealed class SupersededLaunchCleanup
        {
            public ulong AppId;
            public Win32DllDeploymentState DeployState;
            public BranchRestoreInfo BranchRestore;
            public bool RestoreActiveProcessRegistry;
            public PersistedSourceModRestore SourceModRestore;
        }

        public GameLaunchService()
            : this(ServiceLocator.LogService, ServiceLocator.EmulatorConfigService, ServiceLocator.LaunchSessionCleanupService)
        {
        }

        public GameLaunchService(ILogService logger, EmulatorConfigService emulatorConfigService)
            : this(logger, emulatorConfigService, new LaunchSessionCleanupService(logger, emulatorConfigService))
        {
        }

        public GameLaunchService(
            ILogService logger,
            EmulatorConfigService emulatorConfigService,
            LaunchSessionCleanupService launchSessionCleanup)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _emulatorConfigService = emulatorConfigService ?? throw new ArgumentNullException(nameof(emulatorConfigService));
            _launchSessionCleanup = launchSessionCleanup ?? throw new ArgumentNullException(nameof(launchSessionCleanup));
            _launchedProcesses = new Dictionary<ulong, Process>();
        }

        public ValidationResult ValidateEmulatorFilesPrerequisite(GameConfig game, bool requireLaunchModeBinaries = true)
        {
            if (game == null)
                return ValidationResult.Failure("Game configuration cannot be null.");

            bool useX64 = TryResolveLaunchUseX64(game, out bool resolvedUseX64)
                ? resolvedUseX64
                : true;
            return ValidateEmulatorBinariesForSteamAppId(game, useX64, requireLaunchModeBinaries);
        }

        public GoldbergLaunchModeAvailability GetLaunchModeAvailability(GameConfig game)
        {
            bool standardAvailable = IsStandardSteamApiModeAvailable(game, out _);
            return new GoldbergLaunchModeAvailability
            {
                SteamClientAvailable = IsSteamClientModeAvailable(),
                StandardSteamApiAvailable = standardAvailable,
                SteamDllBesideExeAvailable = IsSteamDllModeAvailable()
            };
        }

        public GoldbergLaunchMode ResolveAvailableLaunchMode(GameConfig game)
        {
            GoldbergLaunchModeAvailability availability = GetLaunchModeAvailability(game);
            GoldbergLaunchMode preferred = game?.LaunchMode ?? GoldbergLaunchMode.SteamClient;
            return availability.ResolveAvailable(preferred);
        }

        public bool IsGameRunning(ulong appId, GameConfig game)
        {
            if (appId == 0)
                return false;

            lock (_launchInProgressLock)
            {
                if (_launchInProgressAppIds.Contains(appId))
                    return true;
            }

            return IsGameSessionActive(appId);
        }

        // Non-zero Steam App IDs require Goldberg binaries under goldberg/ when useEmulator is true.
        public ValidationResult LaunchGame(GameConfig game, bool useEmulator = true, LaunchOption launchOption = null)
        {
            ulong appId = 0;
            bool launchMarkedInProgress = false;
            try
            {
                game = ReloadGameConfigFromLibrary(game);
                bool effectiveUseEmulator = useEmulator;
                var validation = ValidateGameConfig(game);
                if (!validation.IsValid)
                {
                    _logger?.LogError($"Validation failed: {validation.ErrorMessage}");
                    return validation;
                }

                appId = game.AppId;
                lock (_launchInProgressLock)
                {
                    if (_launchInProgressAppIds.Contains(appId))
                    {
                        return ValidationResult.Failure(
                            $"{game.AppName} is already being started. Wait for the current launch to finish.");
                    }

                    ValidationResult sessionCheck = ValidateGameNotAlreadyRunning(game);
                    if (!sessionCheck.IsValid)
                    {
                        _logger?.LogWarning(sessionCheck.ErrorMessage);
                        return sessionCheck;
                    }

                    _launchInProgressAppIds.Add(appId);
                    launchMarkedInProgress = true;
                }

                string exeForArch = GameFolderPathHelper.TryResolveStoredExecutable(game, out string resolvedForArch)
                    ? resolvedForArch
                    : game.Path;
                bool useX64 = true;
                if (TryResolveLaunchUseX64(game, out bool resolvedUseX64))
                    useX64 = resolvedUseX64;
                else if (!string.IsNullOrEmpty(exeForArch) && !File.Exists(exeForArch))
                    _logger?.LogWarning($"Architecture detection failed for {exeForArch}, defaulting to 64-bit");

                var emuPrerequisite = ValidateEmulatorBinariesForSteamAppId(game, useX64, effectiveUseEmulator);
                if (!emuPrerequisite.IsValid)
                    return emuPrerequisite;
                string launchOptionLabel = launchOption != null
                    ? launchOption.Description ?? launchOption.Executable ?? "selected"
                    : "default";
                _logger?.LogMessage(
                    $"Launch: {game?.AppName} (AppId {game?.AppId}), emulator={effectiveUseEmulator}, launchMode={game?.LaunchMode}, arch={(useX64 ? "x64" : "x86")}, option={launchOptionLabel}");

                Win32DllDeploymentState win32DeployState = null;
                string sourceModInstallFolder = null;
                bool activeProcessRegistryConfigured = false;
                if (effectiveUseEmulator)
                {
                    if (game.AppId != 0)
                        CancelRegistryRestoreTimer(game.AppId);

                    if (!_launchSessionCleanup.HasAnyLiveLaunchSession())
                        RestoreSteamClientRegistryForApplicationLifecycle();

                    if (game.AppId != 0)
                    {
                        _launchSessionCleanup.ReconcileStaleSessionForAppId(game.AppId);
                        lock (_manifestPathLock)
                            _manifestPathByAppId.Remove(game.AppId);
                        lock (_watcherActiveLock)
                            _watcherActiveAppIds.Remove(game.AppId);
                    }

                    string resolvedLaunch = ResolveLaunchExecutablePath(game, launchOption);
                    string exeDirectory = !string.IsNullOrEmpty(resolvedLaunch) ? Path.GetDirectoryName(resolvedLaunch) : null;
                    string gameRootFolder = GetBaseFolderForLaunchOptions(game, launchOption);
                    GoldbergLaunchMode launchMode = game.LaunchMode;

                    if (!string.IsNullOrEmpty(exeDirectory))
                        RemoveStraySteamApiDllsBesideExecutable(exeDirectory, besideExeDeployActive: launchMode != GoldbergLaunchMode.SteamClient);

                    if (launchMode == GoldbergLaunchMode.StandardSteamApi)
                    {
                        win32DeployState = new Win32DllDeploymentState { AppId = game.AppId };

                        var standardResult = DeployStandardGoldbergReleaseToAllTargets(
                            game, useX64, gameRootFolder, exeDirectory, win32DeployState);
                        if (!standardResult.IsValid)
                        {
                            _logger?.LogError($"Standard Goldberg setup failed: {standardResult.ErrorMessage}");
                            return standardResult;
                        }

                        if (!TryResolveStandardModeRegistryDirectory(
                                exeDirectory,
                                useX64,
                                win32DeployState,
                                out string standardRegistryDllDirectory,
                                out string standardRegistrySteamClientPath))
                        {
                            return ValidationResult.Failure(
                                "Experimental mode could not find the deployed steamclient DLL beside the game executable for registry setup.");
                        }

                        standardRegistryDllDirectory = Path.GetFullPath(standardRegistryDllDirectory);
                        var registryResult = ConfigureActiveProcessGoldbergDlls(useX64, standardRegistryDllDirectory);
                        if (!registryResult.IsValid)
                        {
                            _logger?.LogError($"Steam registry setup failed: {registryResult.ErrorMessage}");
                            return registryResult;
                        }

                        activeProcessRegistryConfigured = true;
                        sourceModInstallFolder = standardRegistryDllDirectory;
                        _logger?.LogDebug(
                            $"Experimental launch: deployed steam_api + steamclient beside game targets; ActiveProcess -> {standardRegistrySteamClientPath}");
                    }
                    else if (launchMode == GoldbergLaunchMode.SteamDllBesideExe)
                    {
                        win32DeployState = new Win32DllDeploymentState { AppId = game.AppId };
                        sourceModInstallFolder = exeDirectory;

                        var steamDllResult = EnsureSteamDllInGoldbergFolder();
                        if (!steamDllResult.IsValid)
                        {
                            _logger?.LogError($"Steam.dll setup failed: {steamDllResult.ErrorMessage}");
                            return steamDllResult;
                        }

                        var setupResult = DeployGoldbergClientDlls(useX64, exeDirectory, deploySteamDllOnly: true, win32DeployState: win32DeployState);
                        if (!setupResult.IsValid)
                        {
                            _logger?.LogError($"Emulator DLL setup failed: {setupResult.ErrorMessage}");
                            return setupResult;
                        }

                        _logger?.LogDebug("Steam.dll mode: deployed Steam.dll beside executable, skipped ActiveProcess registry");
                    }
                    else
                    {
                        string dllTargetDirectory = PathConstants.GetGameFolder(game.AppId);
                        sourceModInstallFolder = dllTargetDirectory;

                        var setupResult = DeployGoldbergClientDlls(useX64, dllTargetDirectory, deploySteamDllOnly: false, win32DeployState: null);
                        if (!setupResult.IsValid)
                        {
                            _logger?.LogError($"Emulator DLL setup failed: {setupResult.ErrorMessage}");
                            return setupResult;
                        }

                        var registryResult = ConfigureActiveProcessGoldbergDlls(useX64, dllTargetDirectory);
                        if (!registryResult.IsValid)
                        {
                            _logger?.LogError($"Steam registry setup failed: {registryResult.ErrorMessage}");
                            return registryResult;
                        }

                        activeProcessRegistryConfigured = true;
                    }

                    if (!string.IsNullOrEmpty(sourceModInstallFolder))
                        ApplySourceModInstallPathForSession(sourceModInstallFolder);

                    if (game.AppId != 0)
                    {
                        var stripResult = _emulatorConfigService.StripSaveLocationFromPerGameUserIni(game.AppId);
                        if (!stripResult.IsSuccess)
                            _logger?.LogWarning($"Per-game save location cleanup failed for app {game.AppId}: {stripResult.ErrorMessage}");
                    }
                }

                BranchRestoreInfo branchRestoreOnExit = null;
                if (effectiveUseEmulator && launchOption != null && game.AppId != 0)
                {
                    var snap = _emulatorConfigService.LoadGameSettingsSnapshot(game.AppId);
                    string prevBranch = snap.App.BranchName ?? SteamPicsKeyNames.SteamDefaultBranchName;
                    bool prevIsBeta = snap.App.IsBetaBranch;
                    LaunchOption.ApplyBetaBranchToAppSettings(launchOption, snap.App);
                    if (snap.App.BranchName != prevBranch || snap.App.IsBetaBranch != prevIsBeta)
                    {
                        var saveRes = _emulatorConfigService.SaveConfigsAppIni(game.AppId, snap.App);
                        if (saveRes.IsSuccess)
                        {
                            branchRestoreOnExit = new BranchRestoreInfo { AppId = game.AppId, BranchName = prevBranch, IsBetaBranch = prevIsBeta };
                            _logger?.LogDebug($"Launch option branch applied (restore on exit): is_beta_branch={snap.App.IsBetaBranch}, branch_name={snap.App.BranchName}");
                        }
                        else
                            _logger?.LogWarning($"Launch option branch not applied (save failed): {saveRes.ErrorMessage}");
                    }
                }

                if (effectiveUseEmulator && game.AppId != 0)
                {
                    string loadDllsFolder = PathConstants.CombineGameSteamSettingsLoadDllsDirectory(game.AppId);
                    int copiedCount;
                    if (SteamClientExtraDllsStaging.TryStageIntoLoadDllsFolder(
                            PathConstants.GoldbergSteamClientExtraDllsDirectory,
                            loadDllsFolder,
                            useX64,
                            out copiedCount)
                        && copiedCount > 0)
                    {
                        _logger?.LogDebug(
                            $"Staged {copiedCount} file(s) from steamclient_extra_dlls into load_dlls for AppId {game.AppId}.");
                    }

                    string gameSoundsFolder = PathConstants.CombineGameSteamSettingsSoundsDirectory(game.AppId);
                    if (OverlayNotificationSoundsStaging.TryStageIntoGameSoundsFolder(
                            PathConstants.GlobalSoundsPath,
                            gameSoundsFolder,
                            out int soundsCopied)
                        && soundsCopied > 0)
                    {
                        _logger?.LogDebug(
                            $"Staged {soundsCopied} overlay notification sound(s) into steam_settings/sounds for AppId {game.AppId}.");
                    }
                }

                ValidationResult result;
                try
                {
                    result = LaunchProcess(
                        game,
                        launchOption,
                        branchRestoreOnExit,
                        win32DeployState,
                        activeProcessRegistryConfigured,
                        trackCleanupSession: effectiveUseEmulator);
                }
                catch (Exception exLaunch)
                {
                    RunImmediateLaunchSessionCleanup(
                        game.AppId,
                        0,
                        win32DeployState,
                        activeProcessRegistryConfigured,
                        branchRestoreOnExit);
                    _logger?.LogError($"Failed to start game process: {exLaunch.Message}", exLaunch);
                    return ValidationResult.Failure($"Failed to start game process: {exLaunch.Message}");
                }

                if (!result.IsValid)
                {
                    RunImmediateLaunchSessionCleanup(
                        game.AppId,
                        0,
                        win32DeployState,
                        activeProcessRegistryConfigured,
                        branchRestoreOnExit);
                }

                if (!result.IsValid)
                    _logger?.LogError($"Game launch failed: {result.ErrorMessage}");
                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Failed to launch game: {ex.Message}", ex);
                return ValidationResult.Failure($"Failed to launch game: {ex.Message}");
            }
            finally
            {
                if (launchMarkedInProgress && appId != 0)
                {
                    lock (_launchInProgressLock)
                        _launchInProgressAppIds.Remove(appId);
                }
            }
        }

        private ValidationResult ValidateGameConfig(GameConfig game)
        {
            if (game == null)
            {
                _logger?.LogError("Game configuration is null");
                return ValidationResult.Failure("Game configuration cannot be null");
            }

            if (game.AppId == 0)
            {
                _logger?.LogError("Steam App ID is missing");
                return ValidationResult.Failure("Steam App ID is required to launch a game");
            }

            if (string.IsNullOrWhiteSpace(game.AppName))
            {
                _logger?.LogError("App name is empty");
                return ValidationResult.Failure("App name cannot be empty");
            }

            if (string.IsNullOrWhiteSpace(game.Path))
            {
                _logger?.LogError("Game executable path is empty");
                return ValidationResult.Failure("Game executable path cannot be empty");
            }

            bool exeOk = GameFolderPathHelper.TryResolveStoredExecutable(game, out string resolvedExe);
            _logger?.LogDebug($"Validate: AppId={game.AppId}, Path={game.Path}, Executable exists={exeOk}" + (exeOk ? $" ({resolvedExe})" : string.Empty));
            if (!exeOk)
            {
                _logger?.LogError($"Game executable not found: {game.Path}");
                return ValidationResult.Failure($"Game executable not found: {game.Path}");
            }

            return ValidationResult.Success();
        }

        private ValidationResult ValidateEmulatorBinariesForSteamAppId(GameConfig game, bool useX64, bool requireLaunchModeBinaries)
        {
            if (!requireLaunchModeBinaries)
            {
                if (IsSteamClientModeAvailable())
                    return ValidationResult.Success();

                _logger?.LogError("Required Goldberg emulator files are missing");
                return ValidationResult.Failure(
                    "Goldberg emulator files are missing under goldberg\\steamclient_experimental. Install or update the emulator.");
            }

            GoldbergLaunchMode launchMode = game?.LaunchMode ?? GoldbergLaunchMode.SteamClient;

            if (launchMode == GoldbergLaunchMode.StandardSteamApi)
            {
                if (PathConstants.HasGoldbergExperimentalFiles(useX64))
                    return ValidationResult.Success();

                _logger?.LogError("Experimental Goldberg DLLs are missing under goldberg\\experimental");
                return ValidationResult.Failure(
                    "Experimental Goldberg DLLs are missing under goldberg\\experimental. "
                    + "Run Goldberg Update or repair the emulator.");
            }

            if (launchMode == GoldbergLaunchMode.SteamDllBesideExe)
            {
                if (IsSteamDllModeAvailable())
                    return ValidationResult.Success();

                _logger?.LogError("Steam.dll is missing from goldberg\\steam_old");
                return ValidationResult.Failure(
                    "Steam.dll is not in goldberg\\steam_old. Run Goldberg Update or install Steam, then try again.");
            }

            if (IsSteamClientModeAvailable())
                return ValidationResult.Success();

            _logger?.LogError("Required Goldberg Steam client DLLs are missing");
            return ValidationResult.Failure(
                "Goldberg Steam client files are missing under goldberg\\steamclient_experimental. Please download or update the emulator.");
        }

        private static bool IsSteamClientModeAvailable() => PathConstants.HasSteamClientGoldbergFiles();

        private static bool IsSteamDllModeAvailable()
        {
            SteamInstallationPathHelper.TryRefreshSteamDllInGoldbergFolder();
            return SteamInstallationPathHelper.IsSteamDllPresentInGoldbergFolder();
        }

        private bool IsStandardSteamApiModeAvailable(GameConfig game, out bool useX64)
        {
            useX64 = true;
            if (game != null && TryResolveLaunchUseX64(game, out bool resolvedUseX64))
            {
                useX64 = resolvedUseX64;
                return PathConstants.HasGoldbergExperimentalFiles(useX64);
            }

            return PathConstants.HasGoldbergExperimentalFiles(false)
                || PathConstants.HasGoldbergExperimentalFiles(true);
        }

        private bool TryResolveLaunchUseX64(GameConfig game, out bool useX64)
        {
            useX64 = true;
            if (game == null)
                return false;

            string exeForArch = GameFolderPathHelper.TryResolveStoredExecutable(game, out string resolved)
                ? resolved
                : game.Path;
            if (string.IsNullOrWhiteSpace(exeForArch))
                return false;

            if (!File.Exists(exeForArch))
                return false;

            useX64 = DetectGameArchitecture(exeForArch);
            return true;
        }

        private bool DetectGameArchitecture(string executablePath)
        {
            try
            {
                if (!File.Exists(executablePath))
                    return true;

                using (var fs = new FileStream(executablePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var reader = new BinaryReader(fs))
                {
                    if (fs.Length < 64)
                        return true;

                    if (reader.ReadUInt16() != 0x5A4D)
                        return true;

                    fs.Position = 0x3C;
                    uint peHeaderOffset = reader.ReadUInt32();
                    if (peHeaderOffset >= fs.Length || peHeaderOffset == 0)
                        return true;

                    fs.Position = peHeaderOffset;
                    if (reader.ReadUInt32() != 0x00004550)
                        return true;

                    ushort machine = reader.ReadUInt16();
                    return machine == 0x8664 || machine == 0xAA64;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error detecting architecture for {executablePath}: {ex.Message}", ex);
                return true;
            }
        }

        private static GameConfig ReloadGameConfigFromLibrary(GameConfig game)
        {
            if (game == null || game.GameGuid == Guid.Empty)
                return game;
            var fresh = ServiceLocator.GameDataService.GetGame(game.GameGuid);
            return fresh ?? game;
        }

        private void RemoveStraySteamApiDllsBesideExecutable(string exeDirectory, bool besideExeDeployActive)
        {
            if (string.IsNullOrEmpty(exeDirectory) || !Directory.Exists(exeDirectory))
                return;

            string[] fileNames = besideExeDeployActive
                ? new[]
                {
                    PathConstants.GoldbergSteamClientDll32,
                    PathConstants.GoldbergSteamClientDll64,
                    PathConstants.GoldbergGameOverlayRendererDll32,
                    PathConstants.GoldbergGameOverlayRendererDll64,
                }
                : new[] { PathConstants.GoldbergSteamDllFileName };

            foreach (string fileName in fileNames)
            {
                string path = Path.Combine(exeDirectory, fileName);
                if (!File.Exists(path))
                    continue;
                try
                {
                    File.Delete(path);
                    _logger?.LogDebug($"Removed stray {fileName} from game folder before launch");
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning($"Could not remove stray {fileName} at {path}: {ex.Message}");
                }
            }
        }

        private ValidationResult EnsureSteamDllInGoldbergFolder()
        {
            if (IsSteamDllModeAvailable())
                return ValidationResult.Success();

            _logger?.LogError("Steam.dll is missing from goldberg\\steam_old");
            return ValidationResult.Failure(
                "Steam.dll is not in goldberg\\steam_old. Run Goldberg Update or install Steam, then try again.");
        }

        private static List<string> CollectStandardGoldbergDeployTargetPaths(string gameRootFolder, bool useX64)
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(gameRootFolder) || !Directory.Exists(gameRootFolder))
                return new List<string>();

            foreach (string existing in SteamApiValidator.GetDeployTargetPathsForBitness(gameRootFolder, useX64))
                paths.Add(existing);

            string dllName = useX64 ? PathConstants.GoldbergStandardSteamApiDll64 : PathConstants.GoldbergStandardSteamApiDll32;
            foreach (string exeDir in EnumerateDirectoriesContainingExecutables(gameRootFolder, maxDepth: 8))
            {
                string candidate = Path.Combine(exeDir, dllName);
                if (File.Exists(candidate))
                    paths.Add(candidate);
            }

            return paths.ToList();
        }

        private static List<string> EnumerateDirectoriesContainingExecutables(string rootFolder, int maxDepth)
        {
            var dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(rootFolder) || !Directory.Exists(rootFolder))
                return new List<string>();

            void Walk(string dir, int depth)
            {
                if (depth > maxDepth)
                    return;
                try
                {
                    if (Directory.GetFiles(dir, "*.exe").Length > 0)
                        dirs.Add(dir);
                }
                catch
                {
                }
                if (depth >= maxDepth)
                    return;
                try
                {
                    foreach (string sub in Directory.GetDirectories(dir))
                        Walk(sub, depth + 1);
                }
                catch
                {
                }
            }

            Walk(rootFolder, 0);
            return dirs.ToList();
        }

        // ActiveProcess must reference the experimental steamclient copied beside the launch executable (game folder).
        private static bool TryResolveStandardModeRegistryDirectory(
            string launchExeDirectory,
            bool useX64,
            Win32DllDeploymentState deployState,
            out string registryDllDirectory,
            out string registrySteamClientPath)
        {
            registryDllDirectory = null;
            registrySteamClientPath = null;

            string steamClientFileName = useX64 ? PathConstants.GoldbergSteamClientDll64 : PathConstants.GoldbergSteamClientDll32;

            if (TryGetDeployedSteamClientDirectory(launchExeDirectory, steamClientFileName, out registryDllDirectory))
            {
                registrySteamClientPath = Path.Combine(registryDllDirectory, steamClientFileName);
                return true;
            }

            if (deployState?.Sites != null)
            {
                foreach (StandardDeploySite site in deployState.Sites)
                {
                    if (site?.Files == null)
                        continue;

                    foreach (FileDeployment file in site.Files)
                    {
                        if (file?.TargetPath == null)
                            continue;

                        if (!string.Equals(Path.GetFileName(file.TargetPath), steamClientFileName, StringComparison.OrdinalIgnoreCase))
                            continue;

                        string directory = Path.GetDirectoryName(file.TargetPath);
                        if (string.IsNullOrEmpty(directory))
                            continue;

                        string clientPath = Path.Combine(directory, steamClientFileName);
                        if (!File.Exists(clientPath))
                            continue;

                        registryDllDirectory = directory;
                        registrySteamClientPath = clientPath;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryGetDeployedSteamClientDirectory(
            string directory,
            string steamClientFileName,
            out string registryDllDirectory)
        {
            registryDllDirectory = null;
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return false;

            string clientPath = Path.Combine(directory, steamClientFileName);
            if (!File.Exists(clientPath))
                return false;

            registryDllDirectory = directory;
            return true;
        }

        private ValidationResult DeployStandardGoldbergReleaseToAllTargets(
            GameConfig game,
            bool useX64,
            string gameRootFolder,
            string launchExeDirectory,
            Win32DllDeploymentState deployState)
        {
            var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in CollectStandardGoldbergDeployTargetPaths(gameRootFolder, useX64))
                targets.Add(path);

            if (!string.IsNullOrEmpty(launchExeDirectory) && Directory.Exists(launchExeDirectory))
            {
                string steamApiFileName = useX64
                    ? PathConstants.GoldbergStandardSteamApiDll64
                    : PathConstants.GoldbergStandardSteamApiDll32;
                targets.Add(Path.Combine(launchExeDirectory, steamApiFileName));
            }

            if (targets.Count == 0)
                return ValidationResult.Failure(
                    "Experimental mode needs a game executable folder or an existing steam_api DLL under the game folder.");

            _logger?.LogDebug($"Standard Goldberg deploy targets ({targets.Count}): {string.Join("; ", targets)}");

            foreach (string steamApiDeployPath in targets)
            {
                var site = new StandardDeploySite();
                var siteResult = DeployStandardGoldbergReleaseToSite(game, useX64, steamApiDeployPath, site);
                if (!siteResult.IsValid)
                    return siteResult;
                if (deployState != null)
                    deployState.Sites.Add(site);
            }

            return ValidationResult.Success();
        }

        private ValidationResult DeployStandardGoldbergReleaseToSite(
            GameConfig game, bool useX64, string steamApiDeployPath, StandardDeploySite site)
        {
            try
            {
                string steamApiFileName = useX64 ? PathConstants.GoldbergStandardSteamApiDll64 : PathConstants.GoldbergStandardSteamApiDll32;
                if (!PathConstants.HasGoldbergExperimentalFiles(useX64))
                {
                    return ValidationResult.Failure(
                        "Experimental Goldberg DLLs are missing under goldberg\\experimental. "
                        + "Run Goldberg Update or repair the emulator.");
                }

                string steamApiSrc = PathConstants.CombineGoldbergExperimentalSteamApiPath(useX64);
                string steamClientSrc = PathConstants.CombineGoldbergExperimentalSteamClientPath(useX64);

                string deployDirectory = Path.GetDirectoryName(steamApiDeployPath);
                if (string.IsNullOrEmpty(deployDirectory))
                    return ValidationResult.Failure("steam_api deployment directory is empty.");

                Directory.CreateDirectory(deployDirectory);
                RemoveStraySteamClientDllsFromDirectory(deployDirectory);

                CopyWithOptionalBackup(steamApiSrc, steamApiDeployPath, site);
                _logger?.LogDebug($"Deployed standard Goldberg {steamApiFileName} to {steamApiDeployPath}");

                string steamClientFileName = useX64 ? PathConstants.GoldbergSteamClientDll64 : PathConstants.GoldbergSteamClientDll32;
                string steamClientDest = Path.Combine(deployDirectory, steamClientFileName);
                CopyWithOptionalBackup(steamClientSrc, steamClientDest, site);
                _logger?.LogDebug($"Deployed experimental {steamClientFileName} to {steamClientDest}");
                // Experimental overlay hooks live inside steam_api; steamclient GameOverlayRenderer is not used here.
                string settingsSource = PathConstants.GetGameSteamSettingsPath(game.AppId);
                if (Directory.Exists(settingsSource))
                {
                    string settingsDest = Path.Combine(deployDirectory, PathConstants.SteamSettingsFolderName);
                    if (TryLinkSteamSettingsJunction(settingsSource, settingsDest, out string junctionError))
                        site.MirroredSteamSettingsPath = settingsDest;
                    else
                        _logger?.LogWarning(
                            $"Could not link {PathConstants.SteamSettingsFolderName} beside {steamApiDeployPath}: {junctionError}");
                }
                else
                    _logger?.LogWarning($"Library {PathConstants.SteamSettingsFolderName} not found at {settingsSource}; standard Goldberg may miss per-game config at {deployDirectory}.");

                return ValidationResult.Success();
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Failed to deploy standard Goldberg release to {steamApiDeployPath}: {ex.Message}", ex);
                return ValidationResult.Failure($"Failed to deploy standard Goldberg release: {ex.Message}");
            }
        }

        private static void RemoveStraySteamClientDllsFromDirectory(string directory)
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return;

            foreach (string fileName in new[]
            {
                PathConstants.GoldbergSteamClientDll32,
                PathConstants.GoldbergSteamClientDll64,
                PathConstants.GoldbergGameOverlayRendererDll32,
                PathConstants.GoldbergGameOverlayRendererDll64,
            })
            {
                string path = Path.Combine(directory, fileName);
                if (!File.Exists(path))
                    continue;
                try
                {
                    File.Delete(path);
                }
                catch
                {
                }
            }
        }

        private bool TryLinkSteamSettingsJunction(string sourceDir, string destDir, out string error)
        {
            DirectoryJunctionHelper.RemoveLinkIfPresent(destDir);

            string sourceFull = Path.GetFullPath(sourceDir);
            string destParent = Path.GetDirectoryName(destDir);
            if (!string.IsNullOrEmpty(destParent))
                Directory.CreateDirectory(destParent);

            if (!DirectoryJunctionHelper.TryCreateDirectoryJunction(destDir, sourceFull, out error))
                return false;

            _logger?.LogDebug(
                $"Linked {PathConstants.SteamSettingsFolderName} junction: {destDir} -> {sourceFull}");
            return true;
        }

        private ValidationResult DeployGoldbergClientDlls(bool useX64, string targetDirectory, bool deploySteamDllOnly, Win32DllDeploymentState win32DeployState)
        {
            try
            {
                if (string.IsNullOrEmpty(targetDirectory))
                    return ValidationResult.Failure("Target directory for emulator DLLs is empty");

                Directory.CreateDirectory(targetDirectory);

                string steamName = useX64 ? PathConstants.GoldbergSteamClientDll64 : PathConstants.GoldbergSteamClientDll32;
                string overlayName = useX64 ? PathConstants.GoldbergGameOverlayRendererDll64 : PathConstants.GoldbergGameOverlayRendererDll32;

                if (deploySteamDllOnly)
                {
                    string oldSrc = PathConstants.CombineGoldbergSteamDllPath();
                    string oldDest = Path.Combine(targetDirectory, PathConstants.GoldbergSteamDllFileName);
                    if (!File.Exists(oldSrc))
                    {
                        _logger?.LogError($"Required DLL not found: {oldSrc}");
                        return ValidationResult.Failure($"Required DLL not found: {oldSrc}");
                    }

                    CopyWithOptionalBackup(oldSrc, oldDest, GetOrCreatePrimaryDeploySite(win32DeployState));
                    _logger?.LogDebug("Steam.dll mode: copied Steam.dll only");
                }
                else
                {
                    string steamSrc = PathConstants.CombineGoldbergSteamClientDllPath(useX64);
                    string steamDest = Path.Combine(targetDirectory, steamName);
                    if (!File.Exists(steamSrc))
                    {
                        _logger?.LogError($"Required DLL not found: {steamSrc}");
                        return ValidationResult.Failure($"Required DLL not found: {steamSrc}");
                    }
                    var deploySite = GetOrCreatePrimaryDeploySite(win32DeployState);
                    CopyWithOptionalBackup(steamSrc, steamDest, deploySite);
                    _logger?.LogDebug($"Copied {steamName}");

                    string overlaySrc = PathConstants.CombineGoldbergGameOverlayRendererPath(useX64);
                    string overlayDest = Path.Combine(targetDirectory, overlayName);
                    if (File.Exists(overlaySrc))
                    {
                        CopyWithOptionalBackup(overlaySrc, overlayDest, deploySite);
                        _logger?.LogDebug($"Copied {overlayName}");
                    }
                    else
                        _logger?.LogWarning($"Overlay DLL not found: {overlaySrc} (non-critical)");

                }

                return ValidationResult.Success();
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Failed to setup game emulator: {ex.Message}", ex);
                return ValidationResult.Failure($"Failed to setup game emulator: {ex.Message}");
            }
        }

        private static StandardDeploySite GetOrCreatePrimaryDeploySite(Win32DllDeploymentState state)
        {
            if (state == null)
                return null;
            if (state.Sites.Count == 0)
                state.Sites.Add(new StandardDeploySite());
            return state.Sites[0];
        }

        private void CopyWithOptionalBackup(string sourcePath, string targetPath, StandardDeploySite site)
        {
            if (site == null)
            {
                File.Copy(sourcePath, targetPath, overwrite: true);
                return;
            }

            var deployment = new FileDeployment { TargetPath = targetPath };

            if (File.Exists(targetPath))
            {
                deployment.HadOriginal = true;
                deployment.BackupPath = targetPath + PathConstants.SteamApiBackupSidecarExtension;
                DeleteLegacySteamApiDeploymentBackup(targetPath);
                try
                {
                    if (File.Exists(deployment.BackupPath))
                        File.Delete(deployment.BackupPath);
                }
                catch
                {
                }
                File.Copy(targetPath, deployment.BackupPath, overwrite: true);
            }

            File.Copy(sourcePath, targetPath, overwrite: true);
            site.Files.Add(deployment);
        }

        private void CompleteLaunchSessionCleanup(
            ulong appId,
            int processId,
            Win32DllDeploymentState deployState,
            bool restoreActiveProcessRegistry,
            BranchRestoreInfo branchRestoreOnExit)
        {
            if (appId != 0)
                CancelRegistryRestoreTimer(appId);

            string manifestPath = null;
            lock (_manifestPathLock)
            {
                if (_manifestPathByAppId.TryGetValue(appId, out manifestPath))
                    _manifestPathByAppId.Remove(appId);
            }

            lock (_watcherActiveLock)
                _watcherActiveAppIds.Remove(appId);

            PersistedLaunchSession session = BuildPersistedLaunchSession(
                appId,
                processId,
                deployState,
                restoreActiveProcessRegistry,
                branchRestoreOnExit);
            _launchSessionCleanup.TryExecuteCleanup(manifestPath, session);

            lock (_sourceModRestoreLock)
            {
                _sourceModRestoreByProcessId.Remove(processId);
                _sourceModRestoreByProcessId.Remove(0);
            }
        }

        private void RunImmediateLaunchSessionCleanup(
            ulong appId,
            int processId,
            Win32DllDeploymentState deployState,
            bool restoreActiveProcessRegistry,
            BranchRestoreInfo branchRestoreOnExit)
        {
            if (appId != 0)
                CancelRegistryRestoreTimer(appId);

            string manifestPath = null;
            lock (_manifestPathLock)
            {
                if (appId != 0 && _manifestPathByAppId.TryGetValue(appId, out manifestPath))
                    _manifestPathByAppId.Remove(appId);
            }

            lock (_watcherActiveLock)
                _watcherActiveAppIds.Remove(appId);

            _launchSessionCleanup.TryDeleteManifest(manifestPath);
            PersistedLaunchSession session = BuildPersistedLaunchSession(
                appId,
                processId,
                deployState,
                restoreActiveProcessRegistry,
                branchRestoreOnExit);
            _launchSessionCleanup.TryExecuteCleanup(null, session);

            lock (_sourceModRestoreLock)
            {
                _sourceModRestoreByProcessId.Remove(processId);
                _sourceModRestoreByProcessId.Remove(0);
            }
        }

        private void ReleaseLaunchSessionTracking(int processId)
        {
            lock (_branchRestoreLock)
                _branchRestoreByProcessId.Remove(processId);

            lock (_deployLock)
                _win32DeploymentByProcessId.Remove(processId);

            lock (_activeProcessRegistryLock)
                _processIdsWithActiveProcessRegistry.Remove(processId);

            lock (_sourceModRestoreLock)
            {
                _sourceModRestoreByProcessId.Remove(processId);
                _sourceModRestoreByProcessId.Remove(0);
            }
        }

        private bool IsWatcherResponsibleForCleanup(ulong appId)
        {
            lock (_watcherActiveLock)
                return _watcherActiveAppIds.Contains(appId);
        }

        private PersistedLaunchSession BuildPersistedLaunchSession(
            ulong appId,
            int processId,
            Win32DllDeploymentState deployState,
            bool restoreActiveProcessRegistry,
            BranchRestoreInfo branchRestoreOnExit)
        {
            var session = new PersistedLaunchSession
            {
                AppId = appId,
                GameProcessId = processId,
                SessionGeneration = Guid.NewGuid().ToString("D"),
                RestoreActiveProcessRegistry = restoreActiveProcessRegistry,
                GameLibraryFolder = appId != 0 ? PathConstants.GetGameFolder(appId) : null,
                LoadDllsFolder = appId != 0 ? PathConstants.CombineGameSteamSettingsLoadDllsDirectory(appId) : null,
                DeploySites = MapDeploySites(deployState),
                SourceModRestore = TryGetPersistedSourceModRestore(processId),
            };

            if (branchRestoreOnExit != null)
            {
                session.BranchRestore = new PersistedBranchRestore
                {
                    AppId = branchRestoreOnExit.AppId,
                    BranchName = branchRestoreOnExit.BranchName,
                    IsBetaBranch = branchRestoreOnExit.IsBetaBranch,
                };
            }

            return session;
        }

        private PersistedSourceModRestore TryGetPersistedSourceModRestore(int processId)
        {
            SteamSourceModRegistryHelper.RestoreToken token = null;
            lock (_sourceModRestoreLock)
            {
                if (!_sourceModRestoreByProcessId.TryGetValue(processId, out token))
                {
                    if (processId != 0 || !_sourceModRestoreByProcessId.TryGetValue(0, out token))
                        return null;
                }
            }

            return new PersistedSourceModRestore
            {
                HadPreviousValue = token.HadPreviousValue,
                PreviousValue = token.PreviousValue,
            };
        }

        private static int CountDeployFiles(System.Collections.Generic.List<PersistedDeploySite> sites)
        {
            if (sites == null)
                return 0;

            int count = 0;
            foreach (PersistedDeploySite site in sites)
            {
                if (site?.Files != null)
                    count += site.Files.Count;
            }

            return count;
        }

        private static System.Collections.Generic.List<PersistedDeploySite> MapDeploySites(Win32DllDeploymentState deployState)
        {
            if (deployState?.Sites == null || deployState.Sites.Count == 0)
                return null;

            var sites = new System.Collections.Generic.List<PersistedDeploySite>();
            foreach (StandardDeploySite site in deployState.Sites)
            {
                if (site == null)
                    continue;

                var persistedSite = new PersistedDeploySite
                {
                    MirroredSteamSettingsPath = site.MirroredSteamSettingsPath,
                };

                if (site.Files != null && site.Files.Count > 0)
                {
                    persistedSite.Files = new System.Collections.Generic.List<PersistedFileDeployment>();
                    foreach (FileDeployment file in site.Files)
                    {
                        persistedSite.Files.Add(new PersistedFileDeployment
                        {
                            TargetPath = file.TargetPath,
                            BackupPath = file.BackupPath,
                            HadOriginal = file.HadOriginal,
                        });
                    }
                }

                sites.Add(persistedSite);
            }

            return sites.Count > 0 ? sites : null;
        }

        private void ApplySourceModInstallPathForSession(string folderContainingSteamClient)
        {
            string applyError;
            SteamSourceModRegistryHelper.RestoreToken token;
            if (!SteamSourceModRegistryHelper.TryApplySourceModInstallPath(folderContainingSteamClient, out token, out applyError))
            {
                _logger?.LogWarning($"SourceModInstallPath registry setup failed: {applyError}");
                return;
            }

            lock (_sourceModRestoreLock)
                _sourceModRestoreByProcessId[0] = token;

            _logger?.LogDebug($"Set SourceModInstallPath to folder containing steamclient: {folderContainingSteamClient}");
        }

        private void AttachSourceModRestoreToProcess(int processId)
        {
            lock (_sourceModRestoreLock)
            {
                if (!_sourceModRestoreByProcessId.TryGetValue(0, out SteamSourceModRegistryHelper.RestoreToken token))
                    return;
                _sourceModRestoreByProcessId.Remove(0);
                _sourceModRestoreByProcessId[processId] = token;
            }
        }

        private void RestoreSourceModInstallPathForProcess(int processId)
        {
            SteamSourceModRegistryHelper.RestoreToken token = null;
            lock (_sourceModRestoreLock)
            {
                if (_sourceModRestoreByProcessId.TryGetValue(processId, out token))
                    _sourceModRestoreByProcessId.Remove(processId);
            }

            if (token == null)
                return;

            SteamSourceModRegistryHelper.RestoreSourceModInstallPath(token);
            _logger?.LogDebug("Restored SourceModInstallPath registry value after game exit.");
        }

        private void RestoreAllSourceModInstallPaths()
        {
            SteamSourceModRegistryHelper.RestoreToken[] tokens;
            lock (_sourceModRestoreLock)
            {
                tokens = new SteamSourceModRegistryHelper.RestoreToken[_sourceModRestoreByProcessId.Count];
                _sourceModRestoreByProcessId.Values.CopyTo(tokens, 0);
                _sourceModRestoreByProcessId.Clear();
            }

            foreach (SteamSourceModRegistryHelper.RestoreToken token in tokens)
                SteamSourceModRegistryHelper.RestoreSourceModInstallPath(token);
        }

        private ValidationResult ConfigureActiveProcessGoldbergDlls(bool useX64, string dllDirectory)
        {
            if (string.IsNullOrEmpty(dllDirectory))
                return ValidationResult.Failure("ActiveProcess registry directory is empty.");

            if (!SteamActiveProcessRegistryHelper.TrySetGoldbergClientDllPaths(dllDirectory, out string errorMessage))
            {
                _logger?.LogError("ActiveProcess registry setup failed: " + errorMessage);
                return ValidationResult.Failure(
                    string.IsNullOrEmpty(errorMessage)
                        ? "ActiveProcess registry setup failed."
                        : "ActiveProcess registry setup failed: " + errorMessage);
            }

            // Placeholder until the game process starts: if pid still points at a running Steam.exe,
            // steam_api can spawn the real client before we update pid after Process.Start.
            int launcherPid = Process.GetCurrentProcess().Id;
            SteamActiveProcessRegistryHelper.SetActiveProcessPid(launcherPid);
            _logger?.LogDebug($"ActiveProcess pid placeholder = {launcherPid} (launcher, before game start)");

            string directoryFull = Path.GetFullPath(dllDirectory);
            _logger?.LogMessage(
                "ActiveProcess registry updated (32- and 64-bit views) to Goldberg steamclient under "
                + directoryFull);
            return ValidationResult.Success();
        }

        private void SetActiveProcessPid(int processId)
        {
            SteamActiveProcessRegistryHelper.SetActiveProcessPid(processId);
            _logger?.LogMessage($"Set ActiveProcess pid = {processId}");
        }

        private void MarkActiveProcessRegistryForProcess(int processId)
        {
            lock (_activeProcessRegistryLock)
                _processIdsWithActiveProcessRegistry.Add(processId);
        }

        private void RestoreActiveProcessRegistryToSteamInstall()
        {
            SteamActiveProcessRegistryHelper.RestoreSteamClientDllPathsToSteamInstall();
            _logger?.LogDebug("Restored ActiveProcess steamclient paths to the Steam installation.");
        }

        private void CancelRegistryRestoreTimer(ulong appId)
        {
            if (appId == 0)
                return;

            CancellationTokenSource cts;
            lock (_registryRestoreTimerLock)
            {
                if (!_registryRestoreCancellationByAppId.TryGetValue(appId, out cts))
                    return;
                _registryRestoreCancellationByAppId.Remove(appId);
            }

            try
            {
                cts.Cancel();
            }
            catch
            {
            }

            cts.Dispose();
        }

        private void ScheduleRegistryRestoreAfterLoadWindow(
            ulong appId,
            string sessionGeneration,
            string manifestPath,
            int processId)
        {
            if (appId == 0 || string.IsNullOrEmpty(manifestPath))
                return;

            CancelRegistryRestoreTimer(appId);

            var cts = new CancellationTokenSource();
            lock (_registryRestoreTimerLock)
                _registryRestoreCancellationByAppId[appId] = cts;

            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(ApplicationConstants.LaunchRegistryRedirectDurationMs, cts.Token)
                        .ConfigureAwait(false);
                    _launchSessionCleanup.TryRestoreRegistryAfterLoadWindow(
                        manifestPath,
                        sessionGeneration,
                        processId);
                    ReleaseRegistryTrackingAfterEarlyRestore(processId);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning($"In-process registry restore after load window failed: {ex.Message}");
                }
                finally
                {
                    lock (_registryRestoreTimerLock)
                    {
                        if (_registryRestoreCancellationByAppId.TryGetValue(appId, out CancellationTokenSource current)
                            && ReferenceEquals(current, cts))
                        {
                            _registryRestoreCancellationByAppId.Remove(appId);
                        }
                    }

                    cts.Dispose();
                }
            });
        }

        private void ReleaseRegistryTrackingAfterEarlyRestore(int processId)
        {
            lock (_activeProcessRegistryLock)
                _processIdsWithActiveProcessRegistry.Remove(processId);

            lock (_sourceModRestoreLock)
            {
                _sourceModRestoreByProcessId.Remove(processId);
                _sourceModRestoreByProcessId.Remove(0);
            }
        }

        private string GetBaseFolderForLaunchOptions(GameConfig game, LaunchOption launchOption)
        {
            string baseFromStart = GameFolderPathHelper.GetLaunchBaseGameFolder(game);

            if (launchOption == null)
                return baseFromStart;

            if (!string.IsNullOrWhiteSpace(game.StartFolder) && Directory.Exists(game.StartFolder))
            {
                string installRoot = Path.GetDirectoryName(game.StartFolder);
                if (!string.IsNullOrEmpty(installRoot) && Directory.Exists(installRoot))
                {
                    bool useInstallRoot = true;
                    if (!string.IsNullOrWhiteSpace(launchOption.WorkingDir))
                    {
                        if (PathValidationHelper.TryResolveAndValidatePath(installRoot, launchOption.WorkingDir, out string resolvedWd))
                            useInstallRoot = Directory.Exists(resolvedWd);
                        else
                            useInstallRoot = false;
                    }
                    if (useInstallRoot && !string.IsNullOrEmpty(launchOption.Executable))
                    {
                        if (PathValidationHelper.TryResolveAndValidatePath(installRoot, launchOption.Executable, out string resolvedExe))
                            useInstallRoot = File.Exists(resolvedExe);
                        else
                            useInstallRoot = false;
                    }
                    if (useInstallRoot)
                        return installRoot;
                }
            }

            return baseFromStart;
        }

        private string GetEffectiveWorkingDirectory(GameConfig game, LaunchOption launchOption, string baseGameFolder)
        {
            if (launchOption != null && !string.IsNullOrWhiteSpace(launchOption.WorkingDir))
            {
                string resolved = ResolveWorkingDirectoryUnderBase(baseGameFolder, launchOption.WorkingDir, "Steam (game assets) launch option");
                return resolved ?? baseGameFolder;
            }

            if (game != null && !string.IsNullOrWhiteSpace(game.WorkingDirectory))
            {
                string resolved = ResolveWorkingDirectoryUnderBase(baseGameFolder, game.WorkingDirectory, "game WorkingDirectory");
                return resolved ?? baseGameFolder;
            }

            return baseGameFolder;
        }

        private string ResolveWorkingDirectoryUnderBase(string baseGameFolder, string userPath, string contextLabel)
        {
            if (string.IsNullOrWhiteSpace(userPath))
                return null;
            if (!PathValidationHelper.TryResolveAndValidatePath(baseGameFolder, userPath.Trim(), out string resolvedWorkingDir))
            {
                _logger?.LogWarning($"Invalid or unsafe working directory ({contextLabel}): {userPath}. Using base folder instead.");
                return null;
            }
            return resolvedWorkingDir;
        }

        private string ResolveLaunchExecutable(string baseGameFolder, GameConfig game, LaunchOption launchOption, bool logWarnings, bool logResolutionSteps)
        {
            string fallback = GameFolderPathHelper.ResolveStoredPathUnderBase(baseGameFolder, game.Path);
            if (logResolutionSteps)
                _logger?.LogDebug($"Initial executable path (from game config): {fallback}");

            if (launchOption == null || string.IsNullOrEmpty(launchOption.Executable))
                return fallback;

            if (logResolutionSteps)
                _logger?.LogDebug($"Launch option executable: {launchOption.Executable}");

            if (!PathValidationHelper.TryResolveAndValidatePath(baseGameFolder, launchOption.Executable, out string resolved))
            {
                if (logWarnings)
                    _logger?.LogWarning($"Invalid or unsafe executable path detected: {launchOption.Executable}. Using game.Path instead.");
                return fallback;
            }

            if (logResolutionSteps)
                _logger?.LogDebug($"Resolved executable path: {resolved}");

            if (!File.Exists(resolved))
            {
                if (logWarnings)
                    _logger?.LogWarning($"Launch option executable not found: {resolved}, falling back to game.Path");
                return fallback;
            }

            if (logResolutionSteps)
                _logger?.LogDebug($"Using launch option executable: {resolved}");
            return resolved;
        }

        private ResolvedLaunchCommand ResolveLaunchCommand(
            GameConfig game,
            LaunchOption launchOption,
            bool logResolution,
            out string baseGameFolder)
        {
            baseGameFolder = null;
            if (game == null)
                return null;

            baseGameFolder = GetBaseFolderForLaunchOptions(game, launchOption);
            string workingDirectory = GetEffectiveWorkingDirectory(game, launchOption, baseGameFolder);
            string executablePath = ResolveLaunchExecutable(
                baseGameFolder,
                game,
                launchOption,
                logWarnings: logResolution,
                logResolutionSteps: logResolution);

            string arguments = (launchOption != null && !string.IsNullOrEmpty(launchOption.Parameters))
                ? launchOption.Parameters
                : (game.Parameters ?? string.Empty);

            return new ResolvedLaunchCommand
            {
                ExecutablePath = executablePath,
                Arguments = arguments,
                WorkingDirectory = workingDirectory
            };
        }

        private bool IsGameSessionActive(ulong appId)
        {
            if (appId == 0)
                return false;

            if (TryGetActiveLaunchedProcess(appId, out _))
                return true;

            lock (_watcherActiveLock)
            {
                if (_watcherActiveAppIds.Contains(appId))
                    return true;
            }

            return _launchSessionCleanup.HasLiveSessionForAppId(appId);
        }

        private bool TryGetActiveLaunchedProcess(ulong appId, out Process activeProcess)
        {
            activeProcess = null;
            lock (_launchedProcesses)
            {
                if (!_launchedProcesses.TryGetValue(appId, out Process process))
                    return false;

                if (process.HasExited)
                {
                    try
                    {
                        process.Exited -= Process_Exited;
                        process.Dispose();
                    }
                    catch
                    {
                    }

                    _launchedProcesses.Remove(appId);
                    return false;
                }

                activeProcess = process;
                return true;
            }
        }

        private ValidationResult ValidateGameNotAlreadyRunning(GameConfig game)
        {
            if (game == null || game.AppId == 0)
                return ValidationResult.Success();

            if (!IsGameSessionActive(game.AppId))
                return ValidationResult.Success();

            string displayName = string.IsNullOrWhiteSpace(game.AppName)
                ? $"AppId {game.AppId}"
                : game.AppName;
            return ValidationResult.Failure(
                $"{displayName} is already running. Close the game before launching again.");
        }

        private ValidationResult LaunchProcess(
            GameConfig game,
            LaunchOption launchOption = null,
            BranchRestoreInfo branchRestoreOnExit = null,
            Win32DllDeploymentState win32DeployState = null,
            bool configureActiveProcessRegistry = false,
            bool trackCleanupSession = false)
        {
            try
            {
                ResolvedLaunchCommand resolved = ResolveLaunchCommand(game, launchOption, logResolution: true, out string baseGameFolder);
                string executablePath = resolved.ExecutablePath;
                string workingDirectory = resolved.WorkingDirectory;
                string launchArguments = resolved.Arguments;

                _logger?.LogDebug($"Base game folder: {baseGameFolder}");
                _logger?.LogDebug($"Working directory: {workingDirectory}");
                _logger?.LogDebug($"Final executable path: {executablePath}");
                _logger?.LogDebug($"Executable exists: {File.Exists(executablePath)}");

                string exeDirForSafeCheck = Path.GetDirectoryName(executablePath);
                if (!PathValidationHelper.IsSafeFilePath(executablePath, baseGameFolder, exeDirForSafeCheck))
                {
                    _logger?.LogError($"Unsafe executable path detected: {executablePath}");
                    return ValidationResult.Failure("Invalid executable path detected. Security validation failed.");
                }

                _logger?.LogDebug($"Launch arguments: {(string.IsNullOrEmpty(launchArguments) ? "(none)" : launchArguments)}");

                if (!PathValidationHelper.IsSafeFilePath(workingDirectory, baseGameFolder))
                {
                    _logger?.LogError($"Unsafe working directory detected: {workingDirectory}");
                    return ValidationResult.Failure("Invalid working directory detected. Security validation failed.");
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    WorkingDirectory = workingDirectory,
                    Arguments = launchArguments,
                    UseShellExecute = false,
                    CreateNoWindow = false
                };
                startInfo.EnvironmentVariables["SteamAppId"] = game.AppId.ToString();
                _logger?.LogDebug($"Starting process: {startInfo.FileName}, WD: {startInfo.WorkingDirectory}, Args: {startInfo.Arguments}");

                Process process;
                bool started = TryStartProcess(startInfo, out process);

                if (!started || process == null)
                {
                    _logger?.LogError("Process.Start returned null");
                    RunImmediateLaunchSessionCleanup(
                        game.AppId,
                        0,
                        win32DeployState,
                        configureActiveProcessRegistry,
                        branchRestoreOnExit);
                    return ValidationResult.Failure("Failed to start game process");
                }

                if (!TryGetProcessId(process, out int processId))
                {
                    _logger?.LogWarning(
                        "Game process exited before launch setup could read its process ID; running immediate session cleanup.");
                    try
                    {
                        process.Dispose();
                    }
                    catch
                    {
                    }

                    RunImmediateLaunchSessionCleanup(
                        game.AppId,
                        0,
                        win32DeployState,
                        configureActiveProcessRegistry,
                        branchRestoreOnExit);
                    return ValidationResult.Success();
                }

                if (configureActiveProcessRegistry)
                {
                    SetActiveProcessPid(processId);
                    MarkActiveProcessRegistryForProcess(processId);
                }

                AttachSourceModRestoreToProcess(processId);

                if (branchRestoreOnExit != null)
                {
                    lock (_branchRestoreLock)
                        _branchRestoreByProcessId[processId] = branchRestoreOnExit;
                }

                if (win32DeployState != null && win32DeployState.Sites != null && win32DeployState.Sites.Count > 0)
                {
                    lock (_deployLock)
                        _win32DeploymentByProcessId[processId] = win32DeployState;
                }

                ulong appId = game.AppId;
                lock (_launchedProcesses)
                {
                    if (_launchedProcesses.TryGetValue(appId, out Process oldProcess))
                        SupersedeTrackedGameProcess(appId, oldProcess);
                    _launchedProcesses[appId] = process;
                }

                if (trackCleanupSession && game.AppId != 0)
                {
                    PersistedLaunchSession persisted = BuildPersistedLaunchSession(
                        game.AppId,
                        processId,
                        win32DeployState,
                        configureActiveProcessRegistry,
                        branchRestoreOnExit);
                    string manifestPath = _launchSessionCleanup.WriteSession(persisted);
                    if (!string.IsNullOrEmpty(manifestPath))
                    {
                        int deployFiles = CountDeployFiles(persisted.DeploySites);
                        _logger?.LogDebug(
                            $"Launch session manifest for AppId {game.AppId} (PID {processId}): {manifestPath}; "
                            + $"generation={persisted.SessionGeneration}, beside-exe files={deployFiles}, "
                            + $"library={persisted.GameLibraryFolder}, load_dlls={persisted.LoadDllsFolder}.");

                        lock (_manifestPathLock)
                            _manifestPathByAppId[game.AppId] = manifestPath;

                        if (_launchSessionCleanup.SpawnDetachedWatcher(manifestPath))
                        {
                            lock (_watcherActiveLock)
                                _watcherActiveAppIds.Add(game.AppId);
                        }
                        else
                        {
                            _logger?.LogWarning(
                                $"Launch cleanup watcher did not start for AppId {game.AppId}; cleanup will run when the game exits in-process.");

                            if (LaunchSessionCleanupService.SessionNeedsRegistryRestore(persisted))
                            {
                                ScheduleRegistryRestoreAfterLoadWindow(
                                    game.AppId,
                                    persisted.SessionGeneration,
                                    manifestPath,
                                    processId);
                            }
                        }
                    }
                }

                process.EnableRaisingEvents = true;
                process.Exited += Process_Exited;
                if (process.HasExited)
                    Process_Exited(process, EventArgs.Empty);

                _logger?.LogMessage($"Game launched successfully: {game.AppName} (PID: {processId})");
                return ValidationResult.Success();
            }
            catch (Exception ex)
            {
                RunImmediateLaunchSessionCleanup(
                    game.AppId,
                    0,
                    win32DeployState,
                    configureActiveProcessRegistry,
                    branchRestoreOnExit);
                _logger?.LogError($"Failed to launch process: {ex.Message}", ex);
                return ValidationResult.Failure($"Failed to launch process: {ex.Message}");
            }
        }

        private void SupersedeTrackedGameProcess(ulong appId, Process oldProcess)
        {
            if (oldProcess == null)
                return;

            int oldProcessId;
            try
            {
                oldProcessId = oldProcess.Id;
            }
            catch
            {
                ReleaseReplacedGameProcess(oldProcess);
                return;
            }

            SupersededLaunchCleanup superseded = TryCaptureSupersededLaunchCleanup(appId, oldProcessId);
            bool alreadyExited;
            try
            {
                alreadyExited = oldProcess.HasExited;
            }
            catch
            {
                alreadyExited = true;
            }

            ReleaseReplacedGameProcess(oldProcess);

            if (superseded == null)
                return;

            if (alreadyExited)
            {
                RunSupersededLaunchCleanupOnProcessExit(oldProcessId, superseded);
                return;
            }

            lock (_supersededCleanupLock)
                _supersededCleanupByProcessId[oldProcessId] = superseded;
        }

        private SupersededLaunchCleanup TryCaptureSupersededLaunchCleanup(ulong appId, int oldProcessId)
        {
            if (appId == 0 || oldProcessId <= 0)
                return null;

            Win32DllDeploymentState deployState = null;
            lock (_deployLock)
            {
                if (_win32DeploymentByProcessId.TryGetValue(oldProcessId, out deployState))
                    _win32DeploymentByProcessId.Remove(oldProcessId);
            }

            BranchRestoreInfo branchRestore = null;
            lock (_branchRestoreLock)
            {
                if (_branchRestoreByProcessId.TryGetValue(oldProcessId, out branchRestore))
                    _branchRestoreByProcessId.Remove(oldProcessId);
            }

            bool restoreActiveProcessRegistry;
            lock (_activeProcessRegistryLock)
                restoreActiveProcessRegistry = _processIdsWithActiveProcessRegistry.Remove(oldProcessId);

            PersistedSourceModRestore sourceModRestore = TryGetPersistedSourceModRestore(oldProcessId);
            lock (_sourceModRestoreLock)
            {
                _sourceModRestoreByProcessId.Remove(oldProcessId);
                if (oldProcessId != 0)
                    _sourceModRestoreByProcessId.Remove(0);
            }

            if (deployState == null
                && branchRestore == null
                && !restoreActiveProcessRegistry
                && sourceModRestore == null)
            {
                return null;
            }

            return new SupersededLaunchCleanup
            {
                AppId = appId,
                DeployState = deployState,
                BranchRestore = branchRestore,
                RestoreActiveProcessRegistry = restoreActiveProcessRegistry,
                SourceModRestore = sourceModRestore,
            };
        }

        private void RunSupersededLaunchCleanupOnProcessExit(int supersededProcessId, SupersededLaunchCleanup superseded)
        {
            if (superseded == null || superseded.AppId == 0)
                return;

            if (IsGameSessionActive(superseded.AppId))
            {
                _logger?.LogDebug(
                    $"Superseded game process {supersededProcessId} (AppId {superseded.AppId}) exited; "
                    + "a newer launch session is still active, skipping deploy cleanup for the replaced process.");
                return;
            }

            bool restoreActiveProcessRegistry = superseded.RestoreActiveProcessRegistry;
            lock (_activeProcessRegistryLock)
            {
                if (_processIdsWithActiveProcessRegistry.Count > 0)
                    restoreActiveProcessRegistry = false;
            }

            PersistedLaunchSession session = BuildPersistedLaunchSession(
                superseded.AppId,
                supersededProcessId,
                superseded.DeployState,
                restoreActiveProcessRegistry,
                superseded.BranchRestore);
            if (superseded.SourceModRestore != null)
                session.SourceModRestore = superseded.SourceModRestore;

            _logger?.LogMessage(
                $"Cleaning superseded launch session for AppId {superseded.AppId} (replaced process PID {supersededProcessId}).");

            _launchSessionCleanup.TryExecuteCleanup(null, session);
        }

        private void ReleaseReplacedGameProcess(Process process)
        {
            if (process == null)
                return;

            try
            {
                process.Exited -= Process_Exited;
                if (process.HasExited)
                {
                    process.Dispose();
                    return;
                }

                process.EnableRaisingEvents = true;
                process.Exited += ReplacedProcess_Exited;
            }
            catch
            {
            }
        }

        private void ReplacedProcess_Exited(object sender, EventArgs e)
        {
            if (!(sender is Process process))
                return;

            int processId;
            try
            {
                processId = process.Id;
            }
            catch
            {
                return;
            }

            SupersededLaunchCleanup superseded = null;
            lock (_supersededCleanupLock)
            {
                if (_supersededCleanupByProcessId.TryGetValue(processId, out superseded))
                    _supersededCleanupByProcessId.Remove(processId);
            }

            try
            {
                process.Exited -= ReplacedProcess_Exited;
                process.Dispose();
            }
            catch
            {
            }

            if (superseded != null)
                RunSupersededLaunchCleanupOnProcessExit(processId, superseded);
        }

        private void Process_Exited(object sender, EventArgs e)
        {
            try
            {
                if (!(sender is Process process))
                    return;

                ulong appId = 0;
                lock (_launchedProcesses)
                {
                    foreach (var kvp in _launchedProcesses)
                    {
                        if (kvp.Value == process)
                        {
                            appId = kvp.Key;
                            _launchedProcesses.Remove(kvp.Key);
                            break;
                        }
                    }
                }

                if (appId == 0)
                    return;

                if (IsWatcherResponsibleForCleanup(appId))
                {
                    ReleaseLaunchSessionTracking(process.Id);
                    lock (_watcherActiveLock)
                        _watcherActiveAppIds.Remove(appId);
                    _logger?.LogDebug(
                        $"Game exited (AppId {appId}); detached watcher will restore deploy and games folder files.");
                }
                else
                {
                    BranchRestoreInfo branchRestore = null;
                    lock (_branchRestoreLock)
                    {
                        if (_branchRestoreByProcessId.TryGetValue(process.Id, out branchRestore))
                            _branchRestoreByProcessId.Remove(process.Id);
                    }

                    Win32DllDeploymentState deployState = null;
                    lock (_deployLock)
                    {
                        if (_win32DeploymentByProcessId.TryGetValue(process.Id, out deployState))
                            _win32DeploymentByProcessId.Remove(process.Id);
                    }

                    bool restoreActiveProcessRegistry;
                    lock (_activeProcessRegistryLock)
                        restoreActiveProcessRegistry = _processIdsWithActiveProcessRegistry.Remove(process.Id);

                    CompleteLaunchSessionCleanup(
                        appId,
                        process.Id,
                        deployState,
                        restoreActiveProcessRegistry,
                        branchRestore);
                }

                process.Exited -= Process_Exited;
                process.Dispose();
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error during game cleanup: {ex.Message}", ex);
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

        private string ResolveLaunchExecutablePath(GameConfig game, LaunchOption launchOption)
        {
            try
            {
                return ResolveLaunchCommand(game, launchOption, logResolution: false, out _)?.ExecutablePath;
            }
            catch
            {
                return null;
            }
        }

        private static bool TryStartProcess(ProcessStartInfo startInfo, out Process process)
        {
            process = null;
            try
            {
                process = Process.Start(startInfo);
                return process != null;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryGetProcessId(Process process, out int processId)
        {
            processId = 0;
            if (process == null)
                return false;

            try
            {
                processId = process.Id;
                return processId > 0;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }

        public void RestoreSteamClientRegistryForApplicationLifecycle()
        {
            try
            {
                if (_launchSessionCleanup.HasAnyLiveLaunchSession())
                {
                    _logger?.LogDebug(
                        "Skipping Steam registry lifecycle restore while a launch session manifest has a live game PID.");
                    return;
                }

                RestoreAllSourceModInstallPaths();
                lock (_activeProcessRegistryLock)
                    _processIdsWithActiveProcessRegistry.Clear();
                RestoreActiveProcessRegistryToSteamInstall();
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Steam client registry lifecycle restore failed: {ex.Message}", ex);
            }
        }

        public bool TryResolvePrimaryExecutable(GameConfig game, out string fullExecutablePath)
        {
            return GameFolderPathHelper.TryResolvePrimaryExecutable(game, out fullExecutablePath);
        }

        public ResolvedLaunchCommand GetResolvedLaunchCommand(GameConfig game, LaunchOption launchOption)
        {
            return ResolveLaunchCommand(game, launchOption, logResolution: false, out _);
        }
    }
}
