using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SmartGoldbergEmu.JsonKit;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Services
{
    public class GoldbergFilesService
    {
        public sealed class ModsSummaryRow
        {
            public string ModId { get; set; }
            public string FileName { get; set; }
        }

        public sealed class AppConfigData
        {
            public Dictionary<long, string> DlcData { get; } = new Dictionary<long, string>();
            public Dictionary<string, string> AppPaths { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        public sealed class AdditionalFilesSaveRequest
        {
            public ulong AppId { get; set; }
            public bool IsEditMode { get; set; }
            public string Leaderboards { get; set; }
            public string Depots { get; set; }
            public string CustomBroadcasts { get; set; }
            public string SubscribedGroups { get; set; }
            public string SubscribedGroupsClans { get; set; }
            public string AutoAcceptInvite { get; set; }
            public string BranchesJson { get; set; }
            public string AchievementsJson { get; set; }
            public string ItemsJson { get; set; }
            public string DefaultItemsJson { get; set; }
            public bool HasLeaderboards { get; set; }
            public bool HasDepots { get; set; }
            public bool HasCustomBroadcasts { get; set; }
            public bool HasSubscribedGroups { get; set; }
            public bool HasSubscribedGroupsClans { get; set; }
            public bool HasAutoAcceptInvite { get; set; }
            public bool HasBranches { get; set; }
            public bool HasAchievements { get; set; }
            public bool HasDefaultItems { get; set; }
        }

        public sealed class AdditionalFilesSaveFailure
        {
            public string Key { get; set; }
            public SaveResult Result { get; set; }
        }

        public static string GetInvalidJsonMessageForAdditionalFile(string key)
        {
            if (string.Equals(key, "branches", StringComparison.Ordinal))
                return $"{PathConstants.GoldbergBranchesJsonFileName} contains invalid JSON. Please fix the format before saving.";
            if (string.Equals(key, "achievements", StringComparison.Ordinal))
                return $"{AchievementConstants.AchievementsFileName} contains invalid JSON. Please fix the format before saving.";
            if (string.Equals(key, "items", StringComparison.Ordinal))
                return $"{PathConstants.GoldbergItemsJsonFileName} contains invalid JSON. Please fix the format before saving.";
            if (string.Equals(key, "default_items", StringComparison.Ordinal))
                return $"{PathConstants.GoldbergDefaultItemsJsonFileName} contains invalid JSON. Please fix the format before saving.";
            return null;
        }

        public List<AdditionalFilesSaveFailure> SaveAdditionalFiles(AdditionalFilesSaveRequest request)
        {
            var failures = new List<AdditionalFilesSaveFailure>();
            if (request == null || request.AppId == 0)
                return failures;

            void Track(string key, SaveResult saveResult)
            {
                if (saveResult == null || saveResult.IsSuccess)
                    return;
                failures.Add(new AdditionalFilesSaveFailure { Key = key, Result = saveResult });
            }

            if (request.HasLeaderboards)
                Track("leaderboards", SaveLeaderboards(request.AppId, request.Leaderboards));

            if (request.HasDepots)
            {
                var depotsText = request.Depots ?? string.Empty;
                if (!request.IsEditMode && string.IsNullOrWhiteSpace(depotsText))
                {
                    if (!HasDepotsFile(request.AppId))
                        Track("depots", SaveDepots(request.AppId, depotsText));
                }
                else
                {
                    Track("depots", SaveDepots(request.AppId, depotsText));
                }
            }

            if (request.HasCustomBroadcasts)
                Track("custom_broadcasts", SaveCustomBroadcasts(request.AppId, request.CustomBroadcasts));
            if (request.HasSubscribedGroups)
                Track("subscribed_groups", SaveSubscribedGroups(request.AppId, request.SubscribedGroups));
            if (request.HasSubscribedGroupsClans)
                Track("subscribed_groups_clans", SaveSubscribedGroupsClans(request.AppId, request.SubscribedGroupsClans));
            if (request.HasAutoAcceptInvite)
                Track("auto_accept_invite", SaveAutoAcceptInvite(request.AppId, request.AutoAcceptInvite));
            if (request.HasBranches)
                Track("branches", SaveBranches(request.AppId, request.BranchesJson));

            // achievements.json on add is written by AchievementService after save; only edit may persist form JSON here.
            if (request.HasAchievements && request.IsEditMode)
                Track("achievements", SaveAchievements(request.AppId, request.AchievementsJson));
            // items.json on add is written by ItemGenerator during RunAddModeGoldbergWorkAsync; skip empty "{}" from the add form.
            if (request.IsEditMode || HasNonEmptyItemsJson(request.ItemsJson))
                Track("items", SaveItems(request.AppId, request.ItemsJson));

            if (request.HasDefaultItems)
                Track("default_items", SaveDefaultItems(request.AppId, request.DefaultItemsJson));

            return failures;
        }

        private static bool HasNonEmptyItemsJson(string itemsJson)
        {
            if (string.IsNullOrWhiteSpace(itemsJson))
                return false;
            try
            {
                var token = JsonValue.Parse(itemsJson.Trim());
                var obj = token as JsonObject;
                return obj != null && obj.Count > 0;
            }
            catch (JsonKitException)
            {
                return true;
            }
        }

        private readonly string _gamesDirectory;
        private readonly ITaskReportService _taskReportService;

        private ITaskReportService Feedback => _taskReportService ?? ServiceLocator.TaskReportService;

        public GoldbergFilesService() : this(null, null)
        {
        }

        public GoldbergFilesService(string gamesDirectory = null, ITaskReportService feedbackService = null)
        {
            _gamesDirectory = gamesDirectory ?? PathConstants.GamesDirectory;
            _taskReportService = feedbackService;
        }

        private string GetGameSteamSettingsPath(ulong appId)
        {
            return PathConstants.CombineGameSteamSettingsDirectory(_gamesDirectory, appId.ToString());
        }

        #region Leaderboards

        public string LoadLeaderboards(ulong appId)
        {
            try
            {
                var path = Path.Combine(GetGameSteamSettingsPath(appId), PathConstants.GoldbergLeaderboardsFileName);
                if (File.Exists(path))
                {
                    var lines = File.ReadAllLines(path);
                    return string.Join(Environment.NewLine, lines);
                }
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService?.LogError($"Failed to load {PathConstants.GoldbergLeaderboardsFileName}: {ex.Message}", ex);
            }
            return string.Empty;
        }

        public SaveResult SaveLeaderboards(ulong appId, string content)
        {
            try
            {
                var path = Path.Combine(GetGameSteamSettingsPath(appId), PathConstants.GoldbergLeaderboardsFileName);
                if (!string.IsNullOrWhiteSpace(content))
                {
                    var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllLines(path, lines);
                    return SaveResult.Success(1);
                }
                else if (File.Exists(path))
                {
                    File.Delete(path);
                    return SaveResult.Success(0);
                }
                return SaveResult.Success(0);
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService?.LogError($"Failed to save {PathConstants.GoldbergLeaderboardsFileName}", ex);
                Feedback?.SetMessage("Could not save leaderboards.", TaskReportKind.Error);
                return SaveResult.Failure($"Failed to save {PathConstants.GoldbergLeaderboardsFileName}: {ex.Message}");
            }
        }

        #endregion

        #region Depots

        public SaveResult SaveDepots(ulong appId, string content)
        {
            try
            {
                var path = Path.Combine(GetGameSteamSettingsPath(appId), PathConstants.GoldbergDepotsFileName);
                if (!string.IsNullOrWhiteSpace(content))
                {
                    var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllLines(path, lines);
                    return SaveResult.Success(1);
                }
                else if (File.Exists(path))
                {
                    File.Delete(path);
                    return SaveResult.Success(0);
                }
                return SaveResult.Success(0);
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService?.LogError($"Failed to save {PathConstants.GoldbergDepotsFileName}", ex);
                Feedback?.SetMessage("Could not save depots.", TaskReportKind.Error);
                return SaveResult.Failure($"Failed to save {PathConstants.GoldbergDepotsFileName}: {ex.Message}");
            }
        }

        public bool HasDepotsFile(ulong appId)
        {
            try
            {
                var path = Path.Combine(GetGameSteamSettingsPath(appId), PathConstants.GoldbergDepotsFileName);
                return File.Exists(path);
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Custom Broadcasts

        public string LoadCustomBroadcasts(ulong appId)
        {
            try
            {
                var path = Path.Combine(GetGameSteamSettingsPath(appId), PathConstants.GoldbergCustomBroadcastsFileName);
                if (File.Exists(path))
                {
                    var lines = File.ReadAllLines(path);
                    return string.Join(Environment.NewLine, lines);
                }
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService?.LogError($"Failed to load {PathConstants.GoldbergCustomBroadcastsFileName}: {ex.Message}", ex);
            }
            return string.Empty;
        }

        public SaveResult SaveCustomBroadcasts(ulong appId, string content)
        {
            try
            {
                var path = Path.Combine(GetGameSteamSettingsPath(appId), PathConstants.GoldbergCustomBroadcastsFileName);
                if (!string.IsNullOrWhiteSpace(content))
                {
                    var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllLines(path, lines);
                    return SaveResult.Success(1);
                }
                else if (File.Exists(path))
                {
                    File.Delete(path);
                    return SaveResult.Success(0);
                }
                return SaveResult.Success(0);
            }
            catch (Exception ex)
            {
                return SaveResult.Failure($"Failed to save {PathConstants.GoldbergCustomBroadcastsFileName}: {ex.Message}");
            }
        }

        #endregion

        #region Subscribed Groups

        public string LoadSubscribedGroups(ulong appId)
        {
            try
            {
                var path = Path.Combine(GetGameSteamSettingsPath(appId), PathConstants.GoldbergSubscribedGroupsFileName);
                if (File.Exists(path))
                {
                    var lines = File.ReadAllLines(path);
                    return string.Join(Environment.NewLine, lines);
                }
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService?.LogError($"Failed to load {PathConstants.GoldbergSubscribedGroupsFileName}: {ex.Message}", ex);
            }
            return string.Empty;
        }

        public SaveResult SaveSubscribedGroups(ulong appId, string content)
        {
            try
            {
                var path = Path.Combine(GetGameSteamSettingsPath(appId), PathConstants.GoldbergSubscribedGroupsFileName);
                var lines = NormalizeTextFileLines(content);
                if (lines.Length > 0)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllLines(path, lines);
                    return SaveResult.Success(1);
                }

                if (File.Exists(path))
                {
                    File.Delete(path);
                    return SaveResult.Success(0);
                }

                return SaveResult.Success(0);
            }
            catch (Exception ex)
            {
                return SaveResult.Failure($"Failed to save {PathConstants.GoldbergSubscribedGroupsFileName}: {ex.Message}");
            }
        }

        #endregion

        #region Subscribed Groups Clans

        public string LoadSubscribedGroupsClans(ulong appId)
        {
            try
            {
                var path = Path.Combine(GetGameSteamSettingsPath(appId), PathConstants.GoldbergSubscribedGroupsClansFileName);
                if (File.Exists(path))
                {
                    var lines = File.ReadAllLines(path);
                    return string.Join(Environment.NewLine, lines);
                }
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService?.LogError($"Failed to load {PathConstants.GoldbergSubscribedGroupsClansFileName}: {ex.Message}", ex);
            }
            return string.Empty;
        }

        public SaveResult SaveSubscribedGroupsClans(ulong appId, string content)
        {
            try
            {
                var path = Path.Combine(GetGameSteamSettingsPath(appId), PathConstants.GoldbergSubscribedGroupsClansFileName);
                var lines = NormalizeTextFileLines(content);
                if (lines.Length > 0)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllLines(path, lines);
                    return SaveResult.Success(1);
                }

                if (File.Exists(path))
                {
                    File.Delete(path);
                    return SaveResult.Success(0);
                }

                return SaveResult.Success(0);
            }
            catch (Exception ex)
            {
                return SaveResult.Failure($"Failed to save {PathConstants.GoldbergSubscribedGroupsClansFileName}: {ex.Message}");
            }
        }

        #endregion

        #region Auto Accept Invite

        public string LoadAutoAcceptInvite(ulong appId)
        {
            try
            {
                var path = Path.Combine(GetGameSteamSettingsPath(appId), PathConstants.GoldbergAutoAcceptInviteFileName);
                if (File.Exists(path))
                {
                    var lines = File.ReadAllLines(path);
                    return string.Join(Environment.NewLine, lines);
                }
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService?.LogError($"Failed to load {PathConstants.GoldbergAutoAcceptInviteFileName}: {ex.Message}", ex);
            }
            return string.Empty;
        }

        public SaveResult SaveAutoAcceptInvite(ulong appId, string content)
        {
            try
            {
                var path = Path.Combine(GetGameSteamSettingsPath(appId), PathConstants.GoldbergAutoAcceptInviteFileName);
                if (!string.IsNullOrWhiteSpace(content))
                {
                    var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllLines(path, lines);
                    return SaveResult.Success(1);
                }
                else if (File.Exists(path))
                {
                    File.Delete(path);
                    return SaveResult.Success(0);
                }
                return SaveResult.Success(0);
            }
            catch (Exception ex)
            {
                return SaveResult.Failure($"Failed to save {PathConstants.GoldbergAutoAcceptInviteFileName}: {ex.Message}");
            }
        }

        #endregion

        #region Branches

        public string LoadBranches(ulong appId)
        {
            try
            {
                var path = Path.Combine(GetGameSteamSettingsPath(appId), PathConstants.GoldbergBranchesJsonFileName);
                if (File.Exists(path))
                {
                    var jsonContent = File.ReadAllText(path);
                    // Pretty-print JSON for display
                    try
                    {
                        var jsonObj = JsonConvert.DeserializeObject(jsonContent);
                        return JsonConvert.SerializeObject(jsonObj, JsonFormatting.Indented);
                    }
                    catch
                    {
                        // If JSON is invalid, return raw content
                        return jsonContent;
                    }
                }
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService?.LogError($"Failed to load {PathConstants.GoldbergBranchesJsonFileName}: {ex.Message}", ex);
            }
            return string.Empty;
        }

        public SaveResult SaveBranches(ulong appId, string content)
        {
            return SaveJsonFile(appId, PathConstants.GoldbergBranchesJsonFileName, content, JsonFormatting.None);
        }

        #endregion

        #region Achievements

        public string LoadAchievements(ulong appId)
        {
            try
            {
                var path = Path.Combine(GetGameSteamSettingsPath(appId), AchievementConstants.AchievementsFileName);
                if (File.Exists(path))
                {
                    var jsonContent = File.ReadAllText(path);
                    // Pretty-print JSON for display
                    try
                    {
                        var jsonObj = JsonConvert.DeserializeObject(jsonContent);
                        return JsonConvert.SerializeObject(jsonObj, JsonFormatting.Indented);
                    }
                    catch
                    {
                        // If JSON is invalid, return raw content
                        return jsonContent;
                    }
                }
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService?.LogError($"Failed to load {AchievementConstants.AchievementsFileName}: {ex.Message}", ex);
            }
            return string.Empty;
        }

        public SaveResult SaveAchievements(ulong appId, string content)
        {
            return SaveJsonFile(appId, AchievementConstants.AchievementsFileName, content, JsonFormatting.None);
        }

        #endregion

        #region Items

        public string LoadItems(ulong appId)
        {
            try
            {
                var path = Path.Combine(GetGameSteamSettingsPath(appId), PathConstants.GoldbergItemsJsonFileName);
                if (File.Exists(path))
                {
                    var jsonContent = File.ReadAllText(path);
                    // Pretty-print JSON for display
                    try
                    {
                        var jsonObj = JsonConvert.DeserializeObject(jsonContent);
                        return JsonConvert.SerializeObject(jsonObj, JsonFormatting.Indented);
                    }
                    catch
                    {
                        // If JSON is invalid, return raw content
                        return jsonContent;
                    }
                }
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService?.LogError($"Failed to load {PathConstants.GoldbergItemsJsonFileName}: {ex.Message}", ex);
            }
            return string.Empty;
        }

        public static void SetItemStringProperty(JsonObject item, string key, string value)
        {
            if (item == null || string.IsNullOrEmpty(key))
                return;
            if (string.IsNullOrWhiteSpace(value))
            {
                item.Remove(key);
                return;
            }
            item[key] = new JsonString(value.Trim());
        }

        public static void ResolveInventoryKeys(JsonObject item, out string quantityKey, out string priceKey)
        {
            quantityKey = "quantity";
            if (item != null)
            {
                if (item["quantity"] != null)
                    quantityKey = "quantity";
                else if (item["count"] != null)
                    quantityKey = "count";
                else if (item["amount"] != null)
                    quantityKey = "amount";
            }

            priceKey = "price";
            if (item != null)
            {
                if (item["price"] != null)
                    priceKey = "price";
                else if (item["tw_price"] != null)
                    priceKey = "tw_price";
                else if (item["base_price"] != null)
                    priceKey = "base_price";
            }
        }

        public static string ComposeAdditionalFilesState(params string[] parts)
        {
            if (parts == null || parts.Length == 0)
                return string.Empty;
            return string.Join("\n---\n", parts);
        }

        public SaveResult SaveItems(ulong appId, string content)
        {
            return SaveJsonFile(
                appId,
                PathConstants.GoldbergItemsJsonFileName,
                content,
                JsonFormatting.None,
                deleteWhenEmptyJsonObject: true);
        }

        public bool ShouldAutoGenerateItems(ulong appId)
        {
            try
            {
                var path = Path.Combine(GetGameSteamSettingsPath(appId), PathConstants.GoldbergItemsJsonFileName);
                if (!File.Exists(path))
                    return true;

                var text = File.ReadAllText(path).Trim();
                if (string.IsNullOrEmpty(text))
                    return true;

                var token = JsonValue.Parse(text);
                var obj = token as JsonObject;
                return obj == null || obj.Count == 0;
            }
            catch
            {
                return true;
            }
        }

        public JsonObject ParseItemsJsonToMap(string rawJson)
        {
            var trimmed = rawJson?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(trimmed))
                return new JsonObject();

            var token = JsonValue.Parse(trimmed);
            var root = token as JsonObject;
            if (root == null)
                throw new JsonReaderException($"{PathConstants.GoldbergItemsJsonFileName} root must be a JSON object.");

            if (root["items"] is JsonArray legacyArray && root.Property("digest") != null)
            {
                var map = new JsonObject();
                int index = 0;
                foreach (var element in legacyArray)
                {
                    var item = element as JsonObject;
                    if (item == null)
                        continue;
                    string id = item["itemdefid"]?.ToString() ?? ("item_" + index);
                    map[id] = item;
                    index++;
                }
                return map;
            }

            foreach (var property in root.Properties())
            {
                if (!(property.Value is JsonObject))
                    throw new JsonReaderException($"{PathConstants.GoldbergItemsJsonFileName} must be an object keyed by itemdefid (each value an object), or legacy {{ digest, items: [...] }}.");
            }

            return root;
        }

        #endregion

        #region Default Items

        public SaveResult SaveDefaultItems(ulong appId, string content)
        {
            return SaveJsonFile(appId, PathConstants.GoldbergDefaultItemsJsonFileName, content, JsonFormatting.None);
        }

        #endregion

        #region Stats

        public string LoadStats(ulong appId)
        {
            try
            {
                var path = Path.Combine(GetGameSteamSettingsPath(appId), PathConstants.GoldbergStatsJsonFileName);
                if (File.Exists(path))
                {
                    var jsonContent = File.ReadAllText(path);
                    try
                    {
                        var jsonObj = JsonConvert.DeserializeObject(jsonContent);
                        return JsonConvert.SerializeObject(jsonObj, JsonFormatting.Indented);
                    }
                    catch
                    {
                        return jsonContent;
                    }
                }
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService?.LogError($"Failed to load {PathConstants.GoldbergStatsJsonFileName}: {ex.Message}", ex);
            }
            return string.Empty;
        }

        public SaveResult SaveStats(ulong appId, string content)
        {
            return SaveJsonFile(appId, PathConstants.GoldbergStatsJsonFileName, content, JsonFormatting.Indented);
        }

        public string FormatStatsForDisplay(string statsJson)
        {
            if (string.IsNullOrWhiteSpace(statsJson))
                return "No stats file found.";

            try
            {
                var token = JsonValue.Parse(statsJson);
                var statsArray = token as JsonArray;
                if (statsArray == null || statsArray.Count == 0)
                    return token.ToJsonString(JsonFormatting.Indented);

                var lines = new List<string>();
                foreach (var stat in statsArray.OfType<JsonObject>())
                {
                    var title = GetStatDisplayTitle(stat);
                    var defTok = stat["default"] ?? stat["defaultvalue"];
                    var defaultValue = defTok != null ? defTok.ToString() : "n/a";
                    lines.Add(string.Format("{0}: {1}", title, defaultValue));
                }

                return string.Join(Environment.NewLine, lines);
            }
            catch
            {
                return statsJson;
            }
        }

        private static string GetStatDisplayTitle(JsonObject stat)
        {
            var displayName = stat["displayName"]?.ToString()?.Trim();
            if (!string.IsNullOrEmpty(displayName))
                return displayName;

            var rawName = stat["name"]?.ToString() ?? string.Empty;
            var s = rawName.Trim();
            if (s.Length >= 5 && s.StartsWith("STAT_", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(5);
            s = s.Replace('_', ' ').Trim();
            if (string.IsNullOrEmpty(s))
                return string.IsNullOrEmpty(rawName) ? "(unnamed)" : rawName;
            return s.ToUpperInvariant();
        }

        #endregion

        #region Server Browser

        public string LoadInternetServers(ulong appId)
        {
            return LoadTextFile(appId, PathConstants.GoldbergInternetServersFileName);
        }

        public SaveResult SaveInternetServers(ulong appId, string content)
        {
            return SaveTextFile(appId, PathConstants.GoldbergInternetServersFileName, content);
        }

        public string LoadFavoriteServers(ulong appId)
        {
            return LoadTextFile(appId, PathConstants.GoldbergFavoriteServersFileName);
        }

        public SaveResult SaveFavoriteServers(ulong appId, string content)
        {
            return SaveTextFile(appId, PathConstants.GoldbergFavoriteServersFileName, content);
        }

        public string LoadHistoryServers(ulong appId)
        {
            return LoadTextFile(appId, PathConstants.GoldbergHistoryServersFileName);
        }

        public SaveResult SaveHistoryServers(ulong appId, string content)
        {
            return SaveTextFile(appId, PathConstants.GoldbergHistoryServersFileName, content);
        }

        private static string[] NormalizeTextFileLines(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return Array.Empty<string>();

            return content
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => line.Length > 0)
                .ToArray();
        }

        // Raw sidecar read (depots.txt, steam_interfaces.txt, etc.).
        public string LoadSteamSettingsTextFile(ulong appId, string fileName)
        {
            if (appId == 0 || string.IsNullOrWhiteSpace(fileName))
                return string.Empty;

            try
            {
                var path = Path.Combine(GetGameSteamSettingsPath(appId), fileName);
                return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService?.LogError($"Failed to load {fileName}: {ex.Message}", ex);
                return string.Empty;
            }
        }

        private string LoadTextFile(ulong appId, string fileName)
        {
            try
            {
                var path = Path.Combine(GetGameSteamSettingsPath(appId), fileName);
                if (File.Exists(path))
                {
                    var lines = File.ReadAllLines(path);
                    return string.Join(Environment.NewLine, lines);
                }
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService?.LogError($"Failed to load {fileName}: {ex.Message}", ex);
            }
            return string.Empty;
        }

        private SaveResult SaveJsonFile(
            ulong appId,
            string fileName,
            string content,
            JsonFormatting formatting,
            bool deleteWhenEmptyJsonObject = false)
        {
            try
            {
                var path = Path.Combine(GetGameSteamSettingsPath(appId), fileName);
                if (!string.IsNullOrWhiteSpace(content))
                {
                    try
                    {
                        JsonValue token = JsonValue.Parse(content.Trim());
                        if (deleteWhenEmptyJsonObject && token is JsonObject emptyObj && emptyObj.Count == 0)
                        {
                            if (File.Exists(path))
                                File.Delete(path);
                            return SaveResult.Success(0);
                        }

                        string jsonContent = JsonConvert.SerializeObject(token, formatting);
                        Directory.CreateDirectory(Path.GetDirectoryName(path));
                        File.WriteAllText(path, jsonContent);
                        return SaveResult.Success(1);
                    }
                    catch (JsonKitException ex)
                    {
                        return SaveResult.Failure($"Invalid JSON in {fileName}: {ex.Message}");
                    }
                }

                if (File.Exists(path))
                {
                    File.Delete(path);
                    return SaveResult.Success(0);
                }

                return SaveResult.Success(0);
            }
            catch (Exception ex)
            {
                return SaveResult.Failure($"Failed to save {fileName}: {ex.Message}");
            }
        }

        private SaveResult SaveTextFile(ulong appId, string fileName, string content)
        {
            try
            {
                var path = Path.Combine(GetGameSteamSettingsPath(appId), fileName);
                if (!string.IsNullOrWhiteSpace(content))
                {
                    var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllLines(path, lines);
                    return SaveResult.Success(1);
                }
                else if (File.Exists(path))
                {
                    File.Delete(path);
                    return SaveResult.Success(0);
                }
                return SaveResult.Success(0);
            }
            catch (Exception ex)
            {
                return SaveResult.Failure($"Failed to save {fileName}: {ex.Message}");
            }
        }

        #endregion

        #region Game Coordinator and purchased keys

        public string LoadGcJson(ulong appId)
        {
            return LoadOptionalJsonFile(appId, PathConstants.GoldbergGcJsonFileName);
        }

        public SaveResult SaveGcJson(ulong appId, string content)
        {
            return SaveOptionalJsonFile(appId, PathConstants.GoldbergGcJsonFileName, content);
        }

        public string LoadPurchasedKeys(ulong appId)
        {
            return LoadTextFile(appId, PathConstants.GoldbergPurchasedKeysFileName);
        }

        public SaveResult SavePurchasedKeys(ulong appId, string content)
        {
            return SaveTextFile(appId, PathConstants.GoldbergPurchasedKeysFileName, content);
        }

        private string LoadOptionalJsonFile(ulong appId, string fileName)
        {
            try
            {
                var path = Path.Combine(GetGameSteamSettingsPath(appId), fileName);
                if (!File.Exists(path))
                    return string.Empty;

                var text = File.ReadAllText(path).Trim();
                if (string.IsNullOrEmpty(text))
                    return string.Empty;

                var jsonObj = JsonConvert.DeserializeObject(text);
                return JsonConvert.SerializeObject(jsonObj, JsonFormatting.Indented);
            }
            catch (JsonKitException)
            {
                return LoadTextFile(appId, fileName);
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService?.LogError($"Failed to load {fileName}: {ex.Message}", ex);
                return string.Empty;
            }
        }

        private SaveResult SaveOptionalJsonFile(ulong appId, string fileName, string content)
        {
            return SaveJsonFile(appId, fileName, content, JsonFormatting.Indented);
        }

        #endregion

        #region Supported Languages

        public string LoadSupportedLanguages(ulong appId)
        {
            try
            {
                var path = Path.Combine(GetGameSteamSettingsPath(appId), PathConstants.GoldbergSupportedLanguagesFileName);
                if (File.Exists(path))
                {
                    var lines = File.ReadAllLines(path);
                    // Return comma-separated list for compatibility with PopulateLanguageDropdown
                    return string.Join(",", lines.Where(l => !string.IsNullOrWhiteSpace(l)));
                }
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService?.LogError($"Failed to load {PathConstants.GoldbergSupportedLanguagesFileName}: {ex.Message}", ex);
            }
            return string.Empty;
        }

        #endregion

        #region Mods

        public List<ModsSummaryRow> LoadModsSummaryRows(ulong appId, ISet<string> skippedExtensions, string noIdDisplay)
        {
            var rows = new List<ModsSummaryRow>();
            try
            {
                var modsDir = Path.Combine(GetGameSteamSettingsPath(appId), PathConstants.GoldbergSteamSettingsModsFolderName);
                if (!Directory.Exists(modsDir))
                    return rows;

                foreach (string dirPath in Directory.GetDirectories(modsDir).OrderBy(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase))
                {
                    string folderName = Path.GetFileName(dirPath);
                    string modIdColumn = IsNumericWorkshopFolderName(folderName) ? folderName : noIdDisplay;
                    foreach (string filePath in Directory.GetFiles(dirPath).OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase))
                    {
                        if (ShouldSkipModsListFile(filePath, skippedExtensions))
                            continue;
                        rows.Add(new ModsSummaryRow { ModId = modIdColumn, FileName = Path.GetFileName(filePath) });
                    }
                }

                foreach (string filePath in Directory.GetFiles(modsDir).OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase))
                {
                    if (ShouldSkipModsListFile(filePath, skippedExtensions))
                        continue;
                    rows.Add(new ModsSummaryRow { ModId = noIdDisplay, FileName = Path.GetFileName(filePath) });
                }

                return rows
                    .OrderBy(r => SortKeyForModsIdColumn(r.ModId, noIdDisplay))
                    .ThenBy(r => r.ModId, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(r => r.FileName, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService?.LogWarning("Failed to load mods summary rows: " + ex.Message);
                return rows;
            }
        }

        public string GetModsDirectory(ulong appId)
        {
            return Path.Combine(GetGameSteamSettingsPath(appId), PathConstants.GoldbergSteamSettingsModsFolderName);
        }

        public string EnsureModsDirectory(ulong appId)
        {
            var modsDir = GetModsDirectory(appId);
            Directory.CreateDirectory(modsDir);
            return modsDir;
        }

        public SaveResult CopyFilesToMods(ulong appId, IEnumerable<string> sourceFiles)
        {
            try
            {
                if (sourceFiles == null)
                    return SaveResult.Success(0);

                string modsDir = EnsureModsDirectory(appId);

                int copied = 0;
                foreach (var src in sourceFiles)
                {
                    if (string.IsNullOrWhiteSpace(src) || !File.Exists(src))
                        continue;

                    string name = Path.GetFileName(src);
                    if (string.IsNullOrEmpty(name))
                        continue;

                    File.Copy(src, Path.Combine(modsDir, name), true);
                    copied++;
                }

                return SaveResult.Success(copied);
            }
            catch (Exception ex)
            {
                return SaveResult.Failure("Failed to copy files to mods: " + ex.Message);
            }
        }

        public SaveResult CopyFolderToMods(ulong appId, string sourceFolder)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(sourceFolder) || !Directory.Exists(sourceFolder))
                    return SaveResult.Success(0);

                string modsDir = EnsureModsDirectory(appId);
                string folderName = Path.GetFileName(sourceFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (string.IsNullOrEmpty(folderName))
                    return SaveResult.Failure("Could not determine source folder name.");

                string destRoot = Path.Combine(modsDir, folderName);
                CopyDirectoryTree(sourceFolder, destRoot, true);
                return SaveResult.Success(1);
            }
            catch (Exception ex)
            {
                return SaveResult.Failure("Failed to copy folder to mods: " + ex.Message);
            }
        }

        private static ulong SortKeyForModsIdColumn(string modIdColumn, string noIdDisplay)
        {
            if (string.IsNullOrEmpty(modIdColumn) || modIdColumn == noIdDisplay)
                return ulong.MaxValue;
            ulong u;
            return ulong.TryParse(modIdColumn, out u) ? u : ulong.MaxValue - 1;
        }

        private static bool IsNumericWorkshopFolderName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length > 20)
                return false;
            for (int i = 0; i < name.Length; i++)
            {
                if (!char.IsDigit(name[i]))
                    return false;
            }
            return true;
        }

        private static bool ShouldSkipModsListFile(string filePath, ISet<string> skippedExtensions)
        {
            if (skippedExtensions == null)
                return false;
            string ext = Path.GetExtension(filePath);
            return ext.Length > 0 && skippedExtensions.Contains(ext);
        }

        private static void CopyDirectoryTree(string sourceDir, string destDir, bool overwriteFiles)
        {
            Directory.CreateDirectory(destDir);
            foreach (string filePath in Directory.GetFiles(sourceDir))
            {
                string name = Path.GetFileName(filePath);
                File.Copy(filePath, Path.Combine(destDir, name), overwriteFiles);
            }
            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string subName = Path.GetFileName(subDir);
                CopyDirectoryTree(subDir, Path.Combine(destDir, subName), overwriteFiles);
            }
        }

        public string EnsureSteamClientExtraDllsDirectory()
        {
            string dir = PathConstants.GoldbergSteamClientExtraDllsDirectory;
            Directory.CreateDirectory(dir);
            return dir;
        }

        public IReadOnlyList<string> ListSteamClientExtraDllFileNames()
        {
            string dir = PathConstants.GoldbergSteamClientExtraDllsDirectory;
            if (!Directory.Exists(dir))
                return Array.Empty<string>();

            try
            {
                return Directory.GetFiles(dir, "*.dll", SearchOption.AllDirectories)
                    .Select(path =>
                    {
                        string fullDir = Path.GetFullPath(dir);
                        if (!fullDir.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                            fullDir += Path.DirectorySeparatorChar;
                        string fullPath = Path.GetFullPath(path);
                        return fullPath.StartsWith(fullDir, StringComparison.OrdinalIgnoreCase)
                            ? fullPath.Substring(fullDir.Length)
                            : Path.GetFileName(path);
                    })
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        public SaveResult CopyDllFilesToSteamClientExtraDlls(IEnumerable<string> sourceFiles)
        {
            try
            {
                if (sourceFiles == null)
                    return SaveResult.Success(0);

                string destDir = EnsureSteamClientExtraDllsDirectory();
                int copied = 0;
                foreach (string src in sourceFiles)
                {
                    if (string.IsNullOrWhiteSpace(src) || !File.Exists(src))
                        continue;

                    if (!string.Equals(Path.GetExtension(src), ".dll", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string name = Path.GetFileName(src);
                    if (string.IsNullOrEmpty(name))
                        continue;

                    File.Copy(src, Path.Combine(destDir, name), true);
                    copied++;
                }

                if (copied == 0)
                    return SaveResult.Failure("No .dll files were copied. Select one or more DLL files.");

                return SaveResult.Success(copied);
            }
            catch (Exception ex)
            {
                return SaveResult.Failure("Failed to copy DLLs: " + ex.Message);
            }
        }

        #endregion

        #region App Config (configs.app.ini)

        public AppConfigData LoadAppConfigDlcAndPaths(ulong appId)
        {
            var result = new AppConfigData();

            try
            {
                var appIniPath = Path.Combine(GetGameSteamSettingsPath(appId), PathConstants.GoldbergAppIniFileName);
                if (!File.Exists(appIniPath))
                    return result;

                var lines = File.ReadAllLines(appIniPath);
                string currentSection = null;

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith(";") || trimmedLine.StartsWith("#"))
                        continue;

                    if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                    {
                        currentSection = trimmedLine.Substring(1, trimmedLine.Length - 2);
                        continue;
                    }

                    if (!trimmedLine.Contains("="))
                        continue;

                    var parts = trimmedLine.Split(new[] { '=' }, 2);
                    if (parts.Length != 2)
                        continue;

                    var key = parts[0].Trim();
                    var value = parts[1].Trim();

                    if (string.Equals(currentSection, "app::dlcs", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(key, "unlock_all", StringComparison.OrdinalIgnoreCase))
                    {
                        long dlcId;
                        if (long.TryParse(key, out dlcId))
                            result.DlcData[dlcId] = value;
                        continue;
                    }

                    if (string.Equals(currentSection, "app::paths", StringComparison.OrdinalIgnoreCase))
                        result.AppPaths[key] = value;
                }
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService?.LogError($"Failed to load {PathConstants.GoldbergAppIniFileName} data: {ex.Message}", ex);
            }

            return result;
        }

        public SaveResult SaveAppConfigDlcAndPaths(ulong appId, Dictionary<long, string> dlcData, Dictionary<string, string> appPaths)
        {
            try
            {
                var steamSettingsPath = GetGameSteamSettingsPath(appId);
                var appIniPath = Path.Combine(steamSettingsPath, PathConstants.GoldbergAppIniFileName);
                Directory.CreateDirectory(steamSettingsPath);

                if (!File.Exists(appIniPath))
                {
                    var fresh = new List<string>
                    {
                        "# ############################################################################## #",
                        "# you do not have to specify everything, pick and choose the options you need only",
                        "# ############################################################################## #",
                        string.Empty,
                        "[app::general]",
                        "# by default the emu will report a `non-beta` branch when the game calls `Steam_Apps::GetCurrentBetaName()`",
                        "# 1=make the game/app think we're playing on a beta branch",
                        "# default=0",
                        "is_beta_branch=0",
                        $"# the name of the current branch, this must also exist in {PathConstants.GoldbergBranchesJsonFileName}",
                        "# otherwise will be ignored by the emu and the default 'public' branch will be used",
                        "# default=public",
                        "branch_name=public",
                        string.Empty,
                        "[app::dlcs]",
                        "# 1=report all DLCs as unlocked",
                        "# 0=report only the DLCs mentioned",
                        "# default=1",
                        "unlock_all=0",
                        "# format: ID=name"
                    };

                    foreach (var kvp in (dlcData ?? new Dictionary<long, string>()).OrderBy(x => x.Key))
                        fresh.Add(kvp.Key + "=" + kvp.Value);

                    if (appPaths != null && appPaths.Count > 0)
                    {
                        fresh.Add(string.Empty);
                        fresh.Add("[app::paths]");
                        fresh.Add("# some rare games might need one or more paths to appids");
                        fresh.Add("# this sets the paths returned by Steam_Apps::GetAppInstallDir");
                        foreach (var kvp in appPaths.OrderBy(x => SortNumericTextKey(x.Key)).ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
                            fresh.Add(kvp.Key + "=" + kvp.Value);
                    }

                    File.WriteAllLines(appIniPath, fresh);
                    return SaveResult.Success(1);
                }

                var lines = new List<string>();
                lines.AddRange(File.ReadAllLines(appIniPath));

                var updatedLines = new List<string>();
                bool inDlcSection = false;
                bool dlcSectionUpdated = false;

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();

                    if (trimmedLine == "[app::dlcs]")
                    {
                        inDlcSection = true;
                        EnsureBlankLineBeforeSectionHeader(updatedLines);
                        updatedLines.Add(line);
                        continue;
                    }

                    if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                    {
                        if (inDlcSection && !dlcSectionUpdated && dlcData != null)
                        {
                            foreach (var kvp in dlcData.OrderBy(x => x.Key))
                                updatedLines.Add($"{kvp.Key}={kvp.Value}");
                            dlcSectionUpdated = true;
                        }
                        inDlcSection = false;
                        EnsureBlankLineBeforeSectionHeader(updatedLines);
                        updatedLines.Add(line);
                        continue;
                    }

                    if (inDlcSection)
                    {
                        if (string.IsNullOrWhiteSpace(trimmedLine))
                            continue;

                        if (trimmedLine.Contains("="))
                        {
                            var parts = trimmedLine.Split(new[] { '=' }, 2);
                            if (parts.Length >= 1)
                            {
                                var key = parts[0].Trim();
                                if (key == "unlock_all")
                                {
                                    updatedLines.Add(line);
                                }
                                else if (!long.TryParse(key, out _))
                                {
                                    updatedLines.Add(line);
                                }
                            }
                        }
                    }
                    else
                    {
                        updatedLines.Add(line);
                    }
                }

                if (inDlcSection && !dlcSectionUpdated && dlcData != null)
                {
                    foreach (var kvp in dlcData.OrderBy(x => x.Key))
                        updatedLines.Add($"{kvp.Key}={kvp.Value}");
                }

                bool inPathsSection = false;
                bool pathsSectionUpdated = false;
                var finalLines = new List<string>();
                foreach (var line in updatedLines)
                {
                    var trimmedLine = line.Trim();

                    if (trimmedLine == "[app::paths]")
                    {
                        inPathsSection = true;
                        EnsureBlankLineBeforeSectionHeader(finalLines);
                        finalLines.Add(line);
                        continue;
                    }

                    if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                    {
                        if (inPathsSection && !pathsSectionUpdated && appPaths != null)
                        {
                            foreach (var kvp in appPaths.OrderBy(x => SortNumericTextKey(x.Key)).ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
                                finalLines.Add($"{kvp.Key}={kvp.Value}");
                            pathsSectionUpdated = true;
                        }
                        inPathsSection = false;
                        EnsureBlankLineBeforeSectionHeader(finalLines);
                        finalLines.Add(line);
                        continue;
                    }

                    if (inPathsSection)
                    {
                        if (!trimmedLine.Contains("=") || appPaths == null || !appPaths.ContainsKey(trimmedLine.Split('=')[0].Trim()))
                            finalLines.Add(line);
                    }
                    else
                    {
                        finalLines.Add(line);
                    }
                }

                if (!inPathsSection && appPaths != null && appPaths.Count > 0)
                {
                    EnsureBlankLineBeforeSectionHeader(finalLines);
                    finalLines.Add("[app::paths]");
                    foreach (var kvp in appPaths.OrderBy(x => SortNumericTextKey(x.Key)).ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
                        finalLines.Add($"{kvp.Key}={kvp.Value}");
                }
                else if (inPathsSection && !pathsSectionUpdated && appPaths != null)
                {
                    foreach (var kvp in appPaths.OrderBy(x => SortNumericTextKey(x.Key)).ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
                        finalLines.Add($"{kvp.Key}={kvp.Value}");
                }

                if (finalLines.Count == 0)
                    return SaveResult.Success(0);

                Directory.CreateDirectory(steamSettingsPath);
                File.WriteAllLines(appIniPath, finalLines);
                return SaveResult.Success(1);
            }
            catch (Exception ex)
            {
                return SaveResult.Failure($"Failed to save {PathConstants.GoldbergAppIniFileName} data: {ex.Message}");
            }
        }

        #endregion

        private static long SortNumericTextKey(string key)
        {
            if (long.TryParse(key, out long numeric))
                return numeric;
            return long.MaxValue;
        }

        private static void EnsureBlankLineBeforeSectionHeader(List<string> lines)
        {
            if (lines == null || lines.Count == 0)
                return;

            if (!string.IsNullOrWhiteSpace(lines[lines.Count - 1]))
                lines.Add(string.Empty);
        }

    }
}

