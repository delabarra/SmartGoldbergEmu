using System;
using System.Collections.Generic;

namespace AppDataKit
{
    public enum SnapshotSectionStatus
    {
        Ok = 0,
        Partial = 1,
        Unavailable = 2,
        Error = 3,
    }

    public enum AppInfoSource
    {
        Unknown = 0,
        Pics = 1,
        SteamCmd = 2,
    }

    public abstract class SnapshotSection
    {
        public SnapshotSectionStatus Status { get; set; } = SnapshotSectionStatus.Unavailable;
        public string Source { get; set; } = string.Empty;
        public string Error { get; set; }
    }

    public sealed class AppMetadataSection : SnapshotSection
    {
        public AppInfoSource AppInfoSource { get; set; } = AppInfoSource.Unknown;
        public AppInfoKeyValue AppInfo { get; set; }
    }

    public sealed class DlcSection : SnapshotSection
    {
        public IReadOnlyList<DlcEntry> Items { get; set; } = Array.Empty<DlcEntry>();
        public IReadOnlyList<uint> UnresolvedAppIds { get; set; } = Array.Empty<uint>();
    }

    public sealed class DlcEntry
    {
        public uint AppId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "dlc";
    }

    public sealed class GameAssetsSection : SnapshotSection
    {
        public IReadOnlyList<GameAssetEntry> Items { get; set; } = Array.Empty<GameAssetEntry>();
    }

    public sealed class GameAssetEntry
    {
        public string KeyPath { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Url { get; set; }
        public IReadOnlyList<string> CandidateUrls { get; set; } = Array.Empty<string>();
    }

    public sealed class AchievementsSection : SnapshotSection
    {
        public string GameName { get; set; }
        public string GameVersion { get; set; }
        public IReadOnlyList<AchievementSchemaEntry> Items { get; set; } = Array.Empty<AchievementSchemaEntry>();
    }

    public sealed class AchievementSchemaEntry
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IconUrl { get; set; }
        public string IconGrayUrl { get; set; }
        public bool Hidden { get; set; }
    }

    public sealed class StatsSection : SnapshotSection
    {
        public IReadOnlyList<StatSchemaEntry> Items { get; set; } = Array.Empty<StatSchemaEntry>();
        // Raw games-infos stats_db.json (no-key fallback); converted to Goldberg stats.json on save, never persisted as-is.
        public string StatsDbJson { get; set; }
    }

    public sealed class StatSchemaEntry
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string DefaultValue { get; set; } = string.Empty;
    }

    public sealed class ItemsSection : SnapshotSection
    {
        public bool Supported { get; set; }
        public string Digest { get; set; }
        // Full GetItemDefArchive body (Goldberg items.json mapping needs all fields).
        public string ArchiveJson { get; set; }
        public IReadOnlyList<ItemSchemaEntry> Items { get; set; } = Array.Empty<ItemSchemaEntry>();
    }

    public sealed class ItemSchemaEntry
    {
        public string ItemDefId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string IconUrl { get; set; }
    }

    public sealed class AppSnapshotOptions
    {
        public const string DefaultSteamCmdInfoUrl = "https://api.steamcmd.net/v1/info/";

        // Optional; achievements, stats, and items need it.
        public string SteamWebApiKey { get; set; }

        public string Language { get; set; } = "english";

        // HEAD-probes asset candidate URLs and keeps the first reachable one.
        public bool ProbeAssetUrls { get; set; } = true;

        public int DlcBatchConcurrency { get; set; } = 16;

        // Trailing slash optional.
        public string SteamCmdInfoUrl { get; set; } = DefaultSteamCmdInfoUrl;

        public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(30);
    }

    public sealed class AppInfoFetchResult
    {
        public bool Success { get; set; }
        public AppInfoSource Source { get; set; } = AppInfoSource.Unknown;
        public AppInfoKeyValue AppInfo { get; set; }
        public string Error { get; set; }
    }

    public sealed class AppDataSectionsResult
    {
        public uint AppId { get; set; }
        public DateTime FetchedAtUtc { get; set; }
        public AppMetadataSection Metadata { get; set; }
        public DlcSection Dlc { get; set; }
        public GameAssetsSection Assets { get; set; }
        public AchievementsSection Achievements { get; set; }
        public StatsSection Stats { get; set; }
        public ItemsSection Items { get; set; }
    }
}
