using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AppDataKit
{
    internal static class AppInfoParser
    {
        public static IReadOnlyList<uint> ParseDlcAppIds(AppInfoKeyValue appInfo)
        {
            if (appInfo == null)
                return Array.Empty<uint>();

            AppInfoKeyValue root = appInfo.GetChild("appinfo") ?? appInfo;
            AppInfoKeyValue extended = root.GetChild("extended");
            if (extended == null)
                return Array.Empty<uint>();

            string raw = extended.GetChild("listofdlc")?.Value;
            return ParseDlcIdList(raw);
        }

        public static List<uint> ParseDlcIdList(string raw)
        {
            var result = new List<uint>();
            if (string.IsNullOrWhiteSpace(raw))
                return result;

            foreach (string part in raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (uint.TryParse(part.Trim(), out uint dlcId) && dlcId > 0)
                    result.Add(dlcId);
            }

            return result;
        }
    }

    internal static class DlcResolver
    {
        public static async Task<DlcSection> ResolveAsync(
            IReadOnlyList<uint> dlcAppIds,
            AppSnapshotOptions options,
            CancellationToken cancellationToken)
        {
            var section = new DlcSection
            {
                Source = "steamcmd.net",
            };

            if (dlcAppIds == null || dlcAppIds.Count == 0)
            {
                section.Status = SnapshotSectionStatus.Ok;
                section.Items = Array.Empty<DlcEntry>();
                section.UnresolvedAppIds = Array.Empty<uint>();
                return section;
            }

            int concurrency = options?.DlcBatchConcurrency ?? 16;
            if (concurrency < 1)
                concurrency = 1;

            TimeSpan timeout = options?.HttpTimeout ?? TimeSpan.FromSeconds(30);
            var entries = new List<DlcEntry>(dlcAppIds.Count);
            var unresolved = new List<uint>();

            using (var http = new HttpClient { Timeout = timeout })
            using (var gate = new SemaphoreSlim(concurrency, concurrency))
            {
                var tasks = new Task[dlcAppIds.Count];
                for (int i = 0; i < dlcAppIds.Count; i++)
                {
                    uint dlcId = dlcAppIds[i];
                    tasks[i] = ResolveOneAsync(dlcId, options, http, gate, entries, unresolved, cancellationToken);
                }

                await Task.WhenAll(tasks).ConfigureAwait(false);
            }

            entries.Sort((a, b) => a.AppId.CompareTo(b.AppId));
            unresolved.Sort();

            section.Items = entries;
            section.UnresolvedAppIds = unresolved;
            section.Status = unresolved.Count == 0
                ? SnapshotSectionStatus.Ok
                : entries.Count > 0 ? SnapshotSectionStatus.Partial : SnapshotSectionStatus.Error;
            if (section.Status == SnapshotSectionStatus.Error && entries.Count == 0)
                section.Error = "No DLC names could be resolved.";

            return section;
        }

        private static async Task ResolveOneAsync(
            uint dlcId,
            AppSnapshotOptions options,
            HttpClient http,
            SemaphoreSlim gate,
            List<DlcEntry> entries,
            List<uint> unresolved,
            CancellationToken cancellationToken)
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // steamcmd appinfo (same source as parent metadata) — Store often fails for DLC packages.
                if (TryNameFromSteamCmd(await AppInfoClient.FetchFromSteamCmdAsync(
                        dlcId, options, http, cancellationToken).ConfigureAwait(false), out string steamCmdName, out string steamCmdType))
                {
                    AddEntry(entries, dlcId, steamCmdName, steamCmdType);
                    return;
                }

                StoreAppDetailsClient.BasicInfo store = await StoreAppDetailsClient.TryGetBasicAsync(
                    dlcId,
                    http,
                    cancellationToken).ConfigureAwait(false);

                if (store.Success)
                {
                    AddEntry(entries, dlcId, store.Name, store.Type);
                    return;
                }

                lock (unresolved)
                    unresolved.Add(dlcId);
            }
            finally
            {
                gate.Release();
            }
        }

        private static bool TryNameFromSteamCmd(AppInfoFetchResult fetch, out string name, out string type)
        {
            name = null;
            type = null;
            if (fetch == null || !fetch.Success || fetch.AppInfo == null)
                return false;

            AppInfoKeyValue root = fetch.AppInfo.GetChild("appinfo") ?? fetch.AppInfo;
            AppInfoKeyValue common = root?.GetChild("common");
            if (common == null)
                return false;

            string n = common.GetChild("name")?.Value;
            if (string.IsNullOrWhiteSpace(n))
                return false;

            name = n.Trim();
            type = common.GetChild("type")?.Value;
            if (string.IsNullOrWhiteSpace(type))
                type = "dlc";
            else
                type = type.Trim();
            return true;
        }

        private static void AddEntry(List<DlcEntry> entries, uint dlcId, string name, string type)
        {
            if (string.IsNullOrWhiteSpace(name))
                name = "DLC " + dlcId;

            if (string.IsNullOrWhiteSpace(type))
                type = "dlc";

            var entry = new DlcEntry
            {
                AppId = dlcId,
                Name = name,
                Type = type,
            };

            lock (entries)
                entries.Add(entry);
        }
    }
}
