using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Win32;
using AppDataKit;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;
using SteamKit;

namespace SmartGoldbergEmu.Services
{
    // Runs Steam-style installscript.vdf "Run Process" entries (per-game + common redistributables) before launch.
    public class SteamInstallScriptService
    {
        private const int DefaultProcessTimeoutMs = 10 * 60 * 1000;
        private const string InstallDirToken = "%INSTALLDIR%";
        private const string RunProcessSectionName = "Run Process";

        private readonly ILogService _logger;
        private readonly SteamProductInfoService _steamProductInfoService;
        private readonly SteamCommonRedistributableDownloadService _commonRedistDownload;

        public SteamInstallScriptService()
            : this(
                ServiceLocator.LogService,
                ServiceLocator.SteamProductInfoService,
                ServiceLocator.SteamCommonRedistributableDownloadService)
        {
        }

        public SteamInstallScriptService(ILogService logger, SteamProductInfoService steamProductInfoService)
            : this(logger, steamProductInfoService, new SteamCommonRedistributableDownloadService(logger))
        {
        }

        public SteamInstallScriptService(
            ILogService logger,
            SteamProductInfoService steamProductInfoService,
            SteamCommonRedistributableDownloadService commonRedistDownload)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _steamProductInfoService = steamProductInfoService ?? throw new ArgumentNullException(nameof(steamProductInfoService));
            _commonRedistDownload = commonRedistDownload ?? throw new ArgumentNullException(nameof(commonRedistDownload));
        }

        // Lists missing Run Process / common-redist units without downloading or installing.
        public List<PendingGameDependency> DiscoverPendingDependencies(GameConfig game, string gameInstallFolder)
        {
            var pending = new List<PendingGameDependency>();
            if (game == null)
                return pending;

            string installFolder = NormalizeExistingDirectory(gameInstallFolder);
            if (string.IsNullOrEmpty(installFolder))
                installFolder = NormalizeExistingDirectory(GameFolderPathHelper.GetLaunchBaseGameFolder(game));
            if (string.IsNullOrEmpty(installFolder))
            {
                _logger?.LogDebug("Install scripts skipped: game install folder not found.");
                return pending;
            }

            try
            {
                foreach (string scriptPath in EnumerateGameInstallScriptFiles(installFolder))
                {
                    AppendPendingFromScriptFile(
                        pending,
                        scriptPath,
                        installFolder,
                        game.AppId,
                        preferredDisplayName: null,
                        depotId: null,
                        downloadDepotId: null,
                        downloadRelative: null);
                }

                AppendPendingCommonRedistributables(game, installFolder, pending);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning($"Install scripts discovery failed: {ex.Message}");
            }

            _logger?.LogDebug(
                $"Install scripts: discovered {pending.Count} pending dependenc"
                + (pending.Count == 1 ? "y" : "ies")
                + $" for AppId={game.AppId}.");
            return pending;
        }

        // Runs previously discovered pending units. Soft-fail per item; returns Failure when any item fails
        // (UI still launches). All-failed uses a distinct message.
        // progress is (message, percent 0–100). isCancelled is checked between items (not mid-installer).
        public ValidationResult InstallPendingDependencies(
            IReadOnlyList<PendingGameDependency> pending,
            Action<string, int> progress = null,
            Func<bool> isCancelled = null)
        {
            if (pending == null || pending.Count == 0)
                return ValidationResult.Success();

            int total = pending.Count;
            int failures = 0;
            for (int i = 0; i < pending.Count; i++)
            {
                if (isCancelled != null && isCancelled())
                    return ValidationResult.Failure("Dependency install cancelled.");

                PendingGameDependency item = pending[i];
                if (item == null)
                    continue;

                string label = string.IsNullOrWhiteSpace(item.DisplayName) ? "dependency" : item.DisplayName.Trim();
                int rangeStart = (i * 100) / total;
                int rangeEnd = ((i + 1) * 100) / total;
                progress?.Invoke(
                    (item.DownloadDepotId.HasValue ? "Preparing " : "Installing ") + label + "…",
                    rangeStart);

                try
                {
                    if (!InstallOnePendingDependency(item, progress, rangeStart, rangeEnd, isCancelled))
                        failures++;
                }
                catch (Exception ex)
                {
                    failures++;
                    _logger?.LogWarning($"Install scripts: '{label}' failed: {ex.Message}");
                }

                progress?.Invoke(
                    failures > 0 && i == pending.Count - 1
                        ? "Dependency install finished."
                        : "Installed " + label + ".",
                    rangeEnd);
            }

            if (isCancelled != null && isCancelled())
                return ValidationResult.Failure("Dependency install cancelled.");

            progress?.Invoke(
                failures == 0 ? "Dependencies installed." : "Dependency install finished.",
                100);
            if (failures == pending.Count)
                return ValidationResult.Failure("All dependency installs failed.");
            if (failures > 0)
                return ValidationResult.Failure(failures + " of " + total + " dependency installs failed.");
            return ValidationResult.Success();
        }

        // Tools entry: discover (+ optionally install). dryRun discovers only.
        public ValidationResult EnsureDependenciesForLaunch(
            GameConfig game,
            string gameInstallFolder,
            bool dryRun = false)
        {
            List<PendingGameDependency> pending = DiscoverPendingDependencies(game, gameInstallFolder);
            if (pending.Count == 0)
                return ValidationResult.Success();

            _logger?.LogMessage(
                $"Install scripts: {pending.Count} pending dependenc"
                + (pending.Count == 1 ? "y" : "ies")
                + $" for AppId={game?.AppId} ({game?.AppName})"
                + (dryRun ? " [dry-run]." : "."));

            if (dryRun)
            {
                foreach (PendingGameDependency item in pending)
                    _logger?.LogMessage("Install scripts: dry-run pending — " + (item.DisplayName ?? "(unnamed)"));
                return ValidationResult.Success();
            }

            return InstallPendingDependencies(pending);
        }

