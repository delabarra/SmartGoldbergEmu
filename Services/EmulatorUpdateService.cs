using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Extensions;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Forms;

namespace SmartGoldbergEmu.Services
{
    public static class EmulatorUpdateService
    {
        private const string SevenZipDownloadUrl = ApplicationConstants.SevenZipStandaloneExeDownloadUrl;
        private const string PreExistentVersion = "pre-existent";

        private const int HttpTimeoutSeconds = 10;
        private const int StartupUpdateCheckTimeoutSeconds = 15;
        private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan SevenZipDownloadTimeout = TimeSpan.FromMinutes(2);

        private static string _downloadUrl;
        private static string _latestVersion;
        private static bool _resolvedFromRepack;
        private static GoldbergForkSource _resolvedFork;
        private static string _localDownloadArchiveFileName;
        private static bool _wasCancelledForMissingFiles = false;
        private static bool _forkSelectionCancelledForDownload = false;
        private static string _lastCancelledUpdateVersion = null;
        private static string _lastStartupUpdateCheckError = null;

        private static GoldbergForkSource GetConfiguredGoldbergForkSource()
        {
            try
            {
                return ServiceLocator.AppDataService.GetGoldbergForkSource();
            }
            catch
            {
                return GoldbergForkSource.Detanup;
            }
        }

        private static IList<UpdateManualDownloadLink> GetGoldbergManualDownloadLinks()
        {
            return new[]
            {
                new UpdateManualDownloadLink
                {
                    Label = "Repack",
                    Url = GoldbergForkConstants.RepackRepositoryWebUrl
                },
                new UpdateManualDownloadLink
                {
                    Label = GoldbergForkConstants.GetForkDisplayName(GoldbergForkSource.Detanup),
                    Url = GoldbergForkConstants.RepositoryWebUrlDetanup
                },
                new UpdateManualDownloadLink
                {
                    Label = GoldbergForkConstants.GetForkDisplayName(GoldbergForkSource.Alex),
                    Url = GoldbergForkConstants.RepositoryWebUrlAlex
                }
            };
        }

        private static string GetGoldbergManualDownloadForkUrlsText()
        {
            var links = GetGoldbergManualDownloadLinks();
            var lines = new List<string>(links.Count);
            foreach (var link in links)
                lines.Add(link.Label + ": " + link.Url);
            return string.Join("\n", lines);
        }

        private static void ApplyResolvedRelease(GoldbergResolvedRelease resolved, UpdateCheckResult result)
        {
            _downloadUrl = resolved.DownloadUrl;
            _latestVersion = resolved.LatestVersion;
            _resolvedFromRepack = resolved.FromRepack;
            _localDownloadArchiveFileName = GoldbergForkConstants.GetLocalDownloadArchiveFileName(
                resolved.FromRepack,
                resolved.ArchiveFileName);

            if (result != null)
            {
                result.DownloadUrl = resolved.DownloadUrl;
                result.LatestVersion = resolved.LatestVersion;
                result.ReleaseNotes = resolved.ReleaseNotes;
                result.FromRepack = resolved.FromRepack;
            }
        }

        private static bool RequiresWindowsDefenderExclusion()
        {
            return !_resolvedFromRepack;
        }

        private static async Task<bool> TryAddWindowsDefenderExclusionAsync(
            string archivePath,
            Action<string, int> progressCallback)
        {
            progressCallback?.Invoke("Requesting Windows Defender exclusion...", 12);
            bool added = await Task.Run(() => AddDefenderExclusionSync(archivePath)).ConfigureAwait(false);
            if (!added)
                throw new UpdateException("Windows Defender exclusion was denied. Installation aborted.");
            return true;
        }

        // Fork (upstream) downloads can trip Defender; warn before requesting the exclusion so the
        // user understands why a UAC prompt appears. Returns false if the user cancels.
        private static bool ShowDefenderExclusionWarningOnUi(SynchronizationContext uiContext)
        {
            if (uiContext == null)
                return true;

            DialogResult choice = DialogResult.OK;
            uiContext.Send(_ =>
            {
                choice = FormMessageBoxHelper.ShowDialogIfAlive(
                    null,
                    "Note:\n" +
                    "ColdLoaderLauncher executables may be flagged by Windows Defender and can trigger false positives.\n\n" +
                    "A temporary Windows Defender exclusion will be added during the download and installation process. " +
                    "The downloaded files and the exclusion will be automatically removed once the installation is complete.\n\n" +
                    "Exclusion target:\n" +
                    "- " + GoldbergForkConstants.UpstreamWinReleaseAssetName + "\n\n" +
                    "Do you want to continue?",
                    ApplicationConstants.WindowTitle,
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Warning);
            }, null);

            return choice == DialogResult.OK;
        }

        private static string GetGoldbergDownloadArchiveFileName()
        {
            return _localDownloadArchiveFileName ?? GoldbergForkConstants.UpstreamDownloadArchiveFileName;
        }

        private static string BuildGoldbergReleaseReadyMessage()
        {
            string forkName = GoldbergForkConstants.GetForkDisplayName(_resolvedFork);
            if (_resolvedFromRepack)
                return "Release ready — Goldberg repack (" + forkName + ")";
            return "Release ready — " + forkName + " upstream release";
        }

        private static string BuildGoldbergDownloadProgressMessage(bool upstreamFallback = false)
        {
            string forkName = GoldbergForkConstants.GetForkDisplayName(_resolvedFork);
            if (upstreamFallback)
                return "Downloading " + forkName + " fork (upstream fallback)...";
            if (_resolvedFromRepack)
                return "Downloading Goldberg repack (" + forkName + ")...";
            return "Downloading " + forkName + " fork (upstream)...";
        }

