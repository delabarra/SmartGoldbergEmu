using System;
using System.Collections.Generic;

namespace AppDataKit
{
    /// <summary>Serializes app data sections as separate top-level JSON nodes.</summary>
    public static class AppDataJson
    {
        /// <summary>
        /// Builds a dictionary with one JSON node per section:
        /// metadata, dlc, assets, achievements, stats, items.
        /// </summary>
        public static Dictionary<string, object> BuildNodes(
            uint appId,
            DateTime fetchedAtUtc,
            AppMetadataSection metadata,
            DlcSection dlc,
            GameAssetsSection gameAssets,
            AchievementsSection achievements,
            StatsSection stats,
            ItemsSection items)
        {
            var root = new Dictionary<string, object>();
            root["appid"] = appId.ToString();
            root["fetched_at_utc"] = fetchedAtUtc.ToString("o");
            root["metadata"] = BuildMetadataNode(metadata);
            root["dlc"] = BuildDlcNode(dlc);
            root["assets"] = BuildAssetsNode(gameAssets);
            root["achievements"] = BuildAchievementsNode(achievements);
            root["stats"] = BuildStatsNode(stats);
            root["items"] = BuildItemsNode(items);
            return root;
        }

        public static Dictionary<string, object> BuildMetadataNode(AppMetadataSection section)
        {
            if (!IsSuccess(section.Status))
                return BuildErrorNode(section);

            var node = new Dictionary<string, object>();
            node["status"] = "success";
            node["app_info_source"] = section.AppInfoSource.ToString();
            if (section.AppInfo != null)
                node["data"] = KeyValueToJson(section.AppInfo);
            return node;
        }

        public static Dictionary<string, object> BuildDlcNode(DlcSection section)
        {
            if (!IsSuccess(section.Status))
                return BuildErrorNode(section);

            var node = new Dictionary<string, object>();
            node["status"] = "success";
            var items = new List<object>();
            if (section.Items != null)
            {
                foreach (DlcEntry entry in section.Items)
                {
                    var item = new Dictionary<string, object>();
                    item["appid"] = entry.AppId.ToString();
                    item["name"] = entry.Name ?? string.Empty;
                    item["type"] = entry.Type ?? "dlc";
                    items.Add(item);
                }
            }
            node["items"] = items;

            var unresolved = new List<object>();
            if (section.UnresolvedAppIds != null)
            {
                foreach (uint id in section.UnresolvedAppIds)
                    unresolved.Add(id.ToString());
            }
            node["unresolved_appids"] = unresolved;
            return node;
        }

        public static Dictionary<string, object> BuildAssetsNode(GameAssetsSection section)
        {
            if (!IsSuccess(section.Status))
                return BuildErrorNode(section);

            var node = new Dictionary<string, object>();
            node["status"] = "success";
            var items = new List<object>();
            if (section.Items != null)
            {
                foreach (GameAssetEntry asset in section.Items)
                {
                    var item = new Dictionary<string, object>();
                    item["key_path"] = asset.KeyPath ?? string.Empty;
                    item["value"] = asset.Value ?? string.Empty;
                    if (!string.IsNullOrEmpty(asset.Url))
                        item["url"] = asset.Url;
                    var urls = new List<object>();
                    if (asset.CandidateUrls != null)
                    {
                        foreach (string url in asset.CandidateUrls)
                            urls.Add(url);
                    }
                    item["candidate_urls"] = urls;
                    items.Add(item);
                }
            }
            node["items"] = items;
            return node;
        }

        public static Dictionary<string, object> BuildAchievementsNode(AchievementsSection section)
        {
            if (!IsSuccess(section.Status))
                return BuildErrorNode(section);

            var node = new Dictionary<string, object>();
            node["status"] = "success";
            if (!string.IsNullOrEmpty(section.GameName))
                node["game_name"] = section.GameName;
            if (!string.IsNullOrEmpty(section.GameVersion))
                node["game_version"] = section.GameVersion;

            var items = new List<object>();
            if (section.Items != null)
            {
                foreach (AchievementSchemaEntry entry in section.Items)
                {
                    var item = new Dictionary<string, object>();
                    item["name"] = entry.Name ?? string.Empty;
                    item["display_name"] = entry.DisplayName ?? string.Empty;
                    item["description"] = entry.Description ?? string.Empty;
                    item["hidden"] = entry.Hidden;
                    if (!string.IsNullOrEmpty(entry.IconUrl))
                        item["icon_url"] = entry.IconUrl;
                    if (!string.IsNullOrEmpty(entry.IconGrayUrl))
                        item["icon_gray_url"] = entry.IconGrayUrl;
                    items.Add(item);
                }
            }
            node["items"] = items;
            return node;
        }

