namespace SmartGoldbergEmu.Models
{
    public sealed class GoldbergLaunchModeAvailability
    {
        public bool SteamClientAvailable { get; set; } = true;
        public bool StandardSteamApiAvailable { get; set; }
        public bool SteamDllBesideExeAvailable { get; set; }

        public bool IsAvailable(GoldbergLaunchMode mode)
        {
            switch (mode)
            {
                case GoldbergLaunchMode.NoEmulation:
                    return true;
                case GoldbergLaunchMode.StandardSteamApi:
                    return StandardSteamApiAvailable;
                case GoldbergLaunchMode.SteamDllBesideExe:
                    return SteamDllBesideExeAvailable;
                default:
                    return SteamClientAvailable;
            }
        }

        public GoldbergLaunchMode GetFirstAvailable()
        {
            if (SteamClientAvailable)
                return GoldbergLaunchMode.SteamClient;
            if (StandardSteamApiAvailable)
                return GoldbergLaunchMode.StandardSteamApi;
            if (SteamDllBesideExeAvailable)
                return GoldbergLaunchMode.SteamDllBesideExe;
            return GoldbergLaunchMode.SteamClient;
        }

        public GoldbergLaunchMode ResolveAvailable(GoldbergLaunchMode preferred)
        {
            if (IsAvailable(preferred))
                return preferred;
            return GetFirstAvailable();
        }
    }
}
