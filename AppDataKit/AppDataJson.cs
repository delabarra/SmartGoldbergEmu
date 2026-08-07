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
            node["items"] = BuildItemEntryNodes(section.Items);
            return node;
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
