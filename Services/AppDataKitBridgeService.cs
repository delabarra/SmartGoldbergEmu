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
    public enum AppMetadataFetchFailure
    {
        None,
        TimedOut,
        Unavailable
    }

    public sealed class AppDataKitMetadataResult
    {
        public OnlineAppData Metadata { get; set; }
        public KeyValue AppRoot { get; set; }
        public Dictionary<long, string> DlcData { get; set; }
        public bool FromAppDataKit { get; set; }
        public AppMetadataFetchFailure Failure { get; set; }
    }

    // AppDataKit-first metadata/DLC; disk VDF then SteamKit PICS only when steamcmd is unusable.
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

        public async Task<AppDataKitMetadataResult> FetchMetadataAsync(
            ulong appId,
            KeyValue existingAppRoot = null,
            ITaskReportService feedback = null,
            CancellationToken cancellationToken = default(CancellationToken),
            bool resolveDlcNames = true)
        {
            if (appId == 0 || appId > uint.MaxValue)
            {
                return new AppDataKitMetadataResult
                {
                    Failure = AppMetadataFetchFailure.Unavailable,
                    AppRoot = existingAppRoot
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
                    KeyValue converted = ConvertToSteamKit(metaSection.AppInfo);
                    OnlineAppData metadata = BuildMetadata(appId, converted, "SteamCmd");
                    if (IsUsable(metadata))
                    {
                        Dictionary<long, string> dlc = resolveDlcNames
                            ? await MergeStoreDlcNamesAsync(kit, metaSection.AppInfo, converted, cancellationToken)
                                .ConfigureAwait(false)
                            : CollectDlcIdsOnly(converted);
                        return new AppDataKitMetadataResult
                        {
                            Metadata = metadata,
                            AppRoot = converted,
                            DlcData = dlc,
                            FromAppDataKit = true,
                            Failure = AppMetadataFetchFailure.None
                        };
                    }

                    Program.LogService?.LogWarning(
                        "AppDataKit metadata for app " + appId + " lacked a usable name; trying cache/PICS.");
                }
                else
                {
                    string err = metaSection?.Error ?? "empty metadata";
                    Program.LogService?.LogWarning(
                        "AppDataKit metadata unavailable for app " + appId + ": " + err + "; trying cache/PICS.");
                }
            }
            catch (OperationCanceledException)
            {
                Program.LogService?.LogWarning("App metadata timed out while fetching app " + appId + ".");
                feedback?.SetMessage(AddGameStatusMessages.MetadataFetchTimedOut, TaskReportKind.Error);
                return new AppDataKitMetadataResult { Failure = AppMetadataFetchFailure.TimedOut };
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError("AppDataKit metadata error for app " + appId + ": " + ex.Message, ex);
            }

            if (existingAppRoot != null)
            {
                OnlineAppData fromExisting = BuildMetadata(appId, existingAppRoot, "Cached app info");
                if (IsUsable(fromExisting))
                {
                    Dictionary<long, string> dlc = resolveDlcNames
                        ? await ResolveStoreNamesForRootAsync(kit, existingAppRoot, cancellationToken).ConfigureAwait(false)
                        : CollectDlcIdsOnly(existingAppRoot);
                    return new AppDataKitMetadataResult
                    {
                        Metadata = fromExisting,
                        AppRoot = existingAppRoot,
                        DlcData = dlc,
                        Failure = AppMetadataFetchFailure.None
                    };
                }
            }

            KeyValue diskRoot = SteamPicsKeyValueHelper.TryLoadExportedAppPicsFromValveFile(
                PathConstants.GamesDirectory,
                appId);
            if (diskRoot != null)
            {
                OnlineAppData fromDisk = BuildMetadata(appId, diskRoot, "Cached app info");
                if (IsUsable(fromDisk))
                {
                    Dictionary<long, string> dlc = resolveDlcNames
                        ? await ResolveStoreNamesForRootAsync(kit, diskRoot, cancellationToken).ConfigureAwait(false)
                        : CollectDlcIdsOnly(diskRoot);
                    return new AppDataKitMetadataResult
                    {
                        Metadata = fromDisk,
                        AppRoot = diskRoot,
                        DlcData = dlc,
                        Failure = AppMetadataFetchFailure.None
                    };
                }
            }

            try
            {
                return await FetchViaPicsAsync(appId, kit, feedback, cancellationToken, resolveDlcNames)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                feedback?.SetMessage(AddGameStatusMessages.MetadataFetchTimedOut, TaskReportKind.Error);
                return new AppDataKitMetadataResult { Failure = AppMetadataFetchFailure.TimedOut };
            }
            catch (Exception picsEx)
            {
                Program.LogService?.LogError("Steam PICS fallback failed for app " + appId + ": " + picsEx.Message, picsEx);
                feedback?.SetMessage(AddGameStatusMessages.MetadataFetchFailed, TaskReportKind.Error);
                return new AppDataKitMetadataResult { Failure = AppMetadataFetchFailure.Unavailable };
            }
        }

        // DLC ids from app root + Store names (no per-DLC PICS).
        public async Task<Dictionary<long, string>> FetchDlcAsync(
            ulong appId,
            KeyValue appRoot = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (appId == 0 || appId > uint.MaxValue)
                return new Dictionary<long, string>();

            AppDataKit.AppDataService kit = CreateKit();
            KeyValue root = appRoot;
            AppInfoKeyValue kitInfo = null;

            if (root == null)
            {
                AppMetadataSection meta = await kit.GetMetadataAsync((uint)appId, cancellationToken).ConfigureAwait(false);
                if (meta?.AppInfo != null
                    && (meta.Status == SnapshotSectionStatus.Ok || meta.Status == SnapshotSectionStatus.Partial))
                {
                    kitInfo = meta.AppInfo;
                    root = ConvertToSteamKit(meta.AppInfo);
                }
            }

            if (root == null)
                return new Dictionary<long, string>();

            if (kitInfo != null)
                return await MergeStoreDlcNamesAsync(kit, kitInfo, root, cancellationToken).ConfigureAwait(false);

            return await ResolveStoreNamesForRootAsync(kit, root, cancellationToken).ConfigureAwait(false);
        }

        private AppDataKit.AppDataService CreateKit()
        {
            string apiKey = null;
            _steamApiKeyService.TryGetValidFormatKey(out apiKey);
            return new AppDataKit.AppDataService(new AppSnapshotOptions
            {
                SteamWebApiKey = apiKey,
                ProbeAssetUrls = false
            });
        }

        private async Task<AppDataKitMetadataResult> FetchViaPicsAsync(
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
                    return new AppDataKitMetadataResult { Failure = AppMetadataFetchFailure.TimedOut };
                }
            }

            feedback?.SetMessage(AddGameStatusMessages.LookingUpData(appId));
            KeyValue picsRoot;
            using (var picsCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                picsCts.CancelAfter(PicsProductInfoTimeout);
                var holder = new GameConfig { AppId = appId };
                picsRoot = await _steamProductInfo.WarmGameConfigAppPicsRootAsync(holder, picsCts.Token)
                    .ConfigureAwait(false);
            }

            if (picsRoot == null)
            {
                feedback?.SetMessage(AddGameStatusMessages.MetadataFetchFailed, TaskReportKind.Error);
                return new AppDataKitMetadataResult { Failure = AppMetadataFetchFailure.Unavailable };
            }

            OnlineAppData metadata = BuildMetadata(appId, picsRoot, "Steam (game assets)");
            Dictionary<long, string> dlc = resolveDlcNames
                ? await ResolveStoreNamesForRootAsync(kit, picsRoot, cancellationToken).ConfigureAwait(false)
                : CollectDlcIdsOnly(picsRoot);
            return new AppDataKitMetadataResult
            {
                Metadata = metadata,
                AppRoot = picsRoot,
                DlcData = dlc,
                FromAppDataKit = false,
                Failure = AppMetadataFetchFailure.None
            };
        }

        private static async Task<Dictionary<long, string>> MergeStoreDlcNamesAsync(
            AppDataKit.AppDataService kit,
            AppInfoKeyValue appInfo,
            KeyValue convertedRoot,
            CancellationToken cancellationToken)
        {
            var result = CollectDlcIdsOnly(convertedRoot);
            try
            {
                DlcSection section = await kit.GetDlcListFromAppInfoAsync(appInfo, cancellationToken).ConfigureAwait(false);
                ApplyStoreDlcSection(section, result);
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning(
                    "AppDataKit DLC name resolve failed: " + ex.Message);
            }

            return result;
        }

        private static async Task<Dictionary<long, string>> ResolveStoreNamesForRootAsync(
            AppDataKit.AppDataService kit,
            KeyValue root,
            CancellationToken cancellationToken)
        {
            var result = CollectDlcIdsOnly(root);
            if (result.Count == 0)
                return result;

            try
            {
                var ids = new List<uint>(result.Count);
                foreach (long id in result.Keys)
                {
                    if (id > 0 && id <= uint.MaxValue)
                        ids.Add((uint)id);
                }

                DlcSection section = await kit.ResolveDlcNamesAsync(ids, cancellationToken).ConfigureAwait(false);
                ApplyStoreDlcSection(section, result);
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning(
                    "Store DLC name resolve failed: " + ex.Message);
            }

            return result;
        }

        private static void ApplyStoreDlcSection(DlcSection section, Dictionary<long, string> result)
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

        private static Dictionary<long, string> CollectDlcIdsOnly(KeyValue root)
        {
            var ids = new List<long>();
            SteamPicsKeyValueHelper.CollectDlcIdsFromAppRoot(root, ids);
            var map = new Dictionary<long, string>();
            foreach (long id in ids)
            {
                if (id > 0 && !map.ContainsKey(id))
                    map[id] = "DLC " + id;
            }
            return map;
        }

        private static OnlineAppData BuildMetadata(ulong appId, KeyValue root, string dataSources)
        {
            var metadata = new OnlineAppData
            {
                AppId = appId.ToString(),
                DataSources = dataSources
            };
            SteamPicsKeyValueHelper.PopulateMetadataFromAppRoot(root, metadata);
            return metadata;
        }

        private static bool IsUsable(OnlineAppData metadata)
        {
            return metadata != null && !string.IsNullOrWhiteSpace(metadata.Name);
        }

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
    }
}
