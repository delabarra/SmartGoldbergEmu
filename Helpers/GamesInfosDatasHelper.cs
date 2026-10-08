using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppDataKit;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.JsonKit;
using SmartGoldbergEmu.Services;

namespace SmartGoldbergEmu.Helpers
{
    // Public Nemirtingas/games-infos-datas steam/{appId}/* files (no Steam Web API key).
    public static class GamesInfosDatasHelper
    {
        private const int HttpTimeoutSeconds = 15;
        private const string AchievementsSource = "games-infos-datas/achievements_db.json";
        private const string StatsSource = "games-infos-datas/stats_db.json";
        private const string InventorySource = "games-infos-datas/inventory_db.json";

        public static string BuildSteamFileUrl(string appId, string fileName)
        {
            return string.Format(ApplicationConstants.GamesInfosDatasSteamFileUrlFormat, appId, fileName);
        }

        public static async Task<string> TryGetSteamFileBodyAsync(
            string appId,
            string fileName,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(fileName))
                return null;

            string url = BuildSteamFileUrl(appId.Trim(), fileName.Trim());
            try
            {
                using (var httpService = HttpServiceFactory.Create(TimeSpan.FromSeconds(HttpTimeoutSeconds)))
                using (var response = await httpService.GetAsync(url, cancellationToken).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                        return null;
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return string.IsNullOrWhiteSpace(body) ? null : body;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static async Task<AchievementsSection> TryFetchAchievementsSectionAsync(
            uint appId,
            string language,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            string body = await TryGetSteamFileBodyAsync(
                appId.ToString(),
                PathConstants.GoldbergAchievementsDbJsonFileName,
                cancellationToken).ConfigureAwait(false);
            if (body == null)
                return null;

            return ParseAchievementsDb(body, language);
        }

        public static async Task<StatsSection> TryFetchStatsSectionAsync(
            uint appId,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            string body = await TryGetSteamFileBodyAsync(
                appId.ToString(),
                PathConstants.GoldbergStatsDbJsonFileName,
                cancellationToken).ConfigureAwait(false);
            if (body == null)
                return null;

            return ParseStatsDb(body);
        }

        public static async Task<ItemsSection> TryFetchItemsSectionAsync(
            uint appId,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            string body = await TryGetSteamFileBodyAsync(
                appId.ToString(),
                PathConstants.GoldbergInventoryDbJsonFileName,
                cancellationToken).ConfigureAwait(false);
            if (body == null)
                return null;

            return ParseInventoryDb(body);
        }

        public static AchievementsSection ParseAchievementsDb(string achievementsDbJson, string language)
        {
            var section = new AchievementsSection { Source = AchievementsSource };
            if (string.IsNullOrWhiteSpace(achievementsDbJson))
            {
                section.Status = SnapshotSectionStatus.Unavailable;
                section.Error = "No achievements found.";
                return section;
            }

            try
            {
                var root = JsonValue.Parse(achievementsDbJson);
                var array = root as JsonArray;
                if (array == null)
                {
                    section.Status = SnapshotSectionStatus.Unavailable;
                    section.Error = "No achievements found.";
                    return section;
                }

                string preferredLanguage = string.IsNullOrWhiteSpace(language) ? "english" : language.Trim();
                var items = new List<AchievementSchemaEntry>(array.Count);
                foreach (JsonValue token in array)
                {
                    var o = token as JsonObject;
                    if (o == null)
                        continue;

                    string name = o["name"]?.ToString();
                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    items.Add(new AchievementSchemaEntry
                    {
                        Name = name.Trim(),
                        DisplayName = ResolveLocalizedText(o["displayName"], preferredLanguage),
                        Description = ResolveLocalizedText(o["description"], preferredLanguage),
                        IconUrl = o["icon"]?.ToString(),
                        IconGrayUrl = FirstNonEmpty(o["icongray"]?.ToString(), o["icon_gray"]?.ToString()),
                        Hidden = ToBool(o["hidden"])
                    });
                }

                section.Items = items;
                if (items.Count == 0)
                {
                    section.Status = SnapshotSectionStatus.Unavailable;
                    section.Error = "No achievements found.";
                }
                else
                {
                    section.Status = SnapshotSectionStatus.Ok;
                }

                return section;
            }
            catch (Exception ex)
            {
                section.Status = SnapshotSectionStatus.Error;
                section.Error = ex.Message;
                return section;
            }
        }

        public static StatsSection ParseStatsDb(string statsDbJson)
        {
            var section = new StatsSection { Source = StatsSource };
            if (string.IsNullOrWhiteSpace(statsDbJson))
            {
                section.Status = SnapshotSectionStatus.Unavailable;
                section.Error = "No stats found.";
                return section;
            }

            try
            {
                var root = JsonValue.Parse(statsDbJson);
                var array = root as JsonArray;
                if (array == null)
                {
                    section.Status = SnapshotSectionStatus.Unavailable;
                    section.Error = "No stats found.";
                    return section;
                }

                var items = new List<StatSchemaEntry>(array.Count);
                foreach (JsonValue token in array)
                {
                    var o = token as JsonObject;
                    if (o == null)
                        continue;

                    string name = o["name"]?.ToString();
                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    string defaultValue = FirstNonEmpty(
                        o["default"]?.ToString(),
                        o["defaultvalue"]?.ToString(),
                        "0");

                    items.Add(new StatSchemaEntry
                    {
                        Name = name.Trim(),
                        DisplayName = o["displayName"]?.ToString() ?? string.Empty,
                        Type = o["type"]?.ToString() ?? "int",
                        DefaultValue = defaultValue
                    });
                }

                section.Items = items;
                if (items.Count == 0)
                {
                    section.Status = SnapshotSectionStatus.Unavailable;
                    section.Error = "No stats found.";
                }
                else
                {
                    section.Status = SnapshotSectionStatus.Ok;
                    section.StatsDbJson = statsDbJson.Trim();
                }

                return section;
            }
            catch (Exception ex)
            {
                section.Status = SnapshotSectionStatus.Error;
                section.Error = ex.Message;
                return section;
            }
        }

        public static ItemsSection ParseInventoryDb(string inventoryDbJson)
        {
            var section = new ItemsSection { Source = InventorySource };
            if (string.IsNullOrWhiteSpace(inventoryDbJson))
            {
                section.Status = SnapshotSectionStatus.Unavailable;
                section.Error = "No items found.";
                return section;
            }

            try
            {
                var root = JsonValue.Parse(inventoryDbJson.Trim());
                var array = root as JsonArray;
                if (array == null || array.Count == 0)
                {
                    section.Status = SnapshotSectionStatus.Unavailable;
                    section.Error = "No items found.";
                    section.Supported = false;
                    return section;
                }

                // ItemGenerator accepts a JSON array of defs (itemdefid + fields).
                section.ArchiveJson = inventoryDbJson.Trim();
                section.Supported = true;
                section.Status = SnapshotSectionStatus.Ok;

                var items = new List<ItemSchemaEntry>(array.Count);
                foreach (JsonValue token in array)
                {
                    var o = token as JsonObject;
                    if (o == null)
                        continue;

                    string itemDefId = o["itemdefid"]?.ToString();
                    if (string.IsNullOrWhiteSpace(itemDefId))
                        continue;

                    items.Add(new ItemSchemaEntry
                    {
                        ItemDefId = itemDefId.Trim(),
                        Name = o["name"]?.ToString() ?? string.Empty,
                        Type = o["type"]?.ToString() ?? string.Empty,
                        IconUrl = FirstNonEmpty(o["icon_url"]?.ToString(), o["icon_url_large"]?.ToString())
                    });
                }

                section.Items = items;
                if (items.Count == 0)
                {
                    section.Status = SnapshotSectionStatus.Unavailable;
                    section.Error = "No items found.";
                    section.Supported = false;
                    section.ArchiveJson = null;
                }

                return section;
            }
            catch (Exception ex)
            {
                section.Status = SnapshotSectionStatus.Error;
                section.Error = ex.Message;
                return section;
            }
        }

        private static string ResolveLocalizedText(JsonValue node, string preferredLanguage)
        {
            if (node == null)
                return string.Empty;

            if (node.Type != JsonValueKind.Object)
                return node.ToString() ?? string.Empty;

            var o = (JsonObject)node;
            string preferred = o[preferredLanguage]?.ToString();
            if (!string.IsNullOrWhiteSpace(preferred))
                return preferred;

            string english = o["english"]?.ToString();
            if (!string.IsNullOrWhiteSpace(english))
                return english;

            foreach (var prop in o.Properties())
            {
                if (string.Equals(prop.Name, "token", StringComparison.OrdinalIgnoreCase))
                    continue;
                string value = prop.Value?.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return string.Empty;
        }

        private static bool ToBool(JsonValue node)
        {
            if (node == null)
                return false;
            if (node is JsonBool b)
                return b.Value;
            if (node.Kind == JsonValueKind.Integer || node.Kind == JsonValueKind.Float)
                return node.ToInt64() != 0;
            string text = node.ToString();
            if (string.IsNullOrWhiteSpace(text))
                return false;
            if (bool.TryParse(text, out bool parsed))
                return parsed;
            return text == "1";
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
                return null;
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
            return null;
        }
    }
}
