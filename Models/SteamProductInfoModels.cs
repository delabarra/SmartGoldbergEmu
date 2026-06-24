using System.Collections.Generic;
using SteamKit;

namespace SmartGoldbergEmu.Models
{
    /// <summary>
    /// Branch information extracted from package PICS / KeyValue data.
    /// </summary>
    public class PackageBranchInfo
    {
        public string Name { get; set; }
        public uint BuildId { get; set; }
        public uint TimeUpdated { get; set; }
        public string Description { get; set; }
        public bool Protected { get; set; }
    }

    /// <summary>
    /// Result of extracting package data (depots, branches, app IDs) for an app.
    /// </summary>
    public class PackageExtractionResult
    {
        public List<string> Depots { get; set; }
        public List<PackageBranchInfo> Branches { get; set; }
        public List<string> AppIds { get; set; }

        public PackageExtractionResult()
        {
            Depots = new List<string>();
            Branches = new List<PackageBranchInfo>();
            AppIds = new List<string>();
        }
    }

    /// <summary>
    /// Result of extracting app-specific data (stats, leaderboards, achievements) from Steam app product info (PICS).
    /// </summary>
    public class AppDataExtractionResult
    {
        public string Stats { get; set; }
        public List<string> Depots { get; set; }
        public List<string> Leaderboards { get; set; }
        public string Achievements { get; set; }

        public AppDataExtractionResult()
        {
            Depots = new List<string>();
            Leaderboards = new List<string>();
        }
    }
}
