using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.JsonKit
{
    public static class JsonConvert
    {
        public static string SerializeObject(object value, JsonFormatting formatting = JsonFormatting.None)
        {
            return JsonWriter.SerializeObject(value, formatting);
        }

        public static T DeserializeObject<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return default;

            if (typeof(T) == typeof(GlobalSettings))
                return (T)(object)DeserializeGlobalSettings(json);
            if (typeof(T) == typeof(Dictionary<string, string>))
                return (T)(object)DeserializeStringDictionary(json);
            if (typeof(T) == typeof(List<CAchievement>))
                return (T)(object)DeserializeAchievementList(json);
            if (typeof(T) == typeof(CSteamGameSchema))
                return (T)(object)DeserializeSteamGameSchema(json);

            var root = JsonValue.Parse(json);
            return (T)DeserializeReflection(root, typeof(T));
        }

        public static object DeserializeObject(string json)
        {
            return JsonValue.Parse(json);
        }

        public static GlobalSettings DeserializeGlobalSettings(string json)
        {
            var root = JsonValue.Parse(json) as JsonObject;
            if (root == null)
                return new GlobalSettings();

            var settings = new GlobalSettings();
            settings.AccountName = GetStringProperty(root, nameof(GlobalSettings.AccountName)) ?? settings.AccountName;
            settings.AccountSteamId = GetStringProperty(root, nameof(GlobalSettings.AccountSteamId)) ?? settings.AccountSteamId;
            settings.Language = GetStringProperty(root, nameof(GlobalSettings.Language)) ?? settings.Language;
            settings.SteamDeck = GetBoolProperty(root, nameof(GlobalSettings.SteamDeck));
            settings.EnableAccountAvatar = GetBoolProperty(root, nameof(GlobalSettings.EnableAccountAvatar));
            return settings;
        }

        public static Dictionary<string, string> DeserializeStringDictionary(string json)
        {
            var root = JsonValue.Parse(json) as JsonObject;
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            if (root == null)
                return result;

            foreach (var prop in root.Properties())
            {
                if (prop.Value == null || prop.Value.Kind == JsonValueKind.Null)
                    continue;
                result[prop.Name] = prop.Value.ToString();
            }
            return result;
        }

        public static List<CAchievement> DeserializeAchievementList(string json)
        {
            var root = JsonValue.Parse(json);
            var list = new List<CAchievement>();
            var array = root as JsonArray;
            if (array == null)
                return list;

            foreach (var token in array)
            {
                var obj = token as JsonObject;
                if (obj == null)
                    continue;
                list.Add(ReadAchievement(obj));
            }
            return list;
        }

        public static CSteamGameSchema DeserializeSteamGameSchema(string json)
        {
            var root = JsonValue.Parse(json) as JsonObject;
            if (root == null)
                return null;

            var gameToken = root["game"] as JsonObject;
            if (gameToken == null)
                return null;

            var statsToken = gameToken["availableGameStats"] as JsonObject;
            if (statsToken == null)
                return new CSteamGameSchema { game = new CGame { availableGameStats = null } };

            var achievementsArray = statsToken["achievements"] as JsonArray;
            var achievements = new List<CAchievement>();
            if (achievementsArray != null)
            {
                foreach (var item in achievementsArray)
                {
                    if (item is JsonObject achievementObj)
                        achievements.Add(ReadAchievement(achievementObj));
                }
            }

            return new CSteamGameSchema
            {
                game = new CGame
                {
                    availableGameStats = new CAvailableGameStats
                    {
                        achievements = achievements
                    }
                }
            };
        }

        internal static CAchievement ReadAchievement(JsonObject obj)
        {
            return new CAchievement
            {
                name = GetStringProperty(obj, "name"),
                displayName = GetStringProperty(obj, "displayName"),
                description = GetStringProperty(obj, "description"),
                hidden = GetIntProperty(obj, "hidden"),
                icon = GetStringProperty(obj, "icon"),
                icongray = GetStringProperty(obj, "icongray"),
                icon_gray = GetStringProperty(obj, "icon_gray")
            };
        }

        private static object DeserializeReflection(JsonValue root, Type type)
        {
            if (root == null || root.Kind == JsonValueKind.Null)
                return type.IsValueType ? Activator.CreateInstance(type) : null;

            if (type == typeof(string))
                return root.ToString();

            if (type == typeof(bool))
                return root.Kind == JsonValueKind.Boolean && ((JsonBool)root).Value;

            if (type == typeof(int))
                return root.TryGetInt64(out long i) ? (int)i : 0;

            if (type == typeof(long))
                return root.TryGetInt64(out long l) ? l : 0L;

            if (type == typeof(ulong))
                return root.TryGetInt64(out long ul) ? (ulong)ul : 0UL;

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                return ReadObjectList(root, type);

            if (type == typeof(List<int>))
                return ReadIntList(root);

            if (type == typeof(List<long>))
                return ReadLongList(root);

            if (type == typeof(JsonValue) || type == typeof(JsonObject) || type == typeof(JsonArray))
                return root;

            var obj = root as JsonObject;
            if (obj == null)
                return type.IsValueType ? Activator.CreateInstance(type) : null;

            object instance = Activator.CreateInstance(type);
            foreach (var prop in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!prop.CanWrite)
                    continue;

                JsonValue token = obj[prop.Name];
                if (token == null || token.Kind == JsonValueKind.Null)
                    continue;

                object value;
                if (typeof(JsonValue).IsAssignableFrom(prop.PropertyType))
                    value = token;
                else
                    value = DeserializeReflection(token, prop.PropertyType);

                if (value != null || !prop.PropertyType.IsValueType)
                    prop.SetValue(instance, value, null);
            }
            return instance;
        }

        private static object ConvertTokenToPropertyType(JsonValue token, Type propertyType)
        {
            Type targetType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

            if (targetType == typeof(string))
                return token.ToString();

            if (targetType == typeof(bool))
                return token.Kind == JsonValueKind.Boolean && ((JsonBool)token).Value;

            if (targetType == typeof(int))
                return token.TryGetInt64(out long i) ? (int)i : 0;

            if (targetType == typeof(long))
                return token.TryGetInt64(out long l) ? l : 0L;

            if (targetType == typeof(List<int>))
                return ReadIntList(token);

            if (targetType == typeof(List<long>))
                return ReadLongList(token);

            return null;
        }

        private static object ReadObjectList(JsonValue token, Type listType)
        {
            var array = token as JsonArray;
            if (array == null)
                return null;

            Type elementType = listType.GetGenericArguments()[0];
            var list = (IList)Activator.CreateInstance(listType);
            foreach (JsonValue item in array)
            {
                object element = DeserializeReflection(item, elementType);
                if (element != null || elementType.IsValueType)
                    list.Add(element);
            }

            return list;
        }

        private static List<int> ReadIntList(JsonValue token)
        {
            var list = new List<int>();
            if (token is JsonArray array)
            {
                foreach (var item in array)
                {
                    if (item.TryGetInt64(out long v))
                        list.Add((int)v);
                }
            }
            else if (token.TryGetInt64(out long single))
            {
                list.Add((int)single);
            }
            return list;
        }

        private static List<long> ReadLongList(JsonValue token)
        {
            var list = new List<long>();
            if (token is JsonArray array)
            {
                foreach (var item in array)
                {
                    if (item.TryGetInt64(out long v))
                        list.Add(v);
                }
            }
            else if (token.TryGetInt64(out long single))
            {
                list.Add(single);
            }
            return list;
        }

        private static string GetStringProperty(JsonObject obj, string name)
        {
            var token = obj[name];
            return token == null || token.Kind == JsonValueKind.Null ? null : token.ToString();
        }

        private static bool GetBoolProperty(JsonObject obj, string name)
        {
            var token = obj[name];
            return token != null && token.Kind == JsonValueKind.Boolean && ((JsonBool)token).Value;
        }

        private static int GetIntProperty(JsonObject obj, string name)
        {
            var token = obj[name];
            return token != null && token.TryGetInt64(out long v) ? (int)v : 0;
        }
    }
}
