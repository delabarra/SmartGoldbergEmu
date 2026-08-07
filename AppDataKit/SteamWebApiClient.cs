using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AppDataKit
{
    internal static class SteamWebApiClient
    {
        private const string SchemaForGameUrl =
            "https://api.steampowered.com/ISteamUserStats/GetSchemaForGame/v2/";
        private const string ItemDefMetaEndpoint =
            "https://api.steampowered.com/IInventoryService/GetItemDefMeta/v1";
        private const string ItemDefArchiveEndpoint =
            "https://api.steampowered.com/IGameInventory/GetItemDefArchive/v0001";

        public static async Task<AchievementsSection> FetchAchievementsAsync(
            uint appId,
            string apiKey,
            AppSnapshotOptions options,
            CancellationToken cancellationToken)
        {
            var section = new AchievementsSection
            {
                Source = "ISteamUserStats.GetSchemaForGame",
            };

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                section.Status = SnapshotSectionStatus.Unavailable;
                section.Error = "Steam Web API key is required.";
                return section;
            }

            string json = await FetchSchemaForGameJsonAsync(appId, apiKey, options, cancellationToken).ConfigureAwait(false);
            if (json == null)
            {
                section.Status = SnapshotSectionStatus.Error;
                section.Error = "No achievements found.";
                return section;
            }

            PopulateAchievementsFromSchema(json, appId, section);
            return section;
        }

        public static async Task<StatsSection> FetchStatsAsync(
            uint appId,
            string apiKey,
            AppSnapshotOptions options,
            CancellationToken cancellationToken)
        {
            var section = new StatsSection
            {
                Source = "ISteamUserStats.GetSchemaForGame",
            };

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                section.Status = SnapshotSectionStatus.Unavailable;
                section.Error = "Steam Web API key is required.";
                return section;
            }

            string json = await FetchSchemaForGameJsonAsync(appId, apiKey, options, cancellationToken).ConfigureAwait(false);
            if (json == null)
            {
                section.Status = SnapshotSectionStatus.Error;
                section.Error = "No stats found.";
                return section;
            }

            PopulateStatsFromSchema(json, section);
            return section;
        }

        internal static async Task<Tuple<AchievementsSection, StatsSection>> FetchAchievementsAndStatsAsync(
            uint appId,
            string apiKey,
            AppSnapshotOptions options,
            CancellationToken cancellationToken)
        {
            var achievements = new AchievementsSection { Source = "ISteamUserStats.GetSchemaForGame" };
            var stats = new StatsSection { Source = "ISteamUserStats.GetSchemaForGame" };

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                achievements.Status = SnapshotSectionStatus.Unavailable;
                achievements.Error = "Steam Web API key is required.";
                stats.Status = SnapshotSectionStatus.Unavailable;
                stats.Error = "Steam Web API key is required.";
                return Tuple.Create(achievements, stats);
            }

            string json = await FetchSchemaForGameJsonAsync(appId, apiKey, options, cancellationToken).ConfigureAwait(false);
            if (json == null)
            {
                achievements.Status = SnapshotSectionStatus.Error;
                achievements.Error = "No achievements found.";
                stats.Status = SnapshotSectionStatus.Error;
                stats.Error = "No stats found.";
                return Tuple.Create(achievements, stats);
            }

            PopulateAchievementsFromSchema(json, appId, achievements);
            PopulateStatsFromSchema(json, stats);
            return Tuple.Create(achievements, stats);
        }

        private static async Task<string> FetchSchemaForGameJsonAsync(
            uint appId,
            string apiKey,
            AppSnapshotOptions options,
            CancellationToken cancellationToken)
        {
            string url = SchemaForGameUrl
                + "?key=" + Uri.EscapeDataString(apiKey)
                + "&appid=" + appId;

            try
            {
                using (var http = CreateHttpClient(options))
                using (HttpResponseMessage response = await http.GetAsync(url, cancellationToken).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                        return null;
                    return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
            }
            catch
            {
                return null;
            }
        }

        public static async Task<ItemsSection> FetchItemsAsync(
            uint appId,
            string apiKey,
            AppSnapshotOptions options,
            CancellationToken cancellationToken)
        {
            var section = new ItemsSection();

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                section.Status = SnapshotSectionStatus.Unavailable;
                section.Error = "Steam Web API key is required.";
                return section;
            }

            try
            {
                using (var http = CreateHttpClient(options))
                {
                    string metaUrl = ItemDefMetaEndpoint
                        + "?key=" + Uri.EscapeDataString(apiKey)
                        + "&appid=" + appId;

                    using (HttpResponseMessage metaResponse = await http.GetAsync(metaUrl, cancellationToken).ConfigureAwait(false))
                    {
                        if (!metaResponse.IsSuccessStatusCode)
                        {
                            section.Status = SnapshotSectionStatus.Unavailable;
                            section.Error = "No items found.";
                            return section;
                        }

                        string metaJson = await metaResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                        string digest = ReadItemDefDigest(metaJson);
                        if (string.IsNullOrWhiteSpace(digest))
                        {
                            section.Status = SnapshotSectionStatus.Unavailable;
                            section.Error = "No items found.";
                            return section;
                        }

                        section.Digest = digest;
                    }

                    string archiveUrl = ItemDefArchiveEndpoint
                        + "?appid=" + appId
                        + "&digest=" + Uri.EscapeDataString(section.Digest);

                    using (HttpResponseMessage archiveResponse = await http.GetAsync(archiveUrl, cancellationToken).ConfigureAwait(false))
                    {
                        if (!archiveResponse.IsSuccessStatusCode)
                        {
                            section.Status = SnapshotSectionStatus.Error;
                            section.Error = "No items found.";
                            return section;
                        }

                        byte[] archiveBytes = await archiveResponse.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                        string archiveJson = SanitizeItemDefArchiveJson(archiveBytes);
                        section.Items = ParseItemDefs(archiveJson, appId);
                        if (section.Items.Count > 0)
                        {
                            section.Status = SnapshotSectionStatus.Ok;
                        }
                        else
                        {
                            section.Status = SnapshotSectionStatus.Unavailable;
                            section.Error = "No items found.";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                section.Status = SnapshotSectionStatus.Error;
                section.Error = ex.Message;
            }

            return section;
        }

        private static void PopulateAchievementsFromSchema(string json, uint appId, AchievementsSection achievements)
        {
            Dictionary<string, object> root = JsonUtil.AsObject(JsonUtil.Parse(json));
            Dictionary<string, object> game = ReadGameFromSchema(root);
            if (game == null)
            {
                achievements.Status = SnapshotSectionStatus.Unavailable;
                achievements.Error = "No achievements found.";
                return;
            }

            achievements.GameName = ReadString(game, "gameName");
            achievements.GameVersion = ReadString(game, "gameVersion");

            Dictionary<string, object> available = ReadObject(game, "availableGameStats");
            if (available == null)
            {
                achievements.Status = SnapshotSectionStatus.Unavailable;
                achievements.Error = "No achievements found.";
                return;
            }

            achievements.Items = ParseAchievements(ReadArray(available, "achievements"), appId);
            achievements.Status = achievements.Items.Count > 0
                ? SnapshotSectionStatus.Ok
                : SnapshotSectionStatus.Unavailable;
            if (achievements.Status == SnapshotSectionStatus.Unavailable)
                achievements.Error = "No achievements found.";
        }

        private static void PopulateStatsFromSchema(string json, StatsSection stats)
        {
            Dictionary<string, object> root = JsonUtil.AsObject(JsonUtil.Parse(json));
            Dictionary<string, object> game = ReadGameFromSchema(root);
            if (game == null)
            {
                stats.Status = SnapshotSectionStatus.Unavailable;
                stats.Error = "No stats found.";
                return;
            }

            Dictionary<string, object> available = ReadObject(game, "availableGameStats");
            if (available == null)
            {
                stats.Status = SnapshotSectionStatus.Unavailable;
                stats.Error = "No stats found.";
                return;
            }

            stats.Items = ParseStats(ReadArray(available, "stats"));
            stats.Status = stats.Items.Count > 0
                ? SnapshotSectionStatus.Ok
                : SnapshotSectionStatus.Unavailable;
            if (stats.Status == SnapshotSectionStatus.Unavailable)
                stats.Error = "No stats found.";
        }

        private static void ParseSchemaForGame(
            string json,
            uint appId,
            AchievementsSection achievements,
            StatsSection stats)
        {
            PopulateAchievementsFromSchema(json, appId, achievements);
            PopulateStatsFromSchema(json, stats);
        }

        private static string SanitizeItemDefArchiveJson(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return string.Empty;

            int length = bytes.Length;
            while (length > 0 && bytes[length - 1] == 0)
                length--;

            return System.Text.Encoding.UTF8.GetString(bytes, 0, length).Trim();
        }

        private static string SanitizeItemDefArchiveJson(string json)
        {
            if (string.IsNullOrEmpty(json))
                return json;

            return json.TrimEnd('\0').Trim();
        }

        private static string ReadItemDefDigest(string metaJson)
        {
            Dictionary<string, object> root = JsonUtil.AsObject(JsonUtil.Parse(metaJson));
            Dictionary<string, object> response = ReadObject(root, "response");
            return ReadString(response, "digest");
        }

        private static IReadOnlyList<AchievementSchemaEntry> ParseAchievements(object[] achievements, uint appId)
        {
            if (achievements == null || achievements.Length == 0)
                return Array.Empty<AchievementSchemaEntry>();

            var items = new List<AchievementSchemaEntry>(achievements.Length);
            foreach (object item in achievements)
            {
                Dictionary<string, object> obj = item as Dictionary<string, object>;
                if (obj == null)
                    continue;

                string icon = ReadString(obj, "icon");
                string iconGray = ReadString(obj, "icongray");
                if (string.IsNullOrWhiteSpace(iconGray))
                    iconGray = ReadString(obj, "icon_gray");

                items.Add(new AchievementSchemaEntry
                {
                    Name = ReadString(obj, "name"),
                    DisplayName = ReadString(obj, "displayName"),
                    Description = ReadString(obj, "description"),
                    Hidden = ReadBool(obj, "hidden"),
                    IconUrl = BuildCommunityImageUrl(appId, icon),
                    IconGrayUrl = BuildCommunityImageUrl(appId, iconGray),
                });
            }

            return items;
        }

        private static IReadOnlyList<StatSchemaEntry> ParseStats(object[] stats)
        {
            if (stats == null || stats.Length == 0)
                return Array.Empty<StatSchemaEntry>();

            var items = new List<StatSchemaEntry>(stats.Length);
            foreach (object item in stats)
            {
                Dictionary<string, object> obj = item as Dictionary<string, object>;
                if (obj == null)
                    continue;

                items.Add(new StatSchemaEntry
                {
                    Name = ReadString(obj, "name"),
                    DisplayName = ReadString(obj, "displayName"),
                    Type = ReadString(obj, "type") ?? ReadString(obj, "stattype"),
                    DefaultValue = ReadString(obj, "defaultvalue"),
                });
            }

            return items;
        }

        private static IReadOnlyList<ItemSchemaEntry> ParseItemDefs(string schemaJson, uint appId)
        {
            object parsed;
            try
            {
                parsed = JsonUtil.Parse(schemaJson);
            }
            catch
            {
                throw new InvalidOperationException("Item archive was not valid JSON.");
            }

            object[] items = parsed as object[];
            if (items == null)
            {
                Dictionary<string, object> root = JsonUtil.AsObject(parsed);
                if (root == null)
                    return Array.Empty<ItemSchemaEntry>();

                items = ReadArray(root, "items");
                if (items == null)
                    items = ReadArray(root, "result", "items");
            }

            if (items == null || items.Length == 0)
                return Array.Empty<ItemSchemaEntry>();

            var output = new List<ItemSchemaEntry>();
            foreach (object item in items)
            {
                Dictionary<string, object> obj = item as Dictionary<string, object>;
                if (obj == null)
                    continue;

                string itemDefId = ReadString(obj, "itemdefid");
                if (string.IsNullOrWhiteSpace(itemDefId))
                    itemDefId = ReadString(obj, "itemdef_id");
                if (string.IsNullOrWhiteSpace(itemDefId))
                    itemDefId = ReadString(obj, "defid");

                string name = ReadString(obj, "name");
                if (string.IsNullOrWhiteSpace(name))
                    name = ReadString(obj, "name_english");

                string icon = ReadString(obj, "icon_url");
                if (string.IsNullOrWhiteSpace(icon))
                    icon = ReadString(obj, "icon_url_large");

                output.Add(new ItemSchemaEntry
                {
                    ItemDefId = itemDefId,
                    Name = name,
                    Type = ReadString(obj, "type"),
                    IconUrl = NormalizeItemIconUrl(icon, appId),
                });
            }

            return output;
        }

        private static string BuildCommunityImageUrl(uint appId, string iconFile)
        {
            if (string.IsNullOrWhiteSpace(iconFile))
                return null;

            if (iconFile.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || iconFile.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return iconFile;

            return "https://cdn.cloudflare.steamstatic.com/steamcommunity/public/images/apps/"
                + appId + "/" + iconFile;
        }

        private static string NormalizeItemIconUrl(string icon, uint appId)
        {
            if (string.IsNullOrWhiteSpace(icon))
                return null;

            if (icon.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || icon.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return icon;

            if (icon.StartsWith("/", StringComparison.Ordinal))
                return "https://community.cloudflare.steamstatic.com/economy/image" + icon;

            return icon;
        }

        private static HttpClient CreateHttpClient(AppSnapshotOptions options)
        {
            return new HttpClient
            {
                Timeout = options?.HttpTimeout ?? TimeSpan.FromSeconds(30),
            };
        }

        private static Dictionary<string, object> ReadGameFromSchema(Dictionary<string, object> root)
        {
            if (root == null)
                return null;

            Dictionary<string, object> game = ReadObject(root, "game");
            if (game != null)
                return game;

            return ReadObject(ReadObject(root, "response"), "game");
        }

        private static Dictionary<string, object> ReadObject(Dictionary<string, object> parent, string key)
        {
            if (parent == null || !parent.TryGetValue(key, out object value))
                return null;
            return value as Dictionary<string, object>;
        }

        private static object[] ReadArray(Dictionary<string, object> parent, string key)
        {
            if (parent == null || !parent.TryGetValue(key, out object value) || value == null)
                return null;

            if (value is object[] array)
                return array;

            if (value is ArrayList list)
            {
                var copy = new object[list.Count];
                list.CopyTo(copy);
                return copy;
            }

            return null;
        }

        private static object[] ReadArray(Dictionary<string, object> parent, string key1, string key2)
        {
            Dictionary<string, object> child = ReadObject(parent, key1);
            return ReadArray(child, key2);
        }

        private static string ReadString(Dictionary<string, object> parent, string key)
        {
            if (parent == null || !parent.TryGetValue(key, out object value) || value == null)
                return string.Empty;

            return value.ToString();
        }

        private static bool ReadBool(Dictionary<string, object> parent, string key)
        {
            if (parent == null || !parent.TryGetValue(key, out object value) || value == null)
                return false;

            if (value is bool boolean)
                return boolean;

            string text = value.ToString();
            return text == "1" || string.Equals(text, "true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
