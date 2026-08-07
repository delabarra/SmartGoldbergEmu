using System;
using System.Collections.Generic;

namespace AppDataKit
{
    /// <summary>Overall status for one section of app data.</summary>
    public enum SnapshotSectionStatus
    {
        Ok = 0,
        Partial = 1,
        Unavailable = 2,
        Error = 3,
    }

    /// <summary>Where app metadata or DLC details were loaded from.</summary>
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

    /// <summary>PICS or steamcmd appinfo payload.</summary>
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
        public IReadOnlyList<ItemSchemaEntry> Items { get; set; } = Array.Empty<ItemSchemaEntry>();
    }

    public sealed class ItemSchemaEntry
    {
        public string ItemDefId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string IconUrl { get; set; }
    }

    /// <summary>Options for <see cref="AppDataService"/>.</summary>
    public sealed class AppSnapshotOptions
    {
        public const string DefaultSteamCmdInfoUrl = "https://api.steamcmd.net/v1/info/";

        /// <summary>Steam Web API key for achievements, stats, and items. Optional.</summary>
        public string SteamWebApiKey { get; set; }

        /// <summary>When true, HEAD-probes asset candidate URLs and picks the first reachable one.</summary>
        public bool ProbeAssetUrls { get; set; } = true;

        /// <summary>Maximum concurrent Steam Store lookups for DLC names.</summary>
        public int DlcBatchConcurrency { get; set; } = 16;

        /// <summary>Base URL for steamcmd.net appinfo (trailing slash optional).</summary>
        public string SteamCmdInfoUrl { get; set; } = DefaultSteamCmdInfoUrl;

        /// <summary>HTTP timeout for steamcmd and Web API calls.</summary>
        public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(30);
    }

    public sealed class AppInfoFetchResult
    {
        public bool Success { get; set; }
        public AppInfoSource Source { get; set; } = AppInfoSource.Unknown;
        public AppInfoKeyValue AppInfo { get; set; }
        public string Error { get; set; }
    }
}
