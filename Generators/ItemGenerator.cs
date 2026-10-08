using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AppDataKit;
using SmartGoldbergEmu.JsonKit;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;

namespace SmartGoldbergEmu.Generators
{
    // Generates Goldberg steam_settings/items.json, default_items.json, and items_note.txt via AppDataKit.
    public sealed class ItemGenerator
    {
        private readonly ITaskReportService _taskReportService;

        public ItemGenerator(ITaskReportService taskReportService = null)
        {
            _taskReportService = taskReportService;
        }

        // prefetchedItems: section already fetched for this app (add collect / catalog refresh); null fetches live.
        public async Task<ItemGeneratorResult> GenerateAndSaveAsync(
            GameConfig game,
            ItemsSection prefetchedItems = null,
            bool showProgress = true,
            bool friendlyProgressMessages = false,
            CancellationToken cancellationToken = default)
        {
            if (game == null || game.AppId == 0)
                return ItemGeneratorResult.Fail("Invalid game or App ID.");

            var emulatorConfig = ServiceLocator.EmulatorConfigService;
            if (emulatorConfig == null)
                return ItemGeneratorResult.Fail("Emulator configuration service is not available.");

            string steamSettingsPath = emulatorConfig.GetGameSteamSettingsPath(game.AppId);
            if (string.IsNullOrEmpty(steamSettingsPath))
                return ItemGeneratorResult.Fail($"Could not resolve {PathConstants.SteamSettingsFolderName} path for this game.");

            string appIdStr = game.AppId.ToString();

            try
            {
                ItemsSection section = prefetchedItems;
                if (section == null)
                {
                    if (showProgress)
                    {
                        _taskReportService?.SetMessage("Fetching item definitions…");
                        if (!friendlyProgressMessages)
                            _taskReportService?.SetProgress(0, 100);
                    }

                    section = await ServiceLocator.AppDataKitBridgeService
                        .FetchItemsAsync(game.AppId, cancellationToken)
                        .ConfigureAwait(false);
                }
                cancellationToken.ThrowIfCancellationRequested();

                if (section == null || string.IsNullOrWhiteSpace(section.ArchiveJson))
                {
                    string failMessage = string.IsNullOrWhiteSpace(section?.Error)
                        ? "No item definitions available for this app."
                        : section.Error;
                    if (showProgress)
                    {
                        ReportFinishedStatus(
                            failMessage,
                            section != null && section.Status == SnapshotSectionStatus.Error
                                ? TaskReportKind.Error
                                : TaskReportKind.Info);
                    }
                    return ItemGeneratorResult.Fail(failMessage);
                }

                string archiveJson = section.ArchiveJson;
                JsonObject map;
                string parseDetail;
                if (!TryBuildItemDefinitionMap(archiveJson, out map, out parseDetail))
                {
                    Program.LogService?.LogWarning("ItemGenerator: could not parse item archive for app " + appIdStr + ". " + parseDetail);
                    if (showProgress)
                        ReportFinishedStatus("Could not parse item definitions.", TaskReportKind.Warning);
                    return ItemGeneratorResult.Fail(parseDetail);
                }

                if (map.Count == 0)
                {
                    if (showProgress)
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
                    _taskReportService?.SetMessage(string.Format("Writing {0} item definition(s)…", map.Count));
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

                Program.LogService?.LogDebug(string.Format("ItemGenerator: wrote {0} item(s) for {1} ({2})", map.Count, game.AppName, game.AppId));
                return ItemGeneratorResult.Ok(map.Count, section);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError("ItemGenerator failed", ex);
                if (showProgress)
                    ReportFinishedStatus("Item generation failed.", TaskReportKind.Error);
                return ItemGeneratorResult.Fail(ex.Message);
            }
        }

        private void ReportFinishedStatus(string message, TaskReportKind kind = TaskReportKind.Info)
        {
            _taskReportService?.SetMessageWithAutoClear(message, kind);
        }

        internal static bool TryBuildItemDefinitionMap(string archiveJson, out JsonObject map, out string errorDetail)
        {
            map = null;
            errorDetail = null;

            string trimmed = archiveJson.Trim();
            string preview = trimmed.Length <= 400 ? trimmed : trimmed.Substring(0, 400) + "…";

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
                    map = new JsonObject();
                    foreach (var p in asObj.Properties())
                        AddGoldbergItemDefinition(map, p.Name, (JsonObject)p.Value);
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

                AddGoldbergItemDefinition(map, item["itemdefid"]?.ToString(), item);
            }

            if (map.Count == 0)
            {
                errorDetail = "The archive array had no item definitions with a numeric itemdefid. Preview: " + preview;
                return false;
            }

            return true;
        }

        // gbe_fork calls std::stoi on every items.json key without try/catch, so non-numeric ids must not be written.
        private static void AddGoldbergItemDefinition(JsonObject map, string itemDefId, JsonObject item)
        {
            if (!int.TryParse(itemDefId, NumberStyles.None, CultureInfo.InvariantCulture, out _))
                return;

            map[itemDefId] = ToGoldbergItemDefinition(item);
        }

        // gbe_fork GetItemDefinitionProperty reads attributes with get<std::string>() and returns empty for numbers/booleans.
        private static JsonObject ToGoldbergItemDefinition(JsonObject item)
        {
            var result = new JsonObject();
            foreach (var prop in item.Properties())
            {
                JsonValue value = prop.Value;
                switch (value.Kind)
                {
                    case JsonValueKind.Integer:
                    case JsonValueKind.Float:
                        result[prop.Name] = new JsonString(value.ToString());
                        break;
                    case JsonValueKind.Boolean:
                        result[prop.Name] = new JsonString(((JsonBool)value).Value ? "true" : "false");
                        break;
                    default:
                        result[prop.Name] = value.DeepClone();
                        break;
                }
            }
            return result;
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
            ServiceLocator.LogService.LogDebug(
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
