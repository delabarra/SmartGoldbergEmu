using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.JsonKit;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Services
{
    public sealed class SteamStaticCdnPreferenceService
    {
        private readonly object _sync = new object();
        private SteamStaticCdnPreferences _preferences;
        private bool _warmUpScheduled;
        private bool _warmUpRunning;

        public IReadOnlyList<string> GetStoreAssetCandidateUrls(ulong appId, string pathOrFileName)
        {
            ScheduleWarmUpIfNeeded();
            var preferences = GetActivePreferences();
            return BuildStoreAssetCandidateUrls(appId, pathOrFileName, preferences);
        }

        public IReadOnlyList<string> GetCommunityClientIconCandidateUrls(ulong appId, string hash)
        {
            return GetCommunityAssetCandidateUrls(appId, hash, "ico");
        }

        public IReadOnlyList<string> GetCommunityAssetCandidateUrls(ulong appId, string hash, string extension)
        {
            ScheduleWarmUpIfNeeded();
            if (appId == 0 || string.IsNullOrWhiteSpace(hash) || string.IsNullOrWhiteSpace(extension))
                return Array.Empty<string>();

            var ext = extension.Trim().TrimStart('.');
            if (ext.Length == 0)
                return Array.Empty<string>();

            var preferences = GetActivePreferences();
            return BuildUniqueUrls(
                preferences.SharedFastlyHosts,
                host => BuildHttpsUrl(host, $"community_assets/images/apps/{appId}/{hash}.{ext}"));
        }

        public IReadOnlyList<string> GetAchievementIconCandidateUrls(string apiUrl)
        {
            ScheduleWarmUpIfNeeded();
            if (string.IsNullOrWhiteSpace(apiUrl))
                return Array.Empty<string>();

            if (!Uri.TryCreate(apiUrl, UriKind.Absolute, out var sourceUri))
                return new[] { apiUrl };

            var preferences = GetActivePreferences();
            var urls = new List<string>();
            AddCandidate(urls, apiUrl);

            // Schema URLs use steamcommunity/public/images; newer icons are often only on community_assets.
            bool rewriteHosts = IsRewriteableSteamCdnHost(sourceUri.Host);
            if (rewriteHosts)
            {
                var pathAndQuery = string.IsNullOrEmpty(sourceUri.PathAndQuery)
                    ? string.Empty
                    : sourceUri.PathAndQuery.TrimStart('/');

                foreach (var host in preferences.GeneralCdnHosts ?? new List<string>())
                {
                    if (string.Equals(host, sourceUri.Host, StringComparison.OrdinalIgnoreCase))
                        continue;

                    AddCandidate(urls, BuildHttpsUrl(host, pathAndQuery));
                }
            }

            if (TryParseSteamCommunityAppImagePath(sourceUri.AbsolutePath, out ulong appId, out string fileName))
            {
                foreach (var host in preferences.SharedFastlyHosts ?? new List<string>())
                {
                    AddCandidate(
                        urls,
                        BuildHttpsUrl(host, $"community_assets/images/apps/{appId}/{fileName}"));
                }
            }

            if (urls.Count == 0)
                urls.Add(apiUrl);

            return urls;
        }

        // Parses /steamcommunity/public/images/apps/{appId}/{file} from a Steam Web API icon URL path.
        internal static bool TryParseSteamCommunityAppImagePath(string absolutePath, out ulong appId, out string fileName)
        {
            appId = 0;
            fileName = null;
            if (string.IsNullOrWhiteSpace(absolutePath))
                return false;

            string[] parts = absolutePath.Trim('/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 6)
                return false;

            if (!parts[0].Equals("steamcommunity", StringComparison.OrdinalIgnoreCase)
                || !parts[1].Equals("public", StringComparison.OrdinalIgnoreCase)
                || !parts[2].Equals("images", StringComparison.OrdinalIgnoreCase)
                || !parts[3].Equals("apps", StringComparison.OrdinalIgnoreCase)
                || !ulong.TryParse(parts[4], out appId)
                || appId == 0
                || string.IsNullOrWhiteSpace(parts[5]))
            {
                return false;
            }

            fileName = parts[5].Trim();
            return fileName.Length > 0;
        }

        public IReadOnlyList<string> GetClientSoundsPackageCandidateUrls()
        {
            ScheduleWarmUpIfNeeded();
            var preferences = GetActivePreferences();
            return BuildUniqueUrls(
                preferences.ClientPackageHosts,
                host => BuildHttpsUrl(host, SteamStaticCdnConstants.ClientSoundsPackageRelativePath));
        }

        public IReadOnlyList<string> GetClientWin32ManifestCandidateUrls()
        {
            ScheduleWarmUpIfNeeded();
            var preferences = GetActivePreferences();
            return BuildUniqueUrls(
                preferences.ClientPackageHosts,
                host => BuildHttpsUrl(host, SteamStaticCdnConstants.ClientWin32ManifestRelativePath));
        }

        public IReadOnlyList<string> GetClientPackageCandidateUrls(string relativePathUnderClientHost)
        {
            ScheduleWarmUpIfNeeded();
            if (string.IsNullOrWhiteSpace(relativePathUnderClientHost))
                return Array.Empty<string>();

            var preferences = GetActivePreferences();
            return BuildUniqueUrls(
                preferences.ClientPackageHosts,
                host => BuildHttpsUrl(host, relativePathUnderClientHost.Trim().TrimStart('/')));
        }

        public async Task WarmUpAsync(CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                if (_warmUpRunning)
                    return;
                _warmUpRunning = true;
            }

            try
            {
                var cached = TryLoadPreferencesFromDisk();
                if (cached != null && !cached.IsExpired(SteamStaticCdnConstants.PreferencesCacheTtl))
                {
                    ReplacePreferences(cached);
                    return;
                }

                var probed = await ProbePreferencesAsync(cancellationToken).ConfigureAwait(false);
                ReplacePreferences(probed);
                TrySavePreferencesToDisk(probed);
            }
            finally
            {
                lock (_sync)
                {
                    _warmUpRunning = false;
                }
            }
        }

        internal void SetPreferencesForTests(SteamStaticCdnPreferences preferences)
        {
            lock (_sync)
            {
                // Keep background CDN warm-up from replacing explicit test preferences.
                _warmUpScheduled = true;
                _warmUpRunning = false;
                _preferences = NormalizePreferences(preferences ?? CreateDefaultPreferences());
            }
        }

        internal static IReadOnlyList<string> BuildStoreAssetCandidateUrls(
            ulong appId,
            string pathOrFileName,
            SteamStaticCdnPreferences preferences)
        {
            var urls = new List<string>();
            if (appId == 0 || string.IsNullOrWhiteSpace(pathOrFileName) || preferences == null)
                return urls;

            var normalizedPath = pathOrFileName.Trim().TrimStart('/');
            foreach (var host in preferences.StoreItemAssetsHosts ?? new List<string>())
            {
                AddCandidate(
                    urls,
                    BuildHttpsUrl(host, $"store_item_assets/steam/apps/{appId}/{normalizedPath}"));
            }

            var barePaths = new List<string> { normalizedPath };
            var fileName = Path.GetFileName(normalizedPath);
            if (normalizedPath.Contains("/") && !string.IsNullOrWhiteSpace(fileName))
                barePaths.Add(fileName);

            foreach (var barePath in barePaths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                foreach (var host in preferences.SteamAppsBareHosts ?? new List<string>())
                {
                    AddCandidate(urls, BuildHttpsUrl(host, $"steam/apps/{appId}/{barePath}"));
                }
            }

            return urls;
        }

        private SteamStaticCdnPreferences GetActivePreferences()
        {
            lock (_sync)
            {
                return _preferences ?? CreateDefaultPreferences();
            }
        }

        private void ReplacePreferences(SteamStaticCdnPreferences preferences)
        {
            lock (_sync)
            {
                _preferences = NormalizePreferences(preferences);
            }
        }

        private void ScheduleWarmUpIfNeeded()
        {
            lock (_sync)
            {
                if (_warmUpScheduled)
                    return;
                _warmUpScheduled = true;
            }

            Task.Run(async () =>
            {
                try
                {
                    await WarmUpAsync(ServiceLocator.ApplicationLifetimeToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    // Best-effort background probe; defaults remain usable.
                    Program.LogService?.LogWarning("Steam CDN probe failed; using the default host order: " + ex.Message);
                }
            }, ServiceLocator.ApplicationLifetimeToken);
        }

        private static SteamStaticCdnPreferences CreateDefaultPreferences()
        {
            return NormalizePreferences(new SteamStaticCdnPreferences
            {
                ProbedUtc = null,
                StoreItemAssetsHosts = SteamStaticCdnConstants.DefaultStoreItemAssetsHosts.ToList(),
                SharedFastlyHosts = SteamStaticCdnConstants.DefaultSharedFastlyHosts.ToList(),
                SteamAppsBareHosts = SteamStaticCdnConstants.DefaultSteamAppsBareHosts.ToList(),
                GeneralCdnHosts = SteamStaticCdnConstants.DefaultGeneralCdnHosts.ToList(),
                ClientPackageHosts = SteamStaticCdnConstants.DefaultClientPackageHosts.ToList()
            });
        }

        private static SteamStaticCdnPreferences NormalizePreferences(SteamStaticCdnPreferences preferences)
        {
            if (preferences == null)
                return CreateDefaultPreferences();

            preferences.StoreItemAssetsHosts = NormalizeHostList(
                preferences.StoreItemAssetsHosts,
                SteamStaticCdnConstants.DefaultStoreItemAssetsHosts);
            preferences.SharedFastlyHosts = NormalizeHostList(
                preferences.SharedFastlyHosts,
                SteamStaticCdnConstants.DefaultSharedFastlyHosts);
            preferences.SteamAppsBareHosts = NormalizeHostList(
                preferences.SteamAppsBareHosts,
                SteamStaticCdnConstants.DefaultSteamAppsBareHosts);
            preferences.GeneralCdnHosts = NormalizeHostList(
                preferences.GeneralCdnHosts,
                SteamStaticCdnConstants.DefaultGeneralCdnHosts);
            preferences.ClientPackageHosts = NormalizeHostList(
                preferences.ClientPackageHosts,
                SteamStaticCdnConstants.DefaultClientPackageHosts);

            return preferences;
        }

        private static List<string> NormalizeHostList(
            IReadOnlyList<string> hosts,
            IReadOnlyList<string> fallback)
        {
            var normalized = new List<string>();
            if (hosts != null)
            {
                foreach (var host in hosts)
                {
                    if (string.IsNullOrWhiteSpace(host))
                        continue;
                    if (normalized.Any(x => string.Equals(x, host, StringComparison.OrdinalIgnoreCase)))
                        continue;
                    normalized.Add(host.Trim());
                }
            }

            if (normalized.Count == 0)
                normalized.AddRange(fallback);

            return normalized;
        }

        private static async Task<SteamStaticCdnPreferences> ProbePreferencesAsync(CancellationToken cancellationToken)
        {
            var probeReport = new List<string>();

            var storeItemAssetsHosts = await RankHostsAsync(
                "store assets",
                SteamStaticCdnConstants.DefaultStoreItemAssetsHosts,
                host => BuildHttpsUrl(host, SteamStaticCdnConstants.ProbeStoreItemAssetsPath),
                probeReport,
                cancellationToken).ConfigureAwait(false);

            var sharedFastlyHosts = await RankHostsAsync(
                "community assets",
                SteamStaticCdnConstants.DefaultSharedFastlyHosts,
                host => BuildHttpsUrl(host, SteamStaticCdnConstants.ProbeStoreItemAssetsPath),
                probeReport,
                cancellationToken).ConfigureAwait(false);

            var steamAppsBareHosts = await RankHostsAsync(
                "steam/apps mirrors",
                SteamStaticCdnConstants.DefaultSteamAppsBareHosts,
                host => BuildHttpsUrl(
                    host,
                    $"steam/apps/{SteamStaticCdnConstants.ProbeAppId}/{SteamStaticCdnConstants.ProbeBareAssetPath}"),
                probeReport,
                cancellationToken).ConfigureAwait(false);

            var generalCdnHosts = await RankHostsAsync(
                "general CDN",
                SteamStaticCdnConstants.DefaultGeneralCdnHosts,
                host => BuildHttpsUrl(
                    host,
                    $"steam/apps/{SteamStaticCdnConstants.ProbeAppId}/{SteamStaticCdnConstants.ProbeBareAssetPath}"),
                probeReport,
                cancellationToken).ConfigureAwait(false);

            var clientPackageHosts = await RankHostsAsync(
                "client packages",
                SteamStaticCdnConstants.DefaultClientPackageHosts,
                host => BuildHttpsUrl(host, SteamStaticCdnConstants.ClientSoundsPackageRelativePath),
                probeReport,
                cancellationToken).ConfigureAwait(false);

            Program.LogService?.LogMessage("Steam CDN probe (fastest first): " + string.Join(" | ", probeReport));

            return NormalizePreferences(new SteamStaticCdnPreferences
            {
                ProbedUtc = DateTime.UtcNow.ToString("o"),
                StoreItemAssetsHosts = storeItemAssetsHosts,
                SharedFastlyHosts = sharedFastlyHosts,
                SteamAppsBareHosts = steamAppsBareHosts,
                GeneralCdnHosts = generalCdnHosts,
                ClientPackageHosts = clientPackageHosts
            });
        }

        private static async Task<List<string>> RankHostsAsync(
            string purpose,
            IReadOnlyList<string> hosts,
            Func<string, string> buildProbeUrl,
            List<string> probeReport,
            CancellationToken cancellationToken)
        {
            var probeTasks = hosts
                .Where(host => !string.IsNullOrWhiteSpace(host))
                .Select(async host =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var probe = await ProbeUrlLatencyMsAsync(
                        buildProbeUrl(host),
                        SteamStaticCdnConstants.ProbeTimeoutMs,
                        cancellationToken).ConfigureAwait(false);
                    return new { Host = host, Latency = probe.LatencyMs, probe.Failure };
                })
                .ToArray();

            var results = await Task.WhenAll(probeTasks).ConfigureAwait(false);
            var ranked = results
                .Where(result => result.Latency.HasValue)
                .OrderBy(result => result.Latency.Value)
                .Select(result => result.Host)
                .ToList();

            var described = results
                .OrderBy(result => result.Latency ?? long.MaxValue)
                .Select(result => result.Latency.HasValue
                    ? $"{result.Host} {result.Latency.Value}ms"
                    : $"{result.Host} failed ({result.Failure})");
            probeReport?.Add($"{purpose}: {string.Join(", ", described)}");

            if (ranked.Count == 0)
                ranked.AddRange(hosts);

            foreach (var host in hosts)
            {
                if (ranked.Any(x => string.Equals(x, host, StringComparison.OrdinalIgnoreCase)))
                    continue;
                ranked.Add(host);
            }

            return ranked;
        }

        private static async Task<(long? LatencyMs, string Failure)> ProbeUrlLatencyMsAsync(
            string url,
            int timeoutMs,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    var request = (HttpWebRequest)WebRequest.Create(url);
                    request.Method = "GET";
                    request.Timeout = timeoutMs;
                    request.ReadWriteTimeout = timeoutMs;
                    request.AddRange(0, 0);
                    request.UserAgent = PathConstants.LauncherPerUserFolderName;

                    using (var response = (HttpWebResponse)request.GetResponse())
                    {
                        var statusCode = (int)response.StatusCode;
                        if (statusCode >= 200 && statusCode < 400)
                            return ((long?)stopwatch.ElapsedMilliseconds, (string)null);
                        return ((long?)null, $"HTTP {statusCode}");
                    }
                }
                catch (WebException ex) when (ex.Response is HttpWebResponse errorResponse)
                {
                    using (errorResponse)
                        return ((long?)null, $"HTTP {(int)errorResponse.StatusCode}");
                }
                catch (Exception ex)
                {
                    return ((long?)null, HttpHelpers.DescribeDownloadFailure(ex, stopwatch.Elapsed));
                }
            }, cancellationToken).ConfigureAwait(false);
        }

        private SteamStaticCdnPreferences TryLoadPreferencesFromDisk()
        {
            try
            {
                var path = PathConstants.SteamStaticCdnPreferencesFilePath;
                if (!File.Exists(path))
                    return null;

                var json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                    return null;

                return NormalizePreferences(JsonConvert.DeserializeObject<SteamStaticCdnPreferences>(json));
            }
            catch
            {
                return null;
            }
        }

        private static void TrySavePreferencesToDisk(SteamStaticCdnPreferences preferences)
        {
            try
            {
                var path = PathConstants.SteamStaticCdnPreferencesFilePath;
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                File.WriteAllText(path, JsonConvert.SerializeObject(preferences, JsonFormatting.Indented));
            }
            catch
            {
            }
        }

        private static IReadOnlyList<string> BuildUniqueUrls(
            IReadOnlyList<string> hosts,
            Func<string, string> buildUrl)
        {
            var urls = new List<string>();
            if (hosts == null)
                return urls;

            foreach (var host in hosts)
            {
                if (string.IsNullOrWhiteSpace(host))
                    continue;
                AddCandidate(urls, buildUrl(host));
            }

            return urls;
        }

        private static string BuildHttpsUrl(string host, string relativePath)
        {
            return string.Concat(
                ApplicationConstants.HttpsUriSchemePrefix,
                host.Trim(),
                "/",
                (relativePath ?? string.Empty).TrimStart('/'));
        }

        private static bool IsRewriteableSteamCdnHost(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
                return false;

            return host.EndsWith(".steamstatic.com", StringComparison.OrdinalIgnoreCase)
                || host.EndsWith(".akamaihd.net", StringComparison.OrdinalIgnoreCase);
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
