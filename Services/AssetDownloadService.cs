using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Services
{
    // Downloads overlay/EXAMPLE assets. WAVs + Steam.dll + LocalAppData clientui image use Steam CDN
    // client packages; avatar/font/glyphs use gbe_fork EXAMPLE.
    public class AssetDownloadService
    {
        private const int HttpTimeoutSeconds = 30;
        private const int SteamDllPackageTimeoutSeconds = 120;
        private const string SteamCdnHealTempFolderName = "steam-cdn-heal";

        public async Task<ValidationResult> DownloadSoundFilesAsync(string soundsPath)
        {
            string tempVzipPath = null;
            try
            {
                var achievementPath = Path.Combine(soundsPath, PathConstants.SteamClientUiAchievementNotificationWav);
                var friendPath = Path.Combine(soundsPath, PathConstants.SteamClientUiFriendNotificationWav);
                bool needsAchievement = !File.Exists(achievementPath);
                bool needsFriend = !File.Exists(friendPath);

                if (!needsAchievement && !needsFriend)
                    return ValidationResult.Success();

                tempVzipPath = CreateTempPackagePath("sounds.zip.vzip");
                await DownloadFirstAvailableToFileAsync(
                    ServiceLocator.SteamStaticCdnPreferenceService.GetClientSoundsPackageCandidateUrls(),
                    tempVzipPath,
                    HttpTimeoutSeconds).ConfigureAwait(false);

                var mappings = new List<KeyValuePair<string, string>>(2);
                if (needsAchievement)
                {
                    mappings.Add(new KeyValuePair<string, string>(
                        AssetConstants.SteamClientAchievementSoundInnerPath,
                        achievementPath));
                }

                if (needsFriend)
                {
                    mappings.Add(new KeyValuePair<string, string>(
                        AssetConstants.SteamClientFriendSoundInnerPath,
                        friendPath));
                }

                // One VZip decompress for both WAVs (previously decompressed twice in RAM).
                global::SmartGoldbergEmu.ExtractKit.ExtractKit.ExtractVzipEntriesToFiles(tempVzipPath, mappings);
                OverlayNotificationSoundsStaging.EnsureLibraryAliasesInSoundsFolder(soundsPath);
                return ValidationResult.Success();
            }
            catch (Exception ex)
            {
                return ValidationResult.Failure($"Failed to download sound files from Steam client package: {ex.Message}");
            }
            finally
            {
                TryCleanupSteamCdnHealTemp();
                // Return LOH pages after bins_win32 / sounds VZip transient buffers (net48 does not by default).
                LargeObjectHeapHelper.CompactAfterLargeTransientAllocation();
            }
        }

        // IfAbsent: download Steam client bins_win32 package and extract Steam.dll (never fork/repack Steam.dll).
        public async Task<ValidationResult> DownloadSteamDllAsync(string steamOldDirectory)
        {
            string tempVzipPath = null;
            try
            {
                if (string.IsNullOrWhiteSpace(steamOldDirectory))
                    return ValidationResult.Failure("Target directory for Steam.dll is empty.");

                string dest = Path.Combine(steamOldDirectory.Trim(), PathConstants.GoldbergSteamDllFileName);
                if (File.Exists(dest))
                    return ValidationResult.Success();

                var cdn = ServiceLocator.SteamStaticCdnPreferenceService;
                byte[] manifestBytes = await DownloadFirstAvailableAsync(
                    cdn.GetClientWin32ManifestCandidateUrls(),
                    HttpTimeoutSeconds).ConfigureAwait(false);
                string manifestText = System.Text.Encoding.UTF8.GetString(manifestBytes);
                manifestBytes = null;
                if (!SteamClientManifestHelper.TryGetBinsWin32ZipVzFileName(manifestText, out string zipVzFileName))
                    return ValidationResult.Failure("Could not resolve bins_win32 package from steam_client_win32 manifest.");

                string relativePath = SteamClientManifestHelper.BuildClientPackageRelativePath(zipVzFileName);
                tempVzipPath = CreateTempPackagePath(zipVzFileName);
                await DownloadFirstAvailableToFileAsync(
                    cdn.GetClientPackageCandidateUrls(relativePath),
                    tempVzipPath,
                    SteamDllPackageTimeoutSeconds).ConfigureAwait(false);

                Directory.CreateDirectory(steamOldDirectory.Trim());
                global::SmartGoldbergEmu.ExtractKit.ExtractKit.ExtractVzipEntriesToFiles(
                    tempVzipPath,
                    new[]
                    {
                        new KeyValuePair<string, string>(
                            SteamStaticCdnConstants.ClientBinsSteamDllEntryName,
                            dest)
                    });

                if (!File.Exists(dest) || new FileInfo(dest).Length == 0)
                    return ValidationResult.Failure("bins_win32 package did not contain Steam.dll.");

                return ValidationResult.Success();
            }
            catch (Exception ex)
            {
                return ValidationResult.Failure("Failed to download Steam.dll from Steam client package: " + ex.Message);
            }
            finally
            {
                TryCleanupSteamCdnHealTemp();
                // Return LOH pages after bins_win32 VZip transient buffers (net48 does not by default).
                LargeObjectHeapHelper.CompactAfterLargeTransientAllocation();
            }
        }

        // IfAbsent: download resources_all and extract the hashed clientui image into LocalAppData.
        public async Task<ValidationResult> DownloadSteamClientUiHashedImageAsync(string destinationFilePath)
        {
            string tempVzipPath = null;
            try
            {
                if (string.IsNullOrWhiteSpace(destinationFilePath))
                    return ValidationResult.Failure("Destination path for Steam clientui image is empty.");

                string dest = destinationFilePath.Trim();
                if (File.Exists(dest))
                    return ValidationResult.Success();

                var cdn = ServiceLocator.SteamStaticCdnPreferenceService;
                byte[] manifestBytes = await DownloadFirstAvailableAsync(
                    cdn.GetClientWin32ManifestCandidateUrls(),
                    HttpTimeoutSeconds).ConfigureAwait(false);
                string manifestText = System.Text.Encoding.UTF8.GetString(manifestBytes);
                manifestBytes = null;
                if (!SteamClientManifestHelper.TryGetResourcesAllZipVzFileName(manifestText, out string zipVzFileName))
                    return ValidationResult.Failure("Could not resolve resources_all package from steam_client_win32 manifest.");

                string relativePath = SteamClientManifestHelper.BuildClientPackageRelativePath(zipVzFileName);
                tempVzipPath = CreateTempPackagePath(zipVzFileName);
                await DownloadFirstAvailableToFileAsync(
                    cdn.GetClientPackageCandidateUrls(relativePath),
                    tempVzipPath,
                    SteamDllPackageTimeoutSeconds).ConfigureAwait(false);

                string destDirectory = Path.GetDirectoryName(dest);
                if (!string.IsNullOrEmpty(destDirectory))
                    Directory.CreateDirectory(destDirectory);

                global::SmartGoldbergEmu.ExtractKit.ExtractKit.ExtractVzipEntriesToFiles(
                    tempVzipPath,
                    new[]
                    {
                        new KeyValuePair<string, string>(
                            SteamStaticCdnConstants.ClientResourcesUiHashedImageEntryPath,
                            dest)
                    });

                if (!File.Exists(dest) || new FileInfo(dest).Length == 0)
                    return ValidationResult.Failure("resources_all package did not contain the hashed clientui image.");

                return ValidationResult.Success();
            }
            catch (Exception ex)
            {
                return ValidationResult.Failure(
                    "Failed to download Steam clientui image from resources_all package: " + ex.Message);
            }
            finally
            {
                TryCleanupSteamCdnHealTemp();
                LargeObjectHeapHelper.CompactAfterLargeTransientAllocation();
            }
        }

        public async Task<bool> DownloadAvatarAsync(string avatarPath)
        {
            try
            {
                await DownloadAndWriteAsync(AssetConstants.AccountAvatarUrl, avatarPath).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                LogRedactionHelper.WriteDebug($"Failed to download avatar from EXAMPLE: {ex.Message}");
                return false;
            }
        }

        public Task<ValidationResult> DownloadFontFilesAsync(string fontsPath)
        {
            return DownloadMissingFilesAsync(
                fontsPath,
                new[] { (AssetConstants.FontRobotoUrl, PathConstants.GoldbergGlobalDefaultOverlayFontFileName) },
                "Failed to download font files from GitHub");
        }

        public Task<ValidationResult> DownloadControllerGlyphsAsync(string glyphsPath)
        {
            var glyphBase = AssetConstants.ControllerGlyphsBaseUrl;
            return DownloadMissingFilesAsync(
                glyphsPath,
                new[]
                {
                    (glyphBase + "xbox_button_select.png", "xbox_button_select.png"),
                    (glyphBase + "xbox_button_start.png", "xbox_button_start.png"),
                    (glyphBase + "button_b.png", "button_b.png"),
                    (glyphBase + "button_a.png", "button_a.png"),
                    (glyphBase + "button_x.png", "button_x.png"),
                    (glyphBase + "button_y.png", "button_y.png"),
                    (glyphBase + "stick_dpad_e.png", "stick_dpad_e.png"),
                    (glyphBase + "stick_dpad_s.png", "stick_dpad_s.png"),
                    (glyphBase + "stick_dpad_w.png", "stick_dpad_w.png"),
                    (glyphBase + "stick_dpad_n.png", "stick_dpad_n.png"),
                    (glyphBase + "trigger_r_pull.png", "trigger_r_pull.png"),
                    (glyphBase + "trigger_l_pull.png", "trigger_l_pull.png"),
                    (glyphBase + "shoulder_r.png", "shoulder_r.png"),
                    (glyphBase + "shoulder_l.png", "shoulder_l.png")
                },
                "Failed to download controller glyphs from GitHub");
        }

        private static async Task<ValidationResult> DownloadMissingFilesAsync(
            string directory,
            (string Url, string FileName)[] files,
            string batchFailureMessage)
        {
            try
            {
                foreach (var file in files)
                {
                    var destPath = Path.Combine(directory, file.FileName);
                    if (File.Exists(destPath))
                        continue;

                    try
                    {
                        await DownloadAndWriteAsync(file.Url, destPath).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        return ValidationResult.Failure($"Failed to download {file.FileName}: {ex.Message}");
                    }
                }

                return ValidationResult.Success();
            }
            catch (Exception ex)
            {
                return ValidationResult.Failure($"{batchFailureMessage}: {ex.Message}");
            }
        }

        private static async Task<byte[]> DownloadFirstAvailableAsync(
            IReadOnlyList<string> candidateUrls,
            int timeoutSeconds)
        {
            if (candidateUrls == null || candidateUrls.Count == 0)
                throw new InvalidOperationException("No CDN candidate URLs are configured.");

            Exception lastError = null;
            foreach (var url in candidateUrls)
            {
                if (string.IsNullOrWhiteSpace(url))
                    continue;

                try
                {
                    return await HttpHelpers.GetByteArrayAsync(url, timeoutSeconds).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    lastError = ex;
                }
            }

            throw lastError ?? new InvalidOperationException("All CDN candidate URLs failed.");
        }

        private static async Task DownloadFirstAvailableToFileAsync(
            IReadOnlyList<string> candidateUrls,
            string destPath,
            int timeoutSeconds)
        {
            if (candidateUrls == null || candidateUrls.Count == 0)
                throw new InvalidOperationException("No CDN candidate URLs are configured.");
            if (string.IsNullOrWhiteSpace(destPath))
                throw new ArgumentException("Destination path is required.", nameof(destPath));

            string directory = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            Exception lastError = null;
            using (IHttpService http = HttpServiceFactory.Create(TimeSpan.FromSeconds(timeoutSeconds)))
            {
                foreach (var url in candidateUrls)
                {
                    if (string.IsNullOrWhiteSpace(url))
                        continue;

                    try
                    {
                        await HttpHelpers.DownloadFileAtomicAsync(http, url, destPath).ConfigureAwait(false);
                        return;
                    }
                    catch (Exception ex)
                    {
                        lastError = ex;
                        TryDeleteFile(destPath);
                    }
                }
            }

            throw lastError ?? new InvalidOperationException("All CDN candidate URLs failed.");
        }

        private static async Task DownloadAndWriteAsync(string url, string destPath)
        {
            string directory = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using (IHttpService http = HttpServiceFactory.Create(TimeSpan.FromSeconds(HttpTimeoutSeconds)))
            {
                await HttpHelpers.DownloadFileAtomicAsync(http, url, destPath).ConfigureAwait(false);
            }
        }

        private static string GetSteamCdnHealTempFolder()
        {
            return Path.Combine(
                PathConstants.AppBaseDirectory,
                PathConstants.LauncherUpdateTempFolderName,
                SteamCdnHealTempFolderName);
        }

        private static string CreateTempPackagePath(string fileName)
        {
            string safeName = string.IsNullOrWhiteSpace(fileName)
                ? "package.zip.vzip"
                : Path.GetFileName(fileName.Trim());
            string folder = GetSteamCdnHealTempFolder();
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, safeName);
        }

        // Removes steam-cdn-heal entirely (packages included) and empty parent temp\.
        private static void TryCleanupSteamCdnHealTemp()
        {
            TryDeleteDirectory(GetSteamCdnHealTempFolder());

            string tempRoot = Path.Combine(PathConstants.AppBaseDirectory, PathConstants.LauncherUpdateTempFolderName);
            TryDeleteEmptyDirectory(tempRoot);
        }

        private static void TryDeleteFile(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch
            {
            }
        }

        private static void TryDeleteEmptyDirectory(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                if (!Directory.Exists(path))
                    return;
                if (Directory.GetFileSystemEntries(path).Length != 0)
                    return;
                Directory.Delete(path, false);
            }
            catch
            {
            }
        }
    }
}
