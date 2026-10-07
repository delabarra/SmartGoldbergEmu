using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AppDataKit;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.JsonKit;
using SmartGoldbergEmu.Services;

namespace SmartGoldbergEmu.Generators
{
    public class StatsGenerator
    {
        private readonly string _gamesDirectory;

        public StatsGenerator() : this(PathConstants.GamesDirectory)
        {
        }

        public StatsGenerator(string gamesDirectory)
        {
            _gamesDirectory = gamesDirectory ?? PathConstants.GamesDirectory;
        }

        public string GetGameSteamSettingsPath(ulong appId)
        {
            return PathConstants.CombineGameSteamSettingsDirectory(_gamesDirectory, appId.ToString());
        }

        public bool TryWriteStatsJsonIfAbsent(string steamSettingsPath, string goldbergStatsJson, ulong appId)
        {
            if (string.IsNullOrEmpty(goldbergStatsJson))
                return false;

            var statsPath = Path.Combine(steamSettingsPath, PathConstants.GoldbergStatsJsonFileName);
            if (File.Exists(statsPath))
                return false;

            File.WriteAllText(statsPath, FormatStatsJsonIndented(goldbergStatsJson));
            ServiceLocator.LogService.LogDebug($"Generated {PathConstants.GoldbergStatsJsonFileName} for app {appId}");
            return true;
        }

        public async Task<string> GetStatsJsonFromSteamApiAsync(string appId, string language, string apiKey)
        {
            try
            {
                if (string.IsNullOrEmpty(apiKey) || !ulong.TryParse(appId, out ulong id) || id == 0)
                    return null;

                StatsSection section = await ServiceLocator.AppDataKitBridgeService
                    .FetchStatsAsync(id, language)
                    .ConfigureAwait(false);
                return BuildGoldbergStatsJson(section);
            }
            catch (Exception)
            {
                return null;
            }
        }

        // Goldberg stats.json from a GetSchemaForGame stats section (name/type/default are not localized).
        public static string BuildGoldbergStatsJson(StatsSection section)
        {
            if (section == null
                || section.Status != SnapshotSectionStatus.Ok
                || section.Items == null
                || section.Items.Count == 0)
                return null;

            var statsList = new List<object>();
            foreach (StatSchemaEntry stat in section.Items)
            {
                if (stat == null || string.IsNullOrWhiteSpace(stat.Name))
                    continue;

                statsList.Add(new Dictionary<string, object>
                {
                    ["name"] = stat.Name,
                    ["type"] = NormalizeGoldbergStatType(stat.Type),
                    ["default"] = string.IsNullOrEmpty(stat.DefaultValue) ? "0" : stat.DefaultValue,
                    ["global"] = "0"
                });
            }

            if (statsList.Count == 0)
                return null;

            return JsonConvert.SerializeObject(statsList, JsonFormatting.Indented);
        }

        public static async Task<string> TryGetGoldbergStatsJsonFromStatsDbAsync(string appId)
        {
            if (string.IsNullOrEmpty(appId))
                return null;

            try
            {
                var body = await GamesInfosDatasHelper
                    .TryGetSteamFileBodyAsync(appId, PathConstants.GoldbergStatsDbJsonFileName)
                    .ConfigureAwait(false);
                return TryConvertStatsDbJsonToGoldbergFormat(body);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static string TryConvertStatsDbJsonToGoldbergFormat(string statsDbJson)
        {
            if (string.IsNullOrWhiteSpace(statsDbJson))
                return null;

            try
            {
                return ConvertStatsDbJsonToGoldbergFormat(statsDbJson);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static string ConvertStatsDbJsonToGoldbergFormat(string statsDbJson)
        {
            if (string.IsNullOrWhiteSpace(statsDbJson))
                return null;

            // gbe_fork reads default/global with value(key, std::string), which throws on JSON numbers and drops the stat.
            return BuildGoldbergStatsJson(GamesInfosDatasHelper.ParseStatsDb(statsDbJson));
        }

        private static string FormatStatsJsonIndented(string json)
        {
            if (string.IsNullOrEmpty(json))
                return json;
            try
            {
                return JsonValue.Parse(json).ToJsonString(JsonFormatting.Indented);
            }
            catch (JsonKitException)
            {
                return json;
            }
        }

        private static string NormalizeGoldbergStatType(string rawType)
        {
            if (string.IsNullOrEmpty(rawType))
                return "int";
            switch (rawType.ToLowerInvariant())
            {
                case "float":
                case "floataggregate":
                    return "float";
                case "avgrate":
                case "averagerate":
                    return "avgrate";
                default:
                    return "int";
            }
        }
    }
}
