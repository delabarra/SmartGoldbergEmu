using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Extensions;
using SmartGoldbergEmu.Forms;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.JsonKit;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.ExtractKit;

namespace SmartGoldbergEmu.Services
{
    public static class LauncherUpdateService
    {
        private const int HttpTimeoutSeconds = 10;
        private const int StartupUpdateCheckTimeoutSeconds = 15;
        private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(10);
        private static readonly string[] InstallSkipDirectoryNames =
        {
            PathConstants.GamesDirectoryFolderName,
            PathConstants.GoldbergDirectoryFolderName
        };

        // Contiguous UI bands: download is usually longest; extract is byte-tracked.
        private const int ProgressFetch = 3;
        private const int ProgressDownloadStart = 5;
        private const int ProgressDownloadEnd = 55;
        private const int ProgressExtractStart = 55;
        private const int ProgressExtractEnd = 92;
        private const int ProgressPrepareRestart = 93;
        private const int ProgressApplying = 96;
        private const int ProgressComplete = 100;

        private static string _downloadUrl;
        private static string _latestVersion;
        private static string _lastCancelledUpdateVersion;

        public static string GetInstalledVersion() => ApplicationVersionHelper.GetVersionForComparison();

        private const string ReleaseRepositoryNotConfiguredMessage =
            "Launcher release repository is not configured yet. Set GitHubOwner and GitHubRepo in Constants/LauncherReleaseConstants.cs, or add launcher_update_api_url under [application] in settings.ini.";

        private const string NoPublishedReleaseMessage = "No published latest release (HTTP 404).";
        private const string RateLimitMessage =
            "GitHub API rate limit exceeded.\nWait a few minutes and try again.";
        private const string MissingReleaseZipMessage =
            "Could not find launcher release zip in the latest GitHub release";
        private const string RequestTimedOutMessage = "Request timed out";
        private const string MissingReleaseTagMessage =
            "Latest GitHub release did not include a version tag.";

        public static async Task<UpdateCheckResult> CheckForUpdatesAsync(
            bool isStartup = false,
            bool requireDownloadAsset = true)
        {
            var result = new UpdateCheckResult
            {
                Success = false,
                UpdateAvailable = false,
                TimedOut = false
            };

            if (!LauncherReleaseConstants.TryGetReleasesApiUrl(out string releasesApiUrl))
            {
                result.ErrorMessage = ReleaseRepositoryNotConfiguredMessage;
                return result;
            }

            try
            {
                _downloadUrl = null;
                using (var httpService = HttpServiceFactory.Create(TimeSpan.FromSeconds(HttpTimeoutSeconds)))
                {
                    using (HttpResponseMessage response = await httpService
                        .GetAsync(releasesApiUrl)
                        .ConfigureAwait(false))
                    {
                        if (!await EnsureReleaseHttpSucceededAsync(response, result).ConfigureAwait(false))
                            return result;

                        string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        if (!TryParseLatestReleaseJson(json, result, requireDownloadAsset))
                            return result;

                        string currentVersion = GetInstalledVersion();
                        result.CurrentVersion = currentVersion;

                        if (string.IsNullOrEmpty(currentVersion))
                            result.UpdateAvailable = true;
                        else if (VersionComparisonHelper.IsNewerVersion(currentVersion, _latestVersion))
                            result.UpdateAvailable = true;

                        result.Success = true;
                    }
                }
            }
            catch (TaskCanceledException)
            {
                result.TimedOut = true;
                result.ErrorMessage = RequestTimedOutMessage;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = "Update check failed: " + ex.Message;
            }

            return result;
        }