        public static Dictionary<string, object> BuildStatsNode(StatsSection section)
        {
            if (!IsSuccess(section.Status))
                return BuildErrorNode(section);

            var node = new Dictionary<string, object>();
            node["status"] = "success";
            var items = new List<object>();
            if (section.Items != null)
            {
                foreach (StatSchemaEntry entry in section.Items)
                {
                    var item = new Dictionary<string, object>();
                    item["name"] = entry.Name ?? string.Empty;
                    item["display_name"] = entry.DisplayName ?? string.Empty;
                    item["type"] = entry.Type ?? string.Empty;
                    item["default_value"] = entry.DefaultValue ?? string.Empty;
                    items.Add(item);
                }
            }
            node["items"] = items;
            return node;
        }

        /// <summary>Flat JSON for a dedicated items request: appid, success, error, and items when available.</summary>
        public static Dictionary<string, object> BuildItemsResponse(uint appId, ItemsSection section)
        {
            var root = new Dictionary<string, object>();
            root["appid"] = appId.ToString();
            if (!IsSuccess(section.Status))
            {
                root["status"] = "error";
                root["error"] = section.Error ?? "Request failed.";
                return root;
            }

            root["status"] = "success";
            root["items"] = BuildItemEntryNodes(section.Items);
            return root;
        }

        public static Dictionary<string, object> BuildItemsNode(ItemsSection section)
        {
            if (!IsSuccess(section.Status))
                return BuildErrorNode(section);

            var node = new Dictionary<string, object>();
            node["status"] = "success";
            node["supported"] = section.Supported;
            if (!string.IsNullOrEmpty(section.Digest))
                node["digest"] = section.Digest;
            if (!string.IsNullOrEmpty(section.ArchiveJson))
                node["archive_json"] = section.ArchiveJson;
            node["items"] = BuildItemEntryNodes(section.Items);
            return node;
        }

        // Rebuild typed sections from BuildNodes output (or disk catalog JSON).
        public static bool TryParseNodes(
            Dictionary<string, object> root,
            out uint appId,
            out DateTime fetchedAtUtc,
            out AppMetadataSection metadata,
            out DlcSection dlc,
            out GameAssetsSection assets,
            out AchievementsSection achievements,
            out StatsSection stats,
            out ItemsSection items)
        {
            appId = 0;
            fetchedAtUtc = default(DateTime);
            metadata = null;
            dlc = null;
            assets = null;
            achievements = null;
            stats = null;
            items = null;

            if (root == null)
                return false;

            if (!TryReadAppId(root, out appId))
                return false;

            if (root.TryGetValue("fetched_at_utc", out object fetchedObj) && fetchedObj != null)
                DateTime.TryParse(fetchedObj.ToString(), null, System.Globalization.DateTimeStyles.RoundtripKind, out fetchedAtUtc);

            metadata = ParseMetadataNode(AsObject(root, "metadata"));
            dlc = ParseDlcNode(AsObject(root, "dlc"));
            assets = ParseAssetsNode(AsObject(root, "assets"));
            achievements = ParseAchievementsNode(AsObject(root, "achievements"));
            stats = ParseStatsNode(AsObject(root, "stats"));
            items = ParseItemsNode(AsObject(root, "items"));
            return true;
        }

        public static object KeyValueToJsonObject(AppInfoKeyValue kv)
        {
            return KeyValueToJson(kv);
        }

        private static bool TryReadAppId(Dictionary<string, object> root, out uint appId)
        {
            appId = 0;
            if (!root.TryGetValue("appid", out object raw) || raw == null)
                return false;
            return uint.TryParse(raw.ToString(), out appId) && appId > 0;
        }

        private static Dictionary<string, object> AsObject(Dictionary<string, object> root, string key)
        {
            if (root == null || !root.TryGetValue(key, out object value))
                return null;
            return value as Dictionary<string, object>;
        }

        private static AppMetadataSection ParseMetadataNode(Dictionary<string, object> node)
        {
            var section = new AppMetadataSection();
            if (node == null)
            {
                section.Status = SnapshotSectionStatus.Unavailable;
                return section;
            }

            if (!IsSuccessStatus(node, section))
                return section;

            if (node.TryGetValue("app_info_source", out object src) && src != null
                && Enum.TryParse(src.ToString(), true, out AppInfoSource appInfoSource))
            {
                section.AppInfoSource = appInfoSource;
            }

            if (node.TryGetValue("data", out object data) && data is Dictionary<string, object> dataObj)
                section.AppInfo = AppInfoJsonConverter.ObjectToRootKeyValue(dataObj, stripLeadingUnderscoreMetadata: false);

            return section;
        }

