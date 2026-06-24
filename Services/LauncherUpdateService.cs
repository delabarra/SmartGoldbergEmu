using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
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

        private static string _downloadUrl;
        private static string _latestVersion;
        private static string _lastCancelledUpdateVersion;

        public static string GetInstalledVersion() => ApplicationVersionHelper.GetVersionForComparison();

        private const string ReleaseRepositoryNotConfiguredMessage =
            "Launcher release repository is not configured yet. Set GitHubOwner and GitHubRepo in Constants/LauncherReleaseConstants.cs, or add launcher_update_api_url under [application] in settings.ini.";

        private const string NoPublishedReleaseMessage = "No published latest release (HTTP 404).";

        public static async Task<UpdateCheckResult> CheckForUpdatesAsync(bool isStartup = false)
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
                        if (response.StatusCode == HttpStatusCode.NotFound)
                        {
                            result.ErrorMessage = NoPublishedReleaseMessage;
                            return result;
                        }

                        if (response.StatusCode == HttpStatusCode.Forbidden)
                        {
                            string errorContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (errorContent.Contains("rate limit"))
                            {
                                result.ErrorMessage =
                                    "GitHub API rate limit exceeded. Try again later or use authenticated requests.";
                                return result;
                            }
                        }

                        response.EnsureSuccessStatusCode();
                        string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        if (!TryParseLatestReleaseJson(json, result))
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
                result.ErrorMessage = "Request timed out";
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Update check failed: {ex.Message}";
            }

            return result;
        }

        private static bool TryParseLatestReleaseJson(string json, UpdateCheckResult result)
        {
            var releaseData = JsonObject.Parse(json);
            _latestVersion = releaseData["tag_name"]?.ToString();
            result.LatestVersion = _latestVersion;
            result.ReleaseNotes = releaseData["body"]?.ToString();

            _downloadUrl = null;
            result.DownloadUrl = null;
            foreach (JsonObject asset in (JsonArray)releaseData["assets"])
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

            if (string.IsNullOrEmpty(_downloadUrl))
            {
                result.ErrorMessage = "Could not find launcher release zip in the latest GitHub release";
                return false;
            }

            return true;
        }

        public static async Task DownloadAndApplyAsync(
            Action<string, int> progressCallback = null,
            Func<bool> cancellationCheck = null)
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
                    progressCallback?.Invoke("Fetching latest release information...", 5);
                    var checkResult = await CheckForUpdatesAsync(isStartup: false).ConfigureAwait(false);
                    if (!checkResult.Success || string.IsNullOrEmpty(_downloadUrl))
                        throw new UpdateException("Could not get download URL");
                }

                if (Directory.Exists(workRoot))
                    Directory.Delete(workRoot, true);
                Directory.CreateDirectory(workRoot);

                progressCallback?.Invoke("Downloading launcher update...", 15);
                await DownloadFileAsync(_downloadUrl, archivePath, progress =>
                {
                    int percentage = 15 + (int)(progress * 55);
                    progressCallback?.Invoke("Downloading launcher update...", percentage);
                }, cancellationCheck).ConfigureAwait(false);

                if (cancellationCheck?.Invoke() == true)
                    throw new UpdateException("Download cancelled by user");

                progressCallback?.Invoke("Extracting update package...", 75);
                await Task.Run(() =>
                {
                    ZipFile.ExtractToDirectory(archivePath, extractRoot);
                }).ConfigureAwait(false);

                if (cancellationCheck?.Invoke() == true)
                    throw new UpdateException("Download cancelled by user");

                string launcherExeName = Path.GetFileName(Application.ExecutablePath);
                string payloadRoot = LauncherUpdatePayloadHelper.ResolvePayloadRoot(extractRoot, launcherExeName);
                if (!string.Equals(payloadRoot, extractRoot, StringComparison.OrdinalIgnoreCase))
                {
                    ServiceLocator.LogService?.LogMessage(
                        "Launcher update: using nested release folder " + Path.GetFileName(payloadRoot));
                }

                progressCallback?.Invoke("Preparing to restart...", 90);
                string installRoot = PathConstants.LauncherInstallDirectory;
                string exePath = Path.Combine(installRoot, launcherExeName);

                progressCallback?.Invoke("Applying update after exit...", 95);
                await Task.Run(() => StartEmbeddedUpdaterApply(workRoot, installRoot, payloadRoot, exePath))
                    .ConfigureAwait(false);

                progressCallback?.Invoke("Restarting application...", 100);
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
            Action<double> progressCallback,
            Func<bool> cancellationCheck)
        {
            var effectiveTimeout = DownloadTimeout;
            using (var cts = new CancellationTokenSource(effectiveTimeout))
            {
                if (cancellationCheck != null)
                {
                    var poll = new System.Threading.Timer(_ =>
                    {
                        if (cancellationCheck())
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
                logger?.LogMessage("Checking for launcher updates...");

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
            if (logger == null)
                throw new ArgumentNullException(nameof(logger));
            if (uiOwner == null)
                throw new ArgumentNullException(nameof(uiOwner));

            void RunSyncOnUi(Action action)
            {
                if (uiOwner.IsDisposed || uiOwner.Disposing)
                    return;
                if (uiOwner.InvokeRequired)
                    uiOwner.Invoke(action);
                else
                    action();
            }

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
                        () => RunDownloadAndApplyWithProgressFormAsync(logger)).ConfigureAwait(true);
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger?.LogError("Launcher update check failed", ex);
                RunSyncOnUi(() =>
                {
                    FormMessageBoxHelper.ShowIfAlive(
                        uiOwner,
                        $"Error checking for launcher updates: {ex.Message}",
                        "Update Check Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                });
            }
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
                        logger?.LogMessage(
                            $"Launcher update {result.LatestVersion} available but user previously declined, skipping prompt");
                        return;
                    }

                    if (!isStartup)
                        _lastCancelledUpdateVersion = null;

                    logger?.LogMessage(
                        $"Launcher update available: {result.LatestVersion} (current: {result.CurrentVersion ?? "unknown"})");

                    var dialogResult = UpdateChangelogForm.ShowDialogIfAlive(owner, BuildUpdateChangelogContent(result));

                    if (dialogResult == DialogResult.OK)
                    {
                        logger?.LogMessage("User chose to download and install launcher update");
                        _lastCancelledUpdateVersion = null;
                        await installWhenUserAcceptedOkAsync().ConfigureAwait(true);
                    }
                    else
                    {
                        logger?.LogMessage("User chose to skip launcher update");
                        _lastCancelledUpdateVersion = result.LatestVersion;
                    }
                }
                else
                {
                    logger?.LogMessage(
                        $"Launcher is up to date (current: {result.CurrentVersion ?? "unknown"}, latest: {result.LatestVersion})");
                    if (!isStartup)
                    {
                        FormMessageBoxHelper.ShowIfAlive(
                            owner,
                            $"You are running the latest version of SmartGoldbergEmu.\n\n" +
                            $"Current version: {ApplicationVersionHelper.GetDisplayVersion()}\n" +
                            $"Latest version: {result.LatestVersion}",
                            "No Updates Available",
                            MessageBoxButtons.OK,
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
            if (result != null && result.ErrorMessage == NoPublishedReleaseMessage)
            {
                return "No launcher release has been published yet.\n\n" +
                    "Check back later for updates.";
            }

            return "Failed to check for launcher updates.\n\n" +
                "Manually check for the latest release on SmartGoldbergEmu's GitHub releases page.";
        }

        private static UpdateChangelogDialogContent BuildUpdateChangelogContent(UpdateCheckResult result)
        {
            var content = new UpdateChangelogDialogContent
            {
                FormTitle = ApplicationConstants.WindowTitle,
                Headline = "A new version of SmartGoldbergEmu is available.",
                ReleaseNotes = result.ReleaseNotes,
                AdditionalInfo =
                    "The application will close and restart to apply the update.\r\n" +
                    "Your games folder, Goldberg files, and settings will be preserved.",
                ProceedQuestion = "Do you want to proceed?",
                ManualDownloadLinks = new List<UpdateManualDownloadLink>()
            };

            if (LauncherReleaseConstants.TryGetReleasesWebUrl(out string releasesWebUrl))
            {
                content.ManualDownloadLinks.Add(new UpdateManualDownloadLink
                {
                    Label = "GitHub releases",
                    Url = releasesWebUrl
                });
            }

            return content;
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
                        () => progressForm.IsCancelled).ConfigureAwait(true);

                    progressForm.Hide();
                    logger?.LogMessage("Launcher update staged; exiting to apply");
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
                        progressForm.Hide();
                    }
                }
            }
        }

    }
}