        // True when the response is usable; false when result.ErrorMessage is set.
        private static async Task<bool> EnsureReleaseHttpSucceededAsync(
            HttpResponseMessage response,
            UpdateCheckResult result)
        {
            if (response == null)
            {
                result.ErrorMessage = "Update check failed: empty HTTP response.";
                return false;
            }

            if (response.IsSuccessStatusCode)
                return true;

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                result.ErrorMessage = NoPublishedReleaseMessage;
                return false;
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                string errorContent = response.Content != null
                    ? await response.Content.ReadAsStringAsync().ConfigureAwait(false)
                    : null;
                if (!string.IsNullOrEmpty(errorContent)
                    && errorContent.IndexOf("rate limit", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    result.ErrorMessage = RateLimitMessage;
                    return false;
                }

                result.ErrorMessage = "GitHub refused the release check (HTTP 403 Forbidden).";
                return false;
            }

            string reason = string.IsNullOrWhiteSpace(response.ReasonPhrase)
                ? response.StatusCode.ToString()
                : response.ReasonPhrase.Trim();
            result.ErrorMessage =
                "GitHub release check failed (HTTP " + (int)response.StatusCode + " " + reason + ").";
            return false;
        }

        private static bool TryParseLatestReleaseJson(
            string json,
            UpdateCheckResult result,
            bool requireDownloadAsset)
        {
            var releaseData = JsonObject.Parse(json);
            _latestVersion = releaseData["tag_name"]?.ToString();
            result.LatestVersion = _latestVersion;
            result.ReleaseNotes = releaseData["body"]?.ToString();

            if (string.IsNullOrWhiteSpace(_latestVersion))
            {
                result.ErrorMessage = MissingReleaseTagMessage;
                return false;
            }

            _downloadUrl = null;
            result.DownloadUrl = null;
            JsonArray assets = releaseData["assets"] as JsonArray;
            if (assets != null)
            {
                foreach (JsonObject asset in assets)
                {
                    string name = asset["name"]?.ToString();
                    if (string.IsNullOrEmpty(name))
                        continue;
                    if (!name.StartsWith(LauncherReleaseConstants.ReleaseZipNamePrefix, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        continue;

                    _downloadUrl = asset["browser_download_url"]?.ToString();
                    result.DownloadUrl = _downloadUrl;
                    break;
                }
            }

            if (string.IsNullOrEmpty(_downloadUrl))
            {
                if (requireDownloadAsset)
                {
                    result.ErrorMessage = MissingReleaseZipMessage;
                    return false;
                }

                // Changelog view only needs tag + notes.
                return true;
            }

            return true;
        }

        public static async Task DownloadAndApplyAsync(
            Action<string, int> progressCallback = null,
            Func<bool> cancellationCheck = null,
            Action disallowCancellation = null)
        {
            string workRoot = PathConstants.LauncherUpdateWorkDirectory;
            string archivePath = Path.Combine(workRoot, PathConstants.LauncherUpdateArchiveFileName);
            string extractRoot = Path.Combine(workRoot, PathConstants.LauncherUpdateExtractFolderName);

            try
            {
                if (cancellationCheck?.Invoke() == true)
                    throw new UpdateException("Download cancelled by user");

                if (string.IsNullOrEmpty(_downloadUrl))
                {
                    progressCallback?.Invoke("Fetching latest release information...", ProgressFetch);
                    var checkResult = await CheckForUpdatesAsync(isStartup: false).ConfigureAwait(false);
                    if (!checkResult.Success || string.IsNullOrEmpty(_downloadUrl))
                        throw new UpdateException("Could not get download URL");
                }

                if (Directory.Exists(workRoot))
                    Directory.Delete(workRoot, true);
                Directory.CreateDirectory(workRoot);

                progressCallback?.Invoke("Downloading launcher update...", ProgressDownloadStart);
                await DownloadFileAsync(_downloadUrl, archivePath, (received, total) =>
                {
                    int percentage;
                    string sizeText;
                    int span = ProgressDownloadEnd - ProgressDownloadStart;
                    if (total > 0)
                    {
                        double ratio = Math.Min(1.0, received / (double)total);
                        percentage = ProgressDownloadStart + (int)(ratio * span);
                        sizeText = HttpHelpers.FormatByteSizeRange(received, total);
                    }
                    else
                    {
                        double softRatio = 1.0 - (1.0 / (1.0 + received / (8.0 * 1024 * 1024)));
                        if (softRatio > 0.95)
                            softRatio = 0.95;
                        percentage = ProgressDownloadStart + (int)(softRatio * span);
                        sizeText = HttpHelpers.FormatByteSize(received);
                    }

                    if (percentage > ProgressDownloadEnd)
                        percentage = ProgressDownloadEnd;
                    progressCallback?.Invoke("Downloading launcher update... " + sizeText, percentage);
                }, cancellationCheck).ConfigureAwait(false);

                if (cancellationCheck?.Invoke() == true)
                    throw new UpdateException("Download cancelled by user");

                const string extractStatus = "Extracting launcher files...";
                progressCallback?.Invoke(extractStatus, ProgressExtractStart);
                try
                {
                    await Task.Run(() =>
                    {
                        global::SmartGoldbergEmu.ExtractKit.ExtractKit.ExtractAll(
                            archivePath,
                            extractRoot,
                            (completedBytes, totalBytes, fileName) =>
                            {
                                if (progressCallback == null || totalBytes <= 0)
                                    return;

                                int percentage = ArchiveExtractProgress.MapToPercent(
                                    completedBytes, totalBytes, ProgressExtractStart, ProgressExtractEnd);
                                // File-count fallback uses tiny totals; only show sizes for real byte progress.
                                if (totalBytes < 1024)
                                {
                                    progressCallback(extractStatus, percentage);
                                    return;
                                }

                                long shownCompleted = completedBytes;
                                if (shownCompleted < 0)
                                    shownCompleted = 0;
                                if (shownCompleted > totalBytes)
                                    shownCompleted = totalBytes;

                                string sizeText = HttpHelpers.FormatByteSizeRange(shownCompleted, totalBytes);
                                progressCallback(extractStatus + " " + sizeText, percentage);
                            },
                            cancellationCheck);
                    }).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw new UpdateException("Download cancelled by user");
                }

                if (cancellationCheck?.Invoke() == true)
                    throw new UpdateException("Download cancelled by user");

                // Download/extract done; apply/restart must finish — do not accept cancel.
                disallowCancellation?.Invoke();

                string launcherExeName = Path.GetFileName(Application.ExecutablePath);
                string payloadRoot = LauncherUpdatePayloadHelper.ResolvePayloadRoot(extractRoot, launcherExeName);
                if (!string.Equals(payloadRoot, extractRoot, StringComparison.OrdinalIgnoreCase))
                {
                    ServiceLocator.LogService?.LogDebug(
                        "Launcher update: using nested release folder " + Path.GetFileName(payloadRoot));
                }

                progressCallback?.Invoke("Preparing to restart...", ProgressPrepareRestart);
                string installRoot = PathConstants.LauncherInstallDirectory;
                string exePath = Path.Combine(installRoot, launcherExeName);

                progressCallback?.Invoke("Applying update after exit...", ProgressApplying);
                await Task.Run(() => StartEmbeddedUpdaterApply(workRoot, installRoot, payloadRoot, exePath))
                    .ConfigureAwait(false);

                progressCallback?.Invoke("Restarting application...", ProgressComplete);
            }
            catch (UpdateException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new UpdateException($"Launcher update failed: {ex.Message}", ex);
            }
        }

        private static async Task DownloadFileAsync(
            string url,
            string destinationPath,
            Action<long, long> progressCallback,
            Func<bool> cancellationCheck)
        {
            var effectiveTimeout = DownloadTimeout;
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ServiceLocator.ApplicationLifetimeToken))
            {
                cts.CancelAfter(effectiveTimeout);
                if (cancellationCheck != null)
                {
                    var poll = new System.Threading.Timer(_ =>
                    {
                        if (cancellationCheck() || ServiceLocator.ApplicationLifetimeToken.IsCancellationRequested)
                            cts.Cancel();
                    }, null, 0, 500);
                    try
                    {
                        using (var httpService = HttpServiceFactory.Create(effectiveTimeout))
                        {
                            await httpService.DownloadFileAsync(url, destinationPath, progressCallback, cts.Token)
                                .ConfigureAwait(false);
                        }
                    }
                    finally
                    {
                        poll.Dispose();
                    }
                }
                else
                {
                    using (var httpService = HttpServiceFactory.Create(effectiveTimeout))
                    {
                        await httpService.DownloadFileAsync(url, destinationPath, progressCallback, cts.Token)
                            .ConfigureAwait(false);
                    }
                }
            }
        }

