using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AppDataKit
{
    internal static class StoreAppDetailsClient
    {
        public const string SourceName = "store.steampowered.com";

        private const string BaseUrl = "https://store.steampowered.com/api/appdetails";

        internal readonly struct BasicInfo
        {
            public BasicInfo(bool success, string name, string type)
            {
                Success = success;
                Name = name ?? string.Empty;
                Type = type ?? string.Empty;
            }

            public bool Success { get; }
            public string Name { get; }
            public string Type { get; }
        }

        public static async Task<BasicInfo> TryGetBasicAsync(
            uint appId,
            HttpClient http,
            CancellationToken cancellationToken)
        {
            if (http == null)
                return new BasicInfo(false, null, null);

            string url = BaseUrl
                + "?appids=" + appId
                + "&filters=basic";

            try
            {
                using (HttpResponseMessage response = await http.GetAsync(url, cancellationToken).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                        return new BasicInfo(false, null, null);

                    string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return ParseBasic(json, appId);
                }
            }
            catch
            {
                return new BasicInfo(false, null, null);
            }
        }

        private static BasicInfo ParseBasic(string json, uint appId)
        {
            if (string.IsNullOrEmpty(json))
                return new BasicInfo(false, null, null);

            try
            {
                Dictionary<string, object> root = JsonUtil.AsObject(JsonUtil.Parse(json));
                if (root == null)
                    return new BasicInfo(false, null, null);

                Dictionary<string, object> app = root.TryGetValue(appId.ToString(), out object appObj)
                    ? appObj as Dictionary<string, object>
                    : null;
                if (app == null)
                    return new BasicInfo(false, null, null);

                if (app.TryGetValue("success", out object successObj) && successObj is bool ok && !ok)
                    return new BasicInfo(false, null, null);

                Dictionary<string, object> data = app.TryGetValue("data", out object dataObj)
                    ? dataObj as Dictionary<string, object>
                    : null;
                if (data == null)
                    return new BasicInfo(false, null, null);

                string name = ReadString(data, "name");
                if (string.IsNullOrWhiteSpace(name))
                    return new BasicInfo(false, null, null);

                string type = ReadString(data, "type");
                if (string.IsNullOrWhiteSpace(type))
                    type = "dlc";

                return new BasicInfo(true, name.Trim(), type.Trim());
            }
            catch
            {
                return new BasicInfo(false, null, null);
            }
        }

        private static string ReadString(Dictionary<string, object> parent, string key)
        {
            if (parent == null || !parent.TryGetValue(key, out object value) || value == null)
                return string.Empty;

            return value.ToString();
        }
    }
}
