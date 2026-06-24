using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SmartGoldbergEmu.JsonKit;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;

namespace SmartGoldbergEmu.Generators
{
    /// <summary>
    /// Generates Goldberg <c>steam_settings/items.json</c>, <c>default_items.json</c>, and <c>items_note.txt</c> via Steam Web API.
    /// </summary>
    public sealed class ItemGenerator
    {
        private readonly ITaskReportService _taskReportService;
        private readonly SteamApiKeyService _steamApiKeyService;

        public ItemGenerator(ITaskReportService taskReportService = null, SteamApiKeyService steamApiKeyService = null)
        {
            _taskReportService = taskReportService;
            _steamApiKeyService = steamApiKeyService ?? ServiceLocator.SteamApiKeyService;
        }

        public async Task<ItemGeneratorResult> GenerateAndSaveAsync(
            GameConfig game,
            bool showProgress = true,
            bool friendlyProgressMessages = false,
            CancellationToken cancellationToken = default)
        {
            if (game == null || game.AppId == 0)
                return ItemGeneratorResult.Fail("Invalid game or App ID.");

            if (!_steamApiKeyService.TryGetValidFormatKey(out string apiKey))
                return ItemGeneratorResult.Fail("A valid Steam Web API key is required (Settings).");

            var emulatorConfig = ServiceLocator.EmulatorConfigService;
            if (emulatorConfig == null)
                return ItemGeneratorResult.Fail("Emulator configuration service is not available.");

            string steamSettingsPath = emulatorConfig.GetGameSteamSettingsPath(game.AppId);
            if (string.IsNullOrEmpty(steamSettingsPath))
                return ItemGeneratorResult.Fail($"Could not resolve {PathConstants.SteamSettingsFolderName} path for this game.");

            string appIdStr = game.AppId.ToString();

            try
            {
                if (showProgress && !friendlyProgressMessages)
                {
                    _taskReportService?.SetMessage("Fetching item definition metadataâ€¦");
                    _taskReportService?.SetProgress(0, 100);
                }

                var meta = await SteamWebApiService.GetItemMetaAsync(appIdStr, apiKey).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();

                if (meta == null || !meta.Success || string.IsNullOrEmpty(meta.Digest))
                {
                    if (showProgress && !friendlyProgressMessages)
                        ReportFinishedStatus("No item definitions available for this app.", TaskReportKind.Info);
                    return ItemGeneratorResult.Fail("No item definitions available for this app.");
                }

                if (showProgress && !friendlyProgressMessages)
                {
                    _taskReportService?.SetMessage("Downloading item definition archiveâ€¦");
                    _taskReportService?.SetProgress(15, 100);
                }

                string archiveJson = await SteamWebApiService.GetItemDefArchiveJsonAsync(appIdStr, meta.Digest).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();

                if (string.IsNullOrWhiteSpace(archiveJson))
                {
                    if (showProgress && !friendlyProgressMessages)
                        ReportFinishedStatus("Failed to download item archive.", TaskReportKind.Error);
                    return ItemGeneratorResult.Fail("Failed to download item definition archive.");
                }

                JsonObject map;
                string parseDetail;
                if (!TryBuildItemDefinitionMap(archiveJson, out map, out parseDetail))
                {
                    Program.LogService?.LogWarning("ItemGenerator: could not parse item archive for app " + appIdStr + ". " + parseDetail);
                    return ItemGeneratorResult.Fail(parseDetail);
                }

                if (map.Count == 0)
                {
                    if (showProgress && !friendlyProgressMessages)
                        ReportFinishedStatus("Item archive contained no item definitions.", TaskReportKind.Info);
                    return ItemGeneratorResult.Fail("Item archive contained no item definitions.");
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (showProgress && friendlyProgressMessages)
                {
                    _taskReportService?.SetMessage($"Generating items 0/{map.Count}");
                    _taskReportService?.SetProgress(0, map.Count);
                }
                else if (showProgress)
                {
                    _taskReportService?.SetProgress(99, 100);
                    _taskReportService?.SetMessage(string.Format("Writing {0} item definition(s)â€¦", map.Count));
                }

                Directory.CreateDirectory(steamSettingsPath);
                string itemsPath = Path.Combine(steamSettingsPath, PathConstants.GoldbergItemsJsonFileName);
                string notePath = Path.Combine(steamSettingsPath, PathConstants.GoldbergItemsNoteFileName);

                string itemsOut = map.ToJsonString(JsonFormatting.Indented);
                File.WriteAllText(itemsPath, itemsOut, Encoding.UTF8);
                TryWriteDefaultItemsJsonIfAbsent(steamSettingsPath, map, game.AppId);

                using (var noteWriter = new StreamWriter(notePath, false, Encoding.UTF8))
                {
                    noteWriter.WriteLine("Item Quantity Modification Instructions");
                    noteWriter.WriteLine("=====================================");
                    noteWriter.WriteLine($"To modify item definitions, edit {PathConstants.GoldbergItemsJsonFileName} in this folder.");
                    noteWriter.WriteLine($"Starting inventory quantities are in {PathConstants.GoldbergDefaultItemsJsonFileName} (instance slot -> definition + quantity).");
                    noteWriter.WriteLine("Each item definition has an ID and a name:");
                    noteWriter.WriteLine();
                    foreach (var prop in map.Properties())
                    {
                        var itemData = prop.Value as JsonObject;
                        string itemName = itemData?["name"]?.ToString() ?? "Unknown Item";
                        noteWriter.WriteLine("ID: " + prop.Name);
                        noteWriter.WriteLine("Name: " + itemName);
                        noteWriter.WriteLine();
                    }
                }

                if (showProgress && friendlyProgressMessages)
                {
                    ReportFinishedStatus($"Generating items {map.Count}/{map.Count}");
                }
                else if (showProgress)
                {
                    ReportFinishedStatus("Items generated successfully.");
                }

                Program.LogService?.LogMessage(string.Format("ItemGenerator: wrote {0} item(s) for {1} ({2})", map.Count, game.AppName, game.AppId));
                return ItemGeneratorResult.Ok(map.Count);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError("ItemGenerator failed", ex);
                if (showProgress && !friendlyProgressMessages)
                    ReportFinishedStatus("Item generation failed.", TaskReportKind.Error);
                return ItemGeneratorResult.Fail(ex.Message);
            }
        }

        private void ReportFinishedStatus(string message, TaskReportKind kind = TaskReportKind.Info)
        {
            _taskReportService?.SetMessageWithAutoClear(
                message,
                kind,
                AddGameStatusMessages.StatusAutoClearDelayMs);
        }

        private static bool TryBuildItemDefinitionMap(string archiveJson, out JsonObject map, out string errorDetail)
        {
            map = null;
            errorDetail = null;

            string trimmed = archiveJson.Trim();
            string preview = trimmed.Length <= 400 ? trimmed : trimmed.Substring(0, 400) + "â€¦";

            JsonValue root;
            try
            {
                root = JsonValue.Parse(trimmed);
            }
            catch (Exception)
            {
                errorDetail = "The archive response was not valid JSON. Preview: " + preview;
                return false;
            }

            var asObj = root as JsonObject;
            if (asObj != null && asObj.Count > 0)
            {
                bool allValuesAreObjects = true;
                foreach (var p in asObj.Properties())
                {
                    if (!(p.Value is JsonObject))
                    {
                        allValuesAreObjects = false;
                        break;
                    }
                }
                if (allValuesAreObjects)
                {
                    map = (JsonObject)asObj.DeepClone();
                    return true;
                }
            }

            JsonArray array = null;
            if (root is JsonArray ja)
            {
                array = ja;
            }
            else if (asObj != null)
            {
                foreach (string key in new[] { "itemdefs", "items", "result", "definitions", "item_definitions", "rgItemDefs" })
                {
                    if (asObj[key] is JsonArray inner)
                    {
                        array = inner;
                        break;
                    }
                }
                if (array == null)
                {
                    foreach (var p in asObj.Properties())
                    {
                        if (p.Value is JsonArray inner2)
                        {
                            array = inner2;
                            break;
                        }
                    }
                }
            }

            if (array == null)
            {
                errorDetail = "Could not find a JSON array of item definitions in the archive. Preview: " + preview;
                return false;
            }

            if (array.Count == 0)
            {
                errorDetail = "Item archive array was empty.";
                return false;
            }

            map = new JsonObject();
            for (int i = 0; i < array.Count; i++)
            {
                var item = array[i] as JsonObject;
                if (item == null)
                    continue;

                string itemId = item["itemdefid"]?.ToString();
                if (string.IsNullOrEmpty(itemId))
                    itemId = "item_" + i;

                map[itemId] = item;
            }

            if (map.Count == 0)
            {
                errorDetail = "The archive array had no JSON objects with item definitions. Preview: " + preview;
                return false;
            }

            return true;
        }

        // Goldberg default_items.json: instance slot -> { definition: itemdefid, quantity }.
        // See https://github.com/Detanup01/gbe_fork/blob/dev/post_build/steam_settings.EXAMPLE/default_items.EXAMPLE.json
        public static JsonObject BuildDefaultItemsMap(JsonObject itemsByDefinition)
        {
            var result = new JsonObject();
            if (itemsByDefinition == null || itemsByDefinition.Count == 0)
                return result;

            var definitionIds = new List<int>();
            foreach (var prop in itemsByDefinition.Properties())
            {
                if (int.TryParse(prop.Name, out int defId))
                    definitionIds.Add(defId);
            }
            definitionIds.Sort();

            int instanceId = 1;
            foreach (int defId in definitionIds)
            {
                var itemDef = itemsByDefinition[defId.ToString()] as JsonObject;
                var entry = new JsonObject();
                entry["definition"] = new JsonNumber(defId);
                entry["quantity"] = new JsonNumber(ResolveDefaultQuantity(itemDef));
                result[instanceId.ToString()] = entry;
                instanceId++;
            }

            return result;
        }

        public static bool TryWriteDefaultItemsJsonIfAbsent(string steamSettingsPath, JsonObject itemsByDefinition, ulong appId)
        {
            if (string.IsNullOrEmpty(steamSettingsPath) || itemsByDefinition == null || itemsByDefinition.Count == 0)
                return false;

            string path = Path.Combine(steamSettingsPath, PathConstants.GoldbergDefaultItemsJsonFileName);
            if (File.Exists(path))
                return false;

            JsonObject defaultMap = BuildDefaultItemsMap(itemsByDefinition);
            if (defaultMap.Count == 0)
                return false;

            File.WriteAllText(path, defaultMap.ToJsonString(JsonFormatting.Indented), Encoding.UTF8);
            ServiceLocator.LogService.LogMessage(
                $"Generated {PathConstants.GoldbergDefaultItemsJsonFileName} with {defaultMap.Count} starting item(s) for app {appId}");
            return true;
        }

        private static int ResolveDefaultQuantity(JsonObject itemDef)
        {
            if (itemDef == null)
                return 1;

            JsonValue countNode = itemDef["count"];
            if (countNode != null && int.TryParse(countNode.ToString(), out int quantity) && quantity > 0)
                return quantity;

            return 1;
        }
    }
}
