using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AppDataKit;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;
using SteamKit;

namespace SmartGoldbergEmu.Services
{
    // AppDataKit-first catalog; SteamKit PICS recovery only when steamcmd is unusable (never VDF).
    public sealed class AppDataKitBridgeService
    {
        private static readonly TimeSpan PicsSessionEnsureTimeout = TimeSpan.FromSeconds(50);
        private static readonly TimeSpan PicsProductInfoTimeout = TimeSpan.FromSeconds(20);

        private readonly SteamApiKeyService _steamApiKeyService;
        private readonly SteamProductInfoService _steamProductInfo;

        public AppDataKitBridgeService()
            : this(ServiceLocator.SteamApiKeyService, ServiceLocator.SteamProductInfoService)
        {
        }

        public AppDataKitBridgeService(SteamApiKeyService steamApiKeyService, SteamProductInfoService steamProductInfo)
        {
            _steamApiKeyService = steamApiKeyService ?? throw new ArgumentNullException(nameof(steamApiKeyService));
            _steamProductInfo = steamProductInfo ?? throw new ArgumentNullException(nameof(steamProductInfo));
        }

        // Live full catalog (add-game collect + Goldberg refresh). Always network; SteamKit recovery only.
        public async Task<AppCatalogSnapshot> FetchFullSnapshotAsync(
            ulong appId,
            ITaskReportService feedback = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (appId == 0 || appId > uint.MaxValue)
            {
                return new AppCatalogSnapshot
                {
                    Failure = AppMetadataFetchFailure.Unavailable
                };
            }

            uint id = (uint)appId;
            AppDataKit.AppDataService kit = CreateKit(language: null, httpTimeout: TimeSpan.FromSeconds(120));

            try
            {
                AppDataSectionsResult sections = await kit.GetAllSectionsAsync(id, cancellationToken).ConfigureAwait(false);
                AppCatalogSnapshot snapshot = AppCatalogSnapshotStore.FinalizeSnapshot(
                    id,
                    sections.FetchedAtUtc,
                    sections.Metadata,
                    sections.Dlc,
                    sections.Assets,
                    sections.Achievements,
                    sections.Stats,
                    sections.Items,
                    fromAppDataKit: true,
                    AppMetadataFetchFailure.None);

                if (snapshot != null && snapshot.IsUsable)
                    return snapshot;

                Program.LogService?.LogWarning(
                    "AppDataKit full snapshot for app " + appId + " lacked a usable name; trying PICS recovery.");
            }
            catch (OperationCanceledException)
            {
                Program.LogService?.LogWarning("Full catalog fetch timed out for app " + appId + ".");
                feedback?.SetMessage(AddGameStatusMessages.MetadataFetchTimedOut, TaskReportKind.Error);
                return new AppCatalogSnapshot { AppId = id, Failure = AppMetadataFetchFailure.TimedOut };
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError("AppDataKit full snapshot error for app " + appId + ": " + ex.Message, ex);
            }

            try
            {
                return await FetchFullViaPicsAsync(appId, kit, feedback, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                feedback?.SetMessage(AddGameStatusMessages.MetadataFetchTimedOut, TaskReportKind.Error);
                return new AppCatalogSnapshot { AppId = id, Failure = AppMetadataFetchFailure.TimedOut };
            }
            catch (Exception picsEx)
            {
                Program.LogService?.LogError("Steam PICS full snapshot recovery failed for app " + appId + ": " + picsEx.Message, picsEx);
                feedback?.SetMessage(AddGameStatusMessages.MetadataFetchFailed, TaskReportKind.Error);
                return new AppCatalogSnapshot { AppId = id, Failure = AppMetadataFetchFailure.Unavailable };
            }
        }

        // Live metadata (+ optional DLC names). Always network for kit path; optional in-memory root only as last non-PICS retry before PICS.
        public async Task<AppCatalogSnapshot> FetchMetadataSnapshotAsync(
            ulong appId,
            AppInfoKeyValue existingAppInfo = null,
            ITaskReportService feedback = null,
            CancellationToken cancellationToken = default(CancellationToken),
            bool resolveDlcNames = true)
        {
            if (appId == 0 || appId > uint.MaxValue)
            {
                return new AppCatalogSnapshot
                {
                    Failure = AppMetadataFetchFailure.Unavailable,
                    Metadata = existingAppInfo != null ? new AppMetadataSection { AppInfo = existingAppInfo } : null
                };
            }

            uint id = (uint)appId;
            AppDataKit.AppDataService kit = CreateKit();

            try
            {
                AppMetadataSection metaSection = await kit.GetMetadataAsync(id, cancellationToken).ConfigureAwait(false);
                if (metaSection != null
                    && (metaSection.Status == SnapshotSectionStatus.Ok || metaSection.Status == SnapshotSectionStatus.Partial)
                    && metaSection.AppInfo != null)
                {
                    DlcSection dlcSection;
                    if (resolveDlcNames)
                    {
                        Dictionary<long, string> dlcMap = await MergeDlcNamesAsync(kit, metaSection.AppInfo, cancellationToken)
                            .ConfigureAwait(false);
                        dlcSection = DlcSectionFromDictionary(dlcMap);
                    }
                    else
                    {
                        dlcSection = DlcSectionFromDictionary(CollectDlcIdsOnly(metaSection.AppInfo));
                    }

                    AppCatalogSnapshot snapshot = AppCatalogSnapshotStore.FinalizeSnapshot(
                        id,
                        DateTime.UtcNow,
                        metaSection,
                        dlcSection,
                        new GameAssetsSection { Status = SnapshotSectionStatus.Unavailable },
                        new AchievementsSection { Status = SnapshotSectionStatus.Unavailable },
                        new StatsSection { Status = SnapshotSectionStatus.Unavailable },
                        new ItemsSection { Status = SnapshotSectionStatus.Unavailable },
                        fromAppDataKit: true,
                        AppMetadataFetchFailure.None);

                    if (snapshot != null && snapshot.IsUsable)
                        return snapshot;

                    Program.LogService?.LogWarning(
                        "AppDataKit metadata for app " + appId + " lacked a usable name; trying PICS.");
                }
                else
                {
                    string err = metaSection?.Error ?? "empty metadata";
                    Program.LogService?.LogWarning(
                        "AppDataKit metadata unavailable for app " + appId + ": " + err + "; trying PICS.");
                }
            }
            catch (OperationCanceledException)
            {
                Program.LogService?.LogWarning("App metadata timed out while fetching app " + appId + ".");
                feedback?.SetMessage(AddGameStatusMessages.MetadataFetchTimedOut, TaskReportKind.Error);
                return new AppCatalogSnapshot { AppId = id, Failure = AppMetadataFetchFailure.TimedOut };
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError("AppDataKit metadata error for app " + appId + ": " + ex.Message, ex);
            }

            // Session root only — never disk VDF. Used when a prior in-memory tree exists (e.g. same dialog).
            if (existingAppInfo != null)
            {
                OnlineAppData fromExisting = BuildMetadata(appId, existingAppInfo, "Cached app info");
                if (IsUsable(fromExisting))
                {
                    Dictionary<long, string> dlc = resolveDlcNames
                        ? await ResolveDlcNamesForRootAsync(kit, existingAppInfo, cancellationToken).ConfigureAwait(false)
                        : CollectDlcIdsOnly(existingAppInfo);
                    return SnapshotFromAppInfo(id, existingAppInfo, dlc, fromAppDataKit: false);
                }
            }

            try
            {
                return await FetchMetadataViaPicsAsync(appId, kit, feedback, cancellationToken, resolveDlcNames)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                feedback?.SetMessage(AddGameStatusMessages.MetadataFetchTimedOut, TaskReportKind.Error);
                return new AppCatalogSnapshot { AppId = id, Failure = AppMetadataFetchFailure.TimedOut };
            }
            catch (Exception picsEx)
            {
                Program.LogService?.LogError("Steam PICS fallback failed for app " + appId + ": " + picsEx.Message, picsEx);
                feedback?.SetMessage(AddGameStatusMessages.MetadataFetchFailed, TaskReportKind.Error);
                return new AppCatalogSnapshot { AppId = id, Failure = AppMetadataFetchFailure.Unavailable };
            }
        }

        // Always live DLC path (fresh metadata + name resolve). Do not reuse stale session appinfo.
        public async Task<Dictionary<long, string>> FetchDlcAsync(
            ulong appId,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (appId == 0 || appId > uint.MaxValue)
                return new Dictionary<long, string>();

            AppDataKit.AppDataService kit = CreateKit();
            DlcSection section = await kit.GetDlcListAsync((uint)appId, cancellationToken).ConfigureAwait(false);
            var result = new Dictionary<long, string>();
            ApplyResolvedDlcSection(section, result);

            if (result.Count == 0)
            {
                AppMetadataSection meta = await kit.GetMetadataAsync((uint)appId, cancellationToken).ConfigureAwait(false);
                if (meta?.AppInfo != null)
                {
                    result = CollectDlcIdsOnly(meta.AppInfo);
                    ApplyResolvedDlcSection(section, result);
                    await FillUnresolvedViaPicsAsync(section, result, cancellationToken).ConfigureAwait(false);
                    return result;
                }
            }

            await FillUnresolvedViaPicsAsync(section, result, cancellationToken).ConfigureAwait(false);
            return result;
        }

        public async Task<AchievementsSection> FetchAchievementsAsync(
            ulong appId,
            string language = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (appId == 0 || appId > uint.MaxValue)
            {
                return new AchievementsSection
                {
                    Status = SnapshotSectionStatus.Unavailable,
                    Error = "Invalid app id."
                };
            }

            AppDataKit.AppDataService kit = CreateKit(language);
            return await kit.GetAchievementsAsync((uint)appId, cancellationToken).ConfigureAwait(false);
        }

        public async Task<StatsSection> FetchStatsAsync(
            ulong appId,
            string language = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (appId == 0 || appId > uint.MaxValue)
            {
                return new StatsSection
                {
                    Status = SnapshotSectionStatus.Unavailable,
                    Error = "Invalid app id."
                };
            }

            AppDataKit.AppDataService kit = CreateKit(language);
            return await kit.GetStatsAsync((uint)appId, cancellationToken).ConfigureAwait(false);
        }

        // Longer HTTP timeout: item def archives can be large.
        public async Task<ItemsSection> FetchItemsAsync(
            ulong appId,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (appId == 0 || appId > uint.MaxValue)
            {
                return new ItemsSection
                {
                    Status = SnapshotSectionStatus.Unavailable,
                    Error = "Invalid app id."
                };
            }

            AppDataKit.AppDataService kit = CreateKit(language: null, httpTimeout: TimeSpan.FromSeconds(120));
            return await kit.GetItemsAsync((uint)appId, cancellationToken).ConfigureAwait(false);
        }

        private AppDataKit.AppDataService CreateKit(string language = null, TimeSpan? httpTimeout = null)
        {
            string apiKey = null;
            _steamApiKeyService.TryGetValidFormatKey(out apiKey);
            return new AppDataKit.AppDataService(new AppSnapshotOptions
            {
                SteamWebApiKey = apiKey,
                ProbeAssetUrls = false,
                Language = string.IsNullOrWhiteSpace(language) ? "english" : language.Trim(),
                HttpTimeout = httpTimeout ?? TimeSpan.FromSeconds(30)
            });
        }

        private async Task<AppCatalogSnapshot> FetchFullViaPicsAsync(
            ulong appId,
            AppDataKit.AppDataService kit,
            ITaskReportService feedback,
            CancellationToken cancellationToken)
        {
            AppCatalogSnapshot metaSnap = await FetchMetadataViaPicsAsync(appId, kit, feedback, cancellationToken, resolveDlcNames: true)
                .ConfigureAwait(false);
            if (metaSnap == null || !metaSnap.IsUsable || metaSnap.AppInfo == null)
                return metaSnap ?? new AppCatalogSnapshot { Failure = AppMetadataFetchFailure.Unavailable };

            uint id = (uint)appId;
            AppInfoKeyValue kitInfo = metaSnap.AppInfo;
            if (metaSnap.Metadata == null)
            {
                metaSnap.Metadata = new AppMetadataSection
                {
                    Status = SnapshotSectionStatus.Ok,
                    AppInfoSource = AppInfoSource.Pics,
                    Source = "Pics",
                    AppInfo = kitInfo
                };
            }

            GameAssetsSection assets = await GameAssetParser.BuildAsync(kitInfo, id, new AppSnapshotOptions
            {
                ProbeAssetUrls = false
            }, cancellationToken).ConfigureAwait(false);

            AchievementsSection achievements = await kit.GetAchievementsAsync(id, cancellationToken).ConfigureAwait(false);
            StatsSection stats = await kit.GetStatsAsync(id, cancellationToken).ConfigureAwait(false);
            ItemsSection items = await kit.GetItemsAsync(id, cancellationToken).ConfigureAwait(false);

            return AppCatalogSnapshotStore.FinalizeSnapshot(
                id,
                DateTime.UtcNow,
                metaSnap.Metadata,
                metaSnap.Dlc,
                assets,
                achievements,
                stats,
                items,
                fromAppDataKit: false,
                AppMetadataFetchFailure.None);
        }

        private async Task<AppCatalogSnapshot> FetchMetadataViaPicsAsync(
            ulong appId,
            AppDataKit.AppDataService kit,
            ITaskReportService feedback,
            CancellationToken cancellationToken,
            bool resolveDlcNames)
        {
            feedback?.SetMessage(AddGameStatusMessages.ConnectingToSteam);
            using (var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                sessionCts.CancelAfter(PicsSessionEnsureTimeout);
                bool sessionReady = await _steamProductInfo.TryEnsureSessionAsync(sessionCts.Token).ConfigureAwait(false);
                if (!sessionReady)
                {
                    Program.LogService?.LogWarning(
                        "Steam session not ready for app " + appId + " after "
                        + (int)PicsSessionEnsureTimeout.TotalSeconds + "s.");
                    feedback?.SetMessage(AddGameStatusMessages.MetadataFetchTimedOut, TaskReportKind.Error);
                    return new AppCatalogSnapshot { AppId = (uint)appId, Failure = AppMetadataFetchFailure.TimedOut };
                }
            }

            feedback?.SetMessage(AddGameStatusMessages.FetchingMetadata(appId));
            AppInfoKeyValue appInfo;
            using (var picsCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                picsCts.CancelAfter(PicsProductInfoTimeout);
                var holder = new GameConfig { AppId = appId };
                appInfo = await _steamProductInfo.WarmGameConfigAppInfoAsync(holder, picsCts.Token)
                    .ConfigureAwait(false);
            }

            if (appInfo == null)
            {
                feedback?.SetMessage(AddGameStatusMessages.MetadataFetchFailed, TaskReportKind.Error);
                return new AppCatalogSnapshot { AppId = (uint)appId, Failure = AppMetadataFetchFailure.Unavailable };
            }

            feedback?.SetMessage(string.Empty);

            Dictionary<long, string> dlc = resolveDlcNames
                ? await ResolveDlcNamesForRootAsync(kit, appInfo, cancellationToken).ConfigureAwait(false)
                : CollectDlcIdsOnly(appInfo);
            return SnapshotFromAppInfo((uint)appId, appInfo, dlc, fromAppDataKit: false);
        }

        private static AppCatalogSnapshot SnapshotFromAppInfo(
            uint appId,
            AppInfoKeyValue appInfo,
            Dictionary<long, string> dlc,
            bool fromAppDataKit)
        {
            var meta = new AppMetadataSection
            {
                Status = SnapshotSectionStatus.Ok,
                AppInfoSource = fromAppDataKit ? AppInfoSource.SteamCmd : AppInfoSource.Pics,
                Source = fromAppDataKit ? "SteamCmd" : "Pics",
                AppInfo = appInfo
            };

            return AppCatalogSnapshotStore.FinalizeSnapshot(
                appId,
                DateTime.UtcNow,
                meta,
                DlcSectionFromDictionary(dlc),
                new GameAssetsSection { Status = SnapshotSectionStatus.Unavailable },
                new AchievementsSection { Status = SnapshotSectionStatus.Unavailable },
                new StatsSection { Status = SnapshotSectionStatus.Unavailable },
                new ItemsSection { Status = SnapshotSectionStatus.Unavailable },
                fromAppDataKit,
                AppMetadataFetchFailure.None);
        }

        private async Task<Dictionary<long, string>> MergeDlcNamesAsync(
            AppDataKit.AppDataService kit,
            AppInfoKeyValue appInfo,
            CancellationToken cancellationToken)
        {
            var result = CollectDlcIdsOnly(appInfo);
            DlcSection section = null;
            try
            {
                section = await kit.GetDlcListFromAppInfoAsync(appInfo, cancellationToken).ConfigureAwait(false);
                ApplyResolvedDlcSection(section, result);
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning(
                    "AppDataKit DLC name resolve failed: " + ex.Message);
            }

            await FillUnresolvedViaPicsAsync(section, result, cancellationToken).ConfigureAwait(false);
            return result;
        }

        private async Task<Dictionary<long, string>> ResolveDlcNamesForRootAsync(
            AppDataKit.AppDataService kit,
            AppInfoKeyValue root,
            CancellationToken cancellationToken)
        {
            var result = CollectDlcIdsOnly(root);
            if (result.Count == 0)
                return result;

            DlcSection section = null;
            try
            {
                var ids = new List<uint>(result.Count);
                foreach (long id in result.Keys)
                {
                    if (id > 0 && id <= uint.MaxValue)
                        ids.Add((uint)id);
                }

                section = await kit.ResolveDlcNamesAsync(ids, cancellationToken).ConfigureAwait(false);
                ApplyResolvedDlcSection(section, result);
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning(
                    "DLC name resolve failed: " + ex.Message);
            }

            await FillUnresolvedViaPicsAsync(section, result, cancellationToken).ConfigureAwait(false);
            return result;
        }

        // Package/live PICS remains SteamKit KeyValue: resolves names for DLC ids still missing after AppDataKit lookups.
        private async Task FillUnresolvedViaPicsAsync(
            DlcSection section,
            Dictionary<long, string> result,
            CancellationToken cancellationToken)
        {
            if (result == null || result.Count == 0)
                return;

            var pending = new List<uint>();
            var seen = new HashSet<uint>();

            if (section?.UnresolvedAppIds != null)
            {
                foreach (uint id in section.UnresolvedAppIds)
                {
                    if (id == 0 || !seen.Add(id))
                        continue;
                    pending.Add(id);
                }
            }

            foreach (KeyValuePair<long, string> kvp in result)
            {
                if (kvp.Key <= 0 || kvp.Key > uint.MaxValue)
                    continue;
                uint id = (uint)kvp.Key;
                if (!IsPlaceholderDlcName(id, kvp.Value) || !seen.Add(id))
                    continue;
                pending.Add(id);
            }

            if (pending.Count == 0)
                return;

            bool sessionReady = await _steamProductInfo.TryEnsureSessionAsync(cancellationToken).ConfigureAwait(false);
            if (!sessionReady)
            {
                Program.LogService?.LogWarning(
                    "PICS DLC name fallback skipped: Steam session not ready ("
                    + pending.Count + " unresolved).");
                return;
            }

            foreach (uint id in pending)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    KeyValue kv = await _steamProductInfo
                        .GetAppKeyValueAsync(id.ToString(), cancellationToken)
                        .ConfigureAwait(false);
                    if (kv != null
                        && SteamPicsKeyValueHelper.TryGetAppDisplayInfo(kv, out string name, out _)
                        && !string.IsNullOrWhiteSpace(name))
                    {
                        result[id] = name.Trim();
                    }
                }
                catch (Exception ex)
                {
                    Program.LogService?.LogWarning(
                        "PICS DLC name resolve failed for " + id + ": " + ex.Message);
                }
            }
        }

