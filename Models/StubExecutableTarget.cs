namespace SmartGoldbergEmu.Models
{
    // Executable the user can pick for SteamStub removal (settings Path and/or a launch option).
    public sealed class StubExecutableTarget
    {
        public string FullPath { get; set; }

        public string DisplayName { get; set; }

        // Relative under StartFolder, or file name (menu tooltip).
        public string RelativeOrExeHint { get; set; }

        public bool IsSettingsExecutable { get; set; }

        public bool IsDetectionPending { get; set; }

        public bool HasSteamStub { get; set; }

        // The detected variant can be unpacked by this build.
        public bool CanRemove { get; set; }

        // True when a *_o.exe backup exists beside this executable.
        public bool HasOriginalBackup { get; set; }

        // DetectResult.Name (e.g. "SteamStub 3.1 (x64)" or "none").
        public string StubName { get; set; }

        public StubExecutableMenuAction MenuAction
        {
            get
            {
                if (IsDetectionPending)
                    return StubExecutableMenuAction.Loading;
                if (CanRemove)
                    return StubExecutableMenuAction.Patch;
                if (HasOriginalBackup)
                    return StubExecutableMenuAction.Restore;
                return StubExecutableMenuAction.NoStub;
            }
        }
    }

    public enum StubExecutableMenuAction
    {
        Loading,
        NoStub,
        Patch,
        Restore
    }
}
