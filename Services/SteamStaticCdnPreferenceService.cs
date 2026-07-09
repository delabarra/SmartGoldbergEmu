using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using SmartGoldbergEmu.Constants;
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
            ScheduleWarmUpIfNeeded();
            if (appId == 0 || string.IsNullOrWhiteSpace(hash))
                return Array.Empty<string>();

            var preferences = GetActivePreferences();
            return BuildUniqueUrls(
                preferences.SharedFastlyHosts,
                host => BuildHttpsUrl(host, $"community_assets/images/apps/{appId}/{hash}.ico"));
        }

        public IReadOnlyList<string> GetCommunityAppImageCandidateUrls(ulong appId, string hash)
        {
            ScheduleWarmUpIfNeeded();
            if (appId == 0 || string.IsNullOrWhiteSpace(hash))
                return Array.Empty<string>();

            var preferences = GetActivePreferences();
            return BuildUniqueUrls(
                preferences.SharedFastlyHosts,
                host => BuildHttpsUrl(host, $"community_assets/images/apps/{appId}/{hash}.jpg"));
        }

        public IReadOnlyList<string> GetAchievementIconCandidateUrls(string apiUrl)
        {
            ScheduleWarmUpIfNeeded();
            if (string.IsNullOrWhiteSpace(apiUrl))
                return Array.Empty<string>();

            if (!Uri.TryCreate(apiUrl, UriKind.Absolute, out var sourceUri)
                || !IsRewriteableSteamCdnHost(sourceUri.Host))
            {
                return new[] { apiUrl };
            }

            var preferences = GetActivePreferences();
            var urls = new List<string>();
            AddCandidate(urls, apiUrl);

            var pathAndQuery = string.IsNullOrEmpty(sourceUri.PathAndQuery)
                ? string.Empty
                : sourceUri.PathAndQuery.TrimStart('/');

            foreach (var host in preferences.GeneralCdnHosts ?? new List<string>())
            {
                if (string.Equals(host, sourceUri.Host, StringComparison.OrdinalIgnoreCase))
                    continue;

                AddCandidate(urls, BuildHttpsUrl(host, pathAndQuery));
            }

            return urls;
        }

        public IReadOnlyList<string> GetDefaultAvatarCandidateUrls()
        {
            ScheduleWarmUpIfNeeded();
            var preferences = GetActivePreferences();
            return BuildUniqueUrls(
                preferences.AvatarHosts,
                host => BuildHttpsUrl(host, SteamStaticCdnConstants.DefaultAvatarRelativePath));
        }

        public IReadOnlyList<string> GetClientSoundsPackageCandidateUrls()
        {
            ScheduleWarmUpIfNeeded();
            var preferences = GetActivePreferences();
            return BuildUniqueUrls(
                preferences.ClientPackageHosts,
                host => BuildHttpsUrl(host, SteamStaticCdnConstants.ClientSoundsPackageRelativePath));
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
            ReplacePreferences(preferences ?? CreateDefaultPreferences());
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
                    await WarmUpAsync().ConfigureAwait(false);
                }
                catch
                {
                    // Best-effort background probe; defaults remain usable.
                }
            });
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
                AvatarHosts = SteamStaticCdnConstants.DefaultAvatarHosts.ToList(),
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
            preferences.AvatarHosts = NormalizeHostList(
                preferences.AvatarHosts,
                SteamStaticCdnConstants.DefaultAvatarHosts);
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
            var storeItemAssetsHosts = await RankHostsAsync(
                SteamStaticCdnConstants.DefaultStoreItemAssetsHosts,
                host => BuildHttpsUrl(host, SteamStaticCdnConstants.ProbeStoreItemAssetsPath),
                cancellationToken).ConfigureAwait(false);

            var sharedFastlyHosts = await RankHostsAsync(
                SteamStaticCdnConstants.DefaultSharedFastlyHosts,
                host => BuildHttpsUrl(host, SteamStaticCdnConstants.ProbeStoreItemAssetsPath),
                cancellationToken).ConfigureAwait(false);

            var steamAppsBareHosts = await RankHostsAsync(
                SteamStaticCdnConstants.DefaultSteamAppsBareHosts,
                host => BuildHttpsUrl(
                    host,
                    $"steam/apps/{SteamStaticCdnConstants.ProbeAppId}/{SteamStaticCdnConstants.ProbeBareAssetPath}"),
                cancellationToken).ConfigureAwait(false);

            var generalCdnHosts = await RankHostsAsync(
                SteamStaticCdnConstants.DefaultGeneralCdnHosts,
                host => BuildHttpsUrl(
                    host,
                    $"steam/apps/{SteamStaticCdnConstants.ProbeAppId}/{SteamStaticCdnConstants.ProbeBareAssetPath}"),
                cancellationToken).ConfigureAwait(false);

            var avatarHosts = await RankHostsAsync(
                SteamStaticCdnConstants.DefaultAvatarHosts,
                host => BuildHttpsUrl(host, SteamStaticCdnConstants.DefaultAvatarRelativePath),
                cancellationToken).ConfigureAwait(false);

            var clientPackageHosts = await RankHostsAsync(
                SteamStaticCdnConstants.DefaultClientPackageHosts,
                host => BuildHttpsUrl(host, SteamStaticCdnConstants.ClientSoundsPackageRelativePath),
                cancellationToken).ConfigureAwait(false);

            return NormalizePreferences(new SteamStaticCdnPreferences
            {
                ProbedUtc = DateTime.UtcNow.ToString("o"),
                StoreItemAssetsHosts = storeItemAssetsHosts,
                SharedFastlyHosts = sharedFastlyHosts,
                SteamAppsBareHosts = steamAppsBareHosts,
                GeneralCdnHosts = generalCdnHosts,
                AvatarHosts = avatarHosts,
                ClientPackageHosts = clientPackageHosts
            });
        }

        private static async Task<List<string>> RankHostsAsync(
            IReadOnlyList<string> hosts,
            Func<string, string> buildProbeUrl,
            CancellationToken cancellationToken)
        {
            var probeTasks = hosts
                .Where(host => !string.IsNullOrWhiteSpace(host))
                .Select(async host =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var latency = await ProbeUrlLatencyMsAsync(
                        buildProbeUrl(host),
                        SteamStaticCdnConstants.ProbeTimeoutMs,
                        cancellationToken).ConfigureAwait(false);
                    return new { Host = host, Latency = latency };
                })
                .ToArray();

            var results = await Task.WhenAll(probeTasks).ConfigureAwait(false);
            var ranked = results
                .Where(result => result.Latency.HasValue)
                .OrderBy(result => result.Latency.Value)
                .Select(result => result.Host)
                .ToList();

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

        private static async Task<long?> ProbeUrlLatencyMsAsync(
            string url,
            int timeoutMs,
            CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var stopwatch = Stopwatch.StartNew();
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
                        {
                            stopwatch.Stop();
                            return (long?)stopwatch.ElapsedMilliseconds;
                        }
                    }
                }
                catch
                {
                }

                return null;
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
