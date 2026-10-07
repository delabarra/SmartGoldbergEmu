using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AppDataKit;
using SmartGoldbergEmu;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Services
{
    public class GameImageService : IDisposable
    {
        // Store Banner list + essentials: rebuild contract — header.jpg (PICS header_image may alias).
        private static readonly string[] StoreBannerPreferredFileNames =
        {
            PathConstants.SteamGameResourcesHeaderImageFileName
        };

        private static readonly string[] LibraryHeaderPreferredFileNames =
        {
            PathConstants.SteamGameResourcesLibraryHeader2xImageFileName,
            PathConstants.SteamGameResourcesLibraryHeaderImageFileName
        };

        private static readonly string[] HeaderImagePreferredFileNames =
        {
            PathConstants.SteamGameResourcesHeader2xImageFileName,
            PathConstants.SteamGameResourcesHeaderImageFileName
        };

        private static readonly string[] LibraryCoverPreferredFileNames =
        {
            PathConstants.SteamGameResourcesLibraryCapsule2xImageFileName,
            PathConstants.SteamGameResourcesLegacyLibraryCapsule2xImageFileName,
            PathConstants.SteamGameResourcesLegacyLibraryCapsuleImageFileName,
            PathConstants.SteamGameResourcesLibraryCapsuleImageFileName,
            PathConstants.SteamGameResourcesSmallCapsuleImageFileName
        };

        private static readonly string[] LibraryLogoPreferredFileNames =
        {
            PathConstants.SteamGameResourcesLibraryLogo2xImageFileName,
            PathConstants.SteamGameResourcesLibraryLogoImageFileName
        };

        // Main list mosaic views: strict filenames per view. No catalog JSON / PICS reload on the binder path.
        private static readonly string[] ListViewLibraryCoverFileNames =
        {
            PathConstants.SteamGameResourcesLibraryCapsule2xImageFileName,
            PathConstants.SteamGameResourcesLegacyLibraryCapsule2xImageFileName,
            PathConstants.SteamGameResourcesLibraryCapsuleImageFileName,
            PathConstants.SteamGameResourcesLegacyLibraryCapsuleImageFileName
        };

        private static readonly string[] ListViewLogoFileNames =
        {
            PathConstants.SteamGameResourcesLibraryLogoPics2xImageFileName,
            PathConstants.SteamGameResourcesLibraryLogoPicsImageFileName,
            PathConstants.SteamGameResourcesLibraryLogo2xImageFileName,
            PathConstants.SteamGameResourcesLibraryLogoImageFileName
        };

        private static readonly string[] LibraryHeroPreferredFileNames =
        {
            PathConstants.SteamGameResourcesLibraryHero2xImageFileName,
            PathConstants.SteamGameResourcesLibraryHeroImageFileName,
            PathConstants.SteamGameResourcesLibraryHeroBlurImageFileName
        };

        private static readonly string[] LibraryLogoPicsPreferredFileNames =
        {
            PathConstants.SteamGameResourcesLibraryLogoPics2xImageFileName,
            PathConstants.SteamGameResourcesLibraryLogoPicsImageFileName
        };

        // Extra store CDN names not always present in PICS (older apps / alternate capsules).
        private static readonly string[] AdditionalStoreAssetFileNames =
        {
            PathConstants.SteamGameResourcesLargeCapsule2xImageFileName,
            PathConstants.SteamGameResourcesLargeCapsuleImageFileName,
            PathConstants.SteamGameResourcesSmallCapsule2xImageFileName,
            PathConstants.SteamGameResourcesCapsuleImageFileName,
            "capsule_467x181.jpg",
            PathConstants.SteamGameResourcesHeroCapsule2xImageFileName,
            PathConstants.SteamGameResourcesHeroCapsuleImageFileName,
            "header_292x136.jpg",
            PathConstants.SteamGameResourcesPageBackgroundImageFileName,
            PathConstants.SteamGameResourcesPageBackgroundRawImageFileName,
            PathConstants.SteamGameResourcesLegacyPageBackgroundImageFileName
        };

        // Mirrors AppDataKit GameAssetParser community hash assets (correct CDN extensions).
        private static readonly (string PicsKey, string[] Extensions)[] CommunityHashAssets =
        {
            (SteamPicsKeyNames.ClientIcon, new[] { "ico" }),
            (SteamPicsKeyNames.Icon, new[] { "jpg" }),
            (SteamPicsKeyNames.Logo, new[] { "jpg" }),
            (SteamPicsKeyNames.LogoSmall, new[] { "jpg" }),
            (SteamPicsKeyNames.ClientTga, new[] { "tga" }),
            (SteamPicsKeyNames.ClientIcns, new[] { "icns" }),
            (SteamPicsKeyNames.LinuxClientIcon, new[] { "png", "jpg", "ico" })
        };

        private sealed class PicsStoreAssetRef
        {
            public string RelativePath { get; set; }
            public bool IsEnglish { get; set; }
        }

        private sealed class AssetDownloadRequest
        {
            public string FileName { get; set; }
            public string[] CandidateUrls { get; set; }
        }

        private readonly IHttpService _httpService;
        private readonly string _gamesDirectory;
        private readonly ITaskReportService _taskReportService;
        private readonly FallbackMosaicArtCache _fallbackMosaicArtCache = new FallbackMosaicArtCache();
        private readonly object _waitingPlaceholderSync = new object();
        private Bitmap _waitingMosaicPlaceholderBitmap;
        private ApngAnimationDecoder.FrameBitmap[] _waitingMosaicAnimationFrames;
        private bool _waitingMosaicPlaceholderLoadAttempted;
        private Task<bool> _waitingMosaicAnimationDecodeTask;
        private int _waitingMosaicAnimationDecodeEpoch;
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
            AppInfoKeyValue appPicsData = null,
            string gameDisplayName = null,
            GameAssetsSection catalogAssets = null,
            Action<int, int> onProgress = null)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(GameImageService));

            var remoteAppId = steamAppIdForRemoteAssets ?? appId;
            if (remoteAppId == 0)
                return true;

            var displayName = ResolveGameDisplayName(gameDisplayName, metadata, appId);
            bool compactLoh = false;

            try
            {
                var gamePath = PathConstants.CombineGamesPerAppResourcesDirectory(_gamesDirectory, appId.ToString());
                Directory.CreateDirectory(gamePath);

                // One catalog parse for AppInfo + assets. Do not reload per filename or after save.
                var picsData = appPicsData;
                if (picsData == null || !HasCatalogAssetItems(catalogAssets))
                {
                    var catalogSnapshot = TryLoadCatalogSnapshot(appId);
                    if (catalogSnapshot != null)
                        compactLoh = true;
                    if (picsData == null)
                        picsData = catalogSnapshot?.AppInfo;
                    if (!HasCatalogAssetItems(catalogAssets))
                        catalogAssets = catalogSnapshot?.Assets;
                }

                var downloadRequests = BuildAssetDownloadRequests(picsData, catalogAssets, remoteAppId);
                catalogAssets = null;
                var totalDownloads = downloadRequests.Count;

                if (reportFeedback)
                {
                    Feedback?.SetMessage($"Downloading game assets ({totalDownloads} files)...");
                    Feedback?.SetProgress(0, Math.Max(totalDownloads, 1));
                }
                onProgress?.Invoke(0, totalDownloads);

                if (_disposed)
                    return ApplyDownloadOutcomeFeedback(
                        gamePath, totalDownloads, displayName, appId, reportFeedback: false, downloadedCount: 0, failedFiles: null, picsData: picsData);

                var completed = 0;
                var downloadedCount = 0;
                var failedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var lockObj = new object();

                await HttpHelpers.ForEachBoundedAsync(
                    downloadRequests,
                    HttpHelpers.DefaultMaxConcurrentDownloads,
                    async request =>
                    {
                        int outcome = await DownloadImageAsync(
                            remoteAppId,
                            gamePath,
                            request.FileName,
                            request.CandidateUrls).ConfigureAwait(false);
                        if (outcome > 0)
                            System.Threading.Interlocked.Increment(ref downloadedCount);
                        else if (outcome < 0)
                        {
                            lock (lockObj)
                                failedFiles.Add(request.FileName);
                        }

                        if (_disposed || (!reportFeedback && onProgress == null))
                            return;

                        lock (lockObj)
                        {
                            if (_disposed)
                                return;
                            completed++;
                            if (reportFeedback)
                            {
                                Feedback?.SetProgress(completed, Math.Max(totalDownloads, 1));
                                Feedback?.SetMessage($"Downloading assets... {completed}/{totalDownloads}");
                            }
                            onProgress?.Invoke(completed, totalDownloads);
                        }
                    }).ConfigureAwait(false);

                if (downloadedCount > 0)
                    compactLoh = true;

                return ApplyDownloadOutcomeFeedback(
                    gamePath,
                    totalDownloads,
                    displayName,
                    appId,
                    reportFeedback && !_disposed,
                    downloadedCount,
                    failedFiles,
                    picsData);
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError(
                    $"Assets for AppId {appId} failed (Steam AppId {remoteAppId}): {ex.Message}",
                    ex);
                if (reportFeedback && !_disposed)
                    Feedback?.SetMessage("Could not download game images.", TaskReportKind.Error);
                if (appId > 0)
                {
                    var resourcesPath = PathConstants.CombineGamesPerAppResourcesDirectory(_gamesDirectory, appId.ToString());
                    UpdateMissingAssetsNote(resourcesPath, displayName, appId, picsData: null);
                }
                return false;
            }
            finally
            {
                if (compactLoh)
                    LargeObjectHeapHelper.CompactAfterLargeTransientAllocation();
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

        // Steam clientui hashed spinner under %LocalAppData%\SmartGoldbergEmu\ — used while add/save waits for real art.
        public Bitmap TryCloneWaitingMosaicPlaceholderBitmap()
        {
            if (_disposed)
                return null;

            EnsureWaitingMosaicPlaceholderLoaded();
            lock (_waitingPlaceholderSync)
            {
                if (_disposed || _waitingMosaicPlaceholderBitmap == null)
                    return null;
                return new Bitmap(_waitingMosaicPlaceholderBitmap);
            }
        }

        // Borrowed APNG frames for mosaic waiting animation (owned by this service; do not dispose).
        public bool TryGetWaitingMosaicAnimationFrames(out ApngAnimationDecoder.FrameBitmap[] frames)
        {
            frames = null;
            if (_disposed)
                return false;

            lock (_waitingPlaceholderSync)
            {
                if (_disposed
                    || _waitingMosaicAnimationFrames == null
                    || _waitingMosaicAnimationFrames.Length < 2)
                {
                    return false;
                }

                frames = _waitingMosaicAnimationFrames;
                return true;
            }
        }

        public Task<bool> EnsureWaitingMosaicAnimationAsync()
        {
            if (_disposed)
                return Task.FromResult(false);

            lock (_waitingPlaceholderSync)
            {
                if (_waitingMosaicAnimationFrames != null && _waitingMosaicAnimationFrames.Length >= 2)
                    return Task.FromResult(true);

                if (_waitingMosaicAnimationDecodeTask != null && !_waitingMosaicAnimationDecodeTask.IsCompleted)
                    return _waitingMosaicAnimationDecodeTask;

                int epoch = _waitingMosaicAnimationDecodeEpoch;
                _waitingMosaicAnimationDecodeTask = Task.Run(() => DecodeWaitingMosaicAnimation(epoch));
                return _waitingMosaicAnimationDecodeTask;
            }
        }

        // Drop decoded APNG frames when waiting ends (or the form closes).
        public void ReleaseWaitingMosaicAnimationFrames()
        {
            if (_disposed)
                return;

            lock (_waitingPlaceholderSync)
            {
                _waitingMosaicAnimationDecodeEpoch++;
                if (_waitingMosaicAnimationFrames != null)
                {
                    for (int i = 0; i < _waitingMosaicAnimationFrames.Length; i++)
                        _waitingMosaicAnimationFrames[i]?.Dispose();
                    _waitingMosaicAnimationFrames = null;
                }
                _waitingMosaicAnimationDecodeTask = null;
            }
        }

        // Store Banner: header.jpg on disk. Missing → mosaic placeholder.
        public string GetHeaderImagePathOrFallback(ulong appId)
        {
            return ResolvePreferredImagePath(appId, StoreBannerPreferredFileNames);
        }

        // Library Cover: 2x capsule then 1x. Missing → mosaic placeholder.
        public string GetCapsuleImagePathOrFallback(ulong appId)
        {
            return ResolvePreferredImagePath(appId, ListViewLibraryCoverFileNames);
        }

        // Logos: library_logo 2x/1x then logo.png. Missing → mosaic placeholder.
        public string GetLogoImagePathOrFallback(ulong appId)
        {
            return ResolvePreferredImagePath(appId, ListViewLogoFileNames);
        }

        // Steam client icon under games/{appId}/resources/ ({appId}.ico or any .ico from the libcache download).
        public string GetClientIconPathOrFallback(ulong appId)
        {
            var canonicalIconPath = GetImagePath(appId, PathConstants.GetSteamGameResourcesClientIconFileName(appId));
            if (!string.IsNullOrEmpty(canonicalIconPath))
                return canonicalIconPath;

            var resourcesDirectory = PathConstants.CombineGamesPerAppResourcesDirectory(_gamesDirectory, appId.ToString());
            if (string.IsNullOrEmpty(resourcesDirectory) || !Directory.Exists(resourcesDirectory))
                return null;

            var icoFiles = Directory.GetFiles(
                resourcesDirectory,
                "*" + PathConstants.SteamGameResourcesClientIconFileExtension);
            return icoFiles.Length > 0 ? icoFiles[0] : null;
        }

        // Same view → file mapping as the rebuild library binder. Details is text-only.
        public string ResolveArtworkPathForViewMode(ulong appId, string viewMode)
        {
            string mode = ApplicationConstants.NormalizeViewMode(viewMode);
            if (string.Equals(mode, ApplicationConstants.ViewModeTile, StringComparison.OrdinalIgnoreCase))
                return GetHeaderImagePathOrFallback(appId);
            if (string.Equals(mode, ApplicationConstants.ViewModeCompactTiles, StringComparison.OrdinalIgnoreCase))
                return GetCapsuleImagePathOrFallback(appId);
            if (string.Equals(mode, ApplicationConstants.ViewModeLogos, StringComparison.OrdinalIgnoreCase))
                return GetLogoImagePathOrFallback(appId);
            if (string.Equals(mode, ApplicationConstants.ViewModeIcons, StringComparison.OrdinalIgnoreCase))
                return GetClientIconPathOrFallback(appId);
            return null;
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
            lock (_waitingPlaceholderSync)
            {
                DisposeWaitingMosaicPlaceholders_NoLock();
            }
        }

        private void EnsureWaitingMosaicPlaceholderLoaded()
        {
            lock (_waitingPlaceholderSync)
            {
                if (_disposed || _waitingMosaicPlaceholderBitmap != null)
                    return;

                string path = PathConstants.LocalAppDataSteamClientUiHashedImagePath;
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    return;

                if (_waitingMosaicPlaceholderLoadAttempted)
                    return;

                _waitingMosaicPlaceholderLoadAttempted = true;
                try
                {
                    // GDI+ reads the default APNG frame only; animation frames are decoded separately.
                    using (var loaded = Image.FromFile(path))
                    {
                        _waitingMosaicPlaceholderBitmap = new Bitmap(loaded);
                    }
                }
                catch (Exception ex)
                {
                    Program.LogService?.LogWarning(
                        $"Could not load waiting mosaic placeholder from {path}: {ex.Message}");
                    _waitingMosaicPlaceholderBitmap = null;
                }
            }
        }

        private bool DecodeWaitingMosaicAnimation(int epoch)
        {
            string path = PathConstants.LocalAppDataSteamClientUiHashedImagePath;
            ApngAnimationDecoder.FrameBitmap[] decoded = null;
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    return false;
                if (!ApngAnimationDecoder.TryDecode(path, out decoded) || decoded == null || decoded.Length < 2)
                {
                    if (decoded != null)
                    {
                        for (int i = 0; i < decoded.Length; i++)
                            decoded[i]?.Dispose();
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning(
                    $"Could not decode waiting mosaic APNG from {path}: {ex.Message}");
                if (decoded != null)
                {
                    for (int i = 0; i < decoded.Length; i++)
                        decoded[i]?.Dispose();
                }
                return false;
            }

            lock (_waitingPlaceholderSync)
            {
                if (_disposed || epoch != _waitingMosaicAnimationDecodeEpoch)
                {
                    for (int i = 0; i < decoded.Length; i++)
                        decoded[i]?.Dispose();
                    return false;
                }

                if (_waitingMosaicAnimationFrames != null)
                {
                    for (int i = 0; i < decoded.Length; i++)
                        decoded[i]?.Dispose();
                    return _waitingMosaicAnimationFrames.Length >= 2;
                }

                _waitingMosaicAnimationFrames = decoded;
                if (_waitingMosaicPlaceholderBitmap == null)
                    _waitingMosaicPlaceholderBitmap = new Bitmap(decoded[0].Bitmap);
                return true;
            }
        }

        private void DisposeWaitingMosaicPlaceholders_NoLock()
        {
            _waitingMosaicAnimationDecodeEpoch++;
            _waitingMosaicAnimationDecodeTask = null;
            _waitingMosaicPlaceholderBitmap?.Dispose();
            _waitingMosaicPlaceholderBitmap = null;
            if (_waitingMosaicAnimationFrames != null)
            {
                for (int i = 0; i < _waitingMosaicAnimationFrames.Length; i++)
                    _waitingMosaicAnimationFrames[i]?.Dispose();
                _waitingMosaicAnimationFrames = null;
            }
        }

        private bool ApplyDownloadOutcomeFeedback(
            string resourcesDirectory,
            int totalDownloads,
            string gameDisplayName,
            ulong appId,
            bool reportFeedback,
            int downloadedCount,
            HashSet<string> failedFiles,
            AppInfoKeyValue picsData)
        {
            UpdateMissingAssetsNote(resourcesDirectory, gameDisplayName, appId, picsData);

            bool hasHeader = !string.IsNullOrEmpty(
                ResolvePreferredImagePath(resourcesDirectory, BuildStoreBannerPreferredFileNames(picsData)));
            bool hasIcon = HasClientIconResource(resourcesDirectory, appId);
            bool hasCapsule = !string.IsNullOrEmpty(
                ResolvePreferredImagePath(resourcesDirectory, BuildLibraryCoverPreferredFileNames(picsData)));
            bool essentialsOk = hasHeader && hasIcon && hasCapsule;

            if (downloadedCount > 0 && essentialsOk)
            {
                Program.LogService?.LogMessage($"Assets for AppId {appId} downloaded.");
            }
            else if (!essentialsOk)
            {
                var missing = new List<string>(4);
                if (!hasHeader)
                    missing.Add("store banner (header.jpg)");
                if (!hasCapsule)
                    missing.Add("library cover (library_capsule_2x.jpg, library_capsule.jpg, or library_600x900.jpg)");
                if (!hasIcon)
                    missing.Add("client icon (.ico)");

                string failedDetail = string.Empty;
                if (failedFiles != null && failedFiles.Count > 0)
                {
                    var names = new List<string>(failedFiles);
                    names.Sort(StringComparer.OrdinalIgnoreCase);
                    failedDetail = "; download failed: " + string.Join(", ", names);
                }

                Program.LogService?.LogWarning(
                    $"Assets for AppId {appId} incomplete: missing {string.Join(", ", missing)}{failedDetail}");
            }

            if (!reportFeedback)
                return essentialsOk;

            if (_disposed)
                return essentialsOk;

            if (essentialsOk)
            {
                Feedback?.SetMessageWithAutoClear("Game images downloaded successfully");
                return true;
            }

            Feedback?.SetMessage("Some game images could not be downloaded.", TaskReportKind.Warning);
            return false;
        }

        private static bool ResourceFileExists(string resourcesDirectory, string fileName)
        {
            if (string.IsNullOrEmpty(resourcesDirectory) || string.IsNullOrEmpty(fileName))
                return false;
            return File.Exists(Path.Combine(resourcesDirectory, fileName));
        }

        private static List<string> CollectMissingLibraryArtworkFileNames(
            string resourcesDirectory,
            AppInfoKeyValue picsData)
        {
            var missing = new List<string>(3);
            if (string.IsNullOrEmpty(
                ResolvePreferredImagePath(resourcesDirectory, BuildStoreBannerPreferredFileNames(picsData))))
            {
                missing.Add("store banner");
            }

            if (string.IsNullOrEmpty(
                ResolvePreferredImagePath(resourcesDirectory, BuildLibraryCoverPreferredFileNames(picsData))))
            {
                missing.Add("library cover");
            }

            if (string.IsNullOrEmpty(
                ResolvePreferredImagePath(resourcesDirectory, BuildLibraryLogoPreferredFileNames(picsData))))
            {
                missing.Add("logo image");
            }

            return missing;
        }

        private static bool HasClientIconResource(string resourcesDirectory, ulong appId)
        {
            if (ResourceFileExists(resourcesDirectory, PathConstants.GetSteamGameResourcesClientIconFileName(appId)))
                return true;

            if (string.IsNullOrEmpty(resourcesDirectory) || !Directory.Exists(resourcesDirectory))
                return false;

            return Directory.GetFiles(resourcesDirectory, "*" + PathConstants.SteamGameResourcesClientIconFileExtension)
                .Length > 0;
        }

        private static string ResolveGameDisplayName(string gameDisplayName, OnlineAppData metadata, ulong appId)
        {
            if (!string.IsNullOrWhiteSpace(gameDisplayName))
                return gameDisplayName.Trim();
            if (!string.IsNullOrWhiteSpace(metadata?.Name))
                return metadata.Name.Trim();
            return appId > 0 ? $"App {appId}" : "this game";
        }

        private void UpdateMissingAssetsNote(
            string resourcesDirectory,
            string gameDisplayName,
            ulong appId,
            AppInfoKeyValue picsData)
        {
            if (appId == 0)
                return;
            if (string.IsNullOrEmpty(resourcesDirectory))
                return;

            var notePath = Path.Combine(resourcesDirectory, PathConstants.SteamGameResourcesMissingAssetsNoteFileName);
            var missingArtwork = CollectMissingLibraryArtworkFileNames(resourcesDirectory, picsData);
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
                await HttpHelpers.DownloadFileAtomicAsync(_httpService, url, imagePath).ConfigureAwait(false);
                return !_disposed;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // 1 = newly downloaded, 0 = skipped (exists / no URLs), -1 = attempted and failed.
        private async Task<int> DownloadImageAsync(
            ulong appId,
            string gamePath,
            string fileName,
            params string[] candidateUrls)
        {
            if (_disposed)
                return 0;

            var imagePath = Path.Combine(gamePath, fileName);
            if (File.Exists(imagePath))
                return 0;

            if (candidateUrls == null || candidateUrls.Length == 0)
                return 0;

            foreach (var url in candidateUrls)
            {
                if (!await TryDownloadImageFromUrlAsync(url, imagePath))
                    continue;
                Program.LogService?.LogDebug($"Downloaded {fileName} for App ID {appId}");
                return 1;
            }

            return -1;
        }

        private static List<AssetDownloadRequest> BuildAssetDownloadRequests(
            AppInfoKeyValue picsData,
            GameAssetsSection catalogAssets,
            ulong remoteAppId)
        {
            var requests = new List<AssetDownloadRequest>();
            var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (picsData != null)
            {
                var appInfoTarget = AppInfoKeyValueHelper.ResolveAppInfoTarget(picsData);
                var storeAssets = new List<PicsStoreAssetRef>();
                CollectDownloadableAssetReferences(appInfoTarget, storeAssets);

                foreach (var storeAsset in storeAssets
                    .OrderByDescending(asset => asset.IsEnglish)
                    .ThenBy(asset => asset.RelativePath, StringComparer.OrdinalIgnoreCase))
                {
                    TryAddStorePathDownloadRequest(
                        requests,
                        seenUrls,
                        seenFileNames,
                        remoteAppId,
                        storeAsset.RelativePath);
                }

                CollectCommunityHashDownloadRequests(requests, picsData, remoteAppId, seenUrls, seenFileNames);
            }

            CollectCatalogAssetDownloadRequests(requests, catalogAssets, remoteAppId, seenUrls, seenFileNames);
            AppendFallbackAssetDownloadRequests(requests, picsData, remoteAppId, seenUrls, seenFileNames);

            return requests;
        }

        private static void AppendFallbackAssetDownloadRequests(
            List<AssetDownloadRequest> requests,
            AppInfoKeyValue picsData,
            ulong remoteAppId,
            HashSet<string> seenUrls,
            HashSet<string> seenFileNames)
        {
            AppendPreferredFilenameDownloads(
                requests,
                remoteAppId,
                picsData,
                seenUrls,
                seenFileNames,
                LibraryHeaderPreferredFileNames,
                TryExtractLibraryHeaderImageRelativePath);

            AppendPreferredFilenameDownloads(
                requests,
                remoteAppId,
                picsData,
                seenUrls,
                seenFileNames,
                HeaderImagePreferredFileNames,
                TryExtractHeaderImageRelativePath);

            AppendPreferredFilenameDownloads(
                requests,
                remoteAppId,
                picsData,
                seenUrls,
                seenFileNames,
                LibraryCoverPreferredFileNames,
                TryExtractLibraryCapsuleImageRelativePath);

            AppendPreferredFilenameDownloads(
                requests,
                remoteAppId,
                picsData,
                seenUrls,
                seenFileNames,
                LibraryLogoPreferredFileNames,
                TryExtractLibraryLogoImageRelativePath);

            AppendPreferredFilenameDownloads(
                requests,
                remoteAppId,
                picsData,
                seenUrls,
                seenFileNames,
                LibraryHeroPreferredFileNames,
                TryExtractLibraryHeroImageRelativePath);

            AppendPreferredFilenameDownloads(
                requests,
                remoteAppId,
                picsData,
                seenUrls,
                seenFileNames,
                LibraryLogoPicsPreferredFileNames,
                TryExtractLibraryLogoImageRelativePath);

            foreach (var fileName in AdditionalStoreAssetFileNames)
            {
                AddFallbackRequest(
                    requests,
                    seenUrls,
                    seenFileNames,
                    fileName,
                    BuildStoreAssetCandidateUrls(remoteAppId, fileName));
            }
        }

        private static void AppendPreferredFilenameDownloads(
            List<AssetDownloadRequest> requests,
            ulong remoteAppId,
            AppInfoKeyValue picsData,
            HashSet<string> seenUrls,
            HashSet<string> seenFileNames,
            string[] preferredFileNames,
            Func<AppInfoKeyValue, string> primaryRelativePathExtractor)
        {
            var primaryRelativePath = primaryRelativePathExtractor?.Invoke(picsData);
            var hashFolder = TryExtractRelativeDirectoryName(primaryRelativePath);

            foreach (var fileName in preferredFileNames ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(fileName))
                    continue;

                var urls = new List<string>();
                if (!string.IsNullOrWhiteSpace(hashFolder))
                {
                    foreach (var url in BuildStoreAssetCandidateUrls(remoteAppId, hashFolder + "/" + fileName))
                        AddCandidate(urls, url);
                }

                foreach (var url in BuildStoreAssetCandidateUrls(remoteAppId, fileName))
                    AddCandidate(urls, url);

                if (!string.IsNullOrWhiteSpace(primaryRelativePath)
                    && string.Equals(fileName, Path.GetFileName(primaryRelativePath), StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var url in BuildStoreAssetCandidateUrls(remoteAppId, primaryRelativePath))
                        AddCandidate(urls, url);
                }

                AddFallbackRequest(requests, seenUrls, seenFileNames, fileName, urls);
            }
        }

        private static void TryAddStorePathDownloadRequest(
            List<AssetDownloadRequest> requests,
            HashSet<string> seenUrls,
            HashSet<string> seenFileNames,
            ulong remoteAppId,
            string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return;

            var diskName = AllocateUniqueResourceFileName(relativePath, seenFileNames);
            if (string.IsNullOrWhiteSpace(diskName))
                return;

            var candidateUrls = BuildStoreAssetCandidateUrls(remoteAppId, relativePath);
            var urls = candidateUrls
                .Where(url => !string.IsNullOrWhiteSpace(url) && seenUrls.Add(url))
                .ToArray();
            if (urls.Length == 0)
            {
                seenFileNames.Remove(diskName);
                return;
            }

            requests.Add(new AssetDownloadRequest
            {
                FileName = diskName,
                CandidateUrls = urls
            });
        }

        // English/first copy keeps the Steam filename; extra language/hash variants keep `{hash}_{file}`.
        internal static string AllocateUniqueResourceFileName(string relativePathOrFileName, HashSet<string> seenFileNames)
        {
            if (string.IsNullOrWhiteSpace(relativePathOrFileName) || seenFileNames == null)
                return null;

            var fileName = Path.GetFileName(relativePathOrFileName.Replace('\\', '/').Trim().TrimStart('/'));
            if (string.IsNullOrWhiteSpace(fileName))
                return null;

            if (seenFileNames.Add(fileName))
                return fileName;

            var hashFolder = TryExtractRelativeDirectoryName(relativePathOrFileName);
            if (string.IsNullOrWhiteSpace(hashFolder))
                return null;

            var uniqueName = hashFolder + "_" + fileName;
            return seenFileNames.Add(uniqueName) ? uniqueName : null;
        }

        private static bool HasCatalogAssetItems(GameAssetsSection catalogAssets)
        {
            return catalogAssets?.Items != null && catalogAssets.Items.Count > 0;
        }

        private static bool IsEnglishCatalogAsset(string keyPath)
        {
            if (string.IsNullOrWhiteSpace(keyPath))
                return false;

            var parts = keyPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Equals(SteamPicsKeyNames.English, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
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
            {
                seenFileNames.Remove(fileName);
                return;
            }

            requests.Add(new AssetDownloadRequest
            {
                FileName = fileName,
                CandidateUrls = urls
            });
        }

        private static void CollectDownloadableAssetReferences(AppInfoKeyValue node, List<PicsStoreAssetRef> output)
        {
            CollectDownloadableAssetReferences(node, output, parentName: null);
        }

        private static void CollectDownloadableAssetReferences(
            AppInfoKeyValue node,
            List<PicsStoreAssetRef> output,
            string parentName)
        {
            if (node == null || output == null)
                return;

            if (string.Equals(node.Name, "ufs", StringComparison.OrdinalIgnoreCase))
                return;

            if (!string.IsNullOrWhiteSpace(node.Value) && IsDownloadableAssetReference(node.Value))
            {
                bool isEnglish = string.Equals(node.Name, SteamPicsKeyNames.English, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(parentName, SteamPicsKeyNames.English, StringComparison.OrdinalIgnoreCase);
                output.Add(new PicsStoreAssetRef
                {
                    RelativePath = node.Value.Trim(),
                    IsEnglish = isEnglish
                });
            }

            if (node.Children == null)
                return;

            foreach (var child in node.Children)
                CollectDownloadableAssetReferences(child, output, node.Name);
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

        private static void CollectCommunityHashDownloadRequests(
            List<AssetDownloadRequest> requests,
            AppInfoKeyValue picsData,
            ulong remoteAppId,
            HashSet<string> seenUrls,
            HashSet<string> seenFileNames)
        {
            if (picsData == null)
                return;

            foreach (var asset in CommunityHashAssets)
            {
                var hash = TryExtractPicsSha1Hash(picsData, asset.PicsKey);
                if (string.IsNullOrWhiteSpace(hash))
                    continue;

                foreach (var extension in asset.Extensions)
                {
                    var urls = ServiceLocator.SteamStaticCdnPreferenceService
                        .GetCommunityAssetCandidateUrls(remoteAppId, hash, extension);
                    AddFallbackRequest(
                        requests,
                        seenUrls,
                        seenFileNames,
                        hash + "." + extension,
                        urls);
                }
            }
        }

        private static void CollectCatalogAssetDownloadRequests(
            List<AssetDownloadRequest> requests,
            GameAssetsSection catalogAssets,
            ulong remoteAppId,
            HashSet<string> seenUrls,
            HashSet<string> seenFileNames)
        {
            if (catalogAssets?.Items == null || catalogAssets.Items.Count == 0)
                return;

            foreach (var entry in catalogAssets.Items.OrderByDescending(item => IsEnglishCatalogAsset(item != null ? item.KeyPath : null)))
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Value))
                    continue;

                var value = entry.Value.Trim();
                if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                    || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    string fileName = null;
                    Uri uri;
                    if (Uri.TryCreate(value, UriKind.Absolute, out uri))
                        fileName = Path.GetFileName(uri.LocalPath);
                    if (string.IsNullOrWhiteSpace(fileName) || !HasImageFileExtension(fileName))
                        continue;

                    var urls = entry.CandidateUrls != null && entry.CandidateUrls.Count > 0
                        ? entry.CandidateUrls
                        : new[] { value };
                    var diskName = AllocateUniqueResourceFileName(fileName, seenFileNames);
                    if (string.IsNullOrWhiteSpace(diskName))
                        continue;
                    var uniqueUrls = urls
                        .Where(url => !string.IsNullOrWhiteSpace(url) && seenUrls.Add(url))
                        .ToArray();
                    if (uniqueUrls.Length == 0)
                    {
                        seenFileNames.Remove(diskName);
                        continue;
                    }

                    requests.Add(new AssetDownloadRequest
                    {
                        FileName = diskName,
                        CandidateUrls = uniqueUrls
                    });
                    continue;
                }

                if (IsDownloadableAssetReference(value))
                {
                    TryAddStorePathDownloadRequest(
                        requests,
                        seenUrls,
                        seenFileNames,
                        remoteAppId,
                        value);
                    continue;
                }

                if (!IsSha1HexHash(value))
                    continue;

                var extensions = TryResolveCommunityHashExtensions(entry.KeyPath);
                if (extensions == null || extensions.Length == 0)
                    continue;

                foreach (var extension in extensions)
                {
                    AddFallbackRequest(
                        requests,
                        seenUrls,
                        seenFileNames,
                        value + "." + extension,
                        ServiceLocator.SteamStaticCdnPreferenceService
                            .GetCommunityAssetCandidateUrls(remoteAppId, value, extension));
                }
            }
        }

        private static string[] TryResolveCommunityHashExtensions(string keyPath)
        {
            if (string.IsNullOrWhiteSpace(keyPath))
                return null;

            var key = keyPath.Trim().TrimEnd('/');
            var slash = key.LastIndexOf('/');
            var leaf = slash >= 0 ? key.Substring(slash + 1) : key;

            foreach (var asset in CommunityHashAssets)
            {
                if (leaf.Equals(asset.PicsKey, StringComparison.OrdinalIgnoreCase))
                    return asset.Extensions;
            }

            return null;
        }

        private static List<string> BuildStoreAssetCandidateUrls(ulong appId, string pathOrFileName)
        {
            return ServiceLocator.SteamStaticCdnPreferenceService
                .GetStoreAssetCandidateUrls(appId, pathOrFileName)
                .ToList();
        }

        private static bool IsStoreAssetRelativePath(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var normalized = value.Trim().TrimStart('/');
            var slashIndex = normalized.IndexOf('/');
            if (slashIndex != 40 || slashIndex >= normalized.Length - 1)
                return false;

            if (!IsSha1HexHash(normalized.Substring(0, 40)))
                return false;

            // Hashed store paths without an image extension are skipped (avoids downloading non-image blobs).
            return HasImageFileExtension(normalized.Substring(slashIndex + 1));
        }

        private static bool HasImageFileExtension(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            var extension = Path.GetExtension(fileName);
            if (string.IsNullOrEmpty(extension))
                return false;

            return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".gif", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".webp", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".ico", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".tga", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".icns", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsBareImageFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)
                || value.IndexOf('/') >= 0
                || value.IndexOf('\\') >= 0)
            {
                return false;
            }

            return HasImageFileExtension(value);
        }

        private string ResolvePreferredImagePath(ulong appId, string[] preferredFileNames)
        {
            var resourcesDirectory = PathConstants.CombineGamesPerAppResourcesDirectory(_gamesDirectory, appId.ToString());
            return ResolvePreferredImagePath(resourcesDirectory, preferredFileNames);
        }

        private static string ResolvePreferredImagePath(string resourcesDirectory, string[] preferredFileNames)
        {
            if (string.IsNullOrWhiteSpace(resourcesDirectory) || preferredFileNames == null)
                return null;

            foreach (var fileName in preferredFileNames)
            {
                if (string.IsNullOrWhiteSpace(fileName))
                    continue;

                var imagePath = Path.Combine(resourcesDirectory, fileName);
                if (File.Exists(imagePath))
                    return imagePath;
            }

            return null;
        }

        internal static string ResolveStrictListViewImagePath(string resourcesDirectory, string[] preferredFileNames)
        {
            return ResolvePreferredImagePath(resourcesDirectory, preferredFileNames);
        }

        private static string[] BuildStoreBannerPreferredFileNames(AppInfoKeyValue picsData)
        {
            var sources = new List<string>();
            TryAddEnglishHeaderImageFileName(sources, picsData);
            sources.AddRange(StoreBannerPreferredFileNames);
            return DeduplicateFileNames(sources);
        }

        private static string[] BuildLibraryCoverPreferredFileNames(AppInfoKeyValue picsData)
        {
            var sources = new List<string>();
            TryAddEnglishLibraryAssetFileName(sources, picsData, SteamPicsKeyNames.LibraryCapsule, prefer2x: true);
            TryAddEnglishLibraryAssetFileName(sources, picsData, SteamPicsKeyNames.LibraryCapsule, prefer2x: false);
            sources.AddRange(LibraryCoverPreferredFileNames);
            return DeduplicateFileNames(sources);
        }

        private static string[] BuildLibraryLogoPreferredFileNames(AppInfoKeyValue picsData)
        {
            var sources = new List<string>();
            TryAddEnglishLibraryAssetFileName(sources, picsData, SteamPicsKeyNames.LibraryLogo, prefer2x: true);
            TryAddEnglishLibraryAssetFileName(sources, picsData, SteamPicsKeyNames.LibraryLogo, prefer2x: false);
            var logoHash = TryExtractPicsSha1Hash(picsData, SteamPicsKeyNames.Logo);
            if (!string.IsNullOrWhiteSpace(logoHash))
                sources.Add(logoHash + ".jpg");
            sources.AddRange(LibraryLogoPreferredFileNames);
            return DeduplicateFileNames(sources);
        }

        private static AppCatalogSnapshot TryLoadCatalogSnapshot(ulong appId)
        {
            if (appId == 0)
                return null;
            return AppCatalogSnapshotStore.TryLoad(appId, out AppCatalogSnapshot snapshot) ? snapshot : null;
        }

        private static void TryAddEnglishLibraryAssetFileName(
            List<string> fileNames,
            AppInfoKeyValue picsData,
            string libraryAssetKey,
            bool prefer2x)
        {
            var relativePath = TryExtractEnglishLibraryAssetRelativePath(picsData, libraryAssetKey, prefer2x);
            TryAddFileNameFromRelativePath(fileNames, relativePath);
        }

        private static void TryAddEnglishHeaderImageFileName(List<string> fileNames, AppInfoKeyValue picsData)
        {
            TryAddFileNameFromRelativePath(fileNames, TryExtractHeaderImageRelativePath(picsData));
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
            AppInfoKeyValue picsData,
            string libraryAssetKey,
            bool prefer2x)
        {
            if (picsData == null || string.IsNullOrWhiteSpace(libraryAssetKey))
                return null;

            var appInfoTarget = AppInfoKeyValueHelper.ResolveAppInfoTarget(picsData);
            var common = AppInfoKeyValueHelper.FindChild(appInfoTarget, PathConstants.SteamAppsCommonDirectoryName);
            var libraryAssetsFull = AppInfoKeyValueHelper.FindChild(common, SteamPicsKeyNames.LibraryAssetsFull);
            var assetNode = AppInfoKeyValueHelper.FindChild(libraryAssetsFull, libraryAssetKey);
            var imageNode = AppInfoKeyValueHelper.FindChild(
                assetNode,
                prefer2x ? SteamPicsKeyNames.Image2x : SteamPicsKeyNames.Image);
            return TryExtractLocalizedRelativePath(imageNode, SteamPicsKeyNames.English);
        }

        private static string TryExtractLocalizedRelativePath(AppInfoKeyValue localizedNode, string preferredLanguageKey)
        {
            if (localizedNode == null)
                return null;

            if (!string.IsNullOrWhiteSpace(preferredLanguageKey))
            {
                var preferred = AppInfoKeyValueHelper.FindChild(localizedNode, preferredLanguageKey);
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

        private static string TryExtractPicsSha1Hash(AppInfoKeyValue appPicsData, string picsKeyName)
        {
            if (appPicsData == null || string.IsNullOrWhiteSpace(picsKeyName))
                return null;

            var appInfoTarget = AppInfoKeyValueHelper.ResolveAppInfoTarget(appPicsData);
            var common = AppInfoKeyValueHelper.FindChild(appInfoTarget, PathConstants.SteamAppsCommonDirectoryName);
            var hashNode = AppInfoKeyValueHelper.FindChild(common, picsKeyName);
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

        private static string TryExtractLibraryHeaderImageRelativePath(AppInfoKeyValue appPicsData)
        {
            return TryExtractEnglishLibraryAssetRelativePath(appPicsData, SteamPicsKeyNames.LibraryHeader, prefer2x: false);
        }

        private static string TryExtractLibraryHeroImageRelativePath(AppInfoKeyValue appPicsData)
        {
            return TryExtractEnglishLibraryAssetRelativePath(appPicsData, SteamPicsKeyNames.LibraryHero, prefer2x: false)
                ?? TryExtractEnglishLibraryAssetRelativePath(appPicsData, SteamPicsKeyNames.LibraryHero, prefer2x: true);
        }

        private static string TryExtractLibraryLogoImageRelativePath(AppInfoKeyValue appPicsData)
        {
            return TryExtractEnglishLibraryAssetRelativePath(appPicsData, SteamPicsKeyNames.LibraryLogo, prefer2x: false);
        }

        private static string TryExtractLibraryCapsuleImageRelativePath(AppInfoKeyValue appPicsData)
        {
            return TryExtractEnglishLibraryAssetRelativePath(appPicsData, SteamPicsKeyNames.LibraryCapsule, prefer2x: false);
        }

        private static string TryExtractHeaderImageRelativePath(AppInfoKeyValue appPicsData)
        {
            if (appPicsData == null)
                return null;

            var appInfoTarget = AppInfoKeyValueHelper.ResolveAppInfoTarget(appPicsData);
            var common = AppInfoKeyValueHelper.FindChild(appInfoTarget, PathConstants.SteamAppsCommonDirectoryName);
            var headerImage = AppInfoKeyValueHelper.FindChild(common, SteamPicsKeyNames.HeaderImage);
            return TryExtractLocalizedRelativePath(headerImage, SteamPicsKeyNames.English);
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
