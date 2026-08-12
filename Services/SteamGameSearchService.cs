using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AppDataKit;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.JsonKit;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Services
{
    public static class SteamGameSearchService
    {
        private const int HttpTimeoutSeconds = 10;
        private const int SteamCmdTimeoutSeconds = 8;
        private const int SteamCmdConcurrency = 6;
        // Extra ranked hits so steamcmd DLC drops can still fill maxResults.
        private const int SteamCmdVerifyPoolMultiplier = 3;
        private const string StoreSource = "Steam Store";
        private const string VercelSource = "Steam Search API";

        public static async Task<List<AppSearchResult>> SearchByNameAsync(
            string searchTerm,
            int maxResults = 10,
            ITaskReportService feedbackService = null,
            CancellationToken cancellationToken = default(CancellationToken),
            IProgress<IReadOnlyList<AppSearchResult>> progress = null)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<AppSearchResult>();

            cancellationToken.ThrowIfCancellationRequested();
            feedbackService?.SetMessage("Searching...");

            try
            {
                using (var httpService = HttpServiceFactory.Create(TimeSpan.FromSeconds(HttpTimeoutSeconds)))
                {
                    // One store suggest pass: games for the primary hit list, types to strip DLC from catalog fallback.
                    var storeSearch = await TrySearchStoreAsync(
                        httpService, searchTerm, maxResults, cancellationToken)
                        .ConfigureAwait(false);

                    if (storeSearch.Games != null && storeSearch.Games.Count > 0)
                    {
                        ReportProgress(progress, storeSearch.Games, cancellationToken);
                        return storeSearch.Games;
                    }

                    return await SearchVercelAsync(
                        httpService,
                        searchTerm,
                        maxResults,
                        storeSearch.TypesByAppId,
                        progress,
                        cancellationToken)
                        .ConfigureAwait(false);
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

        private struct StoreSearchPass
        {
            public List<AppSearchResult> Games;
            public Dictionary<ulong, string> TypesByAppId;
        }

        // Live store titles only (type=game). Empty/failure means caller should try the broader catalog.
        private static async Task<StoreSearchPass> TrySearchStoreAsync(
            IHttpService httpService,
            string searchTerm,
            int maxResults,
            CancellationToken cancellationToken)
        {
            try
            {
                string searchUrl = string.Format(
                    ApplicationConstants.SteamStoreSearchSuggestUrlFormat,
                    Uri.EscapeDataString(searchTerm));

                string responseContent;
                using (var response = await httpService.GetAsync(searchUrl, cancellationToken).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        Program.LogService?.LogWarning(
                            $"Steam Store search returned HTTP {(int)response.StatusCode}; falling back to catalog.");
                        return new StoreSearchPass();
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }

                if (string.IsNullOrWhiteSpace(responseContent))
                {
                    return new StoreSearchPass
                    {
                        Games = new List<AppSearchResult>(),
                        TypesByAppId = new Dictionary<ulong, string>()
                    };
                }

                JsonArray gamesArray = JsonArray.Parse(responseContent);
                var typesByAppId = new Dictionary<ulong, string>();
                var results = new List<AppSearchResult>();

                foreach (JsonValue item in gamesArray)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    JsonObject game = item as JsonObject;
                    if (game == null)
                        continue;

                    ulong appId = ParseAppId(game["id"]);
                    if (appId == 0)
                        continue;

                    string type = game["type"]?.ToString();
                    if (!string.IsNullOrEmpty(type))
                        typesByAppId[appId] = type;

                    if (!IsGameType(type))
                        continue;

                    string appName = game["name"]?.ToString();
                    if (string.IsNullOrEmpty(appName))
                        continue;

                    if (results.Count < maxResults)
                    {
                        results.Add(new AppSearchResult
                        {
                            AppId = appId,
                            Name = appName,
                            Source = StoreSource
                        });
                    }
                }

                return new StoreSearchPass
                {
                    Games = results,
                    TypesByAppId = typesByAppId
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning($"Steam Store search failed; falling back to catalog. {ex.Message}");
                return new StoreSearchPass();
            }
        }

        private static async Task<List<AppSearchResult>> SearchVercelAsync(
            IHttpService httpService,
            string searchTerm,
            int maxResults,
            Dictionary<ulong, string> storeTypesByAppId,
            IProgress<IReadOnlyList<AppSearchResult>> progress,
            CancellationToken cancellationToken)
        {
            var allCandidates = new List<AppSearchResult>();

            string searchUrl = string.Format(
                ApplicationConstants.SteamSearchGamesApiUrlFormat,
                Uri.EscapeDataString(searchTerm));

            string responseContent;
            using (var response = await httpService.GetAsync(searchUrl, cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                cancellationToken.ThrowIfCancellationRequested();
                responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            }

            if (string.IsNullOrWhiteSpace(responseContent))
                throw new InvalidOperationException("Received empty response from Steam Search API");

            JsonArray gamesArray = JsonArray.Parse(responseContent);

            foreach (JsonValue item in gamesArray)
            {
                cancellationToken.ThrowIfCancellationRequested();

                JsonObject game = item as JsonObject;
                if (game == null)
                    continue;

                string appName = game["name"]?.ToString();
                if (string.IsNullOrEmpty(appName))
                    continue;

                ulong appId = ParseAppId(game["appid"]);
                if (appId == 0)
                    continue;

                // Drop live non-games (DLC, music, …). Unknown ids (delisted) stay eligible.
                string storeType;
                if (storeTypesByAppId != null &&
                    storeTypesByAppId.TryGetValue(appId, out storeType) &&
                    !IsGameType(storeType))
                    continue;

                allCandidates.Add(new AppSearchResult
                {
                    AppId = appId,
                    Name = appName,
                    Source = VercelSource
                });
            }

            int verifyPoolSize = Math.Max(maxResults, maxResults * SteamCmdVerifyPoolMultiplier);
            List<AppSearchResult> ranked = FilterAndSort(allCandidates, searchTerm, verifyPoolSize);

            return await KeepGamesViaSteamCmdLiveAsync(
                ranked, maxResults, storeTypesByAppId, progress, cancellationToken)
                .ConfigureAwait(false);
        }

        // Only show apps once typed as game; unknowns stay hidden until confirmed.
        private static async Task<List<AppSearchResult>> KeepGamesViaSteamCmdLiveAsync(
            List<AppSearchResult> ranked,
            int maxResults,
            Dictionary<ulong, string> storeTypesByAppId,
            IProgress<IReadOnlyList<AppSearchResult>> progress,
            CancellationToken cancellationToken)
        {
            if (ranked == null || ranked.Count == 0 || maxResults <= 0)
            {
                var empty = new List<AppSearchResult>();
                ReportProgress(progress, empty, cancellationToken);
                return empty;
            }

            var confirmedGames = new HashSet<ulong>();
            var rejected = new HashSet<ulong>();
            if (storeTypesByAppId != null)
            {
                foreach (var pair in storeTypesByAppId)
                {
                    if (IsGameType(pair.Value))
                        confirmedGames.Add(pair.Key);
                    else
                        rejected.Add(pair.Key);
                }
            }

            var gate = new object();
            List<AppSearchResult> visible = BuildConfirmedVisibleResults(ranked, confirmedGames, maxResults);
            ReportProgress(progress, visible, cancellationToken);

            var needTypeCheck = new List<AppSearchResult>();
            foreach (AppSearchResult candidate in ranked)
            {
                if (confirmedGames.Contains(candidate.AppId) || rejected.Contains(candidate.AppId))
                    continue;

                if (candidate.AppId == 0 || candidate.AppId > uint.MaxValue)
                    continue;

                needTypeCheck.Add(candidate);
            }

            if (needTypeCheck.Count == 0)
                return visible;

            var options = new AppSnapshotOptions
            {
                HttpTimeout = TimeSpan.FromSeconds(SteamCmdTimeoutSeconds)
            };

            using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(SteamCmdTimeoutSeconds) })
            using (var concurrency = new SemaphoreSlim(SteamCmdConcurrency, SteamCmdConcurrency))
            {
                var tasks = new List<Task>(needTypeCheck.Count);
                foreach (AppSearchResult candidate in needTypeCheck)
                {
                    tasks.Add(ResolveCandidateTypeAsync(
                        candidate,
                        ranked,
                        confirmedGames,
                        rejected,
                        maxResults,
                        options,
                        http,
                        concurrency,
                        gate,
                        progress,
                        cancellationToken));
                }

                await Task.WhenAll(tasks).ConfigureAwait(false);
            }

            if (cancellationToken.IsCancellationRequested)
                return visible;

            lock (gate)
                visible = BuildConfirmedVisibleResults(ranked, confirmedGames, maxResults);

            ReportProgress(progress, visible, cancellationToken);
            return visible;
        }

        private static async Task ResolveCandidateTypeAsync(
            AppSearchResult candidate,
            List<AppSearchResult> ranked,
            HashSet<ulong> confirmedGames,
            HashSet<ulong> rejected,
            int maxResults,
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

                // steamcmd first; many store DLCs return empty {} there — fall back to Store appdetails.
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

                // Still unknown — keep hidden (do not show then remove).
                if (string.IsNullOrEmpty(type))
                    return;

                List<AppSearchResult> nextVisible = null;
                lock (gate)
                {
                    if (IsGameType(type))
                    {
                        if (!confirmedGames.Add(candidate.AppId))
                            return;
                    }
                    else
                    {
                        rejected.Add(candidate.AppId);
                        return;
                    }

                    nextVisible = BuildConfirmedVisibleResults(ranked, confirmedGames, maxResults);
                }

                ReportProgress(progress, nextVisible, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Superseded search — do not report stale progress.
            }
            catch (Exception ex)
            {
                Program.LogService?.LogDebug(
                    $"Type check failed for app {candidate.AppId}: {ex.Message}");
            }
            finally
            {
                concurrency.Release();
            }
        }

        private static List<AppSearchResult> BuildConfirmedVisibleResults(
            List<AppSearchResult> ranked,
            HashSet<ulong> confirmedGames,
            int maxResults)
        {
            var visible = new List<AppSearchResult>(maxResults);
            if (ranked == null || confirmedGames == null || confirmedGames.Count == 0)
                return visible;

            foreach (AppSearchResult candidate in ranked)
            {
                if (!confirmedGames.Contains(candidate.AppId))
                    continue;

                visible.Add(candidate);
                if (visible.Count >= maxResults)
                    break;
            }

            return visible;
        }

        private static void ReportProgress(
            IProgress<IReadOnlyList<AppSearchResult>> progress,
            IReadOnlyList<AppSearchResult> results,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (progress == null || results == null)
                return;
            if (cancellationToken.IsCancellationRequested)
                return;
            progress.Report(results);
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
            return !string.IsNullOrEmpty(type);
        }

        private static bool IsGameType(string type)
        {
            return string.Equals(type, "game", StringComparison.OrdinalIgnoreCase);
        }

        private static ulong ParseAppId(JsonValue value)
        {
            if (value == null)
                return 0;

            long asLong = value.ToObject<long>();
            if (asLong > 0)
                return (ulong)asLong;

            string asText = value.ToString();
            if (ulong.TryParse(asText, out ulong parsed) && parsed > 0)
                return parsed;

            return 0;
        }

        private static List<AppSearchResult> FilterAndSort(
            List<AppSearchResult> allCandidates,
            string searchTerm,
            int maxResults)
        {
            string searchLower = searchTerm.ToLowerInvariant();
            string[] searchWords = searchTerm.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string[] searchWordsLower = searchWords.Select(w => w.ToLowerInvariant()).ToArray();

            var filteredResults = new List<AppSearchResult>();
            foreach (var candidate in allCandidates)
            {
                string nameLower = candidate.Name.ToLowerInvariant();
                bool matches;

                if (searchTerm.Length <= 2)
                    matches = nameLower.Contains(searchLower);
                else if (searchTerm.Length <= 5)
                    matches = nameLower.StartsWith(searchLower) ||
                              (searchWordsLower.Length > 0 && searchWordsLower.All(word => nameLower.Contains(word)));
                else
                    matches = searchWordsLower.Length > 0 && searchWordsLower.All(word => nameLower.Contains(word));

                if (matches)
                    filteredResults.Add(candidate);
            }

            return filteredResults.OrderBy(r =>
            {
                string nameLower = r.Name.ToLowerInvariant();
                int exactMatch = r.Name.Equals(searchTerm, StringComparison.OrdinalIgnoreCase) ? 0 : 1;
                int startsWith = nameLower.StartsWith(searchLower) ? 0 : 1;
                int wordMatch = 0;

                if (searchWordsLower.Length > 0)
                {
                    bool allWordsAtBoundary = searchWordsLower.All(word =>
                        nameLower.Contains(" " + word + " ") ||
                        nameLower.StartsWith(word + " ") ||
                        nameLower.EndsWith(" " + word) ||
                        nameLower == word);
                    wordMatch = allWordsAtBoundary ? 0 : 1;
                }

                int position = nameLower.IndexOf(searchLower);
                if (position < 0)
                    position = int.MaxValue;

                return (exactMatch, startsWith, wordMatch, position);
            }).Take(maxResults).ToList();
        }
    }
}
