using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.JsonKit;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Services
{
    public static class SteamGameSearchService
    {
        private const int HttpTimeoutSeconds = 10;

        public static async Task<List<AppSearchResult>> SearchByNameAsync(
            string searchTerm,
            int maxResults = 10,
            ITaskReportService feedbackService = null)
        {
            var allCandidates = new List<AppSearchResult>();

            try
            {
                if (string.IsNullOrWhiteSpace(searchTerm))
                    return new List<AppSearchResult>();

                feedbackService?.SetMessage("Searching for games...");

                string searchLower = searchTerm.ToLowerInvariant();
                string[] searchWords = searchTerm.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                string[] searchWordsLower = searchWords.Select(w => w.ToLowerInvariant()).ToArray();

                string searchUrl = string.Format(ApplicationConstants.SteamSearchGamesApiUrlFormat, Uri.EscapeDataString(searchTerm));
                using (var httpService = HttpServiceFactory.Create(TimeSpan.FromSeconds(HttpTimeoutSeconds)))
                {
                    string responseContent;
                    using (var response = await httpService.GetAsync(searchUrl).ConfigureAwait(false))
                    {
                        response.EnsureSuccessStatusCode();
                        responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    }

                    if (string.IsNullOrWhiteSpace(responseContent))
                        throw new InvalidOperationException("Received empty response from Steam Search API");

                    JsonArray gamesArray = JsonArray.Parse(responseContent);
                    feedbackService?.SetMessage($"Found {gamesArray.Count} candidates, filtering...");

                    foreach (JsonObject game in gamesArray)
                    {
                        string appName = game["name"]?.ToString();
                        if (string.IsNullOrEmpty(appName))
                            continue;

                        long id = game["appid"]?.ToObject<long>() ?? 0;
                        if (id > 0)
                        {
                            allCandidates.Add(new AppSearchResult
                            {
                                AppId = (ulong)id,
                                Name = appName,
                                Source = "Steam Search API"
                            });
                        }
                    }
                }

                feedbackService?.SetMessage("Filtering and sorting results...");

                var filteredResults = new List<AppSearchResult>();
                string nameLower;

                foreach (var candidate in allCandidates)
                {
                    nameLower = candidate.Name.ToLowerInvariant();
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

                var sortedResults = filteredResults.OrderBy(r =>
                {
                    nameLower = r.Name.ToLowerInvariant();
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

                feedbackService?.SetMessage($"Found {sortedResults.Count} matching games");
                return sortedResults;
            }
            catch (Exception ex)
            {
                feedbackService?.SetMessage("Game search failed.", TaskReportKind.Error);
                Program.LogService?.LogError($"SearchByNameAsync error: {ex.Message}", ex);

                if (allCandidates.Count == 0)
                    throw;

                return allCandidates.Take(maxResults).ToList();
            }
        }
    }
}
