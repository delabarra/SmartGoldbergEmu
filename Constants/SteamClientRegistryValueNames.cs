namespace SmartGoldbergEmu.Constants
{
    // HKLM/HKCU Valve\Steam registry value names (Windows Steam client).
    public static class SteamClientRegistryValueNames
    {
        public const string InstallPath = "InstallPath";
        public const string SteamPath = "SteamPath";
        public const string SteamExe = "SteamExe";

        // HKCU ...\ActiveProcess (Steam client process / Goldberg DLL path)
        public const string ActiveProcessPid = "pid";
        public const string ActiveProcessSteamClientDll = "SteamClientDll";
        public const string ActiveProcessSteamClientDll64 = "SteamClientDll64";

        // HKCU Software\Valve\Steam — Source Engine mod install root (gbe_fork steamclient layout).
        public const string SourceModInstallPath = "SourceModInstallPath";
    }
}