        private static bool IsPlaceholderDlcName(uint dlcId, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return true;
            return string.Equals(name.Trim(), "DLC " + dlcId, StringComparison.Ordinal);
        }

        private static void ApplyResolvedDlcSection(DlcSection section, Dictionary<long, string> result)
        {
            if (section == null
                || (section.Status != SnapshotSectionStatus.Ok && section.Status != SnapshotSectionStatus.Partial)
                || section.Items == null)
            {
                return;
            }

            foreach (DlcEntry entry in section.Items)
            {
                if (entry == null || entry.AppId == 0)
                    continue;
                long id = entry.AppId;
                string name = string.IsNullOrWhiteSpace(entry.Name) ? ("DLC " + id) : entry.Name.Trim();
                result[id] = name;
            }
        }

        private static DlcSection DlcSectionFromDictionary(Dictionary<long, string> dlc)
        {
            var section = new DlcSection { Status = SnapshotSectionStatus.Ok };
            if (dlc == null || dlc.Count == 0)
            {
                section.Items = Array.Empty<DlcEntry>();
                return section;
            }

            var items = new List<DlcEntry>(dlc.Count);
            foreach (KeyValuePair<long, string> kvp in dlc)
            {
                if (kvp.Key <= 0 || kvp.Key > uint.MaxValue)
                    continue;
                items.Add(new DlcEntry
                {
                    AppId = (uint)kvp.Key,
                    Name = string.IsNullOrWhiteSpace(kvp.Value) ? ("DLC " + kvp.Key) : kvp.Value.Trim(),
                    Type = "dlc"
                });
            }

            section.Items = items;
            return section;
        }