        private static void StartEmbeddedUpdaterApply(
            string workRoot,
            string installRoot,
            string stageRoot,
            string exePath)
        {
            string updaterPath = LauncherEmbeddedUpdaterHelper.GetUpdaterPath(workRoot);
            string manifestPath = LauncherEmbeddedUpdaterHelper.GetManifestPath(workRoot);

            LauncherEmbeddedUpdaterHelper.ExtractEmbeddedUpdater(updaterPath);
            LauncherEmbeddedUpdaterHelper.WriteApplyManifest(
                manifestPath,
                installRoot,
                stageRoot,
                exePath,
                workRoot,
                Process.GetCurrentProcess().Id,
                InstallSkipDirectoryNames);
            LauncherEmbeddedUpdaterHelper.StartEmbeddedUpdater(updaterPath, manifestPath);
        }

        public static void CheckForUpdatesWithUISync(ILogService logger, bool isStartup = false)
        {
            if (!LauncherReleaseConstants.TryGetReleasesApiUrl(out _))
                return;

            try
            {
                logger?.LogDebug("Checking for launcher updates...");

                var checkTask = Task.Run(() => CheckForUpdatesAsync(isStartup: isStartup));
                bool completed = isStartup
                    ? checkTask.Wait(TimeSpan.FromSeconds(StartupUpdateCheckTimeoutSeconds))
                    : checkTask.Wait(TimeSpan.FromMinutes(2));

                if (!completed)
                {
                    logger?.LogWarning("Launcher update check timed out - proceeding without update info");
                    return;
                }

                if (checkTask.IsFaulted)
                {
                    Exception ex = checkTask.Exception?.GetBaseException();
                    if (ex != null)
                        logger?.LogError("Launcher update check task faulted", ex);
                    else
                        logger?.LogError("Launcher update check task faulted");

                    if (!isStartup)
                    {
                        string msg = ex != null ? ex.Message : "Update check failed";
                        FormMessageBoxHelper.ShowIfAlive(
                            null,
                            $"Error checking for launcher updates: {msg}",
                            "Update Check Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    }

                    return;
                }

                PresentUpdateCheckResult(
                    logger,
                    checkTask.Result,
                    isStartup,
                    () => RunDownloadAndApplyWithProgressForm(logger));
            }
            catch (Exception ex)
            {
                Exception baseEx = ex.GetBaseException();
                logger?.LogError("Launcher update check failed", baseEx);
                if (!isStartup)
                {
                    FormMessageBoxHelper.ShowIfAlive(
                        null,
                        $"Error checking for launcher updates: {baseEx.Message}",
                        "Update Check Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }

        private static void PresentUpdateCheckResult(
            ILogService logger,
            UpdateCheckResult result,
            bool isStartup,
            Action installUpdateOnUiThread)
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
        }

        private static void RunDownloadAndApplyWithProgressForm(ILogService logger)
        {
            var task = RunDownloadAndApplyWithProgressFormAsync(logger);
            while (!task.IsCompleted)
            {
                Application.DoEvents();
                Thread.Sleep(15);
            }

            task.GetAwaiter().GetResult();
        }

        public static async Task CheckForUpdatesWithUIAsync(
            ILogService logger,
            Control uiOwner,
            bool isStartup = false)
        {
            await FetchLatestAndPresentOnUiAsync(
                logger,
                uiOwner,
                isStartup,
                "Launcher update check failed",
                "Update Check Error",
                "Error checking for launcher updates",
                async result =>
                {
                    await PresentUpdateCheckResultCoreAsync(
                        logger,
                        result,
                        isStartup,
                        uiOwner,
                        () => RunDownloadAndApplyWithProgressFormAsync(logger)).ConfigureAwait(true);
                }).ConfigureAwait(false);
        }

        // Confirm via changelog, then download latest release and apply even when already on that version.
        public static async Task ReinstallWithUIAsync(ILogService logger, Control uiOwner)
        {
            await FetchLatestAndPresentOnUiAsync(
                logger,
                uiOwner,
                isStartup: false,
                "Launcher reinstall failed",
                "Reinstall Error",
                "Error preparing launcher reinstall",
                async result =>
                {
                    if (!EnsureReleaseCheckSucceeded(uiOwner, result, "Reinstall Failed"))
                        return;

                    var dialogResult = ChangelogForm.ShowDialogIfAlive(
                        uiOwner,
                        BuildReinstallChangelogContent(result));
                    if (dialogResult != DialogResult.OK)
                    {
                        logger?.LogDebug("User cancelled launcher reinstall");
                        return;
                    }

                    logger?.LogDebug("User confirmed launcher reinstall");
                    await RunDownloadAndApplyWithProgressFormAsync(logger).ConfigureAwait(true);
                }).ConfigureAwait(false);
        }

        public static async Task ShowLatestChangelogWithUIAsync(ILogService logger, Control uiOwner)
        {
            await FetchLatestAndPresentOnUiAsync(
                logger,
                uiOwner,
                isStartup: false,
                "Failed to load launcher changelog",
                "Changelog Error",
                "Error loading changelog",
                result =>
                {
                    if (!EnsureReleaseCheckSucceeded(uiOwner, result, "Changelog Unavailable"))
                        return Task.CompletedTask;

                    ChangelogForm.ShowDialogIfAlive(uiOwner, BuildViewChangelogContent(result));
                    return Task.CompletedTask;
                },
                requireDownloadAsset: false).ConfigureAwait(false);
        }

        private static async Task FetchLatestAndPresentOnUiAsync(
            ILogService logger,
            Control uiOwner,
            bool isStartup,
            string catchLogMessage,
            string catchCaption,
            string catchMessagePrefix,
            Func<UpdateCheckResult, Task> presentOnUiAsync,
            bool requireDownloadAsset = true)
        {
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            if (uiOwner == null)
                throw new ArgumentNullException(nameof(uiOwner));
            if (presentOnUiAsync == null)
                throw new ArgumentNullException(nameof(presentOnUiAsync));

            try
            {
                var result = await Task.Run(
                        () => CheckForUpdatesAsync(isStartup, requireDownloadAsset))
                    .ConfigureAwait(false);
                await ControlInvokeAsyncHelper.InvokeAsync(uiOwner, () => presentOnUiAsync(result))
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(catchLogMessage, ex);
                RunOnUiIfAlive(uiOwner, () =>
                {
                    FormMessageBoxHelper.ShowIfAlive(
                        uiOwner,
                        catchMessagePrefix + ": " + ex.Message,
                        catchCaption,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                });
            }
        }

        private static bool EnsureReleaseCheckSucceeded(
            IWin32Window owner,
            UpdateCheckResult result,
            string failureCaption)
        {
            if (result != null && result.Success)
                return true;

            FormMessageBoxHelper.ShowIfAlive(
                owner,
                BuildUpdateCheckFailedUserMessage(result),
                failureCaption,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }

        private static void RunOnUiIfAlive(Control uiOwner, Action action)
        {
            if (action == null || uiOwner == null || uiOwner.IsDisposed || uiOwner.Disposing)
                return;
            if (uiOwner.InvokeRequired)
                uiOwner.Invoke(action);
            else
                action();
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

            if (result.Success)
            {
                if (result.UpdateAvailable)
                {
                    if (isStartup && _lastCancelledUpdateVersion == result.LatestVersion)
                    {
                        logger?.LogDebug(
                            $"Launcher update {result.LatestVersion} available but user previously declined, skipping prompt");
                        return;
                    }

                    if (!isStartup)
                        _lastCancelledUpdateVersion = null;

                    logger?.LogMessage(
                        $"Launcher update available: {result.LatestVersion} (current: {result.CurrentVersion ?? "unknown"})");

                    var dialogResult = ChangelogForm.ShowDialogIfAlive(owner, BuildUpdateChangelogContent(result));

                    if (dialogResult == DialogResult.OK)
                    {
                        logger?.LogDebug("User chose to download and install launcher update");
                        _lastCancelledUpdateVersion = null;
                        await installWhenUserAcceptedOkAsync().ConfigureAwait(true);
                    }
                    else
                    {
                        logger?.LogDebug("User chose to skip launcher update");
                        _lastCancelledUpdateVersion = result.LatestVersion;
                    }
                }
                else
                {
                    logger?.LogMessage("Launcher up to date.");
                    if (!isStartup)
                    {
                        AppTaskDialogHelper.ShowOk(
                            owner,
                            "You are running the latest version of SmartGoldbergEmu.\n\n"
                            + "Current version: " + ApplicationVersionHelper.GetTaggedDisplayVersion() + "\n"
                            + "Latest version: " + result.LatestVersion,
                            MessageBoxIcon.Information);
                    }
                }
            }
            else
            {
                logger?.LogWarning($"Launcher update check failed: {result.ErrorMessage}");
                if (!isStartup)
                {
                    FormMessageBoxHelper.ShowIfAlive(
                        owner,
                        BuildUpdateCheckFailedUserMessage(result),
                        "Update Check Failed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }
        }

        private static string BuildUpdateCheckFailedUserMessage(UpdateCheckResult result)
        {
            const string manualHint =
                "Manually check for the latest release on SmartGoldbergEmu's GitHub releases page.";

            if (result == null)
                return "Failed to check for launcher updates.\n\n" + manualHint;

            if (result.TimedOut
                || string.Equals(result.ErrorMessage, RequestTimedOutMessage, StringComparison.Ordinal))
            {
                return "The launcher update check timed out.\n\n"
                    + "Check your connection and try again.\n\n"
                    + manualHint;
            }

            if (string.Equals(result.ErrorMessage, NoPublishedReleaseMessage, StringComparison.Ordinal))
            {
                return "No launcher release has been published yet.\n\n"
                    + "Check back later for updates.";
            }

            if (string.Equals(result.ErrorMessage, ReleaseRepositoryNotConfiguredMessage, StringComparison.Ordinal))
                return result.ErrorMessage;

            if (string.Equals(result.ErrorMessage, RateLimitMessage, StringComparison.Ordinal)
                || (!string.IsNullOrEmpty(result.ErrorMessage)
                    && result.ErrorMessage.IndexOf("rate limit", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return RateLimitMessage + "\n\n" + manualHint;
            }

            if (string.Equals(result.ErrorMessage, MissingReleaseZipMessage, StringComparison.Ordinal))
            {
                return "A launcher release was found, but it does not include a downloadable zip yet.\n\n"
                    + manualHint;
            }

            if (string.Equals(result.ErrorMessage, MissingReleaseTagMessage, StringComparison.Ordinal))
            {
                return "The latest GitHub release response did not include a version tag.\n\n"
                    + manualHint;
            }

            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                return "Failed to check for launcher updates.\n\n"
                    + result.ErrorMessage.Trim()
                    + "\n\n"
                    + manualHint;
            }

            return "Failed to check for launcher updates.\n\n" + manualHint;
        }

        private const string ManualDownloadCaptionLabel = "You can also manually download and setup from";
        private const string ViewMoreChangelogsOnlineLabel = "View more changelogs online";

        private static ChangelogDialogContent BuildUpdateChangelogContent(UpdateCheckResult result)
        {
            return CreateChangelogContent(
                result,
                "A new version of SmartGoldbergEmu is available.",
                additionalInfo: null,
                proceedQuestion: string.Empty,
                okButtonText: "Install",
                manualDownloadLinks: BuildManualDownloadLinks(),
                showManualDownloadCaption: false);
        }

        private static ChangelogDialogContent BuildReinstallChangelogContent(UpdateCheckResult result)
        {
            string versionLabel = FormatLatestVersionLabel(result, "latest");
            return CreateChangelogContent(
                result,
                "Reinstall SmartGoldbergEmu (" + versionLabel + ").",
                additionalInfo: null,
                proceedQuestion: string.Empty,
                okButtonText: "Reinstall",
                manualDownloadLinks: BuildManualDownloadLinks(),
                showManualDownloadCaption: false);
        }

        private static ChangelogDialogContent BuildViewChangelogContent(UpdateCheckResult result)
        {
            string latestLabel = FormatLatestVersionLabel(result, "unknown");
            return CreateChangelogContent(
                result,
                "SmartGoldbergEmu release notes (" + latestLabel + ").",
                additionalInfo: null,
                proceedQuestion: string.Empty,
                okButtonText: "Close",
                showCancelButton: false,
                manualDownloadLinks: BuildViewMoreChangelogsOnlineLinks(),
                showManualDownloadCaption: false);
        }

        private static ChangelogDialogContent CreateChangelogContent(
            UpdateCheckResult result,
            string headline,
            string additionalInfo,
            string proceedQuestion,
            string okButtonText = null,
            bool showCancelButton = true,
            IList<UpdateManualDownloadLink> manualDownloadLinks = null,
            bool showManualDownloadCaption = false)
        {
            return new ChangelogDialogContent
            {
                FormTitle = ApplicationConstants.WindowTitle,
                Headline = headline,
                ReleaseNotes = result?.ReleaseNotes,
                AdditionalInfo = additionalInfo,
                ProceedQuestion = proceedQuestion,
                OkButtonText = okButtonText,
                ShowCancelButton = showCancelButton,
                ShowManualDownloadCaption = showManualDownloadCaption,
                ManualDownloadLinks = manualDownloadLinks
            };
        }

        private static string FormatLatestVersionLabel(UpdateCheckResult result, string fallback)
        {
            return string.IsNullOrWhiteSpace(result?.LatestVersion)
                ? fallback
                : result.LatestVersion.Trim();
        }

        private static List<UpdateManualDownloadLink> BuildManualDownloadLinks()
        {
            var links = new List<UpdateManualDownloadLink>();
            if (LauncherReleaseConstants.TryGetReleasesWebUrl(out string releasesWebUrl))
            {
                links.Add(new UpdateManualDownloadLink
                {
                    Label = ManualDownloadCaptionLabel,
                    Url = releasesWebUrl
                });
            }

            return links;
        }

        private static List<UpdateManualDownloadLink> BuildViewMoreChangelogsOnlineLinks()
        {
            var links = new List<UpdateManualDownloadLink>();
            if (LauncherReleaseConstants.TryGetReleasesWebUrl(out string releasesWebUrl))
            {
                links.Add(new UpdateManualDownloadLink
                {
                    Label = ViewMoreChangelogsOnlineLabel,
                    Url = releasesWebUrl
                });
            }

            return links;
        }

        private static async Task RunDownloadAndApplyWithProgressFormAsync(ILogService logger)
        {
            using (var progressForm = new ProgressForm())
            {
                try
                {
                    progressForm.Text = "Updating SmartGoldbergEmu";
                    progressForm.Show();
                    _lastCancelledUpdateVersion = null;

                    await DownloadAndApplyAsync(
                        (message, progress) => { progressForm.UpdateProgress(message, progress); },
                        () => progressForm.IsCancelled,
                        () => progressForm.DisableCancellation()).ConfigureAwait(true);

                    progressForm.Hide();
                    logger?.LogMessage("Launcher update staged; exiting to apply.");
                    Application.Exit();
                }
                catch (Exception ex)
                {
                    logger?.LogError("Launcher update failed", ex);

                    if (ex is UpdateException updateEx && updateEx.Message.Contains("cancelled"))
                    {
                        _lastCancelledUpdateVersion = _latestVersion;
                        progressForm.ShowCancellationAndClose("Download cancelled by user");
                    }
                    else
                    {
                        FormMessageBoxHelper.ShowIfAlive(
                            progressForm,
                            $"Update failed: {ex.Message}\n\nPlease try again." +
                            (LauncherReleaseConstants.TryGetReleasesWebUrl(out string releasesWebUrl)
                                ? "\n\nDownload manually from:\n" + releasesWebUrl
                                : string.Empty),
                            "Update Failed",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                        // Close (do not Hide): FormClosed unsubscribes ThemeChanged; a hidden open form can keep the process alive.
                        progressForm.Close();
                    }
                }
            }
        }

    }
}
