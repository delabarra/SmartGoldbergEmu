using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.JsonKit;
using SmartGoldbergEmu.Services;

namespace SteamKit
{
    // Steam-ranked TCP CM list (GetCMListForConnect netfilter). Hardcoded IPs are last-resort only.
    internal static class SteamCmDirectory
    {
        internal static readonly string[] FallbackTcpEndpoints =
        {
            "185.25.182.52:27017",
            "155.133.226.74:27017",
            "162.254.192.101:27017",
            "162.254.195.71:27017",
        };

        private const int DirectoryHttpTimeoutSeconds = 8;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(45);
        private static readonly object Sync = new object();
        private static string _lastSuccessfulEndpoint;
        private static List<string> _cachedRankedEndpoints;
        private static DateTime _cacheUtc = DateTime.MinValue;

        internal static string LastSuccessfulEndpoint
        {
            get
            {
                lock (Sync)
                    return _lastSuccessfulEndpoint;
            }
        }

        internal static void RememberSuccess(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
                return;

            lock (Sync)
                _lastSuccessfulEndpoint = endpoint.Trim();
        }

        internal static bool TryGetFreshRankedCache(out List<string> ranked)
        {
            lock (Sync)
            {
                if (_cachedRankedEndpoints != null
                    && _cachedRankedEndpoints.Count > 0
                    && (DateTime.UtcNow - _cacheUtc) < CacheTtl)
                {
                    ranked = new List<string>(_cachedRankedEndpoints);
                    return true;
                }
            }

            ranked = null;
            return false;
        }

        internal static async Task<List<string>> RefreshRankedAsync(CancellationToken ct)
        {
            List<string> discovered = await FetchRankedTcpEndpointsAsync(ct).ConfigureAwait(false);
            if (discovered == null || discovered.Count == 0)
                return discovered ?? new List<string>();

            lock (Sync)
            {
                _cachedRankedEndpoints = new List<string>(discovered);
                _cacheUtc = DateTime.UtcNow;
            }

            return discovered;
        }

        internal static bool TryParseTcpEndpoint(string endpoint, out string host, out int port)
        {
            host = null;
            port = 0;
            if (string.IsNullOrWhiteSpace(endpoint))
                return false;

            endpoint = endpoint.Trim();
            int colon = endpoint.LastIndexOf(':');
            if (colon <= 0 || colon >= endpoint.Length - 1)
                return false;

            host = endpoint.Substring(0, colon);
            if (string.IsNullOrWhiteSpace(host) || host.IndexOfAny(new[] { ' ', '"', '/' }) >= 0)
                return false;

            if (!int.TryParse(endpoint.Substring(colon + 1), out port) || port <= 0 || port > 65535)
                return false;

            // TLS WebSocket CMs; this client speaks VT01 TCP only.
            return port != 443;
        }

        internal static bool IsUsableTcpCmEndpoint(string endpoint)
        {
            string host;
            int port;
            return TryParseTcpEndpoint(endpoint, out host, out port);
        }

        // GetCMListForConnect: objects, netfilter TCP only, Steam's order. GetCMList: "ip:port" strings.
        internal static List<string> ParseServerListFromDirectoryResponse(string response)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(response))
                return result;

            try
            {
                JsonValue root = JsonValue.Parse(response);
                JsonObject envelope = root as JsonObject;
                JsonObject payload = envelope != null
                    ? (envelope["response"] as JsonObject ?? envelope)
                    : null;
                JsonArray serverlist = payload != null ? payload["serverlist"] as JsonArray : null;
                if (serverlist == null)
                    return result;

                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (JsonValue item in serverlist)
                {
                    foreach (string endpoint in EnumerateTcpCmEndpoints(item))
                    {
                        if (seen.Add(endpoint))
                            result.Add(endpoint);
                    }
                }

                return result;
            }
            catch
            {
                return result;
            }
        }

        private static async Task<List<string>> FetchRankedTcpEndpointsAsync(CancellationToken ct)
        {
            using (var http = HttpServiceFactory.Create(TimeSpan.FromSeconds(DirectoryHttpTimeoutSeconds)))
            {
                List<string> ranked = await TryFetchParsedCmListAsync(
                    http, ApplicationConstants.SteamDirectoryGetCmListForConnectUrl, ct).ConfigureAwait(false);
                if (ranked.Count > 0)
                    return ranked;

                return await TryFetchParsedCmListAsync(
                    http, ApplicationConstants.SteamDirectoryGetCmListUrl, ct).ConfigureAwait(false);
            }
        }

        private static async Task<List<string>> TryFetchParsedCmListAsync(IHttpService http, string url, CancellationToken ct)
        {
            try
            {
                using (var response = await http.GetAsync(url, ct).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (ct.IsCancellationRequested)
                        ct.ThrowIfCancellationRequested();
                    return ParseServerListFromDirectoryResponse(body);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return new List<string>();
            }
        }

        private static IEnumerable<string> EnumerateTcpCmEndpoints(JsonValue item)
        {
            if (item == null)
                yield break;

            if (item.Kind == JsonValueKind.String)
            {
                string endpoint = item.ToString();
                if (IsUsableTcpCmEndpoint(endpoint))
                    yield return endpoint.Trim();
                yield break;
            }

            JsonObject obj = item as JsonObject;
            if (obj == null)
                yield break;

            string type = ReadJsonString(obj, "type");
            if (!string.IsNullOrEmpty(type)
                && !string.Equals(type, "netfilter", StringComparison.OrdinalIgnoreCase))
                yield break;

            string legacy = ReadJsonString(obj, "legacy_endpoint");
            string endpointValue = ReadJsonString(obj, "endpoint");
            if (IsUsableTcpCmEndpoint(legacy))
                yield return legacy.Trim();
            if (IsUsableTcpCmEndpoint(endpointValue)
                && !string.Equals(endpointValue.Trim(), (legacy ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))
                yield return endpointValue.Trim();
        }

        private static string ReadJsonString(JsonObject obj, string name)
        {
            if (obj == null)
                return null;
            JsonValue value = obj[name];
            if (value == null || value.Kind == JsonValueKind.Null)
                return null;
            return value.ToString();
        }
    }
}