        private static DlcSection ParseDlcNode(Dictionary<string, object> node)
        {
            var section = new DlcSection();
            if (node == null)
            {
                section.Status = SnapshotSectionStatus.Unavailable;
                return section;
            }

            if (!IsSuccessStatus(node, section))
                return section;

            var items = new List<DlcEntry>();
            foreach (Dictionary<string, object> item in EnumerateObjectList(node, "items"))
            {
                var entry = new DlcEntry();
                if (item.TryGetValue("appid", out object idRaw) && uint.TryParse(idRaw?.ToString(), out uint id))
                    entry.AppId = id;
                entry.Name = ReadString(item, "name");
                entry.Type = ReadString(item, "type");
                if (string.IsNullOrEmpty(entry.Type))
                    entry.Type = "dlc";
                if (entry.AppId > 0)
                    items.Add(entry);
            }

            section.Items = items;

            var unresolved = new List<uint>();
            if (node.TryGetValue("unresolved_appids", out object unresolvedRaw))
            {
                foreach (object idObj in EnumerateList(unresolvedRaw))
                {
                    if (uint.TryParse(idObj?.ToString(), out uint id) && id > 0)
                        unresolved.Add(id);
                }
            }

            section.UnresolvedAppIds = unresolved;
            return section;
        }

        private static GameAssetsSection ParseAssetsNode(Dictionary<string, object> node)
        {
            var section = new GameAssetsSection();
            if (node == null)
            {
                section.Status = SnapshotSectionStatus.Unavailable;
                return section;
            }

            if (!IsSuccessStatus(node, section))
                return section;

            var items = new List<GameAssetEntry>();
            foreach (Dictionary<string, object> item in EnumerateObjectList(node, "items"))
            {
                var entry = new GameAssetEntry
                {
                    KeyPath = ReadString(item, "key_path"),
                    Value = ReadString(item, "value"),
                    Url = ReadOptionalString(item, "url")
                };
                var urls = new List<string>();
                if (item.TryGetValue("candidate_urls", out object candRaw))
                {
                    foreach (object urlObj in EnumerateList(candRaw))
                    {
                        string url = urlObj?.ToString();
                        if (!string.IsNullOrEmpty(url))
                            urls.Add(url);
                    }
                }

                entry.CandidateUrls = urls;
                items.Add(entry);
            }

            section.Items = items;
            return section;
        }

        private static AchievementsSection ParseAchievementsNode(Dictionary<string, object> node)
        {
            var section = new AchievementsSection();
            if (node == null)
            {
                section.Status = SnapshotSectionStatus.Unavailable;
                return section;
            }

            if (!IsSuccessStatus(node, section))
                return section;

            section.GameName = ReadOptionalString(node, "game_name");
            section.GameVersion = ReadOptionalString(node, "game_version");
            var items = new List<AchievementSchemaEntry>();
            foreach (Dictionary<string, object> item in EnumerateObjectList(node, "items"))
            {
                items.Add(new AchievementSchemaEntry
                {
                    Name = ReadString(item, "name"),
                    DisplayName = ReadString(item, "display_name"),
                    Description = ReadString(item, "description"),
                    IconUrl = ReadOptionalString(item, "icon_url"),
                    IconGrayUrl = ReadOptionalString(item, "icon_gray_url"),
                    Hidden = ReadBool(item, "hidden")
                });
            }

            section.Items = items;
            return section;
        }

        private static StatsSection ParseStatsNode(Dictionary<string, object> node)
        {
            var section = new StatsSection();
            if (node == null)
            {
                section.Status = SnapshotSectionStatus.Unavailable;
                return section;
            }

            if (!IsSuccessStatus(node, section))
                return section;

            var items = new List<StatSchemaEntry>();
            foreach (Dictionary<string, object> item in EnumerateObjectList(node, "items"))
            {
                items.Add(new StatSchemaEntry
                {
                    Name = ReadString(item, "name"),
                    DisplayName = ReadString(item, "display_name"),
                    Type = ReadString(item, "type"),
                    DefaultValue = ReadString(item, "default_value")
                });
            }

            section.Items = items;
            return section;
        }

