using System;
using System.Collections.Generic;

namespace AppDataKit
{
    /// <summary>Parses depot/manifest metadata from PICS <see cref="AppInfoKeyValue"/> app info.</summary>
    public static class DepotParser
    {
        public static IReadOnlyList<DepotInfo> ParseDepots(AppInfoKeyValue appInfo, uint appId, string branch = "public", string osFilter = null)
        {
            var results = new List<DepotInfo>();
            if (appInfo == null)
                return results;

            AppInfoKeyValue root = appInfo.GetChild("appinfo") ?? appInfo;
            AppInfoKeyValue depots = root.GetChild("depots");
            if (depots == null)
                return results;

            string normalizedBranch = string.IsNullOrWhiteSpace(branch) ? "public" : branch.Trim();

            foreach (AppInfoKeyValue depotNode in depots.Children)
            {
                if (!uint.TryParse(depotNode.Name, out uint depotId) || depotId == 0)
                    continue;

                if (!string.IsNullOrEmpty(osFilter) && !MatchesOsList(depotNode.GetChild("config")?.GetChild("oslist")?.Value, osFilter))
                    continue;

                ulong manifestGid = ResolveManifestGid(depotNode, normalizedBranch);
                if (manifestGid == 0)
                    continue;

                var info = new DepotInfo
                {
                    DepotId = depotId,
                    AppId = appId,
                    Branch = normalizedBranch,
                    ManifestGid = manifestGid,
                    OsList = depotNode.GetChild("config")?.GetChild("oslist")?.Value ?? string.Empty,
                };

                if (uint.TryParse(depotNode.GetChild("depotfromapp")?.Value, out uint depotFromApp) && depotFromApp > 0)
                    info.DepotFromApp = depotFromApp;

                results.Add(info);
            }

            return results;
        }

        private static ulong ResolveManifestGid(AppInfoKeyValue depotNode, string branch)
        {
            AppInfoKeyValue manifests = depotNode.GetChild("manifests");
            if (manifests == null)
                return 0;

            ulong gid = ReadManifestGid(manifests.GetChild(branch));
            if (gid != 0)
                return gid;

            if (!string.Equals(branch, "public", StringComparison.OrdinalIgnoreCase))
                gid = ReadManifestGid(manifests.GetChild("public"));

            return gid;
        }

        private static ulong ReadManifestGid(AppInfoKeyValue manifestNode)
        {
            if (manifestNode == null)
                return 0;

            if (ulong.TryParse(manifestNode.Value, out ulong gid) && gid != 0)
                return gid;

            AppInfoKeyValue gidNode = manifestNode.GetChild("gid");
            if (gidNode != null && ulong.TryParse(gidNode.Value, out gid))
                return gid;

            return 0;
        }

        private static bool MatchesOsList(string osList, string osFilter)
        {
            if (string.IsNullOrWhiteSpace(osList))
                return true;
            if (string.IsNullOrWhiteSpace(osFilter))
                return true;

            foreach (string part in osList.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.Equals(part.Trim(), osFilter.Trim(), StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
