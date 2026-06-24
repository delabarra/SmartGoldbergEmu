using System.Collections.Generic;

namespace SmartGoldbergEmu.Models
{
    // Serialized for the detached launch-cleanup watcher (see LaunchSessionCleanupService).
    public sealed class PersistedLaunchSession
    {
        public ulong AppId { get; set; }
        public int GameProcessId { get; set; }
        // Bumped on each launch so a superseded cleanup watcher no-ops instead of cleaning a new session.
        public string SessionGeneration { get; set; }
        public bool RestoreActiveProcessRegistry { get; set; }
        public string GameLibraryFolder { get; set; }
        public string LoadDllsFolder { get; set; }
        public PersistedSourceModRestore SourceModRestore { get; set; }
        public PersistedBranchRestore BranchRestore { get; set; }
        public List<PersistedDeploySite> DeploySites { get; set; }
    }

    public sealed class PersistedSourceModRestore
    {
        public bool HadPreviousValue { get; set; }
        public string PreviousValue { get; set; }
    }

    public sealed class PersistedBranchRestore
    {
        public ulong AppId { get; set; }
        public string BranchName { get; set; }
        public bool IsBetaBranch { get; set; }
    }

    public sealed class PersistedDeploySite
    {
        public string MirroredSteamSettingsPath { get; set; }
        public List<PersistedFileDeployment> Files { get; set; }
    }

    public sealed class PersistedFileDeployment
    {
        public string TargetPath { get; set; }
        public string BackupPath { get; set; }
        public bool HadOriginal { get; set; }
    }
}
