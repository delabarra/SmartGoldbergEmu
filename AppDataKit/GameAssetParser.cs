using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AppDataKit
{
    internal static class GameAssetParser
    {
        private static readonly Regex AssetLikeValueRegex = new Regex(
            "(https?://|^|/|\\\\).+\\.(png|jpg|jpeg|gif|webp|ico|tga|icns|bmp)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly (string KeyPath, string[] Extensions)[] HashAssetDefinitions =
        {
            ("common/icon", new[] { "jpg" }),
            ("common/logo", new[] { "jpg" }),
            ("common/logo_small", new[] { "jpg" }),
            ("common/clienticon", new[] { "ico" }),
            ("common/clienticns", new[] { "icns" }),
            ("common/clienttga", new[] { "tga" }),
            ("common/linuxclienticon", new[] { "png", "jpg", "ico" }),
        };

        public static async Task<GameAssetsSection> BuildAsync(
            AppInfoKeyValue appInfo,
            uint appId,
            AppSnapshotOptions options,
            CancellationToken cancellationToken)
        {
            var section = new GameAssetsSection
            {
                Source = "appinfo",
            };

            if (appInfo == null)
            {
                section.Status = SnapshotSectionStatus.Unavailable;
                section.Error = "Appinfo is not available.";
                return section;
            }

            var output = new List<GameAssetEntry>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectAssets(appInfo, string.Empty, output, seen, appId);
            await CollectHashAssetsAsync(appInfo, output, seen, appId, options, cancellationToken).ConfigureAwait(false);

            section.Items = output;
            section.Status = output.Count > 0 ? SnapshotSectionStatus.Ok : SnapshotSectionStatus.Unavailable;
            if (section.Status == SnapshotSectionStatus.Unavailable)
                section.Error = "No game assets found in appinfo.";

            return section;
        }

        private static bool IsAssetConfigPath(string keyPath) =>
            keyPath.StartsWith("ufs/", StringComparison.OrdinalIgnoreCase)
            || keyPath.IndexOf("/ufs/", StringComparison.OrdinalIgnoreCase) >= 0;

        private static bool IsAssetLikeValue(string value) =>
            !string.IsNullOrWhiteSpace(value)
            && value.IndexOfAny(new[] { '*', '?' }) < 0
            && AssetLikeValueRegex.IsMatch(value);

        private static void CollectAssets(AppInfoKeyValue node, string path, ICollection<GameAssetEntry> output, ISet<string> seen, uint appId)
        {
            string currentPath = string.IsNullOrWhiteSpace(path)
                ? node.Name
                : path + "/" + node.Name;

            if (!IsAssetConfigPath(currentPath) && IsAssetLikeValue(node.Value))
                AddAsset(output, seen, "appinfo/" + currentPath, node.Value, ToSteamCdnUrls(node.Value, appId));

            foreach (AppInfoKeyValue child in node.Children)
                CollectAssets(child, currentPath, output, seen, appId);
        }

        private static async Task CollectHashAssetsAsync(
            AppInfoKeyValue appInfo,
            ICollection<GameAssetEntry> output,
            ISet<string> seen,
            uint appId,
            AppSnapshotOptions options,
            CancellationToken cancellationToken)
        {
            AppInfoKeyValue root = appInfo.GetChild("appinfo") ?? appInfo;

            foreach ((string keyPath, string[] extensions) in HashAssetDefinitions)
            {
                if (!TryGetString(root, keyPath, out string value) || string.IsNullOrWhiteSpace(value))
                    continue;

                IReadOnlyList<string> candidateUrls = ToSteamCommunityUrls(appId, value, extensions);
                IReadOnlyList<string> resolvedUrls = options != null && options.ProbeAssetUrls
                    ? await ResolveAccessibleUrlsAsync(candidateUrls, options.HttpTimeout, cancellationToken).ConfigureAwait(false)
                    : candidateUrls;

                if (resolvedUrls.Count == 0)
                    continue;

                AddAsset(output, seen, "appinfo/" + keyPath, value, resolvedUrls);
            }
        }

        private static void AddAsset(
            ICollection<GameAssetEntry> output,
            ISet<string> seen,
            string keyPath,
            string value,
            IReadOnlyList<string> candidateUrls)
        {
            string dedupeKey = keyPath + "|" + value;
            if (!seen.Add(dedupeKey))
                return;

            output.Add(new GameAssetEntry
            {
                KeyPath = keyPath,
                Value = value,
                Url = candidateUrls.Count > 0 ? candidateUrls[0] : null,
                CandidateUrls = candidateUrls,
            });
        }

        private static IReadOnlyList<string> ToSteamCdnUrls(string value, uint appId)
        {
            if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { value };
            }

            string sanitized = value.Replace('\\', '/').TrimStart('/');
            if (string.IsNullOrWhiteSpace(sanitized))
                return Array.Empty<string>();

            if (sanitized.StartsWith("store_item_assets/", StringComparison.OrdinalIgnoreCase))
            {
                return new[]
                {
                    "https://shared.fastly.steamstatic.com/" + sanitized,
                    "https://shared.cloudflare.steamstatic.com/" + sanitized,
                    "https://steamcdn-a.akamaihd.net/" + sanitized,
                };
            }

            if (sanitized.StartsWith("steam/apps/", StringComparison.OrdinalIgnoreCase))
            {
                return new[]
                {
                    "https://cdn.cloudflare.steamstatic.com/" + sanitized,
                    "https://steamcdn-a.akamaihd.net/" + sanitized,
                };
            }

            return new[]
            {
                "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/" + appId + "/" + sanitized,
                "https://shared.cloudflare.steamstatic.com/store_item_assets/steam/apps/" + appId + "/" + sanitized,
                "https://steamcdn-a.akamaihd.net/steam/apps/" + appId + "/" + sanitized,
            };
        }

        private static IReadOnlyList<string> ToSteamCommunityUrls(uint appId, string hashValue, IReadOnlyList<string> extensions)
        {
            if (string.IsNullOrWhiteSpace(hashValue) || extensions == null || extensions.Count == 0)
                return Array.Empty<string>();

            string sanitized = hashValue.Trim();
            var urls = new List<string>(extensions.Count * 2);

            foreach (string extension in extensions)
            {
                urls.Add("https://cdn.cloudflare.steamstatic.com/steamcommunity/public/images/apps/"
                    + appId + "/" + sanitized + "." + extension);
                urls.Add("https://steamcdn-a.akamaihd.net/steamcommunity/public/images/apps/"
                    + appId + "/" + sanitized + "." + extension);
            }

            return urls;
        }

        private static async Task<IReadOnlyList<string>> ResolveAccessibleUrlsAsync(
            IReadOnlyList<string> candidateUrls,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            if (candidateUrls == null || candidateUrls.Count == 0)
                return Array.Empty<string>();

            var resolved = new List<string>(candidateUrls.Count);
            using (var http = new HttpClient { Timeout = timeout })
            {
                foreach (string url in candidateUrls)
                {
                    try
                    {
                        using (var request = new HttpRequestMessage(HttpMethod.Head, url))
                        using (HttpResponseMessage response = await http.SendAsync(
                            request,
                            HttpCompletionOption.ResponseHeadersRead,
                            cancellationToken).ConfigureAwait(false))
                        {
                            if (response.IsSuccessStatusCode)
                                resolved.Add(url);
                        }
                    }
                    catch
                    {
                        // Ignore individual probe failures.
                    }
                }
            }

            return resolved;
        }

        private static bool TryGetString(AppInfoKeyValue root, string keyPath, out string value)
        {
            value = string.Empty;
            string[] segments = keyPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            AppInfoKeyValue current = root;

            foreach (string segment in segments)
            {
                current = current?.GetChild(segment);
                if (current == null)
                    return false;
            }

            if (string.IsNullOrWhiteSpace(current.Value))
                return false;

            value = current.Value;
            return true;
        }
    }
}
