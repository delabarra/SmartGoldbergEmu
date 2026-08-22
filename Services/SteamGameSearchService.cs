using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AppDataKit;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.JsonKit;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Services
{
    public static class SteamGameSearchService
    {
        private const int HttpTimeoutSeconds = 10;
        private const int SteamCmdTimeoutSeconds = 8;
        private const int SteamCmdConcurrency = 6;
        private const string StoreSource = "Steam Store";
        private const string VercelSource = "Steam Search API";

        private static readonly Regex StoreSearchResultRowRegex = new Regex(
            @"<a\b[^>]*\bsearch_result_row\b[^>]*>.*?</a>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex StoreSearchAppIdRegex = new Regex(
            @"data-ds-appid=""(\d+)""",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex StoreSearchTitleRegex = new Regex(
            @"class=""title"">([^<]+)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private struct StoreSearchPass
        {
            public List<AppSearchResult> Results;
            public Dictionary<ulong, string> TypesByAppId;
        }

        public static GameSearchFilterConnection CreateFilterConnection()
        {
            return GameSearchFilterConnection.Start(TimeSpan.FromSeconds(SteamCmdTimeoutSeconds));
        }

        public static async Task<List<AppSearchResult>> SearchByNameAsync(
            string searchTerm,
            ITaskReportService feedbackService = null,
            CancellationToken cancellationToken = default(CancellationToken),
            IProgress<IReadOnlyList<AppSearchResult>> progress = null,
            GameSearchFilterConnection filterConnection = null)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<AppSearchResult>();

            cancellationToken.ThrowIfCancellationRequested();
            feedbackService?.SetMessage("Searching...");

            ulong numericAppId;
            bool numericTerm = TryParseAppIdTerm(searchTerm, out numericAppId);
            Task<AppSearchResult> numericAppTask = numericTerm
                ? TryResolveNumericAppIdAsync(numericAppId, cancellationToken)
                : Task.FromResult<AppSearchResult>(null);

            try
            {
                using (var httpService = HttpServiceFactory.Create(TimeSpan.FromSeconds(HttpTimeoutSeconds)))
                {
                    Task<StoreSearchPass> storeTask = TrySearchStoreAsync(
                        httpService, searchTerm, cancellationToken);
                    Task<List<AppSearchResult>> catalogTask = TrySearchCatalogAsync(
                        httpService, searchTerm, cancellationToken);
                    await Task.WhenAll(storeTask, catalogTask).ConfigureAwait(false);

                    StoreSearchPass storeSearch = await storeTask.ConfigureAwait(false);
                    AppSearchResult numericHit = await numericAppTask.ConfigureAwait(false);
                    List<AppSearchResult> catalogResults = await catalogTask.ConfigureAwait(false);

                    return await RevealEligibleAppsAsync(
                        ConcatUnique(
                            numericHit != null ? new List<AppSearchResult> { numericHit } : null,
                            ConcatUnique(storeSearch.Results, catalogResults)),
                        storeSearch.TypesByAppId,
                        storeSearch.Results,
                        numericTerm ? numericAppId : 0UL,
                        progress,
                        filterConnection,
                        cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                feedbackService?.SetMessage("Game search failed.", TaskReportKind.Error);
                Program.LogService?.LogError($"SearchByNameAsync error: {ex.Message}", ex);
                throw;
            }
        }

        private static async Task<StoreSearchPass> TrySearchStoreAsync(
            IHttpService httpService,
            string searchTerm,
            CancellationToken cancellationToken)
        {
            try
            {
                string searchUrl = string.Format(
                    ApplicationConstants.SteamStoreSearchCatalogUrlFormat,
                    Uri.EscapeDataString(searchTerm));

                string responseContent;
                using (var response = await httpService.GetAsync(searchUrl, cancellationToken).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        Program.LogService?.LogWarning(
                            $"Steam Store catalog search returned HTTP {(int)response.StatusCode}; showing secondary catalog only.");
                        return new StoreSearchPass();
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }

                return ParseStoreCatalogHtml(responseContent, searchTerm, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning($"Steam Store catalog search failed; showing secondary catalog only. {ex.Message}");
                return new StoreSearchPass();
            }
        }

        // category1=998 rows are games; packages/bundles have no data-ds-appid and are skipped.
        // Steam also injects related titles — keep only names that match the query.
        private static StoreSearchPass ParseStoreCatalogHtml(
            string html,
            string searchTerm,
            CancellationToken cancellationToken)
        {
            var typesByAppId = new Dictionary<ulong, string>();
            var results = new List<AppSearchResult>();
            var seen = new HashSet<ulong>();

            if (string.IsNullOrWhiteSpace(html))
            {
                return new StoreSearchPass
                {
                    Results = results,
                    TypesByAppId = typesByAppId
                };
            }

            foreach (Match row in StoreSearchResultRowRegex.Matches(html))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (row == null || !row.Success)
                    continue;

                Match appIdMatch = StoreSearchAppIdRegex.Match(row.Value);
                if (!appIdMatch.Success)
                    continue;

                ulong appId;
                if (!ulong.TryParse(appIdMatch.Groups[1].Value, out appId) || appId == 0 || !seen.Add(appId))
                    continue;

                Match titleMatch = StoreSearchTitleRegex.Match(row.Value);
                if (!titleMatch.Success)
                    continue;

                string appName = WebUtility.HtmlDecode(titleMatch.Groups[1].Value)?.Trim();
                if (string.IsNullOrEmpty(appName) || !NameMatchesSearch(appName, searchTerm))
                    continue;

                typesByAppId[appId] = "game";
                results.Add(new AppSearchResult
                {
                    AppId = appId,
                    Name = appName,
                    Source = StoreSource
                });
            }

            return new StoreSearchPass
            {
                Results = results,
                TypesByAppId = typesByAppId
            };
        }

        private static async Task<List<AppSearchResult>> TrySearchCatalogAsync(
            IHttpService httpService,
            string searchTerm,
            CancellationToken cancellationToken)
        {
            try
            {
                var results = new List<AppSearchResult>();
                var seen = new HashSet<ulong>();

                string searchUrl = string.Format(
                    ApplicationConstants.SteamSearchGamesApiUrlFormat,
                    Uri.EscapeDataString(searchTerm));

                string responseContent;
                using (var response = await httpService.GetAsync(searchUrl, cancellationToken).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        Program.LogService?.LogWarning(
                            $"Steam Search API returned HTTP {(int)response.StatusCode}; showing store results only.");
                        return new List<AppSearchResult>();
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }

                if (string.IsNullOrWhiteSpace(responseContent))
                    return new List<AppSearchResult>();

                JsonArray gamesArray = JsonArray.Parse(responseContent);

                foreach (JsonValue item in gamesArray)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    JsonObject game = item as JsonObject;
                    if (game == null)
                        continue;

                    string appName = game["name"]?.ToString();
                    if (string.IsNullOrEmpty(appName) || !NameMatchesSearch(appName, searchTerm))
                        continue;

                    ulong appId = ParseAppId(game["appid"]);
                    if (appId == 0 || !seen.Add(appId))
                        continue;

                    results.Add(new AppSearchResult
                    {
                        AppId = appId,
                        Name = appName,
                        Source = VercelSource
                    });
                }

                return results;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning($"Steam Search API failed; showing store results only. {ex.Message}");
                return new List<AppSearchResult>();
            }
        }

        // Show Steam store catalog games immediately. Secondary catalog unknowns wait for steamcmd/store/PICS;
        // confirmed non-games are never added; anything still untyped after that is added.
        private static async Task<List<AppSearchResult>> RevealEligibleAppsAsync(
            List<AppSearchResult> merged,
            Dictionary<ulong, string> storeTypes,
            List<AppSearchResult> storeResults,
            ulong numericAppId,
            IProgress<IReadOnlyList<AppSearchResult>> progress,
            GameSearchFilterConnection filterConnection,
            CancellationToken cancellationToken)
        {
            var ranked = new List<AppSearchResult>();
            var visibleIds = new HashSet<ulong>();
            var pending = new List<AppSearchResult>();
            var storeIds = new HashSet<ulong>();
            if (storeResults != null)
            {
                foreach (AppSearchResult storeItem in storeResults)
                {
                    if (storeItem != null && storeItem.AppId != 0)
                        storeIds.Add(storeItem.AppId);
                }
            }

            if (merged != null)
            {
                foreach (AppSearchResult item in merged)
                {
                    if (item == null || item.AppId == 0)
                        continue;

                    string type;
                    TryGetKnownType(item.AppId, storeTypes, out type);
                    if (IsKnownNonGame(type))
                        continue;

                    ranked.Add(item);

                    if (item.AppId > uint.MaxValue)
                    {
                        visibleIds.Add(item.AppId);
                        continue;
                    }

                    bool knownEligible = !string.IsNullOrEmpty(type) ||
                        storeIds.Contains(item.AppId) ||
                        (numericAppId != 0 && item.AppId == numericAppId);
                    if (knownEligible)
                        visibleIds.Add(item.AppId);
                    else
                        pending.Add(item);
                }
            }

            List<AppSearchResult> visible = BuildVisibleResults(ranked, visibleIds);
            ReportProgress(progress, visible, cancellationToken);

            if (pending.Count == 0)
                return visible;

            if (filterConnection != null)
                await filterConnection.WaitUntilReadyAsync(cancellationToken).ConfigureAwait(false);

            var options = new AppSnapshotOptions
            {
                HttpTimeout = TimeSpan.FromSeconds(SteamCmdTimeoutSeconds)
            };

            HttpClient http = filterConnection != null ? filterConnection.Http : null;
            bool ownsHttp = http == null;
            if (ownsHttp)
                http = new HttpClient { Timeout = TimeSpan.FromSeconds(SteamCmdTimeoutSeconds) };

            var gate = new object();
            var picsQueue = new List<AppSearchResult>();
            var rejected = new HashSet<ulong>();

            try
            {
                using (var concurrency = new SemaphoreSlim(SteamCmdConcurrency, SteamCmdConcurrency))
                {
                    var tasks = new List<Task>(pending.Count);
                    foreach (AppSearchResult candidate in pending)
                    {
                        tasks.Add(ResolveAndMaybeAddAsync(
                            candidate,
                            ranked,
                            visibleIds,
                            picsQueue,
                            rejected,
                            options,
                            http,
                            concurrency,
                            gate,
                            progress,
                            cancellationToken));
                    }

                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }
            }
            finally
            {
                if (ownsHttp)
                    http.Dispose();
            }

            await AddAfterPicsTypeCheckAsync(
                picsQueue,
                ranked,
                visibleIds,
                rejected,
                gate,
                progress,
                cancellationToken).ConfigureAwait(false);

            lock (gate)
            {
                AddRemainingUndefined(ranked, visibleIds, rejected);
                visible = BuildVisibleResults(ranked, visibleIds);
            }

            ReportProgress(progress, visible, cancellationToken);
            return visible;
        }

        private static async Task ResolveAndMaybeAddAsync(
            AppSearchResult candidate,
            List<AppSearchResult> ranked,
            HashSet<ulong> visibleIds,
            List<AppSearchResult> picsQueue,
            HashSet<ulong> rejected,
            AppSnapshotOptions options,
            HttpClient http,
            SemaphoreSlim concurrency,
            object gate,
            IProgress<IReadOnlyList<AppSearchResult>> progress,
            CancellationToken cancellationToken)
        {
            await concurrency.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                string type = null;
                try
                {
                    AppInfoFetchResult fetch = await AppInfoClient
                        .FetchFromSteamCmdAsync((uint)candidate.AppId, options, http, cancellationToken)
                        .ConfigureAwait(false);
                    TryGetSteamCmdType(fetch, out type);

                    if (string.IsNullOrEmpty(type))
                    {
                        StoreAppDetailsClient.BasicInfo store = await StoreAppDetailsClient
                            .TryGetBasicAsync((uint)candidate.AppId, http, cancellationToken)
                            .ConfigureAwait(false);
                        if (store.Success && !string.IsNullOrEmpty(store.Type))
                            type = store.Type;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Program.LogService?.LogDebug(
                        $"Type check failed for app {candidate.AppId}: {ex.Message}");
                    type = null;
                }

                if (IsKnownNonGame(type))
                {
                    lock (gate)
                        rejected.Add(candidate.AppId);
                    return;
                }

                if (string.IsNullOrEmpty(type))
                {
                    lock (gate)
                        picsQueue.Add(candidate);
                    return;
                }

                List<AppSearchResult> nextVisible = null;
                lock (gate)
                {
                    if (!visibleIds.Add(candidate.AppId))
                        return;
                    nextVisible = BuildVisibleResults(ranked, visibleIds);
                }

                ReportProgress(progress, nextVisible, cancellationToken);
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                concurrency.Release();
            }
        }

        private static async Task AddAfterPicsTypeCheckAsync(
            List<AppSearchResult> picsQueue,
            List<AppSearchResult> ranked,
            HashSet<ulong> visibleIds,
            HashSet<ulong> rejected,
            object gate,
            IProgress<IReadOnlyList<AppSearchResult>> progress,
            CancellationToken cancellationToken)
        {
            List<AppSearchResult> leftovers;
            lock (gate)
                leftovers = picsQueue.Count == 0
                    ? new List<AppSearchResult>()
                    : new List<AppSearchResult>(picsQueue);

            if (leftovers.Count == 0)
                return;

            var appIds = new List<uint>(leftovers.Count);
            foreach (AppSearchResult item in leftovers)
            {
                if (item != null && item.AppId > 0 && item.AppId <= uint.MaxValue)
                    appIds.Add((uint)item.AppId);
            }

            Dictionary<uint, string> picsTypes = null;
            if (appIds.Count > 0)
            {
                try
                {
                    picsTypes = await ServiceLocator.SteamProductInfoService
                        .GetAppCommonTypesAsync(appIds, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Program.LogService?.LogDebug($"PICS type check failed: {ex.Message}");
                }
            }

            bool addedAny = false;
            lock (gate)
            {
                foreach (AppSearchResult item in leftovers)
                {
                    if (item == null)
                        continue;

                    string type = null;
                    uint id;
                    if (picsTypes != null && item.AppId <= uint.MaxValue)
                    {
                        id = (uint)item.AppId;
                        picsTypes.TryGetValue(id, out type);
                    }

                    if (IsKnownNonGame(type))
                    {
                        rejected.Add(item.AppId);
                        continue;
                    }
                }
            }

            if (!addedAny)
                return;

            List<AppSearchResult> nextVisible;
            lock (gate)
                nextVisible = BuildVisibleResults(ranked, visibleIds);
            ReportProgress(progress, nextVisible, cancellationToken);
        }

        // Ranked hits that never got a non-game type: still undefined after steamcmd/store/PICS.
        private static void AddRemainingUndefined(
            List<AppSearchResult> ranked,
            HashSet<ulong> visibleIds,
            HashSet<ulong> rejected)
        {
            if (ranked == null || visibleIds == null)
                return;

            foreach (AppSearchResult item in ranked)
            {
                if (item == null || item.AppId == 0)
                    continue;
                if (rejected != null && rejected.Contains(item.AppId))
                    continue;
                visibleIds.Add(item.AppId);
            }
        }

        private static bool TryGetKnownType(
            ulong appId,
            Dictionary<ulong, string> storeTypes,
            out string type)
        {
            type = null;
            if (storeTypes != null && storeTypes.TryGetValue(appId, out type) && !string.IsNullOrEmpty(type))
                return true;
            return TryGetLocalAppType(appId, out type);
        }

        private static List<AppSearchResult> BuildVisibleResults(
            List<AppSearchResult> ranked,
            HashSet<ulong> visibleIds)
        {
            var visible = new List<AppSearchResult>();
            if (ranked == null || visibleIds == null || visibleIds.Count == 0)
                return visible;

            foreach (AppSearchResult item in ranked)
            {
                if (item == null || !visibleIds.Contains(item.AppId))
                    continue;
                visible.Add(item);
            }

            visible.Sort(CompareByNameThenAppId);
            return visible;
        }

        private static int CompareByNameThenAppId(AppSearchResult a, AppSearchResult b)
        {
            if (ReferenceEquals(a, b))
                return 0;
            if (a == null)
                return 1;
            if (b == null)
                return -1;

            int byName = string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            if (byName != 0)
                return byName;
            return a.AppId.CompareTo(b.AppId);
        }

        private static void ReportProgress(
            IProgress<IReadOnlyList<AppSearchResult>> progress,
            IReadOnlyList<AppSearchResult> results,
            CancellationToken cancellationToken)
        {
            if (progress == null || results == null)
                return;
            if (cancellationToken.IsCancellationRequested)
                return;
            progress.Report(results);
        }

        private static bool TryGetLocalAppType(ulong appId, out string type)
        {
            type = null;
            if (appId == 0 || appId > uint.MaxValue)
                return false;

            try
            {
                if (AppCatalogSnapshotStore.TryLoad(appId, out AppCatalogSnapshot catalog) &&
                    catalog != null &&
                    AppInfoKeyValueHelper.TryGetAppDisplayInfo(catalog.AppInfo, out _, out type) &&
                    !string.IsNullOrEmpty(type))
                    return true;
            }
            catch (Exception ex)
            {
                Program.LogService?.LogDebug(
                    $"Local type lookup failed for app {appId}: {ex.Message}");
            }

            type = null;
            return false;
        }

        private static bool TryGetSteamCmdType(AppInfoFetchResult fetch, out string type)
        {
            type = null;
            if (fetch == null || !fetch.Success || fetch.AppInfo == null)
                return false;

            AppInfoKeyValue root = fetch.AppInfo.GetChild("appinfo") ?? fetch.AppInfo;
            AppInfoKeyValue common = root?.GetChild("common");
            if (common == null)
                return false;

            type = common.GetChild("type")?.Value;
            if (string.IsNullOrWhiteSpace(type))
            {
                type = null;
                return false;
            }

            type = type.Trim();
            return true;
        }

        // Empty / unknown type stays in the list. Only drop when Steam gave a non-game type.
        private static bool IsKnownNonGame(string type)
        {
            return !string.IsNullOrEmpty(type) &&
                !string.Equals(type, "game", StringComparison.OrdinalIgnoreCase);
        }

        // Drop Steam "related" padding (Starfield for fallout, etc.). Every query word must appear in the title.
        private static bool NameMatchesSearch(string name, string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(searchTerm))
                return false;

            string nameLower = name.ToLowerInvariant();
            string[] words = searchTerm.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
                return false;

            for (int i = 0; i < words.Length; i++)
            {
                if (nameLower.IndexOf(words[i].ToLowerInvariant(), StringComparison.Ordinal) < 0)
                    return false;
            }

            return true;
        }

        private static ulong ParseAppId(JsonValue value)
        {
            if (value == null)
                return 0;

            long asLong = value.ToObject<long>();
            if (asLong > 0)
                return (ulong)asLong;

            string asText = value.ToString();
            ulong parsed;
            if (ulong.TryParse(asText, out parsed) && parsed > 0)
                return parsed;

            return 0;
        }

        private static bool TryParseAppIdTerm(string searchTerm, out ulong appId)
        {
            appId = 0;
            return !string.IsNullOrEmpty(searchTerm)
                && ulong.TryParse(searchTerm, out appId)
                && appId > 0;
        }

        private static async Task<AppSearchResult> TryResolveNumericAppIdAsync(
            ulong appId,
            CancellationToken cancellationToken)
        {
            try
            {
                var (appData, _) = await ServiceLocator.GameSetupService
                    .FetchMetadataWithRootAsync(appId.ToString(), cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                if (appData != null &&
                    appData.Success &&
                    !string.IsNullOrEmpty(appData.Name) &&
                    !IsKnownNonGame(appData.Type))
                {
                    return new AppSearchResult
                    {
                        AppId = appId,
                        Name = appData.Name,
                        Source = StoreSource
                    };
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Program.LogService?.LogDebug($"App ID search failed for {appId}: {ex.Message}");
            }

            return null;
        }

        private static List<AppSearchResult> ConcatUnique(
            List<AppSearchResult> first,
            List<AppSearchResult> second)
        {
            var merged = new List<AppSearchResult>();
            var seen = new HashSet<ulong>();
            AppendUnique(merged, seen, first);
            AppendUnique(merged, seen, second);
            return merged;
        }

        private static void AppendUnique(
            List<AppSearchResult> merged,
            HashSet<ulong> seen,
            List<AppSearchResult> source)
        {
            if (source == null)
                return;

            foreach (AppSearchResult item in source)
            {
                if (item == null || item.AppId == 0 || !seen.Add(item.AppId))
                    continue;
                merged.Add(item);
            }
        }
    }
}
