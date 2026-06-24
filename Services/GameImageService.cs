using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using SmartGoldbergEmu;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;
using SteamKit;

namespace SmartGoldbergEmu.Services
{
    public class GameImageService : IDisposable
    {
        private readonly IHttpService _httpService;
        private readonly string _gamesDirectory;
        private readonly ITaskReportService _taskReportService;
        private readonly ImageNormalizationService _imageNormalizationService;
        private readonly FallbackMosaicArtCache _fallbackMosaicArtCache = new FallbackMosaicArtCache();
        private bool _disposed;

        private ITaskReportService Feedback => _taskReportService ?? ServiceLocator.TaskReportService;

        public GameImageService() : this(HttpServiceFactory.Create(TimeSpan.FromSeconds(30)), null, null)
        {
        }

        public GameImageService(
            IHttpService httpService,
            ITaskReportService feedbackService = null,
            ImageNormalizationService imageNormalizationService = null)
        {
            _httpService = httpService ?? throw new ArgumentNullException(nameof(httpService));
            _taskReportService = feedbackService;
            _imageNormalizationService = imageNormalizationService ?? ServiceLocator.ImageNormalizationService;
            _gamesDirectory = PathConstants.GamesDirectory;
        }

        public async Task<bool> DownloadGameImagesAsync(
            ulong appId,
            OnlineAppData metadata = null,
            bool reportFeedback = true,
            ulong? steamAppIdForRemoteAssets = null,
            KeyValue appPicsData = null,
            string gameDisplayName = null)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(GameImageService));

            var remoteAppId = steamAppIdForRemoteAssets ?? appId;
            if (remoteAppId == 0)
                return true;

            var displayName = ResolveGameDisplayName(gameDisplayName, metadata, appId);

