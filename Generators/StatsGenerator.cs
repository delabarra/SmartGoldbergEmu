using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.JsonKit;
using SmartGoldbergEmu.Services;

namespace SmartGoldbergEmu.Generators
{
    public class StatsGenerator
    {
        private const int HttpTimeoutSeconds = 10;

        private static readonly string[] GlobalFallbackPropertyNames = { "max", "default", "defaultvalue" };

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
            ServiceLocator.LogService.LogMessage($"Generated {PathConstants.GoldbergStatsJsonFileName} for app {appId}");
            return true;
        }

        public async Task<string> GetStatsJsonFromSteamApiAsync(string appId, string language, string apiKey)
        {
            try
            {
                if (string.IsNullOrEmpty(apiKey))
                    return null;

                var url = string.Format(AchievementConstants.SteamUserStatsApiUrl, language, apiKey, appId);
                var responseContent = await HttpGetBodyAsync(url, requireSuccessStatusCode: false).ConfigureAwait(false);
                if (string.IsNullOrEmpty(responseContent))
                    return null;

                var root = JsonObject.Parse(responseContent);
                var availableGameStats = root["game"]?["availableGameStats"];
                if (availableGameStats?["stats"] == null)
                    return null;

                var statsList = new List<object>();
                foreach (JsonValue stat in (JsonArray)(availableGameStats["stats"] ?? new JsonArray()))
                {
                    statsList.Add(new Dictionary<string, object>
                    {
                        ["name"] = stat["name"]?.ToString() ?? "",
                        ["type"] = NormalizeGoldbergStatType(stat["type"]?.ToString()),
                        ["default"] = stat["defaultvalue"]?.ToString() ?? "0",
                        ["global"] = stat["max"]?.ToString() ?? "0"
                    });
                }

                if (statsList.Count == 0)
                    return null;

                return JsonConvert.SerializeObject(statsList, JsonFormatting.Indented);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static async Task<string> TryGetGoldbergStatsJsonFromStatsDbAsync(string appId)
        {
            if (string.IsNullOrEmpty(appId))
                return null;

            try
            {
                var url = string.Format(ApplicationConstants.GamesInfosDatasSteamStatsDbUrlFormat, appId);
                var body = await HttpGetBodyAsync(url, requireSuccessStatusCode: true).ConfigureAwait(false);
                return body == null ? null : ConvertStatsDbJsonToGoldbergFormat(body);
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

            var arr = JsonArray.Parse(statsDbJson);
            var outArr = new JsonArray();

            foreach (var token in arr)
            {
                if (token == null || token.Type != JsonValueKind.Object)
                    continue;

                var o = (JsonObject)token.DeepClone();

                if (o["global"] == null)
                {
                    JsonValue chosen = null;
                    foreach (var key in GlobalFallbackPropertyNames)
                    {
                        var candidate = o[key];
                        if (candidate != null)
                        {
                            chosen = candidate.DeepClone();
                            break;
                        }
                    }
                    o["global"] = chosen ?? new JsonNumber(0);
                }

                outArr.Add(o);
            }

            return outArr.Count == 0 ? null : outArr.ToJsonString(JsonFormatting.Indented);
        }

        private static async Task<string> HttpGetBodyAsync(string url, bool requireSuccessStatusCode)
        {
            try
            {
                using (var httpService = HttpServiceFactory.Create(TimeSpan.FromSeconds(HttpTimeoutSeconds)))
                {
                    using (var response = await httpService.GetAsync(url).ConfigureAwait(false))
                    {
                        if (requireSuccessStatusCode && !response.IsSuccessStatusCode)
                            return null;
                        return await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
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
