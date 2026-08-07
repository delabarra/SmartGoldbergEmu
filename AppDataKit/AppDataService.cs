using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AppDataKit
{
    /// <summary>
    /// Fetches app metadata, DLC, game assets, achievements, stats, and items via steamcmd.net and Steam Web API.
    /// </summary>
    public sealed class AppDataService
    {
        private readonly AppSnapshotOptions _options;

        public AppDataService(AppSnapshotOptions options = null)
        {
            _options = options ?? new AppSnapshotOptions();
        }

        public Task<AppMetadataSection> GetMetadataAsync(uint appId, CancellationToken cancellationToken = default(CancellationToken))
        {
            return FetchMetadataSectionAsync(appId, cancellationToken);
        }

        public async Task<DlcSection> GetDlcListAsync(uint appId, CancellationToken cancellationToken = default(CancellationToken))
        {
            MetadataContext context = await LoadMetadataContextAsync(appId, cancellationToken).ConfigureAwait(false);
            if (!context.Success)
            {
                return new DlcSection
                {
                    Status = SnapshotSectionStatus.Unavailable,
                    Error = context.Error ?? "Metadata is required to resolve DLC.",
                };
            }

            return await DlcResolver.ResolveAsync(
                context.DlcAppIds,
                _options,
                cancellationToken).ConfigureAwait(false);
        }

        // Resolve DLC names from an already-fetched appinfo tree (no second steamcmd round-trip).
        public Task<DlcSection> GetDlcListFromAppInfoAsync(
            AppInfoKeyValue appInfo,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (appInfo == null)
            {
                return Task.FromResult(new DlcSection
                {
                    Status = SnapshotSectionStatus.Unavailable,
                    Error = "Metadata is required to resolve DLC.",
                });
            }

            return DlcResolver.ResolveAsync(
                AppInfoParser.ParseDlcAppIds(appInfo),
                _options,
                cancellationToken);
        }

        public Task<DlcSection> ResolveDlcNamesAsync(
            IReadOnlyList<uint> dlcAppIds,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return DlcResolver.ResolveAsync(
                dlcAppIds ?? Array.Empty<uint>(),
                _options,
                cancellationToken);
        }

        public async Task<GameAssetsSection> GetGameAssetsAsync(uint appId, CancellationToken cancellationToken = default(CancellationToken))
        {
            MetadataContext context = await LoadMetadataContextAsync(appId, cancellationToken).ConfigureAwait(false);
            if (!context.Success)
            {
                return new GameAssetsSection
                {
                    Status = SnapshotSectionStatus.Unavailable,
                    Error = context.Error ?? "Metadata is required to resolve game assets.",
                };
            }

            return await GameAssetParser.BuildAsync(
                context.AppInfo,
                appId,
                _options,
                cancellationToken).ConfigureAwait(false);
        }

        public Task<AchievementsSection> GetAchievementsAsync(uint appId, CancellationToken cancellationToken = default(CancellationToken))
        {
            return SteamWebApiClient.FetchAchievementsAsync(appId, _options.SteamWebApiKey, _options, cancellationToken);
        }

        public Task<StatsSection> GetStatsAsync(uint appId, CancellationToken cancellationToken = default(CancellationToken))
        {
            return SteamWebApiClient.FetchStatsAsync(appId, _options.SteamWebApiKey, _options, cancellationToken);
        }

        public Task<ItemsSection> GetItemsAsync(uint appId, CancellationToken cancellationToken = default(CancellationToken))
        {
            return SteamWebApiClient.FetchItemsAsync(appId, _options.SteamWebApiKey, _options, cancellationToken);
        }

        public async Task<Dictionary<string, object>> GetAllJsonNodesAsync(uint appId, CancellationToken cancellationToken = default(CancellationToken))
        {
            DateTime fetchedAtUtc = DateTime.UtcNow;

            AppMetadataSection metadata = await GetMetadataAsync(appId, cancellationToken).ConfigureAwait(false);

            DlcSection dlc;
            GameAssetsSection gameAssets;
            if (metadata != null
                && (metadata.Status == SnapshotSectionStatus.Ok || metadata.Status == SnapshotSectionStatus.Partial)
                && metadata.AppInfo != null)
            {
                dlc = await GetDlcListFromAppInfoAsync(metadata.AppInfo, cancellationToken).ConfigureAwait(false);
                gameAssets = await GameAssetParser.BuildAsync(
                    metadata.AppInfo,
                    appId,
                    _options,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                dlc = new DlcSection
                {
                    Status = SnapshotSectionStatus.Unavailable,
                    Error = metadata?.Error ?? "Metadata is required to resolve DLC.",
                };
                gameAssets = new GameAssetsSection
                {
                    Status = SnapshotSectionStatus.Unavailable,
                    Error = metadata?.Error ?? "Metadata is required to resolve game assets.",
                };
            }

            Tuple<AchievementsSection, StatsSection> schemaSections = await SteamWebApiClient.FetchAchievementsAndStatsAsync(
                appId,
                _options.SteamWebApiKey,
                _options,
                cancellationToken).ConfigureAwait(false);

            ItemsSection items = await GetItemsAsync(appId, cancellationToken).ConfigureAwait(false);

            return AppDataJson.BuildNodes(
                appId,
                fetchedAtUtc,
                metadata,
                dlc,
                gameAssets,
                schemaSections.Item1,
                schemaSections.Item2,
                items);
        }

        private async Task<AppMetadataSection> FetchMetadataSectionAsync(uint appId, CancellationToken cancellationToken)
        {
            AppInfoFetchResult fetch = await AppInfoClient.FetchAsync(
                appId,
                _options,
                cancellationToken).ConfigureAwait(false);

            var section = new AppMetadataSection();
            if (fetch.Success && fetch.AppInfo != null)
            {
                section.Status = SnapshotSectionStatus.Ok;
                section.Source = fetch.Source.ToString();
                section.AppInfoSource = fetch.Source;
                section.AppInfo = fetch.AppInfo;
            }
            else
            {
                section.Status = SnapshotSectionStatus.Error;
                section.Error = fetch.Error ?? "Unable to fetch app metadata.";
            }

            return section;
        }

        private async Task<MetadataContext> LoadMetadataContextAsync(uint appId, CancellationToken cancellationToken)
        {
            AppInfoFetchResult fetch = await AppInfoClient.FetchAsync(
                appId,
                _options,
                cancellationToken).ConfigureAwait(false);

            if (!fetch.Success || fetch.AppInfo == null)
            {
                return new MetadataContext
                {
                    Success = false,
                    Error = fetch.Error ?? "Unable to fetch app metadata.",
                };
            }

            return new MetadataContext
            {
                Success = true,
                Source = fetch.Source,
                AppInfo = fetch.AppInfo,
                DlcAppIds = AppInfoParser.ParseDlcAppIds(fetch.AppInfo),
            };
        }

        private sealed class MetadataContext
        {
            public bool Success { get; set; }
            public AppInfoSource Source { get; set; }
            public AppInfoKeyValue AppInfo { get; set; }
            public IReadOnlyList<uint> DlcAppIds { get; set; } = Array.Empty<uint>();
            public string Error { get; set; }
        }
    }
}