            try
            {
                const int totalDownloads = 4;

                var gamePath = PathConstants.CombineGamesPerAppResourcesDirectory(_gamesDirectory, appId.ToString());
                Directory.CreateDirectory(gamePath);

                var picsData = ResolvePicsDataForImageDownload(appId, appPicsData, _gamesDirectory);

                if (reportFeedback)
                {
                    Feedback?.SetMessage(picsData != null
                        ? "Downloading game images (game assets)..."
                        : "Downloading game images...");
                    Feedback?.SetProgress(0, totalDownloads);
                }

                var headerPrimaryUrl = TryBuildHeaderImageUrl(picsData, remoteAppId);
                var headerAlternateUrl = BuildFastlyStoreAssetFileUrl(
                    remoteAppId,
                    PathConstants.SteamGameResourcesHeaderImageFileName);
                var clientIconHash = TryResolveClientIconHash(picsData);
                var iconPrimaryUrl = TryBuildCommunityAssetsClientIconUrl(remoteAppId, clientIconHash);
                string iconAlternateUrl = null;
                string iconThirdUrl = null;
                var appIconHash = TryExtractPicsSha1Hash(picsData, SteamPicsKeyNames.Icon);
                if (!string.IsNullOrWhiteSpace(appIconHash)
                    && !string.Equals(appIconHash, clientIconHash, StringComparison.OrdinalIgnoreCase))
                {
                    iconThirdUrl = TryBuildCommunityAssetsClientIconUrl(remoteAppId, appIconHash);
                }
                var coverPrimaryUrl = TryBuildLibraryCapsuleImageUrl(picsData, remoteAppId);
                var coverAlternateUrl = BuildFastlyStoreAssetFileUrl(
                    remoteAppId,
                    PathConstants.SteamGameResourcesLegacyLibraryCapsuleImageFileName);
                var logoPrimaryUrl = TryBuildLibraryLogoImageUrl(picsData, remoteAppId);
                var logoAlternateUrl = BuildFastlyStoreAssetFileUrl(
                    remoteAppId,
                    PathConstants.SteamGameResourcesLibraryLogoImageFileName);

                if (_disposed)
                    return ApplyDownloadOutcomeFeedback(gamePath, totalDownloads, displayName, appId, reportFeedback: false);

                var completed = 0;
                var lockObj = new object();

                async Task RunWithProgressAsync(Func<Task> work)
                {
                    await work().ConfigureAwait(false);
                    if (_disposed || !reportFeedback)
                        return;
                    lock (lockObj)
                    {
                        if (_disposed)
                            return;
                        completed++;
                        Feedback?.SetProgress(completed, totalDownloads);
                        Feedback?.SetMessage($"Downloading images... {completed}/{totalDownloads}");
                    }
                }

                var tasks = new List<Task>(totalDownloads)
                {
                    RunWithProgressAsync(() => DownloadImageAsync(remoteAppId, gamePath, PathConstants.SteamGameResourcesHeaderImageFileName, headerPrimaryUrl, headerAlternateUrl)),
                    RunWithProgressAsync(() => DownloadImageAsync(
                        remoteAppId,
                        gamePath,
                        PathConstants.SteamGameResourcesCapsuleCoverImageFileName,
                        coverPrimaryUrl,
                        coverAlternateUrl)),
                    RunWithProgressAsync(() => DownloadImageAsync(remoteAppId, gamePath, PathConstants.SteamGameResourcesLibraryLogoImageFileName, logoPrimaryUrl, logoAlternateUrl)),
                    RunWithProgressAsync(() => DownloadImageAsync(
                        remoteAppId,
                        gamePath,
                        PathConstants.GetSteamGameResourcesClientIconFileName(appId),
                        iconPrimaryUrl,
                        iconAlternateUrl,
                        iconThirdUrl))
                };

                await Task.WhenAll(tasks).ConfigureAwait(false);
                return ApplyDownloadOutcomeFeedback(gamePath, totalDownloads, displayName, appId, reportFeedback && !_disposed);
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError($"Error downloading game images (folder {appId}, Steam {remoteAppId}): {ex.Message}", ex);
                if (reportFeedback && !_disposed)
                    Feedback?.SetMessage("Could not download game images.", TaskReportKind.Error);
                if (appId > 0)
                {
                    var resourcesPath = PathConstants.CombineGamesPerAppResourcesDirectory(_gamesDirectory, appId.ToString());
                    UpdateMissingAssetsNote(resourcesPath, displayName, appId);
                }
                return false;
            }
        }

        public Task EnsureMosaicFallbackForViewAsync(string viewMode, ThemeMode effectiveTheme, Color background, Color foreground)
        {
            if (_disposed)
                return Task.CompletedTask;
            return _fallbackMosaicArtCache.EnsureForViewModeAsync(viewMode, effectiveTheme, background, foreground);
        }

        public Bitmap TryCloneMosaicFallbackBitmap()
        {
            if (_disposed)
                return null;
            return _fallbackMosaicArtCache.TryCloneForImageList();
        }

        public string GetCapsuleImagePathOrFallback(ulong appId)
        {
            var path = GetImagePath(appId, PathConstants.SteamGameResourcesCapsuleCoverImageFileName);
            if (!string.IsNullOrEmpty(path))
                return path;

            // Backward compatibility for already-downloaded assets with legacy names.
            return GetImagePath(appId, PathConstants.SteamGameResourcesLegacyLibraryCapsuleImageFileName);
        }

        public string GetLogoImagePathOrFallback(ulong appId)
        {
            var logoPath = GetImagePath(appId, PathConstants.SteamGameResourcesLibraryLogoImageFileName);
            if (!string.IsNullOrEmpty(logoPath))
                return logoPath;

            return GetCapsuleImagePathOrFallback(appId);
        }

        public void NormalizeResolvedLogoForLogosListIfNeeded(string imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
                return;
            if (!string.Equals(Path.GetFileName(imagePath), PathConstants.SteamGameResourcesLibraryLogoImageFileName, StringComparison.OrdinalIgnoreCase))
                return;
            _imageNormalizationService.EnsureLogoFileNormalizedForLogosView(imagePath);
        }

        public string GetImagePath(ulong appId, string imageName)
        {
            if (string.IsNullOrEmpty(imageName))
                return null;
            var imagePath = Path.Combine(PathConstants.CombineGamesPerAppResourcesDirectory(_gamesDirectory, appId.ToString()), imageName);
            return File.Exists(imagePath) ? imagePath : null;
        }

        public bool ImageExists(ulong appId, string imageName)
        {
            return GetImagePath(appId, imageName) != null;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _httpService?.Dispose();
            _fallbackMosaicArtCache.Dispose();
        }

        private bool ApplyDownloadOutcomeFeedback(
            string resourcesDirectory,
            int totalDownloads,
            string gameDisplayName,
            ulong appId,
            bool reportFeedback)
        {
            UpdateMissingAssetsNote(resourcesDirectory, gameDisplayName, appId);

            bool hasHeader = ResourceFileExists(resourcesDirectory, PathConstants.SteamGameResourcesHeaderImageFileName);
            bool hasIcon = ResourceFileExists(resourcesDirectory, PathConstants.GetSteamGameResourcesClientIconFileName(appId));
            bool hasCapsule = ResourceFileExists(resourcesDirectory, PathConstants.SteamGameResourcesCapsuleCoverImageFileName)
                || ResourceFileExists(resourcesDirectory, PathConstants.SteamGameResourcesLegacyLibraryCapsuleImageFileName);
            bool essentialsOk = hasHeader && hasIcon && hasCapsule;

            if (!reportFeedback)
                return essentialsOk;

            if (_disposed)
                return essentialsOk;

            if (essentialsOk)
            {
                Feedback?.SetMessage("Game images downloaded successfully");
                Feedback?.SetProgress(totalDownloads, totalDownloads);
                return true;
            }

            var missing = new List<string>(4);
            if (!hasHeader)
                missing.Add(PathConstants.SteamGameResourcesHeaderImageFileName);
            if (!hasCapsule)
                missing.Add($"{PathConstants.SteamGameResourcesCapsuleCoverImageFileName} or {PathConstants.SteamGameResourcesLegacyLibraryCapsuleImageFileName}");
            if (!hasIcon)
                missing.Add(PathConstants.GetSteamGameResourcesClientIconFileName(appId));
            Program.LogService?.LogWarning(
                $"Game image download finished with missing files under resources: {string.Join(", ", missing)}");
            Feedback?.SetMessage("Some game images could not be downloaded.", TaskReportKind.Warning);
            Feedback?.SetProgress(totalDownloads, totalDownloads);
            return false;
        }

        private static bool ResourceFileExists(string resourcesDirectory, string fileName)
        {
            if (string.IsNullOrEmpty(resourcesDirectory) || string.IsNullOrEmpty(fileName))
                return false;
            return File.Exists(Path.Combine(resourcesDirectory, fileName));
        }

        private static bool HasCapsuleResource(string resourcesDirectory)
        {
            return ResourceFileExists(resourcesDirectory, PathConstants.SteamGameResourcesCapsuleCoverImageFileName)
                || ResourceFileExists(resourcesDirectory, PathConstants.SteamGameResourcesLegacyLibraryCapsuleImageFileName);
        }

        private static List<string> CollectMissingLibraryArtworkFileNames(string resourcesDirectory)
        {
            var missing = new List<string>(3);
            if (!ResourceFileExists(resourcesDirectory, PathConstants.SteamGameResourcesHeaderImageFileName))
                missing.Add(PathConstants.SteamGameResourcesHeaderImageFileName);
            if (!HasCapsuleResource(resourcesDirectory))
                missing.Add(PathConstants.SteamGameResourcesCapsuleCoverImageFileName);
            if (!ResourceFileExists(resourcesDirectory, PathConstants.SteamGameResourcesLibraryLogoImageFileName))
                missing.Add(PathConstants.SteamGameResourcesLibraryLogoImageFileName);
            return missing;
        }

        private static string ResolveGameDisplayName(string gameDisplayName, OnlineAppData metadata, ulong appId)
        {
            if (!string.IsNullOrWhiteSpace(gameDisplayName))
                return gameDisplayName.Trim();
            if (!string.IsNullOrWhiteSpace(metadata?.Name))
                return metadata.Name.Trim();
            return appId > 0 ? $"App {appId}" : "this game";
        }

        private void UpdateMissingAssetsNote(string resourcesDirectory, string gameDisplayName, ulong appId)
        {
            if (appId == 0)
                return;
            if (string.IsNullOrEmpty(resourcesDirectory))
                return;

            var notePath = Path.Combine(resourcesDirectory, PathConstants.SteamGameResourcesMissingAssetsNoteFileName);
            var missingArtwork = CollectMissingLibraryArtworkFileNames(resourcesDirectory);
            if (missingArtwork.Count == 0)
            {
                TryDeleteFileIfExists(notePath);
                return;
            }

            var missingList = string.Join(", ", missingArtwork);
            var lineBreak = Environment.NewLine;
            var message = string.Format(
                "The following assets were missing for {0}: {1}.{2}" +
                "Search and download any favorites from {3} or elsewhere,{2}" +
                "then save them in this folder using the exact file names listed above so SmartGoldbergEmu can load them.",
                gameDisplayName,
                missingList,
                lineBreak,
                ApplicationConstants.SteamGridDbHomeUrl);
            try
            {
                Directory.CreateDirectory(resourcesDirectory);
                File.WriteAllText(notePath, message);
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning(
                    $"Could not write {PathConstants.SteamGameResourcesMissingAssetsNoteFileName} for app {appId}: {ex.Message}");
            }
        }

        private static void TryDeleteFileIfExists(string path)
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

        private async Task<bool> TryDownloadImageFromUrlAsync(string url, string imagePath, string normalizeForFileName)
        {
            if (_disposed)
                return false;
            if (string.IsNullOrEmpty(url))
                return false;

            try
            {
                await _httpService.DownloadFileAsync(url, imagePath);
                if (_disposed)
                    return false;
                NormalizeImageFileIfNeeded(normalizeForFileName, imagePath);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task DownloadImageAsync(
            ulong appId,
            string gamePath,
            string fileName,
            string primaryUrl,
            string alternateUrl = null,
            string thirdUrl = null)
        {
            if (_disposed)
                return;

            var imagePath = Path.Combine(gamePath, fileName);
            if (File.Exists(imagePath))
            {
                NormalizeImageFileIfNeeded(fileName, imagePath);
                return;
            }

            foreach (var url in new[] { primaryUrl, alternateUrl, thirdUrl })
            {
                if (!await TryDownloadImageFromUrlAsync(url, imagePath, fileName))
                    continue;
                Program.LogService?.LogMessage($"Downloaded {fileName} for App ID {appId}");
                return;
            }
        }

        private void NormalizeImageFileIfNeeded(string fileName, string imagePath)
        {
            if (string.Equals(fileName, PathConstants.SteamGameResourcesLibraryLogoImageFileName, StringComparison.OrdinalIgnoreCase))
            {
                _imageNormalizationService.EnsureLogoFileNormalizedForLogosView(imagePath);
                return;
            }

            if (string.Equals(fileName, PathConstants.SteamGameResourcesHeaderImageFileName, StringComparison.OrdinalIgnoreCase))
            {
                _imageNormalizationService.EnsureHeaderFileNormalizedForSteamBounds(imagePath);
                return;
            }

            if (string.Equals(fileName, PathConstants.SteamGameResourcesCapsuleCoverImageFileName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(fileName, PathConstants.SteamGameResourcesLegacyLibraryCapsuleImageFileName, StringComparison.OrdinalIgnoreCase))
            {
                _imageNormalizationService.EnsureCompactTileFileNormalizedForCompactTilesView(imagePath);
            }
        }

        private static string TryResolveClientIconHash(KeyValue appPicsData)
        {
            return TryExtractPicsSha1Hash(appPicsData, SteamPicsKeyNames.ClientIcon)
                ?? TryExtractPicsSha1Hash(appPicsData, SteamPicsKeyNames.Icon);
        }

        private static string TryExtractPicsSha1Hash(KeyValue appPicsData, string picsKeyName)
        {
            if (appPicsData == null || string.IsNullOrWhiteSpace(picsKeyName))
                return null;

            var appInfoTarget = SteamPicsKeyValueHelper.ResolveAppInfoTarget(appPicsData);
            var common = SteamPicsKeyValueHelper.FindChild(appInfoTarget, PathConstants.SteamAppsCommonDirectoryName);
            var hashNode = SteamPicsKeyValueHelper.FindChild(common, picsKeyName);
            if (string.IsNullOrWhiteSpace(hashNode?.Value))
                return null;

            var hash = hashNode.Value.Trim();
            return IsSha1HexHash(hash) ? hash : null;
        }

        private static bool IsSha1HexHash(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != 40)
                return false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if ((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'))
                    continue;
                return false;
            }
            return true;
        }

        private static string BuildFastlyStoreAssetFileUrl(ulong appId, string pathOrFileName)
        {
            if (appId == 0 || string.IsNullOrWhiteSpace(pathOrFileName))
                return null;
            return string.Format(ApplicationConstants.SteamStoreAssetFileUrlFormat, appId, pathOrFileName.TrimStart('/'));
        }

        private static string TryBuildCommunityAssetsClientIconUrl(ulong appId, string hash)
        {
            if (appId == 0 || string.IsNullOrWhiteSpace(hash))
                return null;
            return string.Format(ApplicationConstants.SteamCommunityAssetsClientIconIcoUrlFormat, appId, hash);
        }

        private static string TryBuildLibraryCapsuleImageUrl(KeyValue appPicsData, ulong appId)
        {
            var relativePath = TryExtractLibraryCapsuleImageRelativePath(appPicsData);
            return BuildFastlyStoreAssetFileUrl(appId, relativePath);
        }

        private static string TryBuildHeaderImageUrl(KeyValue appPicsData, ulong appId)
        {
            var relativePath = TryExtractHeaderImageRelativePath(appPicsData);
            return BuildFastlyStoreAssetFileUrl(appId, relativePath);
        }

        private static string TryExtractLibraryCapsuleImageRelativePath(KeyValue appPicsData)
        {
            if (appPicsData == null)
                return null;

            var appInfoTarget = SteamPicsKeyValueHelper.ResolveAppInfoTarget(appPicsData);
            var common = SteamPicsKeyValueHelper.FindChild(appInfoTarget, PathConstants.SteamAppsCommonDirectoryName);
            var libraryAssetsFull = SteamPicsKeyValueHelper.FindChild(common, SteamPicsKeyNames.LibraryAssetsFull);
            var libraryCapsule = SteamPicsKeyValueHelper.FindChild(libraryAssetsFull, SteamPicsKeyNames.LibraryCapsule);
            var image = SteamPicsKeyValueHelper.FindChild(libraryCapsule, SteamPicsKeyNames.Image);

            if (image == null)
                return null;

            // Prefer English when available, otherwise take the first non-empty localized entry.
            var english = SteamPicsKeyValueHelper.FindChild(image, SteamPicsKeyNames.English);
            if (!string.IsNullOrWhiteSpace(english?.Value))
                return english.Value.Trim();

            if (image.Children == null || image.Children.Count == 0)
                return string.IsNullOrWhiteSpace(image.Value) ? null : image.Value.Trim();

            foreach (var child in image.Children)
            {
                if (!string.IsNullOrWhiteSpace(child?.Value))
                    return child.Value.Trim();
            }

            return null;
        }

        private static string TryExtractHeaderImageRelativePath(KeyValue appPicsData)
        {
            if (appPicsData == null)
                return null;

            var appInfoTarget = SteamPicsKeyValueHelper.ResolveAppInfoTarget(appPicsData);
            var common = SteamPicsKeyValueHelper.FindChild(appInfoTarget, PathConstants.SteamAppsCommonDirectoryName);
            var headerImage = SteamPicsKeyValueHelper.FindChild(common, SteamPicsKeyNames.HeaderImage);

            if (headerImage == null)
                return null;

            // Prefer English when available, otherwise take the first non-empty localized entry.
            var english = SteamPicsKeyValueHelper.FindChild(headerImage, SteamPicsKeyNames.English);
            if (!string.IsNullOrWhiteSpace(english?.Value))
                return english.Value.Trim();

            if (headerImage.Children == null || headerImage.Children.Count == 0)
                return string.IsNullOrWhiteSpace(headerImage.Value) ? null : headerImage.Value.Trim();

            foreach (var child in headerImage.Children)
            {
                if (!string.IsNullOrWhiteSpace(child?.Value))
                    return child.Value.Trim();
            }

            return null;
        }

        private static KeyValue ResolvePicsDataForImageDownload(ulong appId, KeyValue appPicsData, string gamesDirectory)
        {
            if (appPicsData != null)
                return appPicsData;
            return TryLoadExportedAppPicsFromResources(appId, gamesDirectory);
        }

        private static KeyValue TryLoadExportedAppPicsFromResources(ulong appId, string gamesDirectory)
        {
            return SteamPicsKeyValueHelper.TryLoadExportedAppPicsFromValveFile(gamesDirectory, appId);
        }

        private static string TryBuildLibraryLogoImageUrl(KeyValue appPicsData, ulong appId)
        {
            var relativePath = TryExtractLibraryLogoImageRelativePath(appPicsData);
            return BuildFastlyStoreAssetFileUrl(appId, relativePath);
        }

        private static string TryExtractLibraryLogoImageRelativePath(KeyValue appPicsData)
        {
            if (appPicsData == null)
                return null;

            var appInfoTarget = SteamPicsKeyValueHelper.ResolveAppInfoTarget(appPicsData);
            var common = SteamPicsKeyValueHelper.FindChild(appInfoTarget, PathConstants.SteamAppsCommonDirectoryName);
            var libraryAssetsFull = SteamPicsKeyValueHelper.FindChild(common, SteamPicsKeyNames.LibraryAssetsFull);
            var libraryLogo = SteamPicsKeyValueHelper.FindChild(libraryAssetsFull, SteamPicsKeyNames.LibraryLogo);
            var image = SteamPicsKeyValueHelper.FindChild(libraryLogo, SteamPicsKeyNames.Image);

            if (image == null)
                return null;

            var english = SteamPicsKeyValueHelper.FindChild(image, SteamPicsKeyNames.English);
            if (!string.IsNullOrWhiteSpace(english?.Value))
                return english.Value.Trim();

            if (image.Children == null || image.Children.Count == 0)
                return string.IsNullOrWhiteSpace(image.Value) ? null : image.Value.Trim();

            foreach (var child in image.Children)
            {
                if (!string.IsNullOrWhiteSpace(child?.Value))
                    return child.Value.Trim();
            }

            return null;
        }

    }
}