        private static ItemsSection ParseItemsNode(Dictionary<string, object> node)
        {
            var section = new ItemsSection();
            if (node == null)
            {
                section.Status = SnapshotSectionStatus.Unavailable;
                return section;
            }

            if (!IsSuccessStatus(node, section))
                return section;

            section.Supported = ReadBool(node, "supported");
            section.Digest = ReadOptionalString(node, "digest");
            section.ArchiveJson = ReadOptionalString(node, "archive_json");
            var items = new List<ItemSchemaEntry>();
            foreach (Dictionary<string, object> item in EnumerateObjectList(node, "items"))
            {
                items.Add(new ItemSchemaEntry
                {
                    ItemDefId = ReadString(item, "itemdef_id"),
                    Name = ReadString(item, "name"),
                    Type = ReadString(item, "type"),
                    IconUrl = ReadOptionalString(item, "icon_url")
                });
            }

            section.Items = items;
            return section;
        }

        private static bool IsSuccessStatus(Dictionary<string, object> node, SnapshotSection section)
        {
            string status = ReadString(node, "status");
            if (string.Equals(status, "success", StringComparison.OrdinalIgnoreCase)
                || string.Equals(status, "partial", StringComparison.OrdinalIgnoreCase))
            {
                section.Status = string.Equals(status, "partial", StringComparison.OrdinalIgnoreCase)
                    ? SnapshotSectionStatus.Partial
                    : SnapshotSectionStatus.Ok;
                return true;
            }

            section.Status = SnapshotSectionStatus.Error;
            section.Error = ReadOptionalString(node, "error") ?? "Request failed.";
            return false;
        }

        private static IEnumerable<Dictionary<string, object>> EnumerateObjectList(Dictionary<string, object> node, string key)
        {
            if (node == null || !node.TryGetValue(key, out object raw))
                yield break;

            foreach (object item in EnumerateList(raw))
            {
                if (item is Dictionary<string, object> obj)
                    yield return obj;
            }
        }

        private static IEnumerable<object> EnumerateList(object raw)
        {
            if (raw is object[] arr)
            {
                foreach (object item in arr)
                    yield return item;
                yield break;
            }

            if (raw is System.Collections.ArrayList list)
            {
                foreach (object item in list)
                    yield return item;
                yield break;
            }

            if (raw is System.Collections.IEnumerable enumerable && !(raw is string))
            {
                foreach (object item in enumerable)
                    yield return item;
            }
        }

        private static string ReadString(Dictionary<string, object> node, string key)
        {
            return ReadOptionalString(node, key) ?? string.Empty;
        }

        private static string ReadOptionalString(Dictionary<string, object> node, string key)
        {
            if (node == null || !node.TryGetValue(key, out object value) || value == null)
                return null;
            string text = value.ToString();
            return string.IsNullOrEmpty(text) ? null : text;
        }

        private static bool ReadBool(Dictionary<string, object> node, string key)
        {
            if (node == null || !node.TryGetValue(key, out object value) || value == null)
                return false;
            if (value is bool b)
                return b;
            if (value is int i)
                return i != 0;
            if (bool.TryParse(value.ToString(), out bool parsed))
                return parsed;
            return value.ToString() == "1";
        }

        private static List<object> BuildItemEntryNodes(IReadOnlyList<ItemSchemaEntry> entries)
        {
            var items = new List<object>();
            if (entries == null)
                return items;

            foreach (ItemSchemaEntry entry in entries)
            {
                var item = new Dictionary<string, object>();
                item["itemdef_id"] = entry.ItemDefId ?? string.Empty;
                item["name"] = entry.Name ?? string.Empty;
                item["type"] = entry.Type ?? string.Empty;
                if (!string.IsNullOrEmpty(entry.IconUrl))
                    item["icon_url"] = entry.IconUrl;
                items.Add(item);
            }

            return items;
        }

        private static bool IsSuccess(SnapshotSectionStatus status) =>
            status == SnapshotSectionStatus.Ok || status == SnapshotSectionStatus.Partial;

        private static Dictionary<string, object> BuildErrorNode(SnapshotSection section)
        {
            var node = new Dictionary<string, object>();
            node["status"] = "error";
            node["error"] = section.Error ?? "Request failed.";
            return node;
        }

        private static object KeyValueToJson(AppInfoKeyValue kv)
        {
            if (kv == null)
                return null;

            if (kv.Children.Count == 0)
                return kv.Value ?? string.Empty;

            var obj = new Dictionary<string, object>();
            foreach (AppInfoKeyValue child in kv.Children)
                obj[child.Name ?? string.Empty] = KeyValueToJson(child);
            return obj;
        }
    }
}
