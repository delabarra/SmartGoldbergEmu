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

        // True until StubKit PE detection has finished for this target.
        public bool IsDetectionPending { get; set; }

        // True when StubKit detected a known SteamStub variant on this executable.
        public bool HasSteamStub { get; set; }

        // True when the detected variant can be unpacked by this build.
        public bool CanRemove { get; set; }

        // True when a *_o.exe backup exists beside this executable.
        public bool HasOriginalBackup { get; set; }

        // DetectResult.Name (e.g. "SteamStub 3.1 (x64)" or "none").
        public string StubName { get; set; }

        // Loading while detecting; Patch if removable stub; Restore if backup only; otherwise no stub.
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
