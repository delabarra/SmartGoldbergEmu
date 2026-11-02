using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.ExtractKit;
using SmartGoldbergEmu.Forms;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Services
{
    // Downloads ColdClientLoader-*-win.7z from the repack and installs loaders under goldberg/ColdClientLoader/.
    public static class ColdClientInstallService
    {
        private const int HttpTimeoutSeconds = 120;
        private const int IdContinue = 100;

        public static bool AreInstalled() =>
            !GoldbergInstallLayout.AreColdClientInstallFilesMissing(PathConstants.GoldbergDirectory);

        // True when ColdClient install files are missing (same polarity as EmulatorUpdateService.GoldbergFilesCheckSync).
        public static bool ColdClientFilesCheckSync() =>
            GoldbergInstallLayout.AreColdClientInstallFilesMissing(PathConstants.GoldbergDirectory);

        // Modal warning before any download/Defender work. Returns false when the user cancels.
        public static bool TryConfirmSetupWarning(IWin32Window owner)
        {
            AppTaskDialogResult result = AppTaskDialogForm.Show(
                owner,
                new AppTaskDialogRequest
                {
                    Content =
                        "ColdClient mode uses upstream steamclient loader executables. Windows Defender often flags them as false positives.\n\n"
                        + "Continuing will add Windows Defender exclusions for:\n\n"
                        + "- " + GoldbergForkConstants.ColdClientDownloadArchiveFileName + " (temporary; removed after setup)\n"
                        + "- goldberg/ColdClientLoader/" + PathConstants.GoldbergColdClientLoaderExe32 + " (permanent)\n"
                        + "- goldberg/ColdClientLoader/" + PathConstants.GoldbergColdClientLoaderExe64 + " (permanent)",
                    FooterText = "Do you want to continue?",
                    CustomIcon = SystemIcons.Shield,
                    Buttons = new List<AppTaskDialogButton>
                    {
                        new AppTaskDialogButton(IdContinue, "Continue") { IsDefault = true },
                        new AppTaskDialogButton(TaskDialogHelper.IdCancel, "Cancel") { IsCancel = true }
                    }
                });
            return result.ButtonId == IdContinue;
        }

        // Pre-Application.Run / sync callers: pumps DoEvents until async setup completes (same pattern as Goldberg startup install).
        public static bool TryEnsureInstalledWithUiSync(IWin32Window owner, ILogService logger = null)
        {
            if (AreInstalled())
                return true;

            Task<bool> task = TryEnsureInstalledWithUiAsync(owner, logger);
            while (!task.IsCompleted)
            {
                Application.DoEvents();
                Thread.Sleep(15);
            }

            return task.GetAwaiter().GetResult();
        }

        // Warning + progress UI. Returns false if the user cancels or setup fails.
        public static async Task<bool> TryEnsureInstalledWithUiAsync(IWin32Window owner, ILogService logger = null)
        {
            if (AreInstalled())
                return true;

            if (!TryConfirmSetupWarning(owner))
                return false;

            using (var progressForm = new ProgressForm())
            {
                try
                {
                    progressForm.Text = "ColdClient Setup";
                    progressForm.Reset();
                    progressForm.Show(owner);

                    bool installed = await EnsureInstalledAsync(
                        (message, progress) => progressForm.UpdateProgress(message, progress),
                        () => progressForm.IsCancelled).ConfigureAwait(true);

                    if (!installed)
                    {
                        progressForm.ShowCancellationAndClose("ColdClient setup cancelled");
                        return false;
                    }

                    progressForm.ShowSuccessAndClose("ColdClient ready");
                    return true;
                }
                catch (OperationCanceledException)
                {
                    progressForm.ShowCancellationAndClose("ColdClient setup cancelled");
                    return false;
                }
                catch (Exception ex)
                {
                    try { progressForm.Close(); } catch { }
                    logger?.LogError("ColdClient setup failed", ex);
                    FormMessageBoxHelper.ShowIfAlive(
                        owner,
                        "ColdClient setup failed: " + ex.Message,
                        "ColdClient Setup",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return false;
                }
            }
        }

        // Returns false if UAC/exclusion fails. Caller must show TryConfirmSetupWarning first when prompting the user.
        public static async Task<bool> EnsureInstalledAsync(
            Action<string, int> progressCallback = null,
            Func<bool> cancellationCheck = null)
        {
            if (AreInstalled())
                return true;

            string tempFolder = Path.GetFullPath(
                Path.Combine(PathConstants.AppBaseDirectory, PathConstants.LauncherUpdateTempFolderName));
            string archivePath = Path.GetFullPath(
                Path.Combine(tempFolder, GoldbergForkConstants.ColdClientDownloadArchiveFileName));
            string loader32 = Path.GetFullPath(PathConstants.CombineGoldbergColdClientLoaderPath(false));
            string loader64 = Path.GetFullPath(PathConstants.CombineGoldbergColdClientLoaderPath(true));
            bool archiveExclusionAdded = false;

            try
            {
                Directory.CreateDirectory(tempFolder);
                Directory.CreateDirectory(PathConstants.GoldbergDirectory);
                Directory.CreateDirectory(PathConstants.GoldbergColdClientDirectory);

                progressCallback?.Invoke("Fetching ColdClient release information...", 5);
                GoldbergResolvedRelease resolved = await ResolveColdClientReleaseAsync().ConfigureAwait(false);

                // Same shape as non-repack Goldberg Update: exclude the .7z while it exists.
                // Also permanently exclude only the two loader EXEs (DLLs are not flagged; folder-wide exclude is unnecessary).
                IReadOnlyList<string> setupExclusionPaths = new[] { archivePath, loader32, loader64 };

                progressCallback?.Invoke("Requesting Windows Defender exclusions...", 10);
                archiveExclusionAdded = await Task.Run(() => WindowsDefenderExclusionHelper.TryApplyExclusions(setupExclusionPaths, add: true))
                    .ConfigureAwait(false);
                if (!archiveExclusionAdded)
                    throw new UpdateException("Windows Defender exclusion was denied. ColdClient setup aborted.");

                ThrowIfCancelled(cancellationCheck);
                progressCallback?.Invoke("Downloading ColdClient pack...", 20);
                ReleaseAssetIntegrityHelper.EnsureTrustedGitHubHttpsDownloadUrl(resolved.DownloadUrl);
                await DownloadFileAsync(resolved.DownloadUrl, archivePath, progressCallback, cancellationCheck).ConfigureAwait(false);
                ReleaseAssetIntegrityHelper.VerifyFileSha256OrThrow(archivePath, resolved.ExpectedSha256Hex);

                ThrowIfCancelled(cancellationCheck);
                progressCallback?.Invoke("Installing ColdClient files...", 60);
                await Task.Run(() => ExtractColdClientFiles(
                    archivePath,
                    PathConstants.GoldbergDirectory,
                    cancellationCheck)).ConfigureAwait(false);

                if (!AreInstalled())
                    throw new UpdateException("ColdClient files were not installed correctly.");

                // Goldberg order: wipe temp while archive exclusion still applies, then drop only the archive exclude.
                progressCallback?.Invoke("Cleaning up...", 95);
                DeleteTempFolder(tempFolder);
                RemoveArchiveExclusionOnly(archivePath, ref archiveExclusionAdded);

                progressCallback?.Invoke("ColdClient setup complete", 100);
                return true;
            }
            catch (UpdateException)
            {
                DeleteTempFolder(tempFolder);
                RemoveArchiveExclusionOnly(archivePath, ref archiveExclusionAdded);
                throw;
            }
            catch (Exception ex)
            {
                DeleteTempFolder(tempFolder);
                RemoveArchiveExclusionOnly(archivePath, ref archiveExclusionAdded);
                throw new UpdateException("ColdClient setup failed: " + ex.Message, ex);
            }
        }

        // Keep permanent loader EXE exclusions; only the temporary archive path is removed (after temp/ is gone).
        private static void RemoveArchiveExclusionOnly(string archivePath, ref bool archiveExclusionAdded)
        {
            if (!archiveExclusionAdded || string.IsNullOrEmpty(archivePath))
                return;

            try
            {
                WindowsDefenderExclusionHelper.TryApplyExclusions(new[] { archivePath }, add: false);
            }
            catch
            {
            }

            archiveExclusionAdded = false;
        }

        private static void DeleteTempFolder(string tempFolder)
        {
            try
            {
                if (!string.IsNullOrEmpty(tempFolder) && Directory.Exists(tempFolder))
                    Directory.Delete(tempFolder, recursive: true);
            }
            catch
            {
            }
        }

        private static async Task<GoldbergResolvedRelease> ResolveColdClientReleaseAsync()
        {
            using (IHttpService http = HttpServiceFactory.Create(TimeSpan.FromSeconds(HttpTimeoutSeconds)))
            {
                string json;
                using (HttpResponseMessage response = await http.GetAsync(GoldbergForkConstants.RepackReleasesApiUrl).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }

                var resolved = new GoldbergResolvedRelease();
                if (!GoldbergReleaseResolveHelper.TryParseColdClientRepackRelease(json, resolved)
                    || string.IsNullOrEmpty(resolved.DownloadUrl)
                    || string.IsNullOrEmpty(resolved.ExpectedSha256Hex))
                {
                    throw new UpdateException("Could not find a verified ColdClientLoader pack on the Goldberg repack releases.");
                }

                return resolved;
            }
        }

        private static async Task DownloadFileAsync(
            string url,
            string destinationPath,
            Action<string, int> progressCallback,
            Func<bool> cancellationCheck)
        {
            using (var cts = new CancellationTokenSource())
            using (var http = HttpServiceFactory.Create(TimeSpan.FromSeconds(HttpTimeoutSeconds)))
            {
                await http.DownloadFileAsync(
                    url,
                    destinationPath,
                    (received, total) =>
                    {
                        ThrowIfCancelled(cancellationCheck);
                        if (progressCallback == null || total <= 0)
                            return;
                        int pct = 20 + (int)(40.0 * received / total);
                        if (pct > 59)
                            pct = 59;
                        progressCallback("Downloading ColdClient pack...", pct);
                    },
                    cts.Token).ConfigureAwait(false);
            }
        }

        private static void ExtractColdClientFiles(
            string archivePath,
            string tempGoldbergRoot,
            Func<bool> cancellationCheck)
        {
            using (var session = new ArchiveExtractSession(archivePath))
            {
                foreach (GoldbergInstallLayout.GoldbergInstallFile file in GoldbergInstallLayout.GetColdClientInstallFiles())
                {
                    ThrowIfCancelled(cancellationCheck);
                    string relativeDir = file.InstallRelativeDirectory;
                    string destinationFolder = string.IsNullOrEmpty(relativeDir)
                        ? tempGoldbergRoot
                        : Path.Combine(tempGoldbergRoot, relativeDir);
                    Directory.CreateDirectory(destinationFolder);
                    string destinationPath = Path.Combine(destinationFolder, file.FileName);
                    foreach (string archivePathCandidate in GoldbergInstallLayout.GetArchivePathCandidates(file))
                    {
                        session.TryExtractSingleFileFlat(archivePathCandidate, destinationFolder, null);
                        if (File.Exists(destinationPath))
                            break;
                    }

                    if (!File.Exists(destinationPath))
                        throw new UpdateException("Required ColdClient file was not found in the archive: " + file.FileName);
                }
            }
        }

        private static void ThrowIfCancelled(Func<bool> cancellationCheck)
        {
            if (cancellationCheck != null && cancellationCheck())
                throw new OperationCanceledException("ColdClient setup cancelled.");
        }
    }
}
