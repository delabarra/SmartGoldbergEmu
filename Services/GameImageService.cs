using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        // Store Banner fallback; BuildStoreBannerPreferredFileNames tries the PICS header_image file name first.
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

        // Main list mosaic views: strict filename fallbacks when catalog/PICS has no preferred-language path.
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
            // Higher first: preferred settings language, then english, then other.
            public int LanguagePreferenceRank { get; set; }
        }

        private sealed class AssetDownloadRequest
        {
            public string FileName { get; set; }
            public string[] CandidateUrls { get; set; }
        }

        internal enum AssetDownloadOutcome
        {
            Skipped,
            AlreadyPresent,
            Downloaded,
            NotFound,
            Failed
        }

        internal sealed class AssetUrlAttempt
        {
            public string Host { get; set; }
            public TimeSpan Elapsed { get; set; }
            public bool Succeeded { get; set; }
            public bool IsNotFound { get; set; }
            public bool IsTimeout { get; set; }
            public string Failure { get; set; }
        }

        internal sealed class AssetDownloadResult
        {
            public string FileName { get; set; }
            public AssetDownloadOutcome Outcome { get; set; }
            public List<AssetUrlAttempt> Attempts { get; } = new List<AssetUrlAttempt>();
        }

        private static readonly TimeSpan SlowAssetAttemptThreshold = TimeSpan.FromSeconds(10);
        private const int MaxAssetFilesListedInLog = 25;

        private sealed class CachedListViewArtworkFileNames
        {
            public string[] Banner { get; set; }
            public string[] Cover { get; set; }
            public string[] Logo { get; set; }
        }

        private readonly IHttpService _httpService;
        private readonly string _gamesDirectory;
        private readonly ITaskReportService _taskReportService;
        private readonly FallbackMosaicArtCache _fallbackMosaicArtCache = new FallbackMosaicArtCache();
        private readonly object _waitingPlaceholderSync = new object();
        private readonly object _listViewArtworkPreferenceCacheSync = new object();
        private string _listViewArtworkPreferenceCacheLanguage;
        private readonly Dictionary<ulong, CachedListViewArtworkFileNames> _listViewArtworkPreferenceCache =
            new Dictionary<ulong, CachedListViewArtworkFileNames>();
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
            Action<int, int> onProgress = null,
            string listViewMode = null,
            Action onPrimaryListArtworkReady = null)
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

                string preferredLanguage = SteamConfiguredLanguageHelper.Resolve(appId);
                var downloadRequests = BuildAssetDownloadRequests(
                    picsData, catalogAssets, remoteAppId, preferredLanguage);
                catalogAssets = null;
                InvalidateListViewArtworkPreferenceCache(appId);
                var totalDownloads = downloadRequests.Count;

                if (reportFeedback)
                {
                    Feedback?.SetMessage($"Downloading game assets ({totalDownloads} files)...");
                    Feedback?.SetProgress(0, Math.Max(totalDownloads, 1));
                }
                onProgress?.Invoke(0, totalDownloads);

                if (_disposed)
                    return ApplyDownloadOutcomeFeedback(gamePath, displayName, appId, reportFeedback: false, picsData: picsData);

                PartitionListViewPriorityDownloads(
                    downloadRequests,
                    listViewMode,
                    picsData,
                    preferredLanguage,
                    appId,
                    out List<AssetDownloadRequest> priorityRequests,
                    out List<AssetDownloadRequest> remainingRequests);

                var completed = 0;
                var results = new List<AssetDownloadResult>(totalDownloads);
                var lockObj = new object();
                var stopwatch = Stopwatch.StartNew();
                bool viewArtworkNotified = false;

                async Task DownloadBatchAsync(List<AssetDownloadRequest> batch)
                {
                    if (batch == null || batch.Count == 0)
                        return;

                    await Task.WhenAll(batch.Select(async request =>
                    {
                        AssetDownloadResult result = await DownloadImageAsync(
                            remoteAppId,
                            gamePath,
                            request.FileName,
                            request.CandidateUrls).ConfigureAwait(false);

                        lock (lockObj)
                        {
                            results.Add(result);
                            if (_disposed || (!reportFeedback && onProgress == null))
                                return;
                            completed++;
                            if (reportFeedback)
                            {
                                Feedback?.SetProgress(completed, Math.Max(totalDownloads, 1));
                                Feedback?.SetMessage($"Downloading assets... {completed}/{totalDownloads}");
                            }
                            onProgress?.Invoke(completed, totalDownloads);
                        }
                    })).ConfigureAwait(false);
                }

                // Start both waves together (no throughput cap); priority is only for the early mosaic callback.
                Task priorityTask = DownloadBatchAsync(priorityRequests);
                Task remainingTask = DownloadBatchAsync(remainingRequests);
                await priorityTask.ConfigureAwait(false);
                if (!_disposed
                    && onPrimaryListArtworkReady != null
                    && !viewArtworkNotified
                    && !string.IsNullOrEmpty(ResolveArtworkPathForViewMode(appId, listViewMode)))
                {
                    viewArtworkNotified = true;
                    TryInvokePrimaryListArtworkReady(onPrimaryListArtworkReady);
                }

                await remainingTask.ConfigureAwait(false);

                stopwatch.Stop();
                if (results.Any(result => result.Outcome == AssetDownloadOutcome.Downloaded))
                    compactLoh = true;

                if (!_disposed)
                {
                    foreach (var line in BuildAssetDownloadReport(appId, remoteAppId, results, stopwatch.Elapsed))
                        WriteReportLine(line.Key, line.Value);
                }

                return ApplyDownloadOutcomeFeedback(gamePath, displayName, appId, reportFeedback && !_disposed, picsData);
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

        private static void TryInvokePrimaryListArtworkReady(Action onPrimaryListArtworkReady)
        {
            if (onPrimaryListArtworkReady == null)
                return;

            try
            {
                onPrimaryListArtworkReady();
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning("Primary list artwork ready callback failed: " + ex.Message);
            }
        }

        // Pull the current mosaic/icon artwork ahead of the remaining CDN set so the list can refresh early.
        private static void PartitionListViewPriorityDownloads(
            List<AssetDownloadRequest> downloadRequests,
            string listViewMode,
            AppInfoKeyValue picsData,
            string preferredLanguage,
            ulong appId,
            out List<AssetDownloadRequest> priorityRequests,
            out List<AssetDownloadRequest> remainingRequests)
        {
            priorityRequests = new List<AssetDownloadRequest>();
            remainingRequests = new List<AssetDownloadRequest>();
            if (downloadRequests == null || downloadRequests.Count == 0)
                return;

            if (string.IsNullOrWhiteSpace(listViewMode)
                || string.Equals(listViewMode, ApplicationConstants.ViewModeDetails, StringComparison.OrdinalIgnoreCase))
            {
                remainingRequests.AddRange(downloadRequests);
                return;
            }

            string[] preferredFileNames = BuildPreferredFileNamesForListViewMode(
                listViewMode, picsData, preferredLanguage, appId);
            if (preferredFileNames == null || preferredFileNames.Length == 0)
            {
                remainingRequests.AddRange(downloadRequests);
                return;
            }

            bool prioritizeClientIcons = string.Equals(
                listViewMode, ApplicationConstants.ViewModeIcons, StringComparison.OrdinalIgnoreCase);

            foreach (var request in downloadRequests)
            {
                if (request == null || string.IsNullOrWhiteSpace(request.FileName))
                {
                    remainingRequests.Add(request);
                    continue;
                }

                if (IsListViewPriorityFileName(request.FileName, preferredFileNames, prioritizeClientIcons))
                    priorityRequests.Add(request);
                else
                    remainingRequests.Add(request);
            }
        }

        private static string[] BuildPreferredFileNamesForListViewMode(
            string listViewMode,
            AppInfoKeyValue picsData,
            string preferredLanguage,
            ulong appId)
        {
            if (string.Equals(listViewMode, ApplicationConstants.ViewModeTile, StringComparison.OrdinalIgnoreCase))
                return BuildStoreBannerPreferredFileNames(picsData, preferredLanguage);
            if (string.Equals(listViewMode, ApplicationConstants.ViewModeCompactTiles, StringComparison.OrdinalIgnoreCase))
                return BuildListViewLibraryCoverPreferredFileNames(picsData, preferredLanguage);
            if (string.Equals(listViewMode, ApplicationConstants.ViewModeLogos, StringComparison.OrdinalIgnoreCase))
                return BuildListViewLogoPreferredFileNames(picsData, preferredLanguage);
            if (string.Equals(listViewMode, ApplicationConstants.ViewModeIcons, StringComparison.OrdinalIgnoreCase))
            {
                return new[]
                {
                    PathConstants.GetSteamGameResourcesClientIconFileName(appId)
                };
            }

            return Array.Empty<string>();
        }

        private static bool IsListViewPriorityFileName(
            string fileName,
            string[] preferredFileNames,
            bool prioritizeClientIcons)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            if (prioritizeClientIcons
                && fileName.EndsWith(
                    PathConstants.SteamGameResourcesClientIconFileExtension,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            foreach (var preferred in preferredFileNames ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(preferred))
                    continue;
                if (string.Equals(fileName, preferred, StringComparison.OrdinalIgnoreCase))
                    return true;
                if (fileName.EndsWith("_" + preferred, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
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

        // Store Banner: preferred-language header when catalog knows it; else header.jpg. Missing → mosaic placeholder.
        public string GetHeaderImagePathOrFallback(ulong appId)
        {
            return ResolveListViewArtworkPath(appId, ListViewArtworkRole.Banner);
        }

        // Library Cover: preferred-language capsule when catalog knows it; else 2x/1x. Missing → mosaic placeholder.
        public string GetCapsuleImagePathOrFallback(ulong appId)
        {
            return ResolveListViewArtworkPath(appId, ListViewArtworkRole.Cover);
        }

        // Logos: preferred-language library_logo when catalog knows it; else library_logo/logo. Missing → mosaic placeholder.
        public string GetLogoImagePathOrFallback(ulong appId)
        {
            return ResolveListViewArtworkPath(appId, ListViewArtworkRole.Logo);
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
            if (string.Equals(viewMode, ApplicationConstants.ViewModeTile, StringComparison.OrdinalIgnoreCase))
                return GetHeaderImagePathOrFallback(appId);
            if (string.Equals(viewMode, ApplicationConstants.ViewModeCompactTiles, StringComparison.OrdinalIgnoreCase))
                return GetCapsuleImagePathOrFallback(appId);
            if (string.Equals(viewMode, ApplicationConstants.ViewModeLogos, StringComparison.OrdinalIgnoreCase))
                return GetLogoImagePathOrFallback(appId);
            if (string.Equals(viewMode, ApplicationConstants.ViewModeIcons, StringComparison.OrdinalIgnoreCase))
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
            string gameDisplayName,
            ulong appId,
            bool reportFeedback,
            AppInfoKeyValue picsData)
        {
            UpdateMissingAssetsNote(resourcesDirectory, gameDisplayName, appId, picsData);

            string preferredLanguage = SteamConfiguredLanguageHelper.Resolve(appId);
            bool hasHeader = !string.IsNullOrEmpty(
                ResolvePreferredImagePath(
                    resourcesDirectory, BuildStoreBannerPreferredFileNames(picsData, preferredLanguage)));
            bool hasIcon = HasClientIconResource(resourcesDirectory, appId);
            bool hasCapsule = !string.IsNullOrEmpty(
                ResolvePreferredImagePath(
                    resourcesDirectory, BuildLibraryCoverPreferredFileNames(picsData, preferredLanguage)));
            bool essentialsOk = hasHeader && hasIcon && hasCapsule;

            if (!essentialsOk)
            {
                var missing = new List<string>(4);
                if (!hasHeader)
                    missing.Add("store banner (header.jpg)");
                if (!hasCapsule)
                    missing.Add("library cover (library_capsule_2x.jpg, library_capsule.jpg, or library_600x900.jpg)");
                if (!hasIcon)
                    missing.Add("client icon (.ico)");

                Program.LogService?.LogWarning(
                    $"Assets for AppId {appId} incomplete; the game list will show placeholders for: {string.Join(", ", missing)}");
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
            AppInfoKeyValue picsData,
            string preferredLanguage)
        {
            var missing = new List<string>(3);
            if (string.IsNullOrEmpty(
                ResolvePreferredImagePath(
                    resourcesDirectory, BuildStoreBannerPreferredFileNames(picsData, preferredLanguage))))
            {
                missing.Add("store banner");
            }

            if (string.IsNullOrEmpty(
                ResolvePreferredImagePath(
                    resourcesDirectory, BuildLibraryCoverPreferredFileNames(picsData, preferredLanguage))))
            {
                missing.Add("library cover");
            }

            if (string.IsNullOrEmpty(
                ResolvePreferredImagePath(
                    resourcesDirectory, BuildLibraryLogoPreferredFileNames(picsData, preferredLanguage))))
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
            var missingArtwork = CollectMissingLibraryArtworkFileNames(
                resourcesDirectory, picsData, SteamConfiguredLanguageHelper.Resolve(appId));
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

        private async Task<AssetUrlAttempt> TryDownloadImageFromUrlAsync(string url, string imagePath)
        {
            var attempt = new AssetUrlAttempt { Host = GetUrlHostForLog(url) };
            var stopwatch = Stopwatch.StartNew();
            try
            {
                await HttpHelpers.DownloadFileAtomicAsync(_httpService, url, imagePath).ConfigureAwait(false);
                attempt.Succeeded = true;
            }
            catch (Exception ex)
            {
                attempt.IsNotFound = HttpHelpers.IsNotFound(ex);
                attempt.IsTimeout = HttpHelpers.IsTimeout(ex);
                attempt.Failure = HttpHelpers.DescribeDownloadFailure(ex, stopwatch.Elapsed);
            }

            attempt.Elapsed = stopwatch.Elapsed;
            return attempt;
        }

        private async Task<AssetDownloadResult> DownloadImageAsync(
            ulong appId,
            string gamePath,
            string fileName,
            string[] candidateUrls)
        {
            var result = new AssetDownloadResult { FileName = fileName, Outcome = AssetDownloadOutcome.Skipped };
            if (_disposed)
                return result;

            var imagePath = Path.Combine(gamePath, fileName);
            if (File.Exists(imagePath))
            {
                result.Outcome = AssetDownloadOutcome.AlreadyPresent;
                return result;
            }

            if (candidateUrls == null || candidateUrls.Length == 0)
                return result;

            foreach (var url in candidateUrls)
            {
                if (_disposed || string.IsNullOrEmpty(url))
                    continue;

                AssetUrlAttempt attempt = await TryDownloadImageFromUrlAsync(url, imagePath).ConfigureAwait(false);
                result.Attempts.Add(attempt);
                if (!attempt.Succeeded)
                    continue;

                result.Outcome = AssetDownloadOutcome.Downloaded;
                Program.LogService?.LogDebug(
                    $"Downloaded {fileName} for AppId {appId} from {attempt.Host} in {HttpHelpers.FormatSeconds(attempt.Elapsed)}");
                return result;
            }

            if (_disposed || result.Attempts.Count == 0)
                return result;

            result.Outcome = result.Attempts.All(attempt => attempt.IsNotFound)
                ? AssetDownloadOutcome.NotFound
                : AssetDownloadOutcome.Failed;
            return result;
        }

        private static string GetUrlHostForLog(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out Uri uri) ? uri.Host : (url ?? string.Empty);
        }

        private static void WriteReportLine(LogLevel level, string message)
        {
            var log = Program.LogService;
            if (log == null)
                return;
            if (level >= LogLevel.Warning)
                log.LogWarning(message);
            else
                log.LogMessage(message);
        }

        // Summary always; failures with per-mirror reasons, 404-only names, and slow/timed-out hosts only when present.
        internal static List<KeyValuePair<LogLevel, string>> BuildAssetDownloadReport(
            ulong appId,
            ulong remoteAppId,
            IReadOnlyCollection<AssetDownloadResult> results,
            TimeSpan elapsed)
        {
            var lines = new List<KeyValuePair<LogLevel, string>>();
            if (results == null)
                return lines;

            string appLabel = remoteAppId != 0 && remoteAppId != appId
                ? $"AppId {appId} (Steam AppId {remoteAppId})"
                : $"AppId {appId}";

            var failed = results.Where(r => r.Outcome == AssetDownloadOutcome.Failed)
                .OrderBy(r => r.FileName, StringComparer.OrdinalIgnoreCase).ToList();
            var notFound = results.Where(r => r.Outcome == AssetDownloadOutcome.NotFound)
                .OrderBy(r => r.FileName, StringComparer.OrdinalIgnoreCase).ToList();
            int downloaded = results.Count(r => r.Outcome == AssetDownloadOutcome.Downloaded);
            int present = results.Count(r => r.Outcome == AssetDownloadOutcome.AlreadyPresent);
            int skipped = results.Count(r => r.Outcome == AssetDownloadOutcome.Skipped);

            string summary = $"Assets for {appLabel}: {results.Count} requested in {HttpHelpers.FormatSeconds(elapsed)}; "
                + $"{downloaded} downloaded, {present} already on disk, {notFound.Count} not on CDN, {failed.Count} failed";
            if (skipped > 0)
                summary += $", {skipped} skipped";
            lines.Add(new KeyValuePair<LogLevel, string>(failed.Count > 0 ? LogLevel.Warning : LogLevel.Info, summary + "."));

            if (failed.Count > 0)
            {
                var details = failed.Select(r => r.FileName + " ["
                    + string.Join("; ", r.Attempts.Select(a => a.Host + ": " + a.Failure)) + "]");
                lines.Add(new KeyValuePair<LogLevel, string>(
                    LogLevel.Warning,
                    $"Asset downloads failed for {appLabel}: {JoinLimited(details)}"));
            }

            if (notFound.Count > 0)
            {
                lines.Add(new KeyValuePair<LogLevel, string>(
                    LogLevel.Info,
                    $"Assets for {appLabel} not on any CDN mirror (HTTP 404): {JoinLimited(notFound.Select(r => r.FileName))}"));
            }

            var slowHosts = results
                .SelectMany(r => r.Attempts)
                .GroupBy(a => a.Host, StringComparer.OrdinalIgnoreCase)
                .Select(g => new
                {
                    Host = g.Key,
                    Requests = g.Count(),
                    Timeouts = g.Count(a => a.IsTimeout),
                    Slow = g.Count(a => !a.IsTimeout && a.Elapsed >= SlowAssetAttemptThreshold)
                })
                .Where(h => h.Timeouts > 0 || h.Slow > 0)
                .OrderByDescending(h => h.Timeouts)
                .ThenByDescending(h => h.Slow)
                .ToList();
            if (slowHosts.Count > 0)
            {
                var hostDetails = slowHosts.Select(h =>
                    $"{h.Host} {h.Timeouts} timed out, {h.Slow} over {SlowAssetAttemptThreshold.TotalSeconds:0}s (of {h.Requests} requests)");
                lines.Add(new KeyValuePair<LogLevel, string>(
                    LogLevel.Warning,
                    $"Slow CDN responses for {appLabel}: {string.Join("; ", hostDetails)}"));
            }

            return lines;
        }

        private static string JoinLimited(IEnumerable<string> items)
        {
            var list = items.ToList();
            if (list.Count <= MaxAssetFilesListedInLog)
                return string.Join(", ", list);
            return string.Join(", ", list.Take(MaxAssetFilesListedInLog))
                + $", and {list.Count - MaxAssetFilesListedInLog} more";
        }

        private static List<AssetDownloadRequest> BuildAssetDownloadRequests(
            AppInfoKeyValue picsData,
            GameAssetsSection catalogAssets,
            ulong remoteAppId,
            string preferredLanguage)
        {
            preferredLanguage = SteamConfiguredLanguageHelper.Normalize(preferredLanguage);
            var requests = new List<AssetDownloadRequest>();
            var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (picsData != null)
            {
                var appInfoTarget = AppInfoKeyValueHelper.ResolveAppInfoTarget(picsData);
                var storeAssets = new List<PicsStoreAssetRef>();
                CollectDownloadableAssetReferences(appInfoTarget, storeAssets, preferredLanguage);

                foreach (var storeAsset in storeAssets
                    .OrderByDescending(asset => asset.LanguagePreferenceRank)
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

            CollectCatalogAssetDownloadRequests(
                requests, catalogAssets, remoteAppId, seenUrls, seenFileNames, preferredLanguage);
            AppendFallbackAssetDownloadRequests(
                requests, picsData, remoteAppId, seenUrls, seenFileNames, preferredLanguage);

            return requests;
        }

        private static void AppendFallbackAssetDownloadRequests(
            List<AssetDownloadRequest> requests,
            AppInfoKeyValue picsData,
            ulong remoteAppId,
            HashSet<string> seenUrls,
            HashSet<string> seenFileNames,
            string preferredLanguage)
        {
            AppendPreferredFilenameDownloads(
                requests,
                remoteAppId,
                picsData,
                seenUrls,
                seenFileNames,
                LibraryHeaderPreferredFileNames,
                data => TryExtractLibraryHeaderImageRelativePath(data, preferredLanguage));

            AppendPreferredFilenameDownloads(
                requests,
                remoteAppId,
                picsData,
                seenUrls,
                seenFileNames,
                HeaderImagePreferredFileNames,
                data => TryExtractHeaderImageRelativePath(data, preferredLanguage));

            AppendPreferredFilenameDownloads(
                requests,
                remoteAppId,
                picsData,
                seenUrls,
                seenFileNames,
                LibraryCoverPreferredFileNames,
                data => TryExtractLibraryCapsuleImageRelativePath(data, preferredLanguage));

            AppendPreferredFilenameDownloads(
                requests,
                remoteAppId,
                picsData,
                seenUrls,
                seenFileNames,
                LibraryLogoPreferredFileNames,
                data => TryExtractLibraryLogoImageRelativePath(data, preferredLanguage));

            AppendPreferredFilenameDownloads(
                requests,
                remoteAppId,
                picsData,
                seenUrls,
                seenFileNames,
                LibraryHeroPreferredFileNames,
                data => TryExtractLibraryHeroImageRelativePath(data, preferredLanguage));

            AppendPreferredFilenameDownloads(
                requests,
                remoteAppId,
                picsData,
                seenUrls,
                seenFileNames,
                LibraryLogoPicsPreferredFileNames,
                data => TryExtractLibraryLogoImageRelativePath(data, preferredLanguage));

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

        // Preferred-language (then English) copy keeps the Steam filename; extra language/hash variants keep `{hash}_{file}`.
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

        private static int GetCatalogAssetLanguageRank(string keyPath, string preferredLanguage)
        {
            if (string.IsNullOrWhiteSpace(keyPath))
                return 0;

            preferredLanguage = SteamConfiguredLanguageHelper.Normalize(preferredLanguage);
            var parts = keyPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            int best = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                best = Math.Max(best, GetLanguagePreferenceRank(parts[i], preferredLanguage));
            }

            return best;
        }

        private static int GetLanguagePreferenceRank(string languageKey, string preferredLanguage)
        {
            if (string.IsNullOrWhiteSpace(languageKey))
                return 0;

            preferredLanguage = SteamConfiguredLanguageHelper.Normalize(preferredLanguage);
            if (languageKey.Equals(preferredLanguage, StringComparison.OrdinalIgnoreCase))
                return 2;
            if (languageKey.Equals(SteamPicsKeyNames.English, StringComparison.OrdinalIgnoreCase))
                return 1;
            return 0;
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

        private static void CollectDownloadableAssetReferences(
            AppInfoKeyValue node,
            List<PicsStoreAssetRef> output,
            string preferredLanguage)
        {
            CollectDownloadableAssetReferences(node, output, preferredLanguage, parentName: null);
        }

        private static void CollectDownloadableAssetReferences(
            AppInfoKeyValue node,
            List<PicsStoreAssetRef> output,
            string preferredLanguage,
            string parentName)
        {
            if (node == null || output == null)
                return;

            if (string.Equals(node.Name, "ufs", StringComparison.OrdinalIgnoreCase))
                return;

            if (!string.IsNullOrWhiteSpace(node.Value) && IsDownloadableAssetReference(node.Value))
            {
                int rank = Math.Max(
                    GetLanguagePreferenceRank(node.Name, preferredLanguage),
                    GetLanguagePreferenceRank(parentName, preferredLanguage));
                output.Add(new PicsStoreAssetRef
                {
                    RelativePath = node.Value.Trim(),
                    LanguagePreferenceRank = rank
                });
            }

            if (node.Children == null)
                return;

            foreach (var child in node.Children)
                CollectDownloadableAssetReferences(child, output, preferredLanguage, node.Name);
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
            HashSet<string> seenFileNames,
            string preferredLanguage)
        {
            if (catalogAssets?.Items == null || catalogAssets.Items.Count == 0)
                return;

            foreach (var entry in catalogAssets.Items.OrderByDescending(
                item => GetCatalogAssetLanguageRank(item != null ? item.KeyPath : null, preferredLanguage)))
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

        private enum ListViewArtworkRole
        {
            Banner,
            Cover,
            Logo
        }

        private string ResolveListViewArtworkPath(ulong appId, ListViewArtworkRole role)
        {
            string[] preferredFileNames = GetOrBuildListViewArtworkFileNames(appId, role);
            return ResolvePreferredImagePath(appId, preferredFileNames);
        }

        private string[] GetOrBuildListViewArtworkFileNames(ulong appId, ListViewArtworkRole role)
        {
            string preferredLanguage = SteamConfiguredLanguageHelper.Resolve(appId);
            CachedListViewArtworkFileNames cached;
            lock (_listViewArtworkPreferenceCacheSync)
            {
                if (!string.Equals(
                        _listViewArtworkPreferenceCacheLanguage,
                        preferredLanguage,
                        StringComparison.OrdinalIgnoreCase))
                {
                    _listViewArtworkPreferenceCache.Clear();
                    _listViewArtworkPreferenceCacheLanguage = preferredLanguage;
                }

                if (_listViewArtworkPreferenceCache.TryGetValue(appId, out cached) && cached != null)
                    return SelectListViewArtworkFileNames(cached, role);
            }

            AppInfoKeyValue picsData = TryLoadCatalogSnapshot(appId)?.AppInfo;
            cached = new CachedListViewArtworkFileNames
            {
                Banner = BuildStoreBannerPreferredFileNames(picsData, preferredLanguage),
                Cover = BuildListViewLibraryCoverPreferredFileNames(picsData, preferredLanguage),
                Logo = BuildListViewLogoPreferredFileNames(picsData, preferredLanguage)
            };

            lock (_listViewArtworkPreferenceCacheSync)
            {
                if (!string.Equals(
                        _listViewArtworkPreferenceCacheLanguage,
                        preferredLanguage,
                        StringComparison.OrdinalIgnoreCase))
                {
                    // Language changed while loading; do not poison the new language's cache.
                    return SelectListViewArtworkFileNames(cached, role);
                }

                CachedListViewArtworkFileNames existing;
                if (_listViewArtworkPreferenceCache.TryGetValue(appId, out existing) && existing != null)
                    return SelectListViewArtworkFileNames(existing, role);

                _listViewArtworkPreferenceCache[appId] = cached;
                return SelectListViewArtworkFileNames(cached, role);
            }
        }

        private static string[] SelectListViewArtworkFileNames(
            CachedListViewArtworkFileNames cached,
            ListViewArtworkRole role)
        {
            switch (role)
            {
                case ListViewArtworkRole.Banner:
                    return cached?.Banner ?? StoreBannerPreferredFileNames;
                case ListViewArtworkRole.Logo:
                    return cached?.Logo ?? ListViewLogoFileNames;
                default:
                    return cached?.Cover ?? ListViewLibraryCoverFileNames;
            }
        }

        private void InvalidateListViewArtworkPreferenceCache(ulong appId)
        {
            lock (_listViewArtworkPreferenceCacheSync)
            {
                if (appId == 0)
                    _listViewArtworkPreferenceCache.Clear();
                else
                    _listViewArtworkPreferenceCache.Remove(appId);
            }
        }

        private static string[] BuildStoreBannerPreferredFileNames(
            AppInfoKeyValue picsData,
            string preferredLanguage)
        {
            var sources = new List<string>();
            TryAddPreferredHeaderImageFileName(sources, picsData, preferredLanguage);
            sources.AddRange(StoreBannerPreferredFileNames);
            return DeduplicateFileNames(sources);
        }

        private static string[] BuildLibraryCoverPreferredFileNames(
            AppInfoKeyValue picsData,
            string preferredLanguage)
        {
            return BuildLibraryCoverPreferredFileNames(
                picsData, preferredLanguage, LibraryCoverPreferredFileNames);
        }

        // List mosaic cover: preferred-language PICS capsule names, then strict list-view fallbacks (no small_capsule).
        internal static string[] BuildListViewLibraryCoverPreferredFileNames(
            AppInfoKeyValue picsData,
            string preferredLanguage)
        {
            return BuildLibraryCoverPreferredFileNames(
                picsData, preferredLanguage, ListViewLibraryCoverFileNames);
        }

        private static string[] BuildLibraryCoverPreferredFileNames(
            AppInfoKeyValue picsData,
            string preferredLanguage,
            string[] fallbackFileNames)
        {
            var sources = new List<string>();
            TryAddPreferredLibraryAssetFileName(
                sources, picsData, SteamPicsKeyNames.LibraryCapsule, prefer2x: true, preferredLanguage);
            TryAddPreferredLibraryAssetFileName(
                sources, picsData, SteamPicsKeyNames.LibraryCapsule, prefer2x: false, preferredLanguage);
            if (fallbackFileNames != null)
                sources.AddRange(fallbackFileNames);
            return DeduplicateFileNames(sources);
        }

        private static string[] BuildLibraryLogoPreferredFileNames(
            AppInfoKeyValue picsData,
            string preferredLanguage)
        {
            return BuildLibraryLogoPreferredFileNames(
                picsData,
                preferredLanguage,
                LibraryLogoPreferredFileNames,
                includeCommunityLogoHash: true);
        }

        // List mosaic logos: preferred-language PICS library_logo, then strict list-view logo fallbacks.
        internal static string[] BuildListViewLogoPreferredFileNames(
            AppInfoKeyValue picsData,
            string preferredLanguage)
        {
            return BuildLibraryLogoPreferredFileNames(
                picsData,
                preferredLanguage,
                ListViewLogoFileNames,
                includeCommunityLogoHash: false);
        }

        private static string[] BuildLibraryLogoPreferredFileNames(
            AppInfoKeyValue picsData,
            string preferredLanguage,
            string[] fallbackFileNames,
            bool includeCommunityLogoHash)
        {
            var sources = new List<string>();
            TryAddPreferredLibraryAssetFileName(
                sources, picsData, SteamPicsKeyNames.LibraryLogo, prefer2x: true, preferredLanguage);
            TryAddPreferredLibraryAssetFileName(
                sources, picsData, SteamPicsKeyNames.LibraryLogo, prefer2x: false, preferredLanguage);
            if (includeCommunityLogoHash)
            {
                var logoHash = TryExtractPicsSha1Hash(picsData, SteamPicsKeyNames.Logo);
                if (!string.IsNullOrWhiteSpace(logoHash))
                    sources.Add(logoHash + ".jpg");
            }

            if (fallbackFileNames != null)
                sources.AddRange(fallbackFileNames);
            return DeduplicateFileNames(sources);
        }

        private static AppCatalogSnapshot TryLoadCatalogSnapshot(ulong appId)
        {
            if (appId == 0)
                return null;
            return AppCatalogSnapshotStore.TryLoad(appId, out AppCatalogSnapshot snapshot) ? snapshot : null;
        }

        private static void TryAddPreferredLibraryAssetFileName(
            List<string> fileNames,
            AppInfoKeyValue picsData,
            string libraryAssetKey,
            bool prefer2x,
            string preferredLanguage)
        {
            var relativePath = TryExtractPreferredLibraryAssetRelativePath(
                picsData, libraryAssetKey, prefer2x, preferredLanguage);
            TryAddFileNameFromRelativePath(fileNames, relativePath);
        }

        private static void TryAddPreferredHeaderImageFileName(
            List<string> fileNames,
            AppInfoKeyValue picsData,
            string preferredLanguage)
        {
            TryAddFileNameFromRelativePath(
                fileNames, TryExtractHeaderImageRelativePath(picsData, preferredLanguage));
        }

        private static void TryAddFileNameFromRelativePath(List<string> fileNames, string relativePath)
        {
            if (fileNames == null || string.IsNullOrWhiteSpace(relativePath))
                return;

            var normalized = relativePath.Replace('\\', '/').Trim().TrimStart('/');
            var fileName = Path.GetFileName(normalized);
            if (string.IsNullOrWhiteSpace(fileName))
                return;

            // Prefer `{hash}_{file}` so a later language change can select the exact localization on disk.
            var hashFolder = TryExtractRelativeDirectoryName(normalized);
            if (!string.IsNullOrWhiteSpace(hashFolder))
                fileNames.Add(hashFolder + "_" + fileName);

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

        private static string TryExtractPreferredLibraryAssetRelativePath(
            AppInfoKeyValue picsData,
            string libraryAssetKey,
            bool prefer2x,
            string preferredLanguage)
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
            return TryExtractLocalizedRelativePath(imageNode, preferredLanguage);
        }

        // Preferred settings language, then english, then any available localization.
        internal static string TryExtractLocalizedRelativePath(
            AppInfoKeyValue localizedNode,
            string preferredLanguageKey)
        {
            if (localizedNode == null)
                return null;

            preferredLanguageKey = SteamConfiguredLanguageHelper.Normalize(preferredLanguageKey);

            string preferred = TryGetLanguageChildValue(localizedNode, preferredLanguageKey);
            if (preferred != null)
                return preferred;

            if (!preferredLanguageKey.Equals(SteamPicsKeyNames.English, StringComparison.OrdinalIgnoreCase))
            {
                string english = TryGetLanguageChildValue(localizedNode, SteamPicsKeyNames.English);
                if (english != null)
                    return english;
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

        private static string TryGetLanguageChildValue(AppInfoKeyValue localizedNode, string languageKey)
        {
            if (localizedNode == null || string.IsNullOrWhiteSpace(languageKey))
                return null;

            var child = AppInfoKeyValueHelper.FindChild(localizedNode, languageKey);
            return string.IsNullOrWhiteSpace(child?.Value) ? null : child.Value.Trim();
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

        private static string TryExtractLibraryHeaderImageRelativePath(
            AppInfoKeyValue appPicsData,
            string preferredLanguage)
        {
            return TryExtractPreferredLibraryAssetRelativePath(
                appPicsData, SteamPicsKeyNames.LibraryHeader, prefer2x: false, preferredLanguage);
        }

        private static string TryExtractLibraryHeroImageRelativePath(
            AppInfoKeyValue appPicsData,
            string preferredLanguage)
        {
            return TryExtractPreferredLibraryAssetRelativePath(
                    appPicsData, SteamPicsKeyNames.LibraryHero, prefer2x: false, preferredLanguage)
                ?? TryExtractPreferredLibraryAssetRelativePath(
                    appPicsData, SteamPicsKeyNames.LibraryHero, prefer2x: true, preferredLanguage);
        }

        private static string TryExtractLibraryLogoImageRelativePath(
            AppInfoKeyValue appPicsData,
            string preferredLanguage)
        {
            return TryExtractPreferredLibraryAssetRelativePath(
                appPicsData, SteamPicsKeyNames.LibraryLogo, prefer2x: false, preferredLanguage);
        }

        private static string TryExtractLibraryCapsuleImageRelativePath(
            AppInfoKeyValue appPicsData,
            string preferredLanguage)
        {
            return TryExtractPreferredLibraryAssetRelativePath(
                appPicsData, SteamPicsKeyNames.LibraryCapsule, prefer2x: false, preferredLanguage);
        }

        private static string TryExtractHeaderImageRelativePath(
            AppInfoKeyValue appPicsData,
            string preferredLanguage)
        {
            if (appPicsData == null)
                return null;

            var appInfoTarget = AppInfoKeyValueHelper.ResolveAppInfoTarget(appPicsData);
            var common = AppInfoKeyValueHelper.FindChild(appInfoTarget, PathConstants.SteamAppsCommonDirectoryName);
            var headerImage = AppInfoKeyValueHelper.FindChild(common, SteamPicsKeyNames.HeaderImage);
            return TryExtractLocalizedRelativePath(headerImage, preferredLanguage);
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
