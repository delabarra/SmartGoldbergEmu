using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AppDataKit
{
    public static class AppInfoClient
    {
        public static async Task<AppInfoFetchResult> FetchAsync(
            uint appId,
            AppSnapshotOptions options,
            CancellationToken cancellationToken)
        {
            return await FetchFromSteamCmdAsync(appId, options, cancellationToken).ConfigureAwait(false);
        }

        public static Task<AppInfoFetchResult> FetchFromSteamCmdAsync(
            uint appId,
            AppSnapshotOptions options,
            CancellationToken cancellationToken)
        {
            return FetchFromSteamCmdAsync(appId, options, null, cancellationToken);
        }

        public static async Task<AppInfoFetchResult> FetchFromSteamCmdAsync(
            uint appId,
            AppSnapshotOptions options,
            HttpClient http,
            CancellationToken cancellationToken)
        {
            string baseUrl = options?.SteamCmdInfoUrl ?? AppSnapshotOptions.DefaultSteamCmdInfoUrl;
            if (!baseUrl.EndsWith("/", StringComparison.Ordinal))
                baseUrl += "/";

            string url = baseUrl + appId;
            TimeSpan timeout = options?.HttpTimeout ?? TimeSpan.FromSeconds(30);

            try
            {
                if (http == null)
                {
                    using (var localHttp = new HttpClient { Timeout = timeout })
                        return await FetchFromSteamCmdCoreAsync(localHttp, url, appId, cancellationToken).ConfigureAwait(false);
                }

                return await FetchFromSteamCmdCoreAsync(http, url, appId, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                return new AppInfoFetchResult
                {
                    Success = false,
                    Error = ex.Message,
                };
            }
        }

        private static async Task<AppInfoFetchResult> FetchFromSteamCmdCoreAsync(
            HttpClient http,
            string url,
            uint appId,
            CancellationToken cancellationToken)
        {
            using (HttpResponseMessage response = await http.GetAsync(url, cancellationToken).ConfigureAwait(false))
            {
                if (!response.IsSuccessStatusCode)
                {
                    return new AppInfoFetchResult
                    {
                        Success = false,
                        Error = "steamcmd.net HTTP " + (int)response.StatusCode + ".",
                    };
                }

                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return ParseSteamCmdResponse(json, appId);
            }
        }

        public static AppInfoFetchResult ParseSteamCmdResponse(string json, uint appId)
        {
            if (string.IsNullOrEmpty(json))
            {
                return new AppInfoFetchResult
                {
                    Success = false,
                    Error = "Empty steamcmd.net response.",
                };
            }

            try
            {
                Dictionary<string, object> root = JsonUtil.AsObject(JsonUtil.Parse(json));
                if (root == null)
                {
                    return new AppInfoFetchResult
                    {
                        Success = false,
                        Error = "Invalid steamcmd.net JSON.",
                    };
                }

                if (root.TryGetValue("status", out object statusObj))
                {
                    string status = statusObj as string;
                    if (!string.Equals(status, "success", StringComparison.OrdinalIgnoreCase))
                    {
                        return new AppInfoFetchResult
                        {
                            Success = false,
                            Error = "steamcmd.net status: " + (status ?? "unknown") + ".",
                        };
                    }
                }

                Dictionary<string, object> data = root.TryGetValue("data", out object dataObj)
                    ? dataObj as Dictionary<string, object>
                    : null;
                Dictionary<string, object> app = data != null && data.TryGetValue(appId.ToString(), out object appObj)
                    ? appObj as Dictionary<string, object>
                    : null;
                if (app == null || app.Count == 0)
                {
                    return new AppInfoFetchResult
                    {
                        Success = false,
                        Error = "App " + appId + " not found in steamcmd.net response.",
                    };
                }

                AppInfoKeyValue kv = AppInfoJsonConverter.ObjectToRootKeyValue(app);
                if (kv == null)
                {
                    return new AppInfoFetchResult
                    {
                        Success = false,
                        Error = "Unable to convert steamcmd.net appinfo.",
                    };
                }

                return new AppInfoFetchResult
                {
                    Success = true,
                    Source = AppInfoSource.SteamCmd,
                    AppInfo = kv,
                };
            }
            catch (Exception ex)
            {
                return new AppInfoFetchResult
                {
                    Success = false,
                    Error = ex.Message,
                };
            }
        }
    }
}