        // Test / tools entry: run against an arbitrary install folder + AppId.
        public ValidationResult EnsureDependenciesForFolder(
            ulong appId,
            string gameInstallFolder,
            string appName = null,
            bool dryRun = false)
        {
            var game = new GameConfig
            {
                AppId = appId,
                AppName = string.IsNullOrWhiteSpace(appName) ? appId.ToString() : appName,
                StartFolder = gameInstallFolder,
                Path = gameInstallFolder
            };
            return EnsureDependenciesForLaunch(game, gameInstallFolder, dryRun);
        }

        private bool InstallOnePendingDependency(
            PendingGameDependency item,
            Action<string, int> progress = null,
            int progressStart = 0,
            int progressEnd = 100,
            Func<bool> isCancelled = null)
        {
            string scriptPath = item.ScriptPath;
            string installDir = item.InstallDir;
            string label = string.IsNullOrWhiteSpace(item.DisplayName) ? "dependency" : item.DisplayName.Trim();

            // Download uses the first half of this item's progress slice; install uses the second half.
            int downloadEnd = progressStart + Math.Max(1, (progressEnd - progressStart) / 2);
            if (downloadEnd > progressEnd)
                downloadEnd = progressEnd;

            if (item.DownloadDepotId.HasValue)
            {
                string relative = item.DownloadRelativeScriptPath
                    ?? SteamCommonRedistributableConstants.InstallScriptFileName;
                string downloadRoot = string.IsNullOrWhiteSpace(installDir)
                    ? _commonRedistDownload.GetSgeSharedRoot()
                    : installDir;

                progress?.Invoke("Downloading " + label + "…", progressStart);

                ValidationResult download = _commonRedistDownload.EnsureDepotFiles(
                    item.DownloadDepotId.Value,
                    relative,
                    downloadRoot,
                    dryRun: false,
                    progress: (message, pct) =>
                    {
                        int mapped = MapProgress(progressStart, downloadEnd, pct);
                        progress?.Invoke(
                            string.IsNullOrWhiteSpace(message) ? "Downloading " + label + "…" : message,
                            mapped);
                    },
                    isCancelled: isCancelled);
                if (!download.IsValid)
                {
                    _logger?.LogWarning(
                        $"Install scripts: download failed for depot {item.DownloadDepotId}: {download.ErrorMessage}");
                    return false;
                }

                installDir = downloadRoot;
                scriptPath = Path.Combine(downloadRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            }

            if (string.IsNullOrEmpty(scriptPath) || !File.Exists(scriptPath))
            {
                _logger?.LogWarning("Install scripts: script missing for '" + item.DisplayName + "'.");
                return false;
            }

            if (isCancelled != null && isCancelled())
                return false;

            progress?.Invoke("Installing " + label + "…", item.DownloadDepotId.HasValue ? downloadEnd : progressStart);

            var job = new InstallScriptJob
            {
                ScriptPath = scriptPath,
                InstallDir = installDir,
                AppIdForDefaultHasRun = item.AppIdForDefaultHasRun,
                SourceLabel = item.DisplayName ?? "dependency",
                DepotId = item.DepotId ?? item.DownloadDepotId
            };

            // Named entry was already classified pending (HasRun + presence) — always run it.
            if (!string.IsNullOrWhiteSpace(item.ProcessEntryName))
                return RunNamedProcessEntry(job, item.ProcessEntryName, forceRun: true);

            return RunInstallScriptFile(job, dryRun: false);
        }

        private static int MapProgress(int rangeStart, int rangeEnd, int fraction0To100)
        {
            int clamped = Math.Max(0, Math.Min(100, fraction0To100));
            if (rangeEnd <= rangeStart)
                return rangeStart;
            return rangeStart + ((rangeEnd - rangeStart) * clamped) / 100;
        }

        private void AppendPendingCommonRedistributables(
            GameConfig game,
            string gameInstallFolder,
            List<PendingGameDependency> pending)
        {
            HashSet<uint> depotIds = CollectSharedRedistributableDepotIds(game, gameInstallFolder);
            if (depotIds.Count == 0)
                return;

            string steamSharedRoot = ResolveSteamworksSharedDirectory();
            string sgeSharedRoot = _commonRedistDownload.GetSgeSharedRoot();

            Dictionary<uint, string> pathByDepot = LoadCommonRedistInstallScriptPathMap(
                steamSharedRoot ?? sgeSharedRoot);
            var seenScripts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (uint depotId in depotIds.OrderBy(id => id))
            {
                string relative;
                if (!pathByDepot.TryGetValue(depotId, out relative)
                    && !SteamCommonRedistributableConstants.DepotInstallScriptRelativePaths.TryGetValue(depotId, out relative))
                {
                    _logger?.LogWarning(
                        $"Install scripts: no installscript path mapping for common redistributable depot {depotId}.");
                    continue;
                }

                string normalizedRelative = relative.Replace('/', Path.DirectorySeparatorChar);
                string depotDisplayName = SteamCommonRedistributableConstants.GetDepotDisplayName(depotId);
                string scriptPath = null;
                string installDir = null;

                if (!string.IsNullOrEmpty(steamSharedRoot))
                {
                    string steamScript = Path.Combine(steamSharedRoot, normalizedRelative);
                    if (File.Exists(steamScript))
                    {
                        scriptPath = steamScript;
                        installDir = steamSharedRoot;
                    }
                }

                if (scriptPath == null && !string.IsNullOrEmpty(sgeSharedRoot))
                {
                    string sgeScript = Path.Combine(sgeSharedRoot, normalizedRelative);
                    if (File.Exists(sgeScript))
                    {
                        scriptPath = sgeScript;
                        installDir = sgeSharedRoot;
                    }
                }

                if (scriptPath != null)
                {
                    if (!seenScripts.Add(scriptPath))
                        continue;
                    AppendPendingFromScriptFile(
                        pending,
                        scriptPath,
                        installDir,
                        SteamCommonRedistributableConstants.AppId,
                        preferredDisplayName: depotDisplayName,
                        depotId: depotId,
                        downloadDepotId: null,
                        downloadRelative: null);
                    continue;
                }

                // No local script: skip download when a probe family is already on the machine.
                if (SteamRedistributablePresence.IsConfidentlyPresent(depotId, null, normalizedRelative))
                {
                    _logger?.LogDebug(
                        $"Install scripts: skipping download for depot {depotId} ({depotDisplayName}) — already present.");
                    continue;
                }

                // Script not local — one pending unit that downloads then installs.
                string downloadRoot = string.IsNullOrEmpty(sgeSharedRoot)
                    ? _commonRedistDownload.GetSgeSharedRoot()
                    : sgeSharedRoot;
                string expectedScript = Path.Combine(downloadRoot, normalizedRelative);
                if (!seenScripts.Add(expectedScript))
                    continue;

                pending.Add(new PendingGameDependency
                {
                    DisplayName = depotDisplayName,
                    ScriptPath = expectedScript,
                    InstallDir = downloadRoot,
                    AppIdForDefaultHasRun = SteamCommonRedistributableConstants.AppId,
                    ProcessEntryName = null,
                    DepotId = depotId,
                    DownloadDepotId = depotId,
                    DownloadRelativeScriptPath = normalizedRelative
                });
            }
        }

        private void AppendPendingFromScriptFile(
            List<PendingGameDependency> pending,
            string scriptPath,
            string installDir,
            ulong appIdForDefaultHasRun,
            string preferredDisplayName,
            uint? depotId,
            uint? downloadDepotId,
            string downloadRelative)
        {
            if (string.IsNullOrEmpty(scriptPath) || !File.Exists(scriptPath))
                return;

            KeyValue runProcess = TryGetRunProcessSection(scriptPath);
            if (runProcess?.Children == null)
                return;

            var job = new InstallScriptJob
            {
                ScriptPath = scriptPath,
                InstallDir = installDir,
                AppIdForDefaultHasRun = appIdForDefaultHasRun,
                SourceLabel = Path.GetFileName(scriptPath)
            };

            foreach (KeyValue entry in runProcess.Children)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Name))
                    continue;
                if (!IsProcessEntryPending(entry, job, depotId))
                    continue;

