using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
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
        private static readonly string[] HeaderPreferredFileNames =
        {
            "library_header_2x.jpg",
            "library_header.jpg",
            PathConstants.SteamGameResourcesHeaderImageFileName
        };

        private static readonly string[] CoverPreferredFileNames =
        {
            "library_600x900_2x.jpg",
            PathConstants.SteamGameResourcesLegacyLibraryCapsuleImageFileName,
            "library_capsule.jpg",
            "capsule_231x87.jpg"
        };

        private static readonly string[] LogoPreferredFileNames =
        {
            "logo_2x.png",
            PathConstants.SteamGameResourcesLibraryLogoImageFileName
        };

        private sealed class AssetDownloadRequest
        {
            public string FileName { get; set; }
            public string[] CandidateUrls { get; set; }
        }

        private readonly IHttpService _httpService;
        private readonly string _gamesDirectory;
        private readonly ITaskReportService _taskReportService;
        private readonly FallbackMosaicArtCache _fallbackMosaicArtCache = new FallbackMosaicArtCache();
        private bool _disposed;

        private ITaskReportService Feedback => _taskReportService ?? ServiceLocator.TaskReportService;

        public GameImageService() : this(HttpServiceFactory.Create(TimeSpan.FromSeconds(30)), null)
        {
        }

        public GameImageService(
            IHttpService httpService,
            ITaskReportService feedbackService = null)
        {
            _httpService = httpService ?? throw new ArgumentNullException(nameof(httpService));
            _taskReportService = feedbackService;
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
                var gamePath = PathConstants.CombineGamesPerAppResourcesDirectory(_gamesDirectory, appId.ToString());
                Directory.CreateDirectory(gamePath);

                var picsData = ResolvePicsDataForImageDownload(appId, appPicsData, _gamesDirectory);
                var downloadRequests = BuildAssetDownloadRequests(picsData, remoteAppId, appId);
                var totalDownloads = downloadRequests.Count;

                if (reportFeedback)
                {
                    Feedback?.SetMessage(picsData != null
                        ? $"Downloading game assets ({totalDownloads} files)..."
                        : "Downloading game images...");
                    Feedback?.SetProgress(0, Math.Max(totalDownloads, 1));
                }

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
                        Feedback?.SetProgress(completed, Math.Max(totalDownloads, 1));
                        Feedback?.SetMessage($"Downloading assets... {completed}/{totalDownloads}");
                    }
                }

                var tasks = new List<Task>(totalDownloads);
                foreach (var request in downloadRequests)
                {
                    tasks.Add(RunWithProgressAsync(() => DownloadImageAsync(
                        remoteAppId,
                        gamePath,
                        request.FileName,
                        request.CandidateUrls)));
                }

                await Task.WhenAll(tasks).ConfigureAwait(false);
                EnsureCanonicalUiFiles(gamePath, appId, picsData);
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

        private async Task<bool> TryDownloadImageFromUrlAsync(string url, string imagePath)
        {
            if (_disposed)
                return false;
            if (string.IsNullOrEmpty(url))
                return false;

            try
            {
                await _httpService.DownloadFileAsync(url, imagePath);
                return !_disposed;
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
            params string[] candidateUrls)
        {
            if (_disposed)
                return;

            var imagePath = Path.Combine(gamePath, fileName);
            if (File.Exists(imagePath))
                return;

            if (candidateUrls == null || candidateUrls.Length == 0)
                return;

            foreach (var url in candidateUrls)
            {
                if (!await TryDownloadImageFromUrlAsync(url, imagePath))
                    continue;
                Program.LogService?.LogMessage($"Downloaded {fileName} for App ID {appId}");
                return;
            }
        }

        private static List<AssetDownloadRequest> BuildAssetDownloadRequests(
            KeyValue picsData,
            ulong remoteAppId,
            ulong appId)
        {
            var requests = new List<AssetDownloadRequest>();
            var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (picsData != null)
            {
                var appInfoTarget = SteamPicsKeyValueHelper.ResolveAppInfoTarget(picsData);
                var common = SteamPicsKeyValueHelper.FindChild(appInfoTarget, PathConstants.SteamAppsCommonDirectoryName);
                var relativePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                CollectDownloadableAssetReferences(common, relativePaths);

                foreach (var relativePath in relativePaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
                {
                    var fileName = Path.GetFileName(relativePath);
                    if (string.IsNullOrWhiteSpace(fileName) || !seenFileNames.Add(fileName))
                        continue;

                    var candidateUrls = BuildStoreAssetCandidateUrls(remoteAppId, relativePath);
                    if (candidateUrls.Count == 0)
                        continue;

                    foreach (var url in candidateUrls)
                        seenUrls.Add(url);

                    requests.Add(new AssetDownloadRequest
                    {
                        FileName = fileName,
                        CandidateUrls = candidateUrls.ToArray()
                    });
                }

                CollectLogoHashDownloadRequests(requests, picsData, remoteAppId, seenUrls, seenFileNames);

                foreach (var hash in CollectUniqueIconHashes(picsData))
                {
                    var url = TryBuildCommunityAssetsClientIconUrl(remoteAppId, hash);
                    if (string.IsNullOrWhiteSpace(url) || !seenUrls.Add(url))
                        continue;

                    var iconFileName = hash + PathConstants.SteamGameResourcesClientIconFileExtension;
                    if (!seenFileNames.Add(iconFileName))
                        continue;

            requests.Add(new AssetDownloadRequest
            {
                FileName = iconFileName,
                CandidateUrls = new[] { url }
            });
                }
            }

            AppendEssentialCanonicalDownloadRequests(requests, picsData, remoteAppId, appId, seenUrls, seenFileNames);

            return requests;
        }

        private static void AppendEssentialCanonicalDownloadRequests(
            List<AssetDownloadRequest> requests,
            KeyValue picsData,
            ulong remoteAppId,
            ulong appId,
            HashSet<string> seenUrls,
            HashSet<string> seenFileNames)
        {
            AddFallbackRequest(
                requests,
                seenUrls,
                seenFileNames,
                PathConstants.SteamGameResourcesHeaderImageFileName,
                BuildPreferredStoreAssetUrls(
                    remoteAppId,
                    TryExtractHeaderImageRelativePath(picsData),
                    HeaderPreferredFileNames));

            AddFallbackRequest(
                requests,
                seenUrls,
                seenFileNames,
                PathConstants.SteamGameResourcesCapsuleCoverImageFileName,
                BuildPreferredStoreAssetUrls(
                    remoteAppId,
                    TryExtractLibraryCapsuleImageRelativePath(picsData)
                        ?? TryExtractSmallCapsuleRelativePath(picsData),
                    CoverPreferredFileNames));

            AddFallbackRequest(
                requests,
                seenUrls,
                seenFileNames,
                PathConstants.SteamGameResourcesLibraryLogoImageFileName,
                BuildLogoCanonicalCandidateUrls(picsData, remoteAppId));

            var clientIconHash = TryResolveClientIconHash(picsData);
            var iconUrls = new List<string>();
            AddCandidate(iconUrls, TryBuildCommunityAssetsClientIconUrl(remoteAppId, clientIconHash));
            var appIconHash = TryExtractPicsSha1Hash(picsData, SteamPicsKeyNames.Icon);
            if (!string.IsNullOrWhiteSpace(appIconHash)
                && !string.Equals(appIconHash, clientIconHash, StringComparison.OrdinalIgnoreCase))
            {
                AddCandidate(iconUrls, TryBuildCommunityAssetsClientIconUrl(remoteAppId, appIconHash));
            }

            AddFallbackRequest(
                requests,
                seenUrls,
                seenFileNames,
                PathConstants.GetSteamGameResourcesClientIconFileName(appId),
                iconUrls);
        }

        private static List<string> BuildLogoCanonicalCandidateUrls(KeyValue picsData, ulong remoteAppId)
        {
            var candidates = BuildPreferredStoreAssetUrls(
                remoteAppId,
                TryExtractLibraryLogoImageRelativePath(picsData),
                LogoPreferredFileNames);

            var logoHash = TryExtractPicsSha1Hash(picsData, SteamPicsKeyNames.Logo);
            AddCandidate(candidates, TryBuildCommunityAssetsAppImageUrl(remoteAppId, logoHash));
            return candidates;
        }

        private static string TryExtractSmallCapsuleRelativePath(KeyValue picsData)
        {
            if (picsData == null)
                return null;

            var appInfoTarget = SteamPicsKeyValueHelper.ResolveAppInfoTarget(picsData);
            var common = SteamPicsKeyValueHelper.FindChild(appInfoTarget, PathConstants.SteamAppsCommonDirectoryName);
            var smallCapsule = SteamPicsKeyValueHelper.FindChild(common, SteamPicsKeyNames.SmallCapsule);
            return TryExtractLocalizedRelativePath(smallCapsule, SteamPicsKeyNames.English);
        }

        private static void AddFallbackRequest(
            List<AssetDownloadRequest> requests,
            HashSet<string> seenUrls,
            HashSet<string> seenFileNames,
            string fileName,
            IEnumerable<string> candidateUrls)
        {
            if (string.IsNullOrWhiteSpace(fileName) || !seenFileNames.Add(fileName))
                return;

            var urls = candidateUrls?
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .Where(url => seenUrls.Add(url))
                .ToArray();
            if (urls == null || urls.Length == 0)
                return;

            requests.Add(new AssetDownloadRequest
            {
                FileName = fileName,
                CandidateUrls = urls
            });
        }

        private static void CollectDownloadableAssetReferences(KeyValue node, HashSet<string> relativePaths)
        {
            if (node == null || relativePaths == null)
                return;

            if (!string.IsNullOrWhiteSpace(node.Value) && IsDownloadableAssetReference(node.Value))
                relativePaths.Add(node.Value.Trim());

            if (node.Children == null)
                return;

            foreach (var child in node.Children)
                CollectDownloadableAssetReferences(child, relativePaths);
        }

        private static bool IsDownloadableAssetReference(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var normalized = value.Trim().TrimStart('/');
            if (IsStoreAssetRelativePath(normalized))
                return true;

            return IsBareImageFileName(normalized);
        }

        private static bool IsBareImageFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)
                || value.IndexOf('/') >= 0
                || value.IndexOf('\\') >= 0)
            {
                return false;
            }

            var extension = Path.GetExtension(value);
            if (string.IsNullOrEmpty(extension))
                return false;

            return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".gif", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".webp", StringComparison.OrdinalIgnoreCase);
        }

        private static void CollectLogoHashDownloadRequests(
            List<AssetDownloadRequest> requests,
            KeyValue picsData,
            ulong remoteAppId,
            HashSet<string> seenUrls,
            HashSet<string> seenFileNames)
        {
            var logoHash = TryExtractPicsSha1Hash(picsData, SteamPicsKeyNames.Logo);
            if (string.IsNullOrWhiteSpace(logoHash))
                return;

            var url = TryBuildCommunityAssetsAppImageUrl(remoteAppId, logoHash);
            if (string.IsNullOrWhiteSpace(url) || !seenUrls.Add(url))
                return;

            var fileName = logoHash + ".jpg";
            if (!seenFileNames.Add(fileName))
                return;

            requests.Add(new AssetDownloadRequest
            {
                FileName = fileName,
                CandidateUrls = new[] { url }
            });
        }

        private static List<string> BuildStoreAssetCandidateUrls(ulong appId, string pathOrFileName)
        {
            var candidates = new List<string>();
            if (appId == 0 || string.IsNullOrWhiteSpace(pathOrFileName))
                return candidates;

            var normalizedPath = pathOrFileName.Trim().TrimStart('/');
            AddCandidate(candidates, BuildFastlyStoreAssetFileUrl(appId, normalizedPath));

            if (!normalizedPath.Contains("/"))
            {
                AddCandidate(
                    candidates,
                    string.Format(ApplicationConstants.SteamLegacyAkamaiStoreAssetFileUrlFormat, appId, normalizedPath));
            }

            return candidates;
        }

        private static bool IsStoreAssetRelativePath(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var normalized = value.Trim().TrimStart('/');
            var slashIndex = normalized.IndexOf('/');
            if (slashIndex != 40 || slashIndex >= normalized.Length - 1)
                return false;

            return IsSha1HexHash(normalized.Substring(0, 40));
        }

        private static List<string> CollectUniqueIconHashes(KeyValue picsData)
        {
            var hashes = new List<string>(2);
            var clientIconHash = TryResolveClientIconHash(picsData);
            if (!string.IsNullOrWhiteSpace(clientIconHash))
                hashes.Add(clientIconHash);

            var iconHash = TryExtractPicsSha1Hash(picsData, SteamPicsKeyNames.Icon);
            if (!string.IsNullOrWhiteSpace(iconHash)
                && !hashes.Any(hash => string.Equals(hash, iconHash, StringComparison.OrdinalIgnoreCase)))
            {
                hashes.Add(iconHash);
            }

            return hashes;
        }

        private void EnsureCanonicalUiFiles(string gamePath, ulong appId, KeyValue picsData)
        {
            EnsureCanonicalFileFromSources(
                gamePath,
                PathConstants.SteamGameResourcesHeaderImageFileName,
                BuildCanonicalHeaderSourcePreference(picsData));

            EnsureCanonicalFileFromSources(
                gamePath,
                PathConstants.SteamGameResourcesCapsuleCoverImageFileName,
                BuildCanonicalCoverSourcePreference(picsData));

            EnsureCanonicalFileFromSources(
                gamePath,
                PathConstants.SteamGameResourcesLibraryLogoImageFileName,
                BuildCanonicalLogoSourcePreference(picsData));

            var clientIconHash = TryResolveClientIconHash(picsData);
            if (!string.IsNullOrWhiteSpace(clientIconHash))
            {
                EnsureCanonicalFileFromSources(
                    gamePath,
                    PathConstants.GetSteamGameResourcesClientIconFileName(appId),
                    new[] { clientIconHash + PathConstants.SteamGameResourcesClientIconFileExtension });
            }
        }

        private static string[] BuildCanonicalHeaderSourcePreference(KeyValue picsData)
        {
            var sources = new List<string>();
            TryAddEnglishLibraryAssetFileName(sources, picsData, SteamPicsKeyNames.LibraryHeader, prefer2x: true);
            TryAddEnglishLibraryAssetFileName(sources, picsData, SteamPicsKeyNames.LibraryHeader, prefer2x: false);
            TryAddEnglishHeaderImageFileName(sources, picsData);
            sources.AddRange(HeaderPreferredFileNames);
            return DeduplicateFileNames(sources);
        }

        private static string[] BuildCanonicalCoverSourcePreference(KeyValue picsData)
        {
            var sources = new List<string>();
            TryAddEnglishLibraryAssetFileName(sources, picsData, SteamPicsKeyNames.LibraryCapsule, prefer2x: true);
            TryAddEnglishLibraryAssetFileName(sources, picsData, SteamPicsKeyNames.LibraryCapsule, prefer2x: false);
            TryAddEnglishSmallCapsuleFileName(sources, picsData);
            sources.AddRange(CoverPreferredFileNames);
            return DeduplicateFileNames(sources);
        }

        private static string[] BuildCanonicalLogoSourcePreference(KeyValue picsData)
        {
            var sources = new List<string>();
            TryAddEnglishLibraryAssetFileName(sources, picsData, SteamPicsKeyNames.LibraryLogo, prefer2x: true);
            TryAddEnglishLibraryAssetFileName(sources, picsData, SteamPicsKeyNames.LibraryLogo, prefer2x: false);
            var logoHash = TryExtractPicsSha1Hash(picsData, SteamPicsKeyNames.Logo);
            if (!string.IsNullOrWhiteSpace(logoHash))
                sources.Add(logoHash + ".jpg");
            sources.AddRange(LogoPreferredFileNames);
            return DeduplicateFileNames(sources);
        }

        private static void TryAddEnglishLibraryAssetFileName(
            List<string> fileNames,
            KeyValue picsData,
            string libraryAssetKey,
            bool prefer2x)
        {
            var relativePath = TryExtractEnglishLibraryAssetRelativePath(picsData, libraryAssetKey, prefer2x);
            TryAddFileNameFromRelativePath(fileNames, relativePath);
        }

        private static void TryAddEnglishHeaderImageFileName(List<string> fileNames, KeyValue picsData)
        {
            TryAddFileNameFromRelativePath(fileNames, TryExtractHeaderImageRelativePath(picsData));
        }

        private static void TryAddEnglishSmallCapsuleFileName(List<string> fileNames, KeyValue picsData)
        {
            TryAddFileNameFromRelativePath(fileNames, TryExtractSmallCapsuleRelativePath(picsData));
        }

        private static void TryAddFileNameFromRelativePath(List<string> fileNames, string relativePath)
        {
            if (fileNames == null || string.IsNullOrWhiteSpace(relativePath))
                return;

            var fileName = Path.GetFileName(relativePath);
            if (!string.IsNullOrWhiteSpace(fileName))
                fileNames.Add(fileName);
        }

        private static string[] DeduplicateFileNames(IEnumerable<string> fileNames)
        {
            var unique = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var fileName in fileNames ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(fileName) || !seen.Add(fileName))
                    continue;
                unique.Add(fileName);
            }

            return unique.ToArray();
        }

        private static string TryExtractEnglishLibraryAssetRelativePath(
            KeyValue picsData,
            string libraryAssetKey,
            bool prefer2x)
        {
            if (picsData == null || string.IsNullOrWhiteSpace(libraryAssetKey))
                return null;

            var appInfoTarget = SteamPicsKeyValueHelper.ResolveAppInfoTarget(picsData);
            var common = SteamPicsKeyValueHelper.FindChild(appInfoTarget, PathConstants.SteamAppsCommonDirectoryName);
            var libraryAssetsFull = SteamPicsKeyValueHelper.FindChild(common, SteamPicsKeyNames.LibraryAssetsFull);
            var assetNode = SteamPicsKeyValueHelper.FindChild(libraryAssetsFull, libraryAssetKey);
            var imageNode = SteamPicsKeyValueHelper.FindChild(
                assetNode,
                prefer2x ? SteamPicsKeyNames.Image2x : SteamPicsKeyNames.Image);
            return TryExtractLocalizedRelativePath(imageNode, SteamPicsKeyNames.English);
        }

        private static string TryExtractLocalizedRelativePath(KeyValue localizedNode, string preferredLanguageKey)
        {
            if (localizedNode == null)
                return null;

            if (!string.IsNullOrWhiteSpace(preferredLanguageKey))
            {
                var preferred = SteamPicsKeyValueHelper.FindChild(localizedNode, preferredLanguageKey);
                if (!string.IsNullOrWhiteSpace(preferred?.Value))
                    return preferred.Value.Trim();
            }

            if (localizedNode.Children == null || localizedNode.Children.Count == 0)
                return string.IsNullOrWhiteSpace(localizedNode.Value) ? null : localizedNode.Value.Trim();

            foreach (var child in localizedNode.Children)
            {
                if (!string.IsNullOrWhiteSpace(child?.Value))
                    return child.Value.Trim();
            }

            return null;
        }

        private static void EnsureCanonicalFileFromSources(
            string gamePath,
            string canonicalFileName,
            IEnumerable<string> sourceFileNamesInPreferenceOrder)
        {
            if (string.IsNullOrWhiteSpace(gamePath) || string.IsNullOrWhiteSpace(canonicalFileName))
                return;

            string bestSourcePath = null;
            long bestSourceLength = 0;
            foreach (var sourceFileName in sourceFileNamesInPreferenceOrder ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(sourceFileName))
                    continue;

                var sourcePath = Path.Combine(gamePath, sourceFileName);
                if (!File.Exists(sourcePath))
                    continue;

                var sourceLength = new FileInfo(sourcePath).Length;
                if (bestSourcePath == null || sourceLength > bestSourceLength)
                {
                    bestSourcePath = sourcePath;
                    bestSourceLength = sourceLength;
                }
            }

            if (bestSourcePath == null)
                return;

            var canonicalPath = Path.Combine(gamePath, canonicalFileName);
            var shouldCopy = !File.Exists(canonicalPath)
                || new FileInfo(canonicalPath).Length < bestSourceLength;

            if (shouldCopy)
                File.Copy(bestSourcePath, canonicalPath, overwrite: true);
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

        private static string TryBuildCommunityAssetsAppImageUrl(ulong appId, string hash)
        {
            if (appId == 0 || string.IsNullOrWhiteSpace(hash))
                return null;
            return string.Format(ApplicationConstants.SteamCommunityAssetsAppImageUrlFormat, appId, hash);
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
            return TryExtractLocalizedRelativePath(image, SteamPicsKeyNames.English);
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
            return TryExtractLocalizedRelativePath(image, SteamPicsKeyNames.English);
        }

        private static string TryExtractHeaderImageRelativePath(KeyValue appPicsData)
        {
            if (appPicsData == null)
                return null;

            var appInfoTarget = SteamPicsKeyValueHelper.ResolveAppInfoTarget(appPicsData);
            var common = SteamPicsKeyValueHelper.FindChild(appInfoTarget, PathConstants.SteamAppsCommonDirectoryName);
            var headerImage = SteamPicsKeyValueHelper.FindChild(common, SteamPicsKeyNames.HeaderImage);
            return TryExtractLocalizedRelativePath(headerImage, SteamPicsKeyNames.English);
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

        private static List<string> BuildPreferredStoreAssetUrls(
            ulong appId,
            string preferredRelativePath,
            string[] preferredFileNames)
        {
            var candidates = new List<string>();
            if (appId == 0 || preferredFileNames == null || preferredFileNames.Length == 0)
                return candidates;

            var hashFolder = TryExtractRelativeDirectoryName(preferredRelativePath);
            foreach (var fileName in preferredFileNames)
            {
                if (string.IsNullOrWhiteSpace(fileName))
                    continue;

                if (!string.IsNullOrWhiteSpace(hashFolder))
                {
                    foreach (var url in BuildStoreAssetCandidateUrls(appId, hashFolder + "/" + fileName))
                        AddCandidate(candidates, url);
                }

                foreach (var url in BuildStoreAssetCandidateUrls(appId, fileName))
                    AddCandidate(candidates, url);
            }

            foreach (var url in BuildStoreAssetCandidateUrls(appId, preferredRelativePath))
                AddCandidate(candidates, url);

            return candidates;
        }

        private static string TryExtractRelativeDirectoryName(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return null;

            var normalized = relativePath.Trim().TrimStart('/');
            if (normalized.Length == 0)
                return null;

            var lastSlash = normalized.LastIndexOf('/');
            if (lastSlash <= 0)
                return null;

            var directory = normalized.Substring(0, lastSlash);
            return directory.Length == 0 ? null : directory;
        }

        private static void AddCandidate(List<string> candidates, string url)
        {
            if (candidates == null || string.IsNullOrWhiteSpace(url))
                return;
            if (candidates.Any(x => string.Equals(x, url, StringComparison.OrdinalIgnoreCase)))
                return;
            candidates.Add(url);
        }

    }
}
