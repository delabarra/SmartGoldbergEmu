namespace SmartGoldbergEmu.Models
{
    // Executable the user can pick for Steamless (settings Path and/or a launch option).
    public sealed class SteamlessTarget
    {
        public string FullPath { get; set; }

        public string DisplayName { get; set; }

        // Relative under StartFolder, or file name (menu tooltip).
        public string RelativeOrExeHint { get; set; }

        public bool IsSettingsExecutable { get; set; }

        // True when a Steamless *_o.exe backup exists beside this executable.
        public bool AlreadyPatched { get; set; }
    }
}
