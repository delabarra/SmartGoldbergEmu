using System;
using System.Collections.Generic;
using System.IO;
using AppDataKit;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.JsonKit;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Services
{
    // Load/save resources/{appId}.json catalog SoT (JsonKit at app boundary).
    public static class AppCatalogSnapshotStore
    {
        public static string GetCatalogJsonPath(ulong appId, string gamesDirectoryRoot = null)
        {
            string root = string.IsNullOrWhiteSpace(gamesDirectoryRoot)
                ? PathConstants.GamesDirectory
                : gamesDirectoryRoot;
            return PathConstants.CombineGamesPerAppCatalogJsonFilePath(root, appId.ToString());
        }

        public static bool TryLoad(ulong appId, out AppCatalogSnapshot snapshot, string gamesDirectoryRoot = null)
        {
            snapshot = null;
            if (appId == 0 || appId > uint.MaxValue)
                return false;

            string path = GetCatalogJsonPath(appId, gamesDirectoryRoot);
            if (!File.Exists(path))
                return false;

            try
            {
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                    return false;

                // AppDataJson.TryParseNodes expects the JavaScriptSerializer-style Dictionary<string, object> tree.
                object parsed = AppDataKitJsonParse(json);
                var root = parsed as Dictionary<string, object>;
                if (root == null)
                    return false;

                if (!AppDataJson.TryParseNodes(
                    root,
                    out uint parsedAppId,
                    out DateTime fetchedAtUtc,
                    out AppMetadataSection metadata,
                    out DlcSection dlc,
                    out GameAssetsSection assets,
                    out AchievementsSection achievements,
                    out StatsSection stats,
                    out ItemsSection items))
                {
                    return false;
                }

                if (parsedAppId != (uint)appId)
                {
                    Program.LogService?.LogWarning(
                        "Catalog JSON appid mismatch for " + appId + " (file has " + parsedAppId + ").");
                }

                snapshot = FinalizeSnapshot(
                    parsedAppId,
                    fetchedAtUtc,
                    metadata,
                    dlc,
                    assets,
                    achievements,
                    stats,
                    items,
                    fromAppDataKit: true,
                    AppMetadataFetchFailure.None);
                return snapshot != null && snapshot.IsUsable;
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning(
                    "Failed to load catalog JSON for app " + appId + ": " + ex.Message);
                snapshot = null;
                return false;
            }
        }

        public static bool TrySave(AppCatalogSnapshot snapshot, string gamesDirectoryRoot = null)
        {
            if (snapshot == null || snapshot.AppId == 0)
                return false;

            try
            {
                string path = GetCatalogJsonPath(snapshot.AppId, gamesDirectoryRoot);
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                Dictionary<string, object> nodes = AppDataJson.BuildNodes(
                    snapshot.AppId,
                    snapshot.FetchedAtUtc == default(DateTime) ? DateTime.UtcNow : snapshot.FetchedAtUtc,
                    snapshot.Metadata ?? new AppMetadataSection { Status = SnapshotSectionStatus.Unavailable },
                    snapshot.Dlc ?? new DlcSection { Status = SnapshotSectionStatus.Unavailable },
                    snapshot.Assets ?? new GameAssetsSection { Status = SnapshotSectionStatus.Unavailable },
                    snapshot.Achievements ?? new AchievementsSection { Status = SnapshotSectionStatus.Unavailable },
                    snapshot.Stats ?? new StatsSection { Status = SnapshotSectionStatus.Unavailable },
                    snapshot.Items ?? new ItemsSection { Status = SnapshotSectionStatus.Unavailable });

                string json = JsonConvert.SerializeObject(nodes, JsonFormatting.Indented);
                File.WriteAllText(path, json);
                return true;
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning(
                    "Failed to save catalog JSON for app " + snapshot.AppId + ": " + ex.Message);
                return false;
            }
        }

        public static AppCatalogSnapshot FinalizeSnapshot(
            uint appId,
            DateTime fetchedAtUtc,
            AppMetadataSection metadata,
            DlcSection dlc,
            GameAssetsSection assets,
            AchievementsSection achievements,
            StatsSection stats,
            ItemsSection items,
            bool fromAppDataKit,
            AppMetadataFetchFailure failure)
        {
            AppInfoKeyValue appInfo = metadata?.AppInfo;

            OnlineAppData online = null;
            if (appInfo != null)
            {
                online = new OnlineAppData
                {
                    AppId = appId.ToString(),
                    DataSources = fromAppDataKit ? "SteamCmd" : "Steam (game assets)"
                };
                AppInfoKeyValueHelper.PopulateMetadataFromAppRoot(appInfo, online);
            }

            var depotIds = new List<uint>();
            if (metadata?.AppInfo != null)
            {
                foreach (DepotInfo depot in DepotParser.ParseDepots(metadata.AppInfo, appId))
                {
                    if (depot != null && depot.DepotId > 0 && !depotIds.Contains(depot.DepotId))
                        depotIds.Add(depot.DepotId);
                }
            }

            return new AppCatalogSnapshot
            {
                AppId = appId,
                FetchedAtUtc = fetchedAtUtc == default(DateTime) ? DateTime.UtcNow : fetchedAtUtc,
                Metadata = metadata,
                Dlc = dlc,
                Assets = assets,
                Achievements = achievements,
                Stats = stats,
                Items = items,
                Online = online,
                AppDepotIds = depotIds,
                InstallDir = online?.InstallDir,
                SupportedLanguages = online?.SupportedLanguages,
                FromAppDataKit = fromAppDataKit,
                Failure = failure
            };
        }

        // Kit JsonUtil is internal, so walk the JsonKit tree into the Dictionary shape AppDataJson Build/Parse uses.
        private static object AppDataKitJsonParse(string json)
        {
            JsonValue root = JsonValue.Parse(json);
            return JsonValueToObject(root);
        }

        private static object JsonValueToObject(JsonValue value)
        {
            if (value == null || value.Kind == JsonValueKind.Null)
                return null;

            switch (value.Kind)
            {
                case JsonValueKind.String:
                    return ((JsonString)value).Value;
                case JsonValueKind.Boolean:
                    return ((JsonBool)value).Value;
                case JsonValueKind.Integer:
                    return ((JsonNumber)value).IntegerValue;
                case JsonValueKind.Float:
                    return ((JsonNumber)value).FloatValue;
                case JsonValueKind.Array:
                    {
                        var list = new List<object>();
                        foreach (JsonValue item in (JsonArray)value)
                            list.Add(JsonValueToObject(item));
                        return list.ToArray();
                    }
                case JsonValueKind.Object:
                    {
                        var dict = new Dictionary<string, object>();
                        foreach (JsonProperty prop in ((JsonObject)value).Properties())
                            dict[prop.Name ?? string.Empty] = JsonValueToObject(prop.Value);
                        return dict;
                    }
                default:
                    return null;
            }
        }
    }
}