        private static Dictionary<long, string> CollectDlcIdsOnly(AppInfoKeyValue root)
        {
            var ids = new List<long>();
            AppInfoKeyValueHelper.CollectDlcIdsFromAppRoot(root, ids);
            var map = new Dictionary<long, string>();
            foreach (long id in ids)
            {
                if (id > 0 && !map.ContainsKey(id))
                    map[id] = "DLC " + id;
            }
            return map;
        }

        private static OnlineAppData BuildMetadata(ulong appId, AppInfoKeyValue root, string dataSources)
        {
            var metadata = new OnlineAppData
            {
                AppId = appId.ToString(),
                DataSources = dataSources
            };
            AppInfoKeyValueHelper.PopulateMetadataFromAppRoot(root, metadata);
            return metadata;
        }

        private static bool IsUsable(OnlineAppData metadata)
        {
            return metadata != null && !string.IsNullOrWhiteSpace(metadata.Name);
        }

        // Package PICS boundary only (EmulatorConfigService.ExtractAppDataFromAppRoot edge; see 24-goldberg-launch-deploy.mdc).
        public static KeyValue ConvertToSteamKit(AppInfoKeyValue source)
        {
            if (source == null)
                return null;

            var target = new KeyValue(source.Name ?? string.Empty, source.Value ?? string.Empty);
            if (source.Children == null)
                return target;

            foreach (AppInfoKeyValue child in source.Children)
            {
                if (child == null)
                    continue;
                target.Children.Add(ConvertToSteamKit(child));
            }

            return target;
        }

        // Converts a live PICS KeyValue root to the catalog SoT type — call once, then keep AppInfoKeyValue.
        public static AppInfoKeyValue ConvertFromSteamKit(KeyValue source)
        {
            if (source == null)
                return null;

            var target = new AppInfoKeyValue(source.Name ?? string.Empty, source.Value ?? string.Empty);
            if (source.Children == null)
                return target;

            foreach (KeyValue child in source.Children)
            {
                if (child == null)
                    continue;
                target.Children.Add(ConvertFromSteamKit(child));
            }

            return target;
        }
    }
}
