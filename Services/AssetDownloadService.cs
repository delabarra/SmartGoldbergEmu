using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.ExtractKit;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Services
{
    public class AssetDownloadService
    {
        private const int HttpTimeoutSeconds = 30;

        public async Task<ValidationResult> DownloadSoundFilesAsync(string soundsPath)
        {
            try
            {
                var achievementPath = Path.Combine(soundsPath, PathConstants.SteamClientUiAchievementNotificationWav);
                var friendPath = Path.Combine(soundsPath, PathConstants.SteamClientUiFriendNotificationWav);
                bool needsAchievement = !File.Exists(achievementPath);
                bool needsFriend = !File.Exists(friendPath);

                if (!needsAchievement && !needsFriend)
                    return ValidationResult.Success();

                var packageBytes = await DownloadFirstAvailableAsync(
                    ServiceLocator.SteamStaticCdnPreferenceService.GetClientSoundsPackageCandidateUrls(),
                    HttpTimeoutSeconds).ConfigureAwait(false);

                if (needsAchievement)
                {
                    var data = global::SmartGoldbergEmu.ExtractKit.ExtractKit.ExtractVzipEntry(
                        packageBytes,
                        AssetConstants.SteamClientAchievementSoundInnerPath);
                    await Task.Run(() => File.WriteAllBytes(achievementPath, data)).ConfigureAwait(false);
                }

                if (needsFriend)
                {
                    var data = global::SmartGoldbergEmu.ExtractKit.ExtractKit.ExtractVzipEntry(
                        packageBytes,
                        AssetConstants.SteamClientFriendSoundInnerPath);
                    await Task.Run(() => File.WriteAllBytes(friendPath, data)).ConfigureAwait(false);
                }

                return ValidationResult.Success();
            }
            catch (Exception ex)
            {
                return ValidationResult.Failure($"Failed to download sound files from Steam client package: {ex.Message}");
            }
        }

        public async Task<bool> DownloadAvatarAsync(string avatarPath)
        {
            try
            {
                await DownloadFirstAvailableToFileAsync(
                    ServiceLocator.SteamStaticCdnPreferenceService.GetDefaultAvatarCandidateUrls(),
                    avatarPath);
                return true;
            }
            catch (Exception ex)
            {
                LogRedactionHelper.WriteDebug($"Failed to download avatar from configured source: {ex.Message}");
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
                        await DownloadAndWriteAsync(file.Url, destPath);
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
            string destPath)
        {
            var content = await DownloadFirstAvailableAsync(candidateUrls, HttpTimeoutSeconds).ConfigureAwait(false);
            await Task.Run(() => File.WriteAllBytes(destPath, content)).ConfigureAwait(false);
        }

        private static async Task DownloadAndWriteAsync(string url, string destPath)
        {
            var content = await HttpHelpers.GetByteArrayAsync(url, HttpTimeoutSeconds);
            await Task.Run(() => File.WriteAllBytes(destPath, content));
        }
    }
}
