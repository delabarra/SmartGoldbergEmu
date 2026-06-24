namespace SmartGoldbergEmu.Constants
{
    // Windows registry subkeys for the Steam client (Valve). Hive is chosen by the caller (HKCU vs HKLM).
    public static class SteamClientRegistryKeyPaths
    {
        public const string CurrentUserSteamClient = @"Software\Valve\Steam";
        public const string CurrentUserSteamClientWow6432Node = @"Software\WOW6432Node\Valve\Steam";
        public const string CurrentUserSteamActiveProcess = @"Software\Valve\Steam\ActiveProcess";

        public const string LocalMachineSteamClientWow64 = @"SOFTWARE\WOW6432Node\Valve\Steam";
        public const string LocalMachineSteamClient = @"SOFTWARE\Valve\Steam";
    }
}
