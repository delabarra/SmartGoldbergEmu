using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Generators;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.JsonKit;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Services
{
        public static class SteamWebApiService
    {
        private const int HttpTimeoutSeconds = 10;
        private const int ItemArchiveTimeoutSeconds = 120;
        private const int PublishedFileDetailsBatchSize = 100;

        public static async Task<AchievementSchema> GetAchievementsAsync(string appId, string language, string apiKey)
        {
            try
            {
                if (string.IsNullOrEmpty(apiKey))
                    return new AchievementSchema { Success = false, ErrorMessage = "API key required" };

                string url = string.Format(ApplicationConstants.SteamUserStatsSchemaApiUrlFormat, language, apiKey, appId);
                using (var httpService = HttpServiceFactory.Create(TimeSpan.FromSeconds(HttpTimeoutSeconds)))
                {
                    string responseContent;
                    using (var response = await httpService.GetAsync(url).ConfigureAwait(false))
                    {
                        responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    }

                    JsonObject root = JsonObject.Parse(responseContent);
                    JsonValue game = root["game"];

                    if (game == null)
                        return new AchievementSchema { Success = false, ErrorMessage = "No achievement data found" };

                    var achievements = new List<AchievementData>();
                    JsonValue availableGameStats = game["availableGameStats"];

                    if (availableGameStats != null && availableGameStats["achievements"] is JsonArray achievementsArray)
                    {
                        foreach (JsonValue ach in achievementsArray)
                        {
                            achievements.Add(new AchievementData
                            {
                                Name = ach["name"]?.ToString(),
                                DisplayName = ach["displayName"]?.ToString(),
                                Description = ach["description"]?.ToString(),
                                Icon = ach["icon"]?.ToString(),
                                IconGray = ach["icongray"]?.ToString(),
                                Hidden = ach["hidden"]?.ToObject<int>() == 1
                            });
                        }
                    }

                    return new AchievementSchema
                    {
                        Success = true,
                        AppId = appId,
                        GameName = game["gameName"]?.ToString(),
                        GameVersion = game["gameVersion"]?.ToString(),
                        Achievements = achievements
                    };
                }
            }
            catch (Exception ex)
            {
                return new AchievementSchema { Success = false, ErrorMessage = LogRedactionHelper.RedactApiKey(ex.Message, apiKey) };
            }
        }

        public static async Task<ItemMeta> GetItemMetaAsync(string appId, string apiKey)
        {
            try
            {
                if (string.IsNullOrEmpty(apiKey))
                    return new ItemMeta { Success = false };

                string url = string.Format(ApplicationConstants.SteamInventoryItemDefMetaApiUrlFormat, apiKey, appId);
                using (var httpService = HttpServiceFactory.Create(TimeSpan.FromSeconds(HttpTimeoutSeconds)))
                {
                    string responseContent;
                    using (var response = await httpService.GetAsync(url).ConfigureAwait(false))
                    {
                        responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    }

                    JsonObject root = JsonObject.Parse(responseContent);
                    JsonValue responseObj = root["response"];

                    if (responseObj == null)
                        return new ItemMeta { Success = false };

                    return new ItemMeta
                    {
                        Success = true,
                        Digest = responseObj["digest"]?.ToString(),
                        Modified = responseObj["modified"]?.ToObject<long>() ?? 0
                    };
                }
            }
            catch (Exception)
            {
                return new ItemMeta { Success = false };
            }
        }

        public static async Task<string> GetItemDefArchiveJsonAsync(string appId, string digest)
        {
            if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(digest))
                return null;

            string url = string.Format(
                ApplicationConstants.SteamGameInventoryItemDefArchiveApiUrlFormat,
                Uri.EscapeDataString(appId),
                Uri.EscapeDataString(digest));

            using (var httpService = HttpServiceFactory.Create(TimeSpan.FromSeconds(ItemArchiveTimeoutSeconds)))
            {
                using (var response = await httpService.GetAsync(url).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                        return null;
                    return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
            }
        }

        public static async Task<Dictionary<string, string>> GetPublishedFileTitlesAsync(
            string apiKey,
            IList<string> publishedFileIds,
            CancellationToken cancellationToken = default)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(apiKey) || publishedFileIds == null || publishedFileIds.Count == 0)
                return map;

            List<string> distinctIds = publishedFileIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (distinctIds.Count == 0)
                return map;

            using (var httpService = HttpServiceFactory.Create(TimeSpan.FromSeconds(HttpTimeoutSeconds)))
            {
                for (int offset = 0; offset < distinctIds.Count; offset += PublishedFileDetailsBatchSize)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int batchCount = Math.Min(PublishedFileDetailsBatchSize, distinctIds.Count - offset);
                    var urlBuilder = new StringBuilder(256 + batchCount * 32);
                    urlBuilder.Append(ApplicationConstants.SteamPublishedFileDetailsApiUrlPrefix);
                    urlBuilder.Append(Uri.EscapeDataString(apiKey));
                    for (int i = 0; i < batchCount; i++)
                    {
                        urlBuilder.Append("&publishedfileids[");
                        urlBuilder.Append(i);
                        urlBuilder.Append("]=");
                        urlBuilder.Append(Uri.EscapeDataString(distinctIds[offset + i]));
                    }

                    using (HttpResponseMessage response = await httpService.GetAsync(urlBuilder.ToString(), cancellationToken).ConfigureAwait(false))
                    {
                        if (!response.IsSuccessStatusCode)
                            continue;
                        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        JsonObject root = JsonObject.Parse(body);
                        JsonValue responseNode = root["response"];
                        if (!(responseNode?["publishedfiledetails"] is JsonArray details))
                            continue;
                        foreach (JsonValue item in details)
                        {
                            if (item == null || item.Type != JsonValueKind.Object)
                                continue;
                            string id = item["publishedfileid"]?.ToString();
                            if (string.IsNullOrEmpty(id))
                                continue;
                            int result = item["result"]?.ToInt32() ?? 0;
                            if (result != 1)
                                continue;
                            string title = item["title"]?.ToString();
                            if (string.IsNullOrWhiteSpace(title))
                                continue;
                            map[id] = title.Trim();
                        }
                    }
                }
            }

            return map;
        }

        public static async Task<List<string>> GetLeaderboardsFromCommunityAsync(string appId)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(appId))
                return result;

            try
            {
                string url = string.Format(ApplicationConstants.SteamCommunityLeaderboardsXmlUrlFormat, Uri.EscapeDataString(appId));
                using (var httpService = HttpServiceFactory.Create(TimeSpan.FromSeconds(HttpTimeoutSeconds)))
                {
                    string xml;
                    using (var response = await httpService.GetAsync(url).ConfigureAwait(false))
                    {
                        if (!response.IsSuccessStatusCode)
                            return result;

                        xml = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    }
                    if (string.IsNullOrWhiteSpace(xml))
                        return result;

                    var doc = XDocument.Parse(xml);
                    var names = doc.Descendants("name")
                        .Select(e => (e.Value ?? string.Empty).Trim())
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    foreach (string name in names)
                        result.Add(name + "=0=0");
                }
            }
            catch
            {
                return new List<string>();
            }

            return result;
        }
    }
}

