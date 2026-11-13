namespace SmartGoldbergEmu.Models
{
    // One missing Steam install-script / common-redist unit shown to the player before launch.
    public sealed class PendingGameDependency
    {
        public string DisplayName { get; set; }

        public string ScriptPath { get; set; }

        public string InstallDir { get; set; }

        public ulong AppIdForDefaultHasRun { get; set; }

        // When set, only this Run Process child is installed; otherwise all pending entries in the script.
        public string ProcessEntryName { get; set; }

        // Known 228980 depot id (for presence probes); may be set even when files are already local.
        public uint? DepotId { get; set; }

        public uint? DownloadDepotId { get; set; }

        public string DownloadRelativeScriptPath { get; set; }
    }
}