        private static bool IsGitHubRateLimitResponse(HttpResponseMessage response, string errorContent)
        {
            return response.StatusCode == HttpStatusCode.Forbidden
                && !string.IsNullOrEmpty(errorContent)
                && errorContent.IndexOf("rate limit", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static async Task<string> FetchReleaseJsonAsync(IHttpService httpService, string apiUrl)
        {
            using (HttpResponseMessage response = await httpService.GetAsync(apiUrl).ConfigureAwait(false))
            {
                string errorContent = null;
                if (response.StatusCode == HttpStatusCode.Forbidden)
                    errorContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (IsGitHubRateLimitResponse(response, errorContent))
                    throw new InvalidOperationException("GitHub API rate limit exceeded. Try again later or use authenticated requests.");

                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
        }

        private static async Task<bool> TryResolveUpstreamReleaseAsync(
            IHttpService httpService,
            GoldbergForkSource fork,
            UpdateCheckResult result)
        {
            string upstreamJson = await FetchReleaseJsonAsync(
                httpService,
                GoldbergForkConstants.GetUpstreamReleasesApiUrl(fork)).ConfigureAwait(false);

            var resolved = new GoldbergResolvedRelease();
            if (!GoldbergReleaseResolveHelper.TryParseUpstreamRelease(upstreamJson, resolved))
                return false;

            ApplyResolvedRelease(resolved, result);
            ServiceLocator.LogService?.LogMessage(
                $"Using upstream Goldberg fork release as download source ({GoldbergForkConstants.GetForkDisplayName(fork)}).");
            return true;
        }

        // Resolution order: delabarra repack (configured fork asset), then that fork's upstream only.
        private static async Task<bool> TryResolveDownloadReleaseAsync(
            IHttpService httpService,
            GoldbergForkSource configuredFork,
            UpdateCheckResult result)
        {
            var errors = new List<string>();
            string forkName = GoldbergForkConstants.GetForkDisplayName(configuredFork);
            _resolvedFork = configuredFork;

            try
            {
                string repackJson = await FetchReleaseJsonAsync(httpService, GoldbergForkConstants.RepackReleasesApiUrl).ConfigureAwait(false);
                var repackResolved = new GoldbergResolvedRelease();
                if (!string.IsNullOrEmpty(repackJson)
                    && GoldbergReleaseResolveHelper.TryParseRepackRelease(repackJson, configuredFork, repackResolved))
                {
                    ApplyResolvedRelease(repackResolved, result);
                    ServiceLocator.LogService?.LogMessage("Using Goldberg repack release as download source.");
                    return true;
                }
                errors.Add("Repack: no asset for " + forkName);
            }
            catch (Exception ex)
            {
                if (IsRateLimitMessage(ex.Message))
                {
                    result.ErrorMessage = ex.Message;
                    return false;
                }
                errors.Add("Repack: " + ex.Message);
                ServiceLocator.LogService?.LogWarning($"Goldberg repack release check failed: {ex.Message}");
            }

            try
            {
                if (await TryResolveUpstreamReleaseAsync(httpService, configuredFork, result).ConfigureAwait(false))
                    return true;
                errors.Add(forkName + " upstream: no Windows release asset");
            }
            catch (Exception ex)
            {
                if (IsRateLimitMessage(ex.Message))
                {
                    result.ErrorMessage = ex.Message;
                    return false;
                }
                errors.Add(forkName + " upstream: " + ex.Message);
                ServiceLocator.LogService?.LogWarning($"Goldberg upstream ({forkName}) release check failed: {ex.Message}");
            }

            result.ErrorMessage = "Could not find a Goldberg download for " + forkName + ".\n" + string.Join("\n", errors);
            return false;
        }

        private static bool IsRateLimitMessage(string message)
        {
            return !string.IsNullOrEmpty(message)
                && message.IndexOf("rate limit", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetUpdateFailureDetail(Exception ex)
        {
            if (ex == null)
                return "unknown error";

            if (ex is UpdateException && ex.InnerException != null && !string.IsNullOrEmpty(ex.InnerException.Message))
                return ex.InnerException.Message;

            return ex.GetBaseException().Message;
        }

        private static bool IsUserCancelledUpdate(Exception ex)
        {
            string detail = GetUpdateFailureDetail(ex);
            return detail.IndexOf("cancelled", StringComparison.OrdinalIgnoreCase) >= 0
                || (ex != null && ex.Message.IndexOf("cancelled", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static void LogGoldbergUpdateFailure(ILogService logger, Exception ex, string scenario)
        {
            if (logger == null || ex == null)
                return;

            string detail = GetUpdateFailureDetail(ex);
            if (IsUserCancelledUpdate(ex))
            {
                logger.LogMessage("Goldberg " + scenario + " cancelled: " + detail);
                return;
            }

            string logLine = "Goldberg " + scenario + " failed: " + detail;
            if (IsRateLimitMessage(detail))
            {
                logger.LogWarning(logLine + " Retry later or download the emulator manually from GitHub.");
                return;
            }

            logger.LogError(logLine, ex);
        }

        // UI thread: may show ForkSelectForm until goldberg_fork is saved; false if user cancelled.
        private static bool EnsureGoldbergForkConfiguredInteractive(ILogService logger)
        {
            if (ServiceLocator.AppDataService.IsGoldbergForkConfigured())
                return true;

            using (var f = new ForkSelectForm(forceExplicitChoice: true))
            {
                if (f.ShowDialog() != DialogResult.OK)
                {
                    _forkSelectionCancelledForDownload = true;
                    logger?.LogMessage("Emulator download source not selected; download cancelled.");
                    return false;
                }
            }

            return ServiceLocator.AppDataService.IsGoldbergForkConfigured();
        }

        public static string GetAndClearLastStartupUpdateCheckError()
        {
            var err = _lastStartupUpdateCheckError;
            _lastStartupUpdateCheckError = null;
            return err;
        }

        private static bool AreGoldbergBinariesMissing() =>
            GoldbergInstallLayout.AreReleaseInstallFilesMissing(PathConstants.GoldbergDirectory);

        public static Task<bool> GoldbergFilesCheck() => Task.Run(() => AreGoldbergBinariesMissing());

        public static bool GoldbergFilesCheckSync() => AreGoldbergBinariesMissing();

        private static List<string> CollectMissingAssetFileNames()
        {
            var missing = new List<string>();
            if (!File.Exists(PathConstants.GlobalAccountAvatarPath))
                missing.Add(PathConstants.GlobalAccountAvatarFileName);
            if (!File.Exists(Path.Combine(PathConstants.GlobalFontsPath, PathConstants.GoldbergGlobalDefaultOverlayFontFileName)))
                missing.Add(PathConstants.GoldbergGlobalDefaultOverlayFontFileName);
            if (!File.Exists(Path.Combine(PathConstants.GlobalSoundsPath, PathConstants.SteamClientUiFriendNotificationWav)))
                missing.Add(PathConstants.SteamClientUiFriendNotificationWav);
            if (!File.Exists(Path.Combine(PathConstants.GlobalSoundsPath, PathConstants.SteamClientUiAchievementNotificationWav)))
                missing.Add(PathConstants.SteamClientUiAchievementNotificationWav);
            return missing;
        }

        public static Task<List<string>> AssetsFilesCheck() => Task.Run(() => CollectMissingAssetFileNames());

        public static List<string> AssetsFilesCheckSync() => CollectMissingAssetFileNames();

        public static async Task<UpdateCheckResult> CheckForUpdatesAsync(bool isStartup = false)
        {
            var result = new UpdateCheckResult
            {
                Success = false,
                UpdateAvailable = false,
                TimedOut = false
            };

            try
            {
                _downloadUrl = null;
                _resolvedFromRepack = false;
                _localDownloadArchiveFileName = null;
                GoldbergForkSource configuredFork = GetConfiguredGoldbergForkSource();
                _resolvedFork = configuredFork;

                using (var httpService = HttpServiceFactory.Create(TimeSpan.FromSeconds(HttpTimeoutSeconds)))
                {
                    if (!await TryResolveDownloadReleaseAsync(httpService, configuredFork, result).ConfigureAwait(false))
                        return result;

                    // Compare only against the configured fork's latest release (resolved above).
                    string currentVersion = GetCurrentVersion();
                    result.CurrentVersion = currentVersion;

                    if (string.IsNullOrEmpty(currentVersion) || currentVersion == PreExistentVersion)
                    {
                        result.UpdateAvailable = true;
                    }
                    else if (!string.IsNullOrEmpty(_latestVersion)
                        && GoldbergVersionHelper.IsNewerGoldbergVersion(currentVersion, _latestVersion))
                    {
                        result.UpdateAvailable = true;
                    }

                    result.Success = true;
                }
            }
            catch (TaskCanceledException)
            {
                result.TimedOut = true;
                result.ErrorMessage = "Request timed out";
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Update check failed: {ex.Message}";
            }

            return result;
        }

        public static async Task DownloadAndInstallAsync(Action<string, int> progressCallback = null, Func<bool> cancellationCheck = null, Action onCopyPhaseStart = null)
        {
            var uiMarshalingContext = SynchronizationContext.Current;
            string tempFolder = Path.Combine(PathConstants.AppBaseDirectory, PathConstants.LauncherUpdateTempFolderName);
            string archivePath = null;
            string sevenZipPath;
            string tempGoldbergFolder = Path.Combine(tempFolder, PathConstants.GoldbergDirectoryFolderName);
            string tempUserAssetsFolder = Path.Combine(tempFolder, PathConstants.LauncherUpdateUserAssetsUnpackFolderName);
            bool exclusionAdded = false;

            try
            {
                _downloadUrl = null;

                // Check for cancellation
                if (cancellationCheck?.Invoke() == true)
                {
                    throw new UpdateException("Download cancelled by user");
                }

                // Ensure we have the download URL
                if (string.IsNullOrEmpty(_downloadUrl))
                {
                    progressCallback?.Invoke("Fetching latest release information...", 5);
                    var checkResult = await CheckForUpdatesAsync(isStartup: false).ConfigureAwait(false);
                    if (!checkResult.Success || string.IsNullOrEmpty(_downloadUrl))
                    {
                        throw new UpdateException(string.IsNullOrEmpty(checkResult.ErrorMessage)
                            ? "Could not get download URL"
                            : checkResult.ErrorMessage);
                    }

                    progressCallback?.Invoke(BuildGoldbergReleaseReadyMessage(), 8);
                }

                archivePath = Path.Combine(tempFolder, GetGoldbergDownloadArchiveFileName());

                progressCallback?.Invoke("Preparing download...", 10);
                await Task.Run(() =>
                {
                    Directory.CreateDirectory(tempFolder);
                    Directory.CreateDirectory(tempGoldbergFolder);
                    Directory.CreateDirectory(tempUserAssetsFolder);
                }).ConfigureAwait(false);

                if (RequiresWindowsDefenderExclusion())
                {
                    if (!ShowDefenderExclusionWarningOnUi(uiMarshalingContext))
                        throw new UpdateException("Download cancelled by user");
                    exclusionAdded = await TryAddWindowsDefenderExclusionAsync(archivePath, progressCallback).ConfigureAwait(false);
                }

                // Check for cancellation before download
                if (cancellationCheck?.Invoke() == true)
                {
                    throw new UpdateException("Download cancelled by user");
                }

                // Download archive (10 min timeout, cancellable)
                try
                {
                    var forkSource = GetConfiguredGoldbergForkSource();
                    progressCallback?.Invoke(BuildGoldbergDownloadProgressMessage(), 15);
                    bool exclusionAddedDuringDownload = await DownloadGoldbergArchiveWithFallbackAsync(
                        forkSource,
                        archivePath,
                        progressCallback,
                        cancellationCheck,
                        exclusionAdded,
                        uiMarshalingContext).ConfigureAwait(false);
                    exclusionAdded = exclusionAdded || exclusionAddedDuringDownload;
                    archivePath = Path.Combine(tempFolder, GetGoldbergDownloadArchiveFileName());
                    progressCallback?.Invoke("Download completed", 40);

                    if (cancellationCheck?.Invoke() == true)
                        throw new UpdateException("Download cancelled by user");

                    if (TryResolveInstalledSevenZipExecutable(out sevenZipPath))
                    {
                        progressCallback?.Invoke("Using installed 7-Zip", 49);
                    }
                    else
                    {
                        sevenZipPath = Path.Combine(tempFolder, PathConstants.LauncherSevenZipReducedExecutableName);
                        if (!File.Exists(sevenZipPath))
                        {
                            progressCallback?.Invoke("Downloading 7-Zip extractor...", 41);
                            await DownloadFileAsync(SevenZipDownloadUrl, sevenZipPath, (progress) =>
                            {
                                int percentage = 41 + (int)(progress * 8);
                                progressCallback?.Invoke("Downloading 7-Zip extractor...", percentage);
                            }, cancellationCheck, SevenZipDownloadTimeout).ConfigureAwait(false);
                            progressCallback?.Invoke("7-Zip extractor downloaded", 49);
                        }
                        else
                        {
                            progressCallback?.Invoke("7-Zip extractor already present", 49);
                        }
                    }
                }
                catch (UpdateException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new UpdateException($"Download failed: {ex.Message}", ex);
                }

                if (cancellationCheck?.Invoke() == true)
                    throw new UpdateException("Download cancelled by user");

                // Extract (55-87) â€” heavy 7-Zip work off the UI thread
                try
                {
                    await Task.Run(() =>
                    {
                        progressCallback?.Invoke("Extracting Goldberg emulator files...", 55);
                        ExtractGoldbergReleaseLayoutSync(sevenZipPath, archivePath, tempGoldbergFolder, cancellationCheck);
                        progressCallback?.Invoke("Emulator files extracted", 77);

                        progressCallback?.Invoke("Extracting user assets...", 80);
                        ExtractUserAssetsToTempSync(sevenZipPath, archivePath, tempUserAssetsFolder, cancellationCheck);
                        progressCallback?.Invoke("User assets extracted", 87);
                    }).ConfigureAwait(false);
                }
                catch (UpdateException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new UpdateException($"Extraction failed: {ex.Message}", ex);
                }

                if (cancellationCheck?.Invoke() == true)
                    throw new UpdateException("Download cancelled by user");

                if (onCopyPhaseStart != null)
                {
                    if (uiMarshalingContext != null)
                        uiMarshalingContext.Send(_ => onCopyPhaseStart(), null);
                    else
                        onCopyPhaseStart();
                }

                // Copy files to final destinations (reports 88, 89, 91, 95)
                try
                {
                    await Task.Run(() => CopyFilesFromTempSync(tempGoldbergFolder, tempUserAssetsFolder, cancellationCheck, progressCallback)).ConfigureAwait(false);
                }
                catch (UpdateException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    throw new UpdateException($"File installation failed: {ex.Message}", ex);
                }

                await Task.Run(() =>
                {
                    if (!string.IsNullOrEmpty(_latestVersion))
                    {
                        SaveCurrentVersion(_latestVersion);
                    }

                    progressCallback?.Invoke("Cleaning up...", 96);
                    DeleteTempFolder(tempFolder);
                    DeleteGoldbergGenerateInterfacesInstallFolder();
                    if (exclusionAdded && !string.IsNullOrEmpty(archivePath))
                    {
                        progressCallback?.Invoke("Removing Windows Defender exclusion...", 98);
                        RemoveDefenderExclusionSync(archivePath);
                    }
                }).ConfigureAwait(false);

                progressCallback?.Invoke("Installation complete!", 100);
            }
            catch (Exception ex)
            {
                await Task.Run(() =>
                {
                    DeleteTempFolder(tempFolder);
                    DeleteGoldbergGenerateInterfacesInstallFolder();
                    if (exclusionAdded && !string.IsNullOrEmpty(archivePath))
                    {
                        RemoveDefenderExclusionSync(archivePath);
                    }
                }).ConfigureAwait(false);

                if (ex is UpdateException)
                    throw;
                else
                    throw new UpdateException($"Installation failed: {ex.Message}", ex);
            }
        }

        private static void TryDeletePartialDownloadQuiet(string destinationPath)
        {
            try
            {
                if (File.Exists(destinationPath))
                    File.Delete(destinationPath);
            }
            catch (Exception delEx)
            {
                Program.LogService?.LogWarning($"Failed to delete partial download: {delEx.Message}");
            }
        }

        private static async Task<bool> DownloadGoldbergArchiveWithFallbackAsync(
            GoldbergForkSource forkSource,
            string archivePath,
            Action<string, int> progressCallback,
            Func<bool> cancellationCheck,
            bool defenderExclusionAlreadyAdded,
            SynchronizationContext uiContext)
        {
            Exception primaryException = null;
            string downloadMessage = BuildGoldbergDownloadProgressMessage();

            if (!string.IsNullOrEmpty(_downloadUrl))
            {
                try
                {
                    await DownloadFileAsync(_downloadUrl, archivePath, (progress) =>
                    {
                        int percentage = 15 + (int)(progress * 25);
                        progressCallback?.Invoke(downloadMessage, percentage);
                    }, cancellationCheck).ConfigureAwait(false);
                    return false;
                }
                catch (UpdateException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    primaryException = ex;
                    TryDeletePartialDownloadQuiet(archivePath);
                    ServiceLocator.LogService?.LogWarning(
                        $"Goldberg download failed ({(_resolvedFromRepack ? "repack" : "upstream")}): {ex.Message}");
                }
            }

            if (!_resolvedFromRepack)
            {
                if (primaryException != null)
                    throw new UpdateException($"Download failed: {primaryException.Message}", primaryException);
                throw new UpdateException("Could not download Goldberg emulator archive.");
            }

            string forkName = GoldbergForkConstants.GetForkDisplayName(forkSource);
            ServiceLocator.LogService?.LogMessage("Repack download failed; trying " + forkName + " upstream release.");
            progressCallback?.Invoke("Repack download failed — trying " + forkName + " upstream...", 18);

            using (var httpService = HttpServiceFactory.Create(TimeSpan.FromSeconds(HttpTimeoutSeconds)))
            {
                _resolvedFork = forkSource;
                var resolveResult = new UpdateCheckResult();
                if (!await TryResolveUpstreamReleaseAsync(httpService, forkSource, resolveResult).ConfigureAwait(false)
                    || string.IsNullOrEmpty(_downloadUrl))
                {
                    throw new UpdateException(
                        "Could not download Goldberg emulator archive for " + forkName + " from repack or upstream.",
                        primaryException);
                }
            }

            downloadMessage = BuildGoldbergDownloadProgressMessage(upstreamFallback: true);

            string upstreamArchivePath = Path.Combine(
                Path.GetDirectoryName(archivePath) ?? string.Empty,
                GoldbergForkConstants.UpstreamDownloadArchiveFileName);
            if (!string.Equals(archivePath, upstreamArchivePath, StringComparison.OrdinalIgnoreCase))
            {
                TryDeletePartialDownloadQuiet(archivePath);
                archivePath = upstreamArchivePath;
            }

            bool exclusionAddedDuringFallback = false;
            if (!defenderExclusionAlreadyAdded)
            {
                if (!ShowDefenderExclusionWarningOnUi(uiContext))
                    throw new UpdateException("Download cancelled by user");
                progressCallback?.Invoke("Requesting Windows Defender exclusion...", 20);
                exclusionAddedDuringFallback = await Task.Run(() => AddDefenderExclusionSync(archivePath)).ConfigureAwait(false);
                if (!exclusionAddedDuringFallback)
                    throw new UpdateException("Windows Defender exclusion was denied. Installation aborted.");
            }

            await DownloadFileAsync(_downloadUrl, archivePath, (progress) =>
            {
                int percentage = 20 + (int)(progress * 20);
                progressCallback?.Invoke(downloadMessage, percentage);
            }, cancellationCheck).ConfigureAwait(false);

            return exclusionAddedDuringFallback;
        }

        private static async Task DownloadFileAsync(string url, string destinationPath, Action<double> progressCallback = null, Func<bool> cancellationCheck = null, TimeSpan? timeout = null)
        {
            var cts = new CancellationTokenSource();
            var effectiveTimeout = timeout ?? DownloadTimeout;
            var lastReportedProgress = 0.0;
            var progressLock = new object();
            Action<double> wrappedProgress = (p) =>
            {
                if (cancellationCheck?.Invoke() == true)
                    cts.Cancel();
                lock (progressLock)
                {
                    if (progressCallback != null && (p - lastReportedProgress >= 0.01 || p >= 1.0))
                    {
                        lastReportedProgress = p;
                        progressCallback(p);
                    }
                }
            };

            try
            {
                using (var httpService = HttpServiceFactory.Create(effectiveTimeout))
                {
                    await httpService.DownloadFileAsync(url, destinationPath, wrappedProgress, cts.Token).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                var baseEx = ex is AggregateException ae ? (ae.InnerException ?? ae) : ex;
                if (baseEx is OperationCanceledException || baseEx is TaskCanceledException)
                {
                    TryDeletePartialDownloadQuiet(destinationPath);
                    throw new UpdateException("Download cancelled by user");
                }
                if (baseEx is TimeoutException || (baseEx?.Message ?? "").IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    TryDeletePartialDownloadQuiet(destinationPath);
                    throw new UpdateException($"Download timed out after {(int)effectiveTimeout.TotalMinutes} minutes. Check your connection and try again.");
                }
                throw;
            }
        }

        private static bool TryResolveInstalledSevenZipExecutable(out string executablePath)
        {
            executablePath = null;
            string installDir = GetSevenZipInstallDirectoryFromRegistry();
            if (string.IsNullOrWhiteSpace(installDir))
                return false;

            installDir = installDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string[] candidates = { "7z.exe", "7za.exe", PathConstants.LauncherSevenZipReducedExecutableName };
            foreach (string name in candidates)
            {
                string full = Path.Combine(installDir, name);
                if (File.Exists(full))
                {
                    executablePath = full;
                    return true;
                }
            }

            return false;
        }

        private static string GetSevenZipInstallDirectoryFromRegistry()
        {
            foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                try
                {
                    using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                    using (RegistryKey key = baseKey.OpenSubKey(ApplicationConstants.SevenZipRegistrySubKey))
                    {
                        string path = ReadSevenZipPathValue(key);
                        if (!string.IsNullOrWhiteSpace(path))
                            return path;
                    }
                }
                catch
                {
                    // try next view
                }
            }

            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(ApplicationConstants.SevenZipRegistrySubKey))
                {
                    return ReadSevenZipPathValue(key);
                }
            }
            catch
            {
                return null;
            }
        }

        private static string ReadSevenZipPathValue(RegistryKey key)
        {
            if (key == null)
                return null;
            object pathValue = key.GetValue(ApplicationConstants.SevenZipRegistryInstallPathValueName);
            string s = pathValue as string;
            return string.IsNullOrWhiteSpace(s) ? null : s;
        }

        private const string ArchiveReleaseUserSettingsDir = "release/files/settings/";
        private static readonly string ArchiveSoundsDir =
            ArchiveReleaseUserSettingsDir + PathConstants.GoldbergGlobalSoundsFolderName + "/";

        private static bool TryCopySteamUiSoundsTo(string targetSoundsPath, string logFallbackHint)
        {
            if (SteamInstallationPathHelper.TryCopyOverlayNotificationSoundsFromSteam(targetSoundsPath))
                return true;

            ServiceLocator.LogService?.LogWarning(
                $"Steam overlay notification sounds were not copied ({logFallbackHint}); using bundled or extracted sounds if available.");
            return false;
        }

        private static ProcessStartInfo CreateDefenderMpPreferenceStartInfo(string path, bool add, ProcessWindowStyle? windowStyle = null)
        {
            string inner = add
                ? $"Add-MpPreference -ExclusionPath '{path}'"
                : $"Remove-MpPreference -ExclusionPath '{path}'";
            var psi = new ProcessStartInfo
            {
                FileName = "powershell",
                Arguments = $"-Command \"{inner}\"",
                UseShellExecute = true,
                Verb = "runas",
                CreateNoWindow = true
            };
            if (windowStyle.HasValue)
                psi.WindowStyle = windowStyle.Value;
            return psi;
        }

        public static string GetCurrentVersion()
        {
            try
            {
                var appDataService = ServiceLocator.AppDataService;
                return appDataService.GetGoldbergVersion();
            }
            catch
            {
                return null;
            }
        }

        public static void SaveCurrentVersion(string version)
        {
            try
            {
                var appDataService = ServiceLocator.AppDataService;
                appDataService.SetGoldbergVersion(version);
            }
            catch
            {
            }
        }

        private static void DeleteTempFolder(string tempFolder)
        {
            try
            {
                if (Directory.Exists(tempFolder))
                {
                    Directory.Delete(tempFolder, true);
                }
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService?.LogWarning($"Cleanup error (non-critical): {ex.Message}");
            }
        }

        private static bool AddDefenderExclusionSync(string path)
        {
            try
            {
                using (var process = Process.Start(CreateDefenderMpPreferenceStartInfo(path, add: true, ProcessWindowStyle.Hidden)))
                {
                    process.WaitForExit();
                    return process.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        private static void RemoveDefenderExclusionSync(string path)
        {
            try
            {
                using (var process = Process.Start(CreateDefenderMpPreferenceStartInfo(path, add: false, ProcessWindowStyle.Hidden)))
                    process.WaitForExit();
            }
            catch
            {
            }
        }

        private static void ExtractGoldbergReleaseLayoutSync(
            string sevenZipPath,
            string archivePath,
            string tempGoldbergRoot,
            Func<bool> cancellationCheck = null)
        {
            foreach (GoldbergInstallLayout.GoldbergInstallFile file in GoldbergInstallLayout.GetReleaseInstallFiles())
            {
                if (cancellationCheck?.Invoke() == true)
                    throw new UpdateException("Download cancelled by user");

                string relativeDir = file.InstallRelativeDirectory;
                string destinationFolder = string.IsNullOrEmpty(relativeDir)
                    ? tempGoldbergRoot
                    : Path.Combine(tempGoldbergRoot, relativeDir);
                Directory.CreateDirectory(destinationFolder);
                ExtractInstallFileSync(sevenZipPath, archivePath, file, destinationFolder);
            }
        }

        private static void ExtractInstallFileSync(
            string sevenZipPath,
            string archivePath,
            GoldbergInstallLayout.GoldbergInstallFile file,
            string destinationFolder)
        {
            string destinationPath = Path.Combine(destinationFolder, file.FileName);
            foreach (string archivePathCandidate in GoldbergInstallLayout.GetArchivePathCandidates(file))
            {
                ExtractSingleFileSync(sevenZipPath, archivePath, archivePathCandidate, destinationFolder);
                if (File.Exists(destinationPath))
                    return;
            }

            throw new UpdateException(
                "Required Goldberg file was not found in the archive: " + file.FileName);
        }

        private static void ExtractUserAssetsToTempSync(string sevenZipPath, string archivePath, string tempUserAssetsFolder, Func<bool> cancellationCheck = null)
        {
            // Check for cancellation
            if (cancellationCheck?.Invoke() == true)
            {
                throw new UpdateException("Download cancelled by user");
            }

            Directory.CreateDirectory(tempUserAssetsFolder);
            string tempSettingsFolder = Path.Combine(tempUserAssetsFolder, PathConstants.GoldbergGlobalSettingsFolderName);
            Directory.CreateDirectory(tempSettingsFolder);

            // Extract avatar to temp
            try
            {
                string avatarPath = ArchiveReleaseUserSettingsDir + PathConstants.GlobalAccountAvatarFileName;
                ExtractSingleFileSync(sevenZipPath, archivePath, avatarPath, tempSettingsFolder);
            }
            catch (Exception ex)
            {
                // Avatar is optional - log but don't fail
                ServiceLocator.LogService?.LogWarning($"Optional avatar extraction failed: {ex.Message}");
            }

            // Check for cancellation
            if (cancellationCheck?.Invoke() == true)
            {
                throw new UpdateException("Download cancelled by user");
            }

            // Extract fonts to temp
            try
            {
                string tempFontsPath = Path.Combine(tempSettingsFolder, PathConstants.GoldbergGlobalFontsFolderName);
                Directory.CreateDirectory(tempFontsPath);
                
                string fontFile = ArchiveReleaseUserSettingsDir + PathConstants.GoldbergGlobalFontsFolderName + "/" + PathConstants.GoldbergGlobalDefaultOverlayFontFileName;
                ExtractSingleFileSync(sevenZipPath, archivePath, fontFile, tempFontsPath);
            }
            catch (Exception ex)
            {
                // Fonts are optional - log but don't fail
                ServiceLocator.LogService?.LogWarning($"Optional fonts extraction failed: {ex.Message}");
            }

            // Check for cancellation
            if (cancellationCheck?.Invoke() == true)
            {
                throw new UpdateException("Download cancelled by user");
            }

            // Extract sounds to temp (we'll try Steam copy during the copy phase)
            try
            {
                string tempSoundsPath = Path.Combine(tempSettingsFolder, PathConstants.GoldbergGlobalSoundsFolderName);
                Directory.CreateDirectory(tempSoundsPath);

                ExtractSingleFileSync(sevenZipPath, archivePath, ArchiveSoundsDir + PathConstants.SteamClientUiFriendNotificationWav, tempSoundsPath);
                ExtractSingleFileSync(sevenZipPath, archivePath, ArchiveSoundsDir + PathConstants.SteamClientUiAchievementNotificationWav, tempSoundsPath);
            }
            catch (Exception ex)
            {
                // Sounds are optional - log but don't fail
                ServiceLocator.LogService?.LogWarning($"Optional sounds extraction failed: {ex.Message}");
            }
        }

        private static void DeleteGoldbergGenerateInterfacesInstallFolder()
        {
            string toolsDir = PathConstants.LauncherDevToolsDirectory;
            string generateInterfacesDir = PathConstants.GoldbergGenerateInterfacesInstallDirectory;
            try
            {
                if (Directory.Exists(generateInterfacesDir))
                    Directory.Delete(generateInterfacesDir, recursive: true);

                if (Directory.Exists(toolsDir) && Directory.GetFileSystemEntries(toolsDir).Length == 0)
                    Directory.Delete(toolsDir);
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService?.LogWarning($"Could not remove generate_interfaces install folder: {ex.Message}");
            }
        }

        private static void CopyFilesFromTempSync(string tempGoldbergFolder, string tempUserAssetsFolder, Func<bool> cancellationCheck, Action<string, int> progressCallback)
        {
            if (cancellationCheck?.Invoke() == true)
                throw new UpdateException("Download cancelled by user");

            progressCallback?.Invoke("Installing emulator DLLs...", 88);

            string targetGoldbergDirectory = PathConstants.GoldbergDirectory;
            Directory.CreateDirectory(targetGoldbergDirectory);
            CopyDirectoryContents(tempGoldbergFolder, targetGoldbergDirectory, true);
            GoldbergInstallLayout.RemoveLegacyFlatDllsFromGoldbergRoot(targetGoldbergDirectory);
            GoldbergInstallLayout.RemoveLegacyGoldbergSubfolders(targetGoldbergDirectory);
            GoldbergInstallLayout.WriteGoldbergReadmeFile(targetGoldbergDirectory);
            SteamInstallationPathHelper.TrySyncSteamDllToDirectory(PathConstants.GoldbergSteamOldDirectory, out _);
            progressCallback?.Invoke("Installing user assets...", 91);

            if (cancellationCheck?.Invoke() == true)
                throw new UpdateException("Download cancelled by user");

            // Copy user assets
            string targetSettingsPath = PathConstants.GlobalSettingsPath;
            string tempSettingsFolder = Path.Combine(tempUserAssetsFolder, PathConstants.GoldbergGlobalSettingsFolderName);
            
            if (Directory.Exists(tempSettingsFolder))
            {
                Directory.CreateDirectory(targetSettingsPath);

                // Copy avatar (only if doesn't exist)
                string avatarSource = Path.Combine(tempSettingsFolder, PathConstants.GlobalAccountAvatarFileName);
                if (File.Exists(avatarSource) && !File.Exists(PathConstants.GlobalAccountAvatarPath))
                {
                    File.Copy(avatarSource, PathConstants.GlobalAccountAvatarPath, false);
                }

                // Check for cancellation
                if (cancellationCheck?.Invoke() == true)
                {
                    throw new UpdateException("Download cancelled by user");
                }

                // Copy fonts
                string tempFontsPath = Path.Combine(tempSettingsFolder, PathConstants.GoldbergGlobalFontsFolderName);
                if (Directory.Exists(tempFontsPath))
                {
                    string targetFontsPath = PathConstants.GlobalFontsPath;
                    Directory.CreateDirectory(targetFontsPath);
                    CopyDirectoryContents(tempFontsPath, targetFontsPath, false); // false = only if doesn't exist
                }

                // Check for cancellation
                if (cancellationCheck?.Invoke() == true)
                {
                    throw new UpdateException("Download cancelled by user");
                }

                string targetSoundsPath = PathConstants.GlobalSoundsPath;
                Directory.CreateDirectory(targetSoundsPath);
                if (!TryCopySteamUiSoundsTo(targetSoundsPath, "using extracted"))
                {
                    string tempSoundsPath = Path.Combine(tempSettingsFolder, PathConstants.GoldbergGlobalSoundsFolderName);
                    if (Directory.Exists(tempSoundsPath))
                        CopyDirectoryContents(tempSoundsPath, targetSoundsPath, false);
                }
            }
            progressCallback?.Invoke("Files installed", 95);
        }

        private static void CopyDirectoryContents(string sourceDir, string targetDir, bool overwrite)
        {
            if (!Directory.Exists(sourceDir))
                return;

            Directory.CreateDirectory(targetDir);

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(file);
                string targetPath = Path.Combine(targetDir, fileName);

                if (overwrite || !File.Exists(targetPath))
                {
                    File.Copy(file, targetPath, overwrite);
                }
            }

            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string subDirName = Path.GetFileName(subDir);
                string targetSubDir = Path.Combine(targetDir, subDirName);
                CopyDirectoryContents(subDir, targetSubDir, overwrite);
            }
        }

        private static void ExtractSingleFileSync(string sevenZipPath, string archivePath, string fileInArchive, string destinationFolder)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = sevenZipPath,
                Arguments = $"e \"{archivePath}\" -o\"{destinationFolder}\" -y \"-ir!{fileInArchive}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using (var process = Process.Start(startInfo))
            {
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    string error = process.StandardError.ReadToEnd();
                    throw new UpdateException($"7-Zip extraction failed: {error}");
                }
            }
        }

        private enum GoldbergMissingInstallContext
        {
            StartupSessionCheck,
            GameLaunch
        }

        private static string BuildMissingGoldbergInstallPromptMessage()
        {
            return
                "Goldberg Emulator files are missing.\n\n" +
                "Do you want to proceed with the installation?";
        }

        private static bool ResolveMissingGoldbergDownloadOutcome(
            ILogService logger,
            GoldbergMissingInstallContext context,
            bool downloadSucceeded)
        {
            if (downloadSucceeded)
                return true;

            bool cancelledOrForkSkipped = _wasCancelledForMissingFiles || _forkSelectionCancelledForDownload;
            _forkSelectionCancelledForDownload = false;
            if (context != GoldbergMissingInstallContext.StartupSessionCheck)
                return false;

            if (cancelledOrForkSkipped)
                logger?.LogMessage("Download was cancelled or fork not chosen - allowing app to continue without files");
            else
                logger?.LogMessage("Goldberg download failed at startup - allowing app to continue without files");
            return true;
        }

        // Caller must have verified GoldbergFilesCheckSync (DLLs missing).
        private static async Task<bool> PromptAndInstallMissingGoldbergFilesAsync(ILogService logger, Control uiOwner, GoldbergMissingInstallContext context)
        {
            if (uiOwner == null)
                throw new ArgumentNullException(nameof(uiOwner));

            logger?.LogWarning("Goldberg emulator files are missing");

            var dialogResult = FormMessageBoxHelper.ShowDialogIfAlive(
                uiOwner,
                BuildMissingGoldbergInstallPromptMessage(),
                ApplicationConstants.WindowTitle,
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question);

            if (dialogResult == DialogResult.OK)
            {
                logger?.LogMessage("User chose to download Goldberg files");
                bool success = await DownloadAndInstallWithUIAsync(logger, uiOwner).ConfigureAwait(true);
                return ResolveMissingGoldbergDownloadOutcome(logger, context, success);
            }

            if (context == GoldbergMissingInstallContext.StartupSessionCheck)
            {
                logger?.LogMessage("User chose to skip Goldberg files download");
                _wasCancelledForMissingFiles = true;
                return true;
            }

            logger?.LogMessage("User declined Goldberg emulator download from game launch");
            return false;
        }

        // No Control: startup uses invokeOnUIThread (often action => action()). Caller verified GoldbergFilesCheckSync.
        private static bool PromptAndInstallMissingGoldbergFiles(ILogService logger, Action<Action> invokeOnUIThread, GoldbergMissingInstallContext context)
        {
            logger?.LogWarning("Goldberg emulator files are missing");

            DialogResult dialogResult = DialogResult.Cancel;
            if (invokeOnUIThread != null)
            {
                invokeOnUIThread(() =>
                {
                    dialogResult = FormMessageBoxHelper.ShowDialogIfAlive(
                        null,
                        BuildMissingGoldbergInstallPromptMessage(),
                        ApplicationConstants.WindowTitle,
                        MessageBoxButtons.OKCancel,
                        MessageBoxIcon.Question);
                });
            }

            if (dialogResult == DialogResult.OK)
            {
                logger?.LogMessage("User chose to download Goldberg files");
                bool success = false;
                try
                {
                    invokeOnUIThread?.Invoke(() => { success = RunDownloadAndInstallWithProgressForm(logger); });
                }
                catch (Exception ex)
                {
                    LogGoldbergUpdateFailure(logger, ex, "emulator download");
                    invokeOnUIThread?.Invoke(() =>
                    {
                        FormMessageBoxHelper.ShowIfAlive(
                            null,
                            $"Failed to start update: {ex.Message}",
                            "Update Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    });
                    success = false;
                }

                return ResolveMissingGoldbergDownloadOutcome(logger, context, success);
            }

            if (context == GoldbergMissingInstallContext.StartupSessionCheck)
            {
                logger?.LogMessage("User chose to skip Goldberg files download");
                _wasCancelledForMissingFiles = true;
                return true;
            }

            logger?.LogMessage("User declined Goldberg emulator download from game launch");
            return false;
        }

        public static async Task<bool> TryEnsureGoldbergBinariesForLaunchAsync(ILogService logger, Control uiRoot)
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            if (uiRoot == null)
                throw new ArgumentNullException(nameof(uiRoot));

            try
            {
                if (!GoldbergFilesCheckSync())
                    return true;
                if (!await PromptAndInstallMissingGoldbergFilesAsync(logger, uiRoot, GoldbergMissingInstallContext.GameLaunch).ConfigureAwait(true))
                    return false;
                if (GoldbergFilesCheckSync())
                {
                    logger?.LogError("Goldberg emulator files still missing after download attempt (game launch)");
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                logger?.LogError("Error ensuring Goldberg binaries for launch", ex);
                return false;
            }
        }

        public static bool CheckAndInstallMissingFilesWithUI(ILogService logger, Action<Action> invokeOnUIThread)
        {
            try
            {
                if (_wasCancelledForMissingFiles)
                {
                    logger?.LogMessage("User previously cancelled missing files download, skipping check but allowing app to continue");
                    return true;
                }

                if (GoldbergFilesCheckSync())
                    return PromptAndInstallMissingGoldbergFiles(logger, invokeOnUIThread, GoldbergMissingInstallContext.StartupSessionCheck);

                logger?.LogMessage("Goldberg emulator files are present");

                var appDataService = ServiceLocator.AppDataService;
                string currentVersion = appDataService.GetGoldbergVersion();
                if (string.IsNullOrEmpty(currentVersion))
                {
                    logger?.LogMessage("No version found in cfg, setting to pre-existent");
                    appDataService.SetGoldbergVersion("pre-existent");
                }
                else
                {
                    logger?.LogMessage($"Current emulator version: {currentVersion}");
                }

                return true;
            }
            catch (Exception ex)
            {
                logger?.LogError("Error checking for missing files", ex);
                return false;
            }
        }

        // owner null: no parent window (startup before main form). marshalToUi null: caller already on STA thread for dialogs.
        private static void PresentUpdateCheckResult(
            ILogService logger,
            UpdateCheckResult result,
            bool isStartup,
            Action<Action> marshalToUi,
            Action installUpdateOnUiThread)
        {
            void RunOnUi(Action action)
            {
                if (marshalToUi != null)
                    marshalToUi(action);
                else
                    action();
            }

            RunOnUi(() =>
            {
                PresentUpdateCheckResultCoreAsync(
                    logger,
                    result,
                    isStartup,
                    owner: null,
                    () =>
                    {
                        installUpdateOnUiThread();
                        return Task.CompletedTask;
                    }).GetAwaiter().GetResult();
            });
        }

        private static async Task PresentUpdateCheckResultCoreAsync(
            ILogService logger,
            UpdateCheckResult result,
            bool isStartup,
            IWin32Window owner,
            Func<Task> installWhenUserAcceptedOkAsync)
        {
            if (result == null)
                return;

            DialogResult ShowUpdateAvailableQuestion()
            {
                return UpdateChangelogForm.ShowDialogIfAlive(owner, BuildUpdateChangelogContent(result));
            }

            void ShowNoUpdatesInfo()
            {
                const string caption = "No Updates Available";
                string text =
                    $"You are running the latest version of Goldberg Emulator.\n\n" +
                    $"Current version: {result.CurrentVersion ?? "unknown"}\n" +
                    $"Latest version: {result.LatestVersion}";
                FormMessageBoxHelper.ShowIfAlive(owner, text, caption, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            void ShowCheckFailedWarning()
            {
                const string caption = "Update Check Failed";
                string text = $"Failed to check for updates: {result.ErrorMessage}";
                FormMessageBoxHelper.ShowIfAlive(owner, text, caption, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            if (result.Success)
            {
                if (result.UpdateAvailable)
                {
                    if (isStartup && _lastCancelledUpdateVersion == result.LatestVersion)
                    {
                        logger?.LogMessage($"Update {result.LatestVersion} available but user previously declined, skipping prompt");
                        return;
                    }

                    if (!isStartup)
                        _lastCancelledUpdateVersion = null;

                    logger?.LogMessage($"Update available: {result.LatestVersion} (current: {result.CurrentVersion ?? "unknown"})");

                    var dialogResult = ShowUpdateAvailableQuestion();
                    if (dialogResult == DialogResult.OK)
                    {
                        logger?.LogMessage("User chose to download and install update");
                        _lastCancelledUpdateVersion = null;
                        await installWhenUserAcceptedOkAsync().ConfigureAwait(true);
                    }
                    else
                    {
                        logger?.LogMessage("User chose to skip update");
                        _lastCancelledUpdateVersion = result.LatestVersion;
                    }
                }
                else
                {
                    logger?.LogMessage($"No updates available (current: {result.CurrentVersion ?? "unknown"}, latest: {result.LatestVersion})");

                    if (!isStartup)
                        ShowNoUpdatesInfo();
                }
            }
            else
            {
                string errorDetail = result.ErrorMessage ?? "unknown error";
                logger?.LogWarning("Goldberg update check failed: " + errorDetail);
                if (isStartup)
                    _lastStartupUpdateCheckError = errorDetail;
                else
                    ShowCheckFailedWarning();
            }
        }

        // Must run on the UI thread; pumps with DoEvents until async install completes (startup path only).
        private static bool RunDownloadAndInstallWithProgressForm(ILogService logger)
        {
            var task = RunDownloadAndInstallWithProgressFormAsync(logger);
            // Install work runs off the UI thread; TP uses Control.Invoke for progress/cleanup — pump so those complete.
            while (!task.IsCompleted)
            {
                Application.DoEvents();
                Thread.Sleep(15);
            }

            return task.GetAwaiter().GetResult();
        }

        private static Task WaitForProgressFormCloseAsync(ProgressForm progressForm)
        {
            if (progressForm == null || progressForm.IsDisposed)
                return Task.CompletedTask;

            var tcs = new TaskCompletionSource<bool>();
            FormClosedEventHandler handler = null;
            handler = (sender, e) =>
            {
                progressForm.FormClosed -= handler;
                tcs.TrySetResult(true);
            };
            progressForm.FormClosed += handler;
            return tcs.Task;
        }

        private static async Task<bool> RunDownloadAndInstallWithProgressFormAsync(ILogService logger)
        {
            _forkSelectionCancelledForDownload = false;
            if (!EnsureGoldbergForkConfiguredInteractive(logger))
                return false;

            using (var progressForm = new ProgressForm())
            {
                try
                {
                    progressForm.Text = "Updating Goldberg Emulator";
                    progressForm.Reset();
                    progressForm.Show();

                    _lastCancelledUpdateVersion = null;

                    await DownloadAndInstallAsync(
                        (message, progress) => { progressForm.UpdateProgress(message, progress); },
                        () => progressForm.IsCancelled,
                        () => progressForm.DisableCancel()).ConfigureAwait(true);

                    logger?.LogMessage("Goldberg emulator update completed successfully");
                    progressForm.ShowSuccessAndClose("Installation complete!");
                    await WaitForProgressFormCloseAsync(progressForm).ConfigureAwait(true);
                    return true;
                }
                catch (Exception ex)
                {
                    if (ex is UpdateException updateEx && updateEx.Message.Contains("cancelled"))
                    {
                        _lastCancelledUpdateVersion = _latestVersion;
                        LogGoldbergUpdateFailure(logger, ex, "emulator download");
                        progressForm.ShowCancellationAndClose("Download cancelled by user");
                        await WaitForProgressFormCloseAsync(progressForm).ConfigureAwait(true);
                    }
                    else
                    {
                        LogGoldbergUpdateFailure(logger, ex, "emulator download");
                        FormMessageBoxHelper.ShowIfAlive(
                            progressForm,
                            $"Update failed: {ex.Message}\n\nPlease try again or download manually from:\n{GetGoldbergManualDownloadForkUrlsText()}",
                            "Update Failed",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                        progressForm.Hide();
                    }

                    return false;
                }
            }
        }

        // Runs the HTTP check off the UI thread; presents dialogs on uiOwner (async install, no DoEvents wait loop).
        public static async Task CheckForUpdatesWithUIAsync(
            ILogService logger,
            Control uiOwner,
            bool isStartup = false,
            Action onCheckStart = null,
            Action onCheckComplete = null)
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            if (uiOwner == null)
                throw new ArgumentNullException(nameof(uiOwner));

            void RunSyncOnUi(Action a)
            {
                if (uiOwner.IsDisposed || uiOwner.Disposing)
                    return;
                if (uiOwner.InvokeRequired)
                    uiOwner.Invoke(a);
                else
                    a();
            }

            RunSyncOnUi(() => onCheckStart?.Invoke());
            try
            {
                var result = await Task.Run(() => CheckForUpdatesAsync(isStartup)).ConfigureAwait(false);
                await ControlInvokeAsyncHelper.InvokeAsync(uiOwner, async () =>
                {
                    await PresentUpdateCheckResultCoreAsync(
                        logger,
                        result,
                        isStartup,
                        uiOwner,
                        () => RunDownloadAndInstallWithProgressFormAsync(logger)).ConfigureAwait(true);
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogGoldbergUpdateFailure(logger, ex, "update check");
                string detail = GetUpdateFailureDetail(ex);
                if (isStartup)
                    _lastStartupUpdateCheckError = detail;
                else
                {
                    RunSyncOnUi(() =>
                    {
                        FormMessageBoxHelper.ShowIfAlive(
                            uiOwner,
                            $"Error checking for updates: {detail}",
                            "Update Check Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    });
                }
            }
            finally
            {
                RunSyncOnUi(() => onCheckComplete?.Invoke());
            }
        }

        public static Task<bool> DownloadAndInstallWithUIAsync(ILogService logger, Control uiRoot)
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            if (uiRoot == null)
                throw new ArgumentNullException(nameof(uiRoot));

            return ControlInvokeAsyncHelper.InvokeAsync(uiRoot, async () => await RunDownloadAndInstallWithProgressFormAsync(logger).ConfigureAwait(true));
        }

        // Child dialog closes first; run download on Owner so progress UI has a message loop.
        public static void BeginDownloadAndInstallOnOwnerForm(Form owner, ILogService logger)
        {
            if (owner == null || owner.IsDisposed || owner.Disposing)
                return;
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            owner.BeginInvoke(new Action(() =>
            {
                if (owner.IsDisposed || owner.Disposing)
                    return;
                _ = DownloadAndInstallWithUIAsync(logger, owner).ForgetFaults(logger, nameof(DownloadAndInstallWithUIAsync));
            }));
        }

        public static void CheckForUpdatesWithUISync(ILogService logger, bool isStartup = false)
        {
            try
            {
                logger?.LogMessage("Checking for Goldberg emulator updates...");

                // Run off the UI sync context so WinForms cannot deadlock if this is invoked from a form thread.
                var checkTask = Task.Run(() => CheckForUpdatesAsync(isStartup: isStartup));
                bool completed = isStartup
                    ? checkTask.Wait(TimeSpan.FromSeconds(StartupUpdateCheckTimeoutSeconds))
                    : checkTask.Wait(TimeSpan.FromMinutes(2));

                if (!completed)
                {
                    logger?.LogWarning("Update check timed out - proceeding without update info");
                    if (isStartup)
                        _lastStartupUpdateCheckError = "Update check timed out";
                    return;
                }

                if (checkTask.IsFaulted)
                {
                    Exception ex = checkTask.Exception?.GetBaseException();
                    if (ex != null)
                        LogGoldbergUpdateFailure(logger, ex, "update check");
                    else
                        logger?.LogWarning("Goldberg update check failed: unknown error");
                    string msg = ex != null ? GetUpdateFailureDetail(ex) : "Update check failed";
                    if (isStartup)
                        _lastStartupUpdateCheckError = msg;
                    else
                    {
                        FormMessageBoxHelper.ShowIfAlive(
                            null,
                            $"Error checking for updates: {msg}",
                            "Update Check Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }
                    return;
                }

                PresentUpdateCheckResult(logger, checkTask.Result, isStartup, null, () => RunDownloadAndInstallWithProgressForm(logger));
            }
            catch (Exception ex)
            {
                LogGoldbergUpdateFailure(logger, ex, "update check");
                string baseExMessage = GetUpdateFailureDetail(ex);
                if (isStartup)
                    _lastStartupUpdateCheckError = baseExMessage;
                else
                {
                    FormMessageBoxHelper.ShowIfAlive(
                        null,
                        $"Error checking for updates: {baseExMessage}",
                        "Update Check Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }

        private static UpdateChangelogDialogContent BuildUpdateChangelogContent(UpdateCheckResult result)
        {
            return new UpdateChangelogDialogContent
            {
                FormTitle = ApplicationConstants.WindowTitle,
                Headline = "A new version of Goldberg Emulator is available.",
                ReleaseNotes = result.ReleaseNotes,
                ProceedQuestion = "Do you want to proceed with the installation?",
                ManualDownloadLinks = GetGoldbergManualDownloadLinks()
            };
        }
    }
}

