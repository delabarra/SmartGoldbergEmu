using SmartGoldbergEmu.Constants;

namespace SmartGoldbergEmu.Helpers
{
    public static class SteamIdHelper
    {
        // Steam3 account ID: folder name under [Steam Install]/userdata/{Steam3AccountID}/{AppID}/
        public static bool TryGetSteam3AccountId(string steamId64, out string steam3AccountId)
        {
            steam3AccountId = null;
            if (string.IsNullOrWhiteSpace(steamId64))
                return false;

            if (!ulong.TryParse(steamId64.Trim(), out ulong id))
                return false;

            if (id < ApplicationConstants.SteamId64Base || id > ApplicationConstants.SteamId64Max)
                return false;

            steam3AccountId = (id - ApplicationConstants.SteamId64Base).ToString();
            return true;
        }
    }
}