                string entryName = entry.Name.Trim();
                string displayName = !string.IsNullOrWhiteSpace(preferredDisplayName)
                    ? FormatCommonRedistDisplayName(preferredDisplayName, entryName)
                    : ResolveGameScriptDisplayName(entry, entryName);

                pending.Add(new PendingGameDependency
                {
                    DisplayName = displayName,
                    ScriptPath = scriptPath,
                    InstallDir = installDir,
                    AppIdForDefaultHasRun = appIdForDefaultHasRun,
                    ProcessEntryName = entryName,
                    DepotId = depotId ?? downloadDepotId,
                    DownloadDepotId = downloadDepotId,
                    DownloadRelativeScriptPath = downloadRelative
                });
            }
        }

        private bool RunNamedProcessEntry(InstallScriptJob job, string processEntryName, bool forceRun = false)
        {
            KeyValue runProcess = TryGetRunProcessSection(job.ScriptPath);
            if (runProcess?.Children == null)
                return false;

            foreach (KeyValue entry in runProcess.Children)
            {
                if (entry == null || !NameEqualsIgnoreCase(entry.Name, processEntryName))
                    continue;
                return RunProcessEntry(entry, job, dryRun: false, forceRun: forceRun);
            }

            _logger?.LogWarning(
                $"Install scripts: Run Process entry '{processEntryName}' not found in {job.ScriptPath}");
            return false;
        }

        private KeyValue TryGetRunProcessSection(string scriptPath)
        {
            KeyValue root = TryParseVdfFile(scriptPath);
            if (root == null)
                return null;

            KeyValue scriptRoot = root;
            if (!NameEqualsIgnoreCase(root.Name, "InstallScript")
                && !NameEqualsIgnoreCase(root.Name, "installscript"))
            {
                KeyValue nested = FindChildIgnoreCase(root, "InstallScript")
                    ?? FindChildIgnoreCase(root, "installscript");
                if (nested != null)
                    scriptRoot = nested;
            }

            return FindChildIgnoreCase(scriptRoot, RunProcessSectionName);
        }

        private bool IsProcessEntryPending(KeyValue entry, InstallScriptJob job, uint? depotId)
        {
            string processName = entry.Name.Trim();
            if (ShouldSkipForOsRequirement(entry))
                return false;

            string hasRunKey = GetChildValueIgnoreCase(entry, "HasRunKey")
                ?? GetChildValueIgnoreCase(entry, "hasrunkey");
            if (string.IsNullOrWhiteSpace(hasRunKey) && job.AppIdForDefaultHasRun != 0)
            {
                hasRunKey = string.Format(
                    SteamCommonRedistributableConstants.DefaultHasRunKeyFormat,
                    job.AppIdForDefaultHasRun);
            }

            string runKeyName = GetChildValueIgnoreCase(entry, "RunKeyName")
                ?? GetChildValueIgnoreCase(entry, "runkeyname")
                ?? processName;

            int minimumHasRun = 1;
            string minRaw = GetChildValueIgnoreCase(entry, "MinimumHasRunValue")
                ?? GetChildValueIgnoreCase(entry, "minimumhasrunvalue");
            if (!string.IsNullOrWhiteSpace(minRaw))
                int.TryParse(minRaw.Trim(), out minimumHasRun);

            bool ignoreHasRun = IsTruthy(
                GetChildValueIgnoreCase(entry, "IgnoreHasRunKey")
                ?? GetChildValueIgnoreCase(entry, "ignorehasrunkey"));

            string processPath = GetChildValueIgnoreCase(entry, "process 1")
                ?? GetChildValueIgnoreCase(entry, "Process 1")
                ?? GetChildValueIgnoreCase(entry, "Process");
            string expandedProcessPath = ExpandInstallDir(processPath, job.InstallDir);

            // Runtime on the machine wins over a missing HasRun (first SGE launch / non-Steam install).
            if (SteamRedistributablePresence.IsConfidentlyPresent(
                    depotId, hasRunKey, expandedProcessPath, processName))
                return false;

            // Known missing wins over a stale HasRun marker (e.g. OpenAL uninstalled).
            if (!SteamRedistributablePresence.IsPresentOrUnknown(
                    depotId, hasRunKey, expandedProcessPath, processName))
                return true;

            // No reliable probe (PhysX/XNA/…): HasRun gates whether we still need to run.
            bool hasRunOk = !ignoreHasRun
                && !string.IsNullOrWhiteSpace(hasRunKey)
                && HasRunSatisfied(hasRunKey, runKeyName, minimumHasRun, job.AppIdForDefaultHasRun, processName);
            return !hasRunOk;
        }

        // Game-local scripts only: description if present, otherwise the Run Process key.
        private static string ResolveGameScriptDisplayName(KeyValue entry, string processName)
        {
            string description = GetChildValueIgnoreCase(entry, "description");
            if (!string.IsNullOrWhiteSpace(description))
                return description.Trim();
            return string.IsNullOrWhiteSpace(processName) ? "Game redistributable" : processName;
        }

        // VC installscripts use sibling "x86" / "x64" Run Process keys under one depot display name.
        private static string FormatCommonRedistDisplayName(string depotDisplayName, string processEntryName)
        {
            if (string.IsNullOrWhiteSpace(depotDisplayName))
                return processEntryName;
            if (string.IsNullOrWhiteSpace(processEntryName))
                return depotDisplayName;
            if (IsArchProcessEntryName(processEntryName))
                return depotDisplayName.Trim() + " (" + processEntryName.Trim() + ")";
            return depotDisplayName;
        }

        private static bool IsArchProcessEntryName(string processEntryName)
        {
            return NameEqualsIgnoreCase(processEntryName, "x86")
                || NameEqualsIgnoreCase(processEntryName, "x64")
                || NameEqualsIgnoreCase(processEntryName, "win32")
                || NameEqualsIgnoreCase(processEntryName, "win64");
        }

        private HashSet<uint> CollectSharedRedistributableDepotIds(GameConfig game, string gameInstallFolder)
        {
            var ids = new HashSet<uint>();

            TryAddSharedDepotsFromAppInfo(game, ids);
            TryAddSharedDepotsFromAppManifest(game, gameInstallFolder, ids);

            return ids;
        }

        private void TryAddSharedDepotsFromAppInfo(GameConfig game, HashSet<uint> ids)
        {
            if (game == null || game.AppId == 0)
                return;

            try
            {
                AppInfoKeyValue root = game.AppInfo;
                if (root == null)
                {
                    _steamProductInfoService.TryLoadAppInfoFromValveDataFile(
                        game.AppId.ToString(),
                        out root);
                }

                if (root == null)
                    return;

                AppInfoKeyValue target = AppInfoKeyValueHelper.ResolveAppInfoTarget(root) ?? root;
                AppInfoKeyValue depots = AppInfoKeyValueHelper.FindChild(target, SteamPicsKeyNames.Depots);
                if (depots?.Children == null)
                    return;

                foreach (AppInfoKeyValue depot in depots.Children)
                {
                    if (depot == null || !uint.TryParse(depot.Name, out uint depotId) || depotId == 0)
                        continue;

                    AppInfoKeyValue fromApp = AppInfoKeyValueHelper.FindChild(depot, SteamPicsKeyNames.DepotFromApp);
                    if (fromApp == null || string.IsNullOrWhiteSpace(fromApp.Value))
                        continue;
                    if (!uint.TryParse(fromApp.Value.Trim(), out uint fromAppId))
                        continue;
                    if (fromAppId != SteamCommonRedistributableConstants.AppId)
                        continue;

                    ids.Add(depotId);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug($"Install scripts: appinfo shared-depot parse skipped: {ex.Message}");
            }
        }

        private void TryAddSharedDepotsFromAppManifest(GameConfig game, string gameInstallFolder, HashSet<uint> ids)
        {
            if (game == null || game.AppId == 0)
                return;

            try
            {
                string acfPath = FindAppManifestAcfPath(game.AppId, gameInstallFolder);
                if (string.IsNullOrEmpty(acfPath) || !File.Exists(acfPath))
                    return;

                KeyValue root = TryParseVdfFile(acfPath);
                if (root == null)
                    return;

                KeyValue appState = FindChildIgnoreCase(root, SteamAppManifestAcfKeys.AppState) ?? root;
                KeyValue shared = FindChildIgnoreCase(appState, SteamAppManifestAcfKeys.SharedDepots);
                if (shared?.Children == null)
                    return;

                foreach (KeyValue entry in shared.Children)
                {
                    if (entry == null || !uint.TryParse(entry.Name, out uint depotId) || depotId == 0)
                        continue;
                    if (!string.IsNullOrWhiteSpace(entry.Value)
                        && uint.TryParse(entry.Value.Trim(), out uint ownerApp)
                        && ownerApp != SteamCommonRedistributableConstants.AppId)
                        continue;
                    ids.Add(depotId);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug($"Install scripts: appmanifest SharedDepots parse skipped: {ex.Message}");
            }
        }

        private Dictionary<uint, string> LoadCommonRedistInstallScriptPathMap(string sharedRoot)
        {
            var map = new Dictionary<uint, string>();
            foreach (KeyValuePair<uint, string> pair in SteamCommonRedistributableConstants.DepotInstallScriptRelativePaths)
                map[pair.Key] = pair.Value;

            try
            {
                string acfPath = FindAppManifestAcfPath(
                    SteamCommonRedistributableConstants.AppId,
                    Path.Combine(sharedRoot, "..", ".."));
                if (string.IsNullOrEmpty(acfPath))
                {
                    foreach (string libRoot in EnumerateSteamLibraryRoots())
                    {
                        string candidate = Path.Combine(
                            libRoot,
                            PathConstants.SteamAppsDirectoryName,
                            PathConstants.SteamAppManifestFilePrefix
                                + SteamCommonRedistributableConstants.AppId
                                + PathConstants.SteamAppManifestFileExtension);
                        if (File.Exists(candidate))
                        {
                            acfPath = candidate;
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(acfPath) || !File.Exists(acfPath))
                    return map;

                KeyValue root = TryParseVdfFile(acfPath);
                KeyValue appState = FindChildIgnoreCase(root, SteamAppManifestAcfKeys.AppState) ?? root;
                KeyValue scripts = FindChildIgnoreCase(appState, SteamAppManifestAcfKeys.InstallScripts);
                if (scripts?.Children == null)
                    return map;

                foreach (KeyValue entry in scripts.Children)
                {
                    if (entry == null || !uint.TryParse(entry.Name, out uint depotId) || depotId == 0)
                        continue;
                    if (string.IsNullOrWhiteSpace(entry.Value))
                        continue;
                    map[depotId] = entry.Value.Trim();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug($"Install scripts: common-redist InstallScripts map merge skipped: {ex.Message}");
            }

            return map;
        }

        // true = every applicable entry succeeded or was already satisfied / OS-skipped.
        private bool RunInstallScriptFile(InstallScriptJob job, bool dryRun)
        {
            if (job == null || string.IsNullOrEmpty(job.ScriptPath) || !File.Exists(job.ScriptPath))
                return false;

            KeyValue root = TryParseVdfFile(job.ScriptPath);
            if (root == null)
            {
                _logger?.LogWarning($"Install scripts: could not parse {job.ScriptPath}");
                return false;
            }

            KeyValue scriptRoot = root;
            if (!NameEqualsIgnoreCase(root.Name, "InstallScript")
                && !NameEqualsIgnoreCase(root.Name, "installscript"))
            {
                KeyValue nested = FindChildIgnoreCase(root, "InstallScript")
                    ?? FindChildIgnoreCase(root, "installscript");
                if (nested != null)
                    scriptRoot = nested;
            }

            KeyValue runProcess = FindChildIgnoreCase(scriptRoot, RunProcessSectionName);
            if (runProcess?.Children == null || runProcess.Children.Count == 0)
            {
                _logger?.LogDebug($"Install scripts: no Run Process entries in {job.ScriptPath}");
                return true;
            }

            _logger?.LogDebug(
                $"Install scripts: {(dryRun ? "dry-run " : "")}{job.SourceLabel} script {job.ScriptPath} (INSTALLDIR={job.InstallDir})");

            bool allOk = true;
            foreach (KeyValue entry in runProcess.Children)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Name))
                    continue;
                if (!RunProcessEntry(entry, job, dryRun))
                    allOk = false;
            }
            return allOk;
        }

        // true = installed, already satisfied, dry-run logged, or OS-skipped; false = failed to run/install.
        private bool RunProcessEntry(KeyValue entry, InstallScriptJob job, bool dryRun, bool forceRun = false)
        {
            string processName = entry.Name.Trim();
            if (ShouldSkipForOsRequirement(entry))
            {
                _logger?.LogDebug($"Install scripts: skipped '{processName}' (OS requirement).");
                return true;
            }

            string hasRunKey = GetChildValueIgnoreCase(entry, "HasRunKey")
                ?? GetChildValueIgnoreCase(entry, "hasrunkey");
            if (string.IsNullOrWhiteSpace(hasRunKey) && job.AppIdForDefaultHasRun != 0)
            {
                hasRunKey = string.Format(
                    SteamCommonRedistributableConstants.DefaultHasRunKeyFormat,
                    job.AppIdForDefaultHasRun);
            }

            string runKeyName = GetChildValueIgnoreCase(entry, "RunKeyName")
                ?? GetChildValueIgnoreCase(entry, "runkeyname")
                ?? processName;

            int minimumHasRun = 1;
            string minRaw = GetChildValueIgnoreCase(entry, "MinimumHasRunValue")
                ?? GetChildValueIgnoreCase(entry, "minimumhasrunvalue");
            if (!string.IsNullOrWhiteSpace(minRaw))
                int.TryParse(minRaw.Trim(), out minimumHasRun);

            string ignoreHasRunRaw = GetChildValueIgnoreCase(entry, "IgnoreHasRunKey")
                ?? GetChildValueIgnoreCase(entry, "ignorehasrunkey");
            bool ignoreHasRun = IsTruthy(ignoreHasRunRaw);

            string processPath = GetChildValueIgnoreCase(entry, "process 1")
                ?? GetChildValueIgnoreCase(entry, "Process 1")
                ?? GetChildValueIgnoreCase(entry, "Process");
            string command = GetChildValueIgnoreCase(entry, "command 1")
                ?? GetChildValueIgnoreCase(entry, "Command 1")
                ?? GetChildValueIgnoreCase(entry, "Command")
                ?? string.Empty;

            processPath = ExpandInstallDir(processPath, job.InstallDir);

            if (!forceRun)
            {
                // Match discovery: confidently present → skip; known missing → install; else HasRun.
                if (SteamRedistributablePresence.IsConfidentlyPresent(
                        job.DepotId, hasRunKey, processPath, processName))
                {
                    _logger?.LogDebug($"Install scripts: already installed '{processName}'.");
                    return true;
                }

                bool knownMissing = !SteamRedistributablePresence.IsPresentOrUnknown(
                    job.DepotId, hasRunKey, processPath, processName);
                if (!knownMissing)
                {
                    bool hasRunOk = !ignoreHasRun
                        && !string.IsNullOrWhiteSpace(hasRunKey)
                        && HasRunSatisfied(hasRunKey, runKeyName, minimumHasRun, job.AppIdForDefaultHasRun, processName);
                    if (hasRunOk)
                    {
                        _logger?.LogDebug($"Install scripts: already installed '{processName}'.");
                        return true;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(processPath) || !File.Exists(processPath))
            {
                _logger?.LogWarning(
                    $"Install scripts: installer missing for '{processName}': {processPath ?? "(null)"}");
                return false;
            }

            string description = GetChildValueIgnoreCase(entry, "description") ?? processName;
            bool ignoreExitCode = IsTruthy(
                GetChildValueIgnoreCase(entry, "IgnoreExitCode")
                ?? GetChildValueIgnoreCase(entry, "ignoreexitcode"));
            HashSet<int> validExitCodes = ParseValidExitCodes(
                GetChildValueIgnoreCase(entry, "ValidExitCodes")
                ?? GetChildValueIgnoreCase(entry, "validexitcodes"));

            if (dryRun)
            {
                _logger?.LogMessage(
                    $"Install scripts: dry-run would install '{description}' -> {processPath} {command}".TrimEnd());
                return true;
            }

            _logger?.LogMessage($"Install scripts: installing '{description}'…");

            int exitCode;
            if (!TryRunInstaller(processPath, command ?? string.Empty, out exitCode, out string runError))
            {
                _logger?.LogWarning(
                    $"Install scripts: failed to start '{description}': {runError}");
                return false;
            }

            bool success = exitCode == 0
                || ignoreExitCode
                || (validExitCodes != null && validExitCodes.Contains(exitCode));

            if (!success)
            {
                _logger?.LogWarning(
                    $"Install scripts: '{description}' exited {exitCode} (will retry on next launch).");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(hasRunKey))
                TryMarkHasRun(hasRunKey, runKeyName, job.AppIdForDefaultHasRun, processName);

            _logger?.LogDebug($"Install scripts: completed '{description}' (exit {exitCode}).");
            return true;
        }

        private static bool ShouldSkipForOsRequirement(KeyValue entry)
        {
            KeyValue requirement = FindChildIgnoreCase(entry, "Requirement_OS")
                ?? FindChildIgnoreCase(entry, "requirement_os");
            if (requirement == null)
                return false;

            string need64 = GetChildValueIgnoreCase(requirement, "is64bitwindows")
                ?? GetChildValueIgnoreCase(requirement, "Is64BitWindows");
            if (IsTruthy(need64) && !Environment.Is64BitOperatingSystem)
                return true;

            return false;
        }

        private bool HasRunSatisfied(
            string hasRunKey,
            string valueName,
            int minimumHasRun,
            ulong appId,
            string processName)
        {
            if (TryReadHasRunDword(hasRunKey, valueName, out int value) && value >= minimumHasRun)
                return true;

            // WOW6432Node twin for HKLM\Software\...
            string wowKey = ToWow6432NodeKey(hasRunKey);
            if (!string.IsNullOrEmpty(wowKey)
                && TryReadHasRunDword(wowKey, valueName, out value)
                && value >= minimumHasRun)
                return true;

            if (TryReadSgeMirrorHasRun(appId, processName, valueName, out value) && value >= minimumHasRun)
                return true;

            return false;
        }

        private void TryMarkHasRun(string hasRunKey, string valueName, ulong appId, string processName)
        {
            if (!TryWriteHasRunDword(hasRunKey, valueName, 1))
            {
                string wowKey = ToWow6432NodeKey(hasRunKey);
                if (!string.IsNullOrEmpty(wowKey))
                    TryWriteHasRunDword(wowKey, valueName, 1);
            }

            TryWriteSgeMirrorHasRun(appId, processName, valueName, 1);
        }

        private static bool TryReadHasRunDword(string hasRunKey, string valueName, out int value)
        {
            value = 0;
            RegistryHive hive;
            string subKey;
            if (!TrySplitRegistryKey(hasRunKey, out hive, out subKey))
                return false;

            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry32))
                using (RegistryKey key = baseKey.OpenSubKey(subKey, false))
                {
                    if (TryReadDword(key, valueName, out value))
                        return true;
                }

                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64))
                using (RegistryKey key = baseKey.OpenSubKey(subKey, false))
                {
                    if (TryReadDword(key, valueName, out value))
                        return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static bool TryWriteHasRunDword(string hasRunKey, string valueName, int value)
        {
            RegistryHive hive;
            string subKey;
            if (!TrySplitRegistryKey(hasRunKey, out hive, out subKey))
                return false;

            try
            {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Default))
                using (RegistryKey key = baseKey.CreateSubKey(subKey, true))
                {
                    if (key == null)
                        return false;
                    key.SetValue(valueName, value, RegistryValueKind.DWord);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadSgeMirrorHasRun(ulong appId, string processName, string valueName, out int value)
        {
            value = 0;
            try
            {
                string subKey = BuildSgeMirrorSubKey(appId, processName);
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(subKey, false))
                {
                    if (TryReadDword(key, valueName, out value))
                        return true;
                    if (!string.Equals(valueName, processName, StringComparison.OrdinalIgnoreCase)
                        && TryReadDword(key, processName, out value))
                        return true;
                }
            }
            catch
            {
            }

            return false;
        }

        private static void TryWriteSgeMirrorHasRun(ulong appId, string processName, string valueName, int value)
        {
            try
            {
                string subKey = BuildSgeMirrorSubKey(appId, processName);
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(subKey, true))
                {
                    if (key == null)
                        return;
                    key.SetValue(valueName, value, RegistryValueKind.DWord);
                }
            }
            catch
            {
            }
        }

        private static string BuildSgeMirrorSubKey(ulong appId, string processName)
        {
            string safeName = SanitizeRegistryName(processName);
            if (appId != 0)
                return Path.Combine(SteamCommonRedistributableConstants.SgeHasRunRegistryRoot, appId.ToString(), safeName);
            return Path.Combine(SteamCommonRedistributableConstants.SgeHasRunRegistryRoot, "0", safeName);
        }

        private static string SanitizeRegistryName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "process";
            var sb = new StringBuilder(name.Length);
            foreach (char c in name.Trim())
            {
                if (c == '\\' || c == '/' || c < 32)
                    sb.Append('_');
                else
                    sb.Append(c);
            }
            return sb.Length == 0 ? "process" : sb.ToString();
        }

        private static bool TryReadDword(RegistryKey key, string valueName, out int value)
        {
            value = 0;
            if (key == null || string.IsNullOrEmpty(valueName))
                return false;
            object raw = key.GetValue(valueName);
            if (raw is int)
            {
                value = (int)raw;
                return true;
            }
            if (raw != null && int.TryParse(raw.ToString(), out value))
                return true;
            return false;
        }

        private static bool TrySplitRegistryKey(string hasRunKey, out RegistryHive hive, out string subKey)
        {
            hive = RegistryHive.LocalMachine;
            subKey = null;
            if (string.IsNullOrWhiteSpace(hasRunKey))
                return false;

            string key = hasRunKey.Trim().Replace('/', '\\');
            const string hklm = "HKEY_LOCAL_MACHINE\\";
            const string hkcu = "HKEY_CURRENT_USER\\";
            if (key.StartsWith(hklm, StringComparison.OrdinalIgnoreCase))
            {
                hive = RegistryHive.LocalMachine;
                subKey = key.Substring(hklm.Length);
                return !string.IsNullOrWhiteSpace(subKey);
            }
            if (key.StartsWith(hkcu, StringComparison.OrdinalIgnoreCase))
            {
                hive = RegistryHive.CurrentUser;
                subKey = key.Substring(hkcu.Length);
                return !string.IsNullOrWhiteSpace(subKey);
            }

            // Bare Software\... path — Steam often omits the hive in docs; treat as HKLM.
            hive = RegistryHive.LocalMachine;
            subKey = key.TrimStart('\\');
            return !string.IsNullOrWhiteSpace(subKey);
        }

        private static string ToWow6432NodeKey(string hasRunKey)
        {
            if (string.IsNullOrWhiteSpace(hasRunKey))
                return null;
            string key = hasRunKey.Replace('/', '\\');
            const string marker = @"\Software\";
            int idx = key.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
                return null;
            if (key.IndexOf(@"\WOW6432Node\", StringComparison.OrdinalIgnoreCase) >= 0)
                return null;
            return key.Substring(0, idx) + @"\Software\WOW6432Node\" + key.Substring(idx + marker.Length);
        }

        private bool TryRunInstaller(string filePath, string arguments, out int exitCode, out string error)
        {
            exitCode = -1;
            error = null;

            string workDir = Path.GetDirectoryName(filePath) ?? Environment.CurrentDirectory;
            string args = arguments ?? string.Empty;
            string extension = Path.GetExtension(filePath) ?? string.Empty;
            bool isBatch = extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".bat", StringComparison.OrdinalIgnoreCase);

            // Batch wrappers (VC/.NET CommonRedist) already embed /q; run via cmd with no window.
            // EXEs get CreateNoWindow + Steam's silent command 1 (/quiet, /silent, /s, …).
            string fileName;
            string processArgs;
            if (isBatch)
            {
                fileName = Environment.GetEnvironmentVariable("ComSpec");
                if (string.IsNullOrWhiteSpace(fileName))
                    fileName = Path.Combine(Environment.SystemDirectory, "cmd.exe");
                processArgs = "/d /c \"" + Path.GetFileName(filePath) + "\"";
                if (!string.IsNullOrWhiteSpace(args))
                    processArgs += " " + args;
            }
            else
            {
                fileName = filePath;
                processArgs = args;
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = processArgs,
                    WorkingDirectory = workDir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using (Process process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        error = "Process.Start returned null.";
                        return false;
                    }

                    if (!process.WaitForExit(DefaultProcessTimeoutMs))
                    {
                        try { process.Kill(); } catch { }
                        error = "Installer timed out.";
                        return false;
                    }

                    exitCode = process.ExitCode;
                    return true;
                }
            }
            catch (Exception ex)
            {
                // Elevation / ShellExecute fallback (Steam does CreateProcess then ShellExecuteEx).
                // Keep WindowStyle.Hidden so quiet installers do not flash a console after UAC.
                try
                {
                    var shellInfo = new ProcessStartInfo
                    {
                        FileName = isBatch ? fileName : filePath,
                        Arguments = isBatch ? processArgs : args,
                        WorkingDirectory = workDir,
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    using (Process process = Process.Start(shellInfo))
                    {
                        if (process == null)
                        {
                            error = ex.Message;
                            return false;
                        }
                        if (!process.WaitForExit(DefaultProcessTimeoutMs))
                        {
                            try { process.Kill(); } catch { }
                            error = "Installer timed out.";
                            return false;
                        }
                        exitCode = process.ExitCode;
                        return true;
                    }
                }
                catch (Exception shellEx)
                {
                    error = shellEx.Message;
                    return false;
                }
            }
        }

        private static string ExpandInstallDir(string path, string installDir)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;
            string expanded = path.Trim();
            if (!string.IsNullOrEmpty(installDir))
            {
                expanded = ReplaceIgnoreCase(expanded, InstallDirToken, installDir.TrimEnd('\\', '/'));
            }
            expanded = Environment.ExpandEnvironmentVariables(expanded);
            try
            {
                return Path.GetFullPath(expanded);
            }
            catch
            {
                return expanded;
            }
        }

        private static string ReplaceIgnoreCase(string input, string oldValue, string newValue)
        {
            if (string.IsNullOrEmpty(input) || string.IsNullOrEmpty(oldValue))
                return input;
            int index = 0;
            var sb = new StringBuilder(input.Length);
            while (index < input.Length)
            {
                int found = input.IndexOf(oldValue, index, StringComparison.OrdinalIgnoreCase);
                if (found < 0)
                {
                    sb.Append(input, index, input.Length - index);
                    break;
                }
                sb.Append(input, index, found - index);
                sb.Append(newValue ?? string.Empty);
                index = found + oldValue.Length;
            }
            return sb.ToString();
        }

        private static IEnumerable<string> EnumerateGameInstallScriptFiles(string installFolder)
        {
            if (string.IsNullOrEmpty(installFolder) || !Directory.Exists(installFolder))
                yield break;

            string[] files;
            try
            {
                files = Directory.GetFiles(installFolder, "*.vdf", SearchOption.TopDirectoryOnly);
            }
            catch
            {
                yield break;
            }

            foreach (string file in files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                string name = Path.GetFileName(file);
                if (string.IsNullOrEmpty(name))
                    continue;
                if (IsGameInstallScriptFileName(name))
                    yield return file;
            }
        }

        private static bool IsGameInstallScriptFileName(string fileName)
        {
            if (string.Equals(fileName, SteamCommonRedistributableConstants.InstallScriptFileName, StringComparison.OrdinalIgnoreCase))
                return true;
            if (string.Equals(fileName, SteamCommonRedistributableConstants.RunAsAdminScriptFileName, StringComparison.OrdinalIgnoreCase))
                return true;
            if (fileName.StartsWith("installscript", StringComparison.OrdinalIgnoreCase)
                && fileName.EndsWith(".vdf", StringComparison.OrdinalIgnoreCase))
                return true;
            if (fileName.EndsWith("_install.vdf", StringComparison.OrdinalIgnoreCase))
                return true;
            return false;
        }

        private string ResolveSteamworksSharedDirectory()
        {
            foreach (string libRoot in EnumerateSteamLibraryRoots())
            {
                string candidate = Path.Combine(
                    libRoot,
                    PathConstants.SteamAppsDirectoryName,
                    PathConstants.SteamAppsCommonDirectoryName,
                    PathConstants.SteamworksSharedFolderName);
                if (Directory.Exists(Path.Combine(candidate, PathConstants.SteamCommonRedistFolderName))
                    || Directory.Exists(candidate))
                    return Path.GetFullPath(candidate);
            }

            return null;
        }

        private string FindAppManifestAcfPath(ulong appId, string startFolder)
        {
            string fileName = PathConstants.SteamAppManifestFilePrefix
                + appId
                + PathConstants.SteamAppManifestFileExtension;

            string normalizedStart = NormalizeExistingDirectory(startFolder);
            if (!string.IsNullOrEmpty(normalizedStart))
            {
                try
                {
                    for (DirectoryInfo dir = new DirectoryInfo(normalizedStart); dir != null; dir = dir.Parent)
                    {
                        string inSteamApps = Path.Combine(dir.FullName, PathConstants.SteamAppsDirectoryName, fileName);
                        if (File.Exists(inSteamApps))
                            return inSteamApps;
                        string beside = Path.Combine(dir.FullName, fileName);
                        if (File.Exists(beside))
                            return beside;
                    }
                }
                catch
                {
                }
            }

            foreach (string libRoot in EnumerateSteamLibraryRoots())
            {
                string candidate = Path.Combine(libRoot, PathConstants.SteamAppsDirectoryName, fileName);
                if (File.Exists(candidate))
                    return candidate;
            }

            return null;
        }

        private IEnumerable<string> EnumerateSteamLibraryRoots()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string root in new[]
            {
                SteamInstallationPathHelper.ResolveSteamRootFromCurrentUserIfPresent(),
                SteamInstallationPathHelper.GetLocalMachineSteamInstallPath(),
                PathConstants.GetProgramFilesX86DefaultSteamInstallationRoot()
            })
            {
                string normalized = NormalizeExistingDirectory(root);
                if (normalized != null && seen.Add(normalized))
                    yield return normalized;
            }

            foreach (string steamRoot in seen.ToList())
            {
                string vdfPath = Path.Combine(
                    steamRoot,
                    PathConstants.SteamAppsDirectoryName,
                    PathConstants.SteamLibraryFoldersVdfFileName);
                if (!File.Exists(vdfPath))
                    continue;

                KeyValue root = TryParseVdfFile(vdfPath);
                if (root == null)
                    continue;

                foreach (string path in EnumerateLibraryFolderPaths(root))
                {
                    string normalized = NormalizeExistingDirectory(path);
                    if (normalized != null && seen.Add(normalized))
                        yield return normalized;
                }
            }
        }

        private static IEnumerable<string> EnumerateLibraryFolderPaths(KeyValue root)
        {
            if (root == null)
                yield break;

            KeyValue folders = NameEqualsIgnoreCase(root.Name, "libraryfolders")
                ? root
                : FindChildIgnoreCase(root, "libraryfolders") ?? root;

            if (folders?.Children == null)
                yield break;

            foreach (KeyValue child in folders.Children)
            {
                if (child == null)
                    continue;

                string path = GetChildValueIgnoreCase(child, PathConstants.SteamLibraryFoldersVdfPathKey);
                if (!string.IsNullOrWhiteSpace(path))
                {
                    yield return path;
                    continue;
                }

                // Legacy: "1" "D:\\SteamLibrary"
                if (!string.IsNullOrWhiteSpace(child.Value) && LooksLikePath(child.Value))
                    yield return child.Value;
            }
        }

        private static bool LooksLikePath(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;
            string v = value.Trim();
            return v.IndexOf(':') >= 0 || v.StartsWith(@"\\", StringComparison.Ordinal);
        }

        private static KeyValue TryParseVdfFile(string path)
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                return KeyValue.ParseVdf(bytes);
            }
            catch
            {
                return null;
            }
        }

        private static KeyValue FindChildIgnoreCase(KeyValue parent, string name)
        {
            if (parent?.Children == null || string.IsNullOrEmpty(name))
                return null;
            foreach (KeyValue child in parent.Children)
            {
                if (child != null && NameEqualsIgnoreCase(child.Name, name))
                    return child;
            }
            return null;
        }

        private static string GetChildValueIgnoreCase(KeyValue parent, string name)
        {
            KeyValue child = FindChildIgnoreCase(parent, name);
            return child?.Value;
        }

        private static bool NameEqualsIgnoreCase(string a, string b)
        {
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTruthy(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;
            string v = value.Trim();
            return v == "1"
                || v.Equals("true", StringComparison.OrdinalIgnoreCase)
                || v.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }

        private static HashSet<int> ParseValidExitCodes(string raw)
        {
            var set = new HashSet<int>();
            if (string.IsNullOrWhiteSpace(raw))
                return set;
            foreach (string part in raw.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(part.Trim(), out int code))
                    set.Add(code);
            }
            return set;
        }

        private static string NormalizeExistingDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;
            try
            {
                string full = Path.GetFullPath(path.Trim().TrimEnd('\\', '/'));
                return Directory.Exists(full) ? full : null;
            }
            catch
            {
                return null;
            }
        }

        private sealed class InstallScriptJob
        {
            public string ScriptPath;
            public string InstallDir;
            public ulong AppIdForDefaultHasRun;
            public string SourceLabel;
            public uint? DepotId;
        }
    }
}
