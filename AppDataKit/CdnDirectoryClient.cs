using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AppDataKit
{
    /// <summary>Anonymous Steam WebAPI access to the content-server directory.</summary>
    public static class CdnDirectoryClient
    {
        private const string GetServersUrl =
            "https://api.steampowered.com/IContentServerDirectoryService/GetServersForSteamPipe/v1/?format=json";

        public static async Task<IReadOnlyList<ContentServer>> GetContentServersAsync(
            uint cellId = 0,
            int maxServers = 20,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            string url = GetServersUrl + "&cellid=" + cellId + "&max_servers=" + maxServers;

            using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(12) })
            using (var response = await http.GetAsync(url, cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return ParseServers(body);
            }
        }

        private static List<ContentServer> ParseServers(string json)
        {
            var servers = new List<ContentServer>();
            if (string.IsNullOrEmpty(json))
                return servers;

            var objectMatches = Regex.Matches(json, "\\{(?<obj>[^\\{\\}]*?)\\}", RegexOptions.Singleline);
            foreach (Match match in objectMatches)
            {
                string obj = match.Groups["obj"].Value;
                if (obj.IndexOf("\"host\"", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                var server = new ContentServer
                {
                    Type = ReadJsonString(obj, "type"),
                    Host = ReadJsonString(obj, "host"),
                    VHost = ReadJsonString(obj, "vhost"),
                    ProxyRequestPathTemplate = ReadJsonString(obj, "proxy_request_path_template"),
                };

                if (string.IsNullOrEmpty(server.VHost))
                    server.VHost = server.Host;

                server.CellId = ReadJsonInt(obj, "cell_id");
                server.Load = ReadJsonInt(obj, "load");
                server.WeightedLoad = ReadJsonFloat(obj, "weighted_load");
                server.UseAsProxy = ReadJsonBool(obj, "use_as_proxy");
                server.Port = ReadJsonInt(obj, "port");
                if (server.Port <= 0)
                    server.Port = 443;

                string httpsSupport = ReadJsonString(obj, "https_support");
                server.UseHttps = !string.Equals(httpsSupport, "none", StringComparison.OrdinalIgnoreCase);

                if (!string.IsNullOrEmpty(server.Host))
                    servers.Add(server);
            }

            return servers;
        }

        private static string ReadJsonString(string obj, string key)
        {
            var match = Regex.Match(obj, "\"" + Regex.Escape(key) + "\"\\s*:\\s*\"(?<value>(?:\\\\.|[^\"\\\\])*)\"");
            if (!match.Success)
                return string.Empty;
            return Regex.Unescape(match.Groups["value"].Value);
        }

        private static int ReadJsonInt(string obj, string key)
        {
            var match = Regex.Match(obj, "\"" + Regex.Escape(key) + "\"\\s*:\\s*(?<value>-?\\d+)");
            if (!match.Success)
                return 0;
            return int.TryParse(match.Groups["value"].Value, out int value) ? value : 0;
        }

        private static float ReadJsonFloat(string obj, string key)
        {
            var match = Regex.Match(obj, "\"" + Regex.Escape(key) + "\"\\s*:\\s*(?<value>-?\\d+(?:\\.\\d+)?)");
            if (!match.Success)
                return 0f;
            return float.TryParse(match.Groups["value"].Value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float value) ? value : 0f;
        }

        private static bool ReadJsonBool(string obj, string key)
        {
            var match = Regex.Match(obj, "\"" + Regex.Escape(key) + "\"\\s*:\\s*(?<value>true|false)", RegexOptions.IgnoreCase);
            if (!match.Success)
                return false;
            return string.Equals(match.Groups["value"].Value, "true", StringComparison.OrdinalIgnoreCase);
        }
    }
}
