using System;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Services;

namespace SmartGoldbergEmu.Helpers
{
    // Single place for Steam API language codes from app settings (store search, PICS assets, schemas, CM logon).
    public static class SteamConfiguredLanguageHelper
    {
        public static string Normalize(string language)
        {
            return string.IsNullOrWhiteSpace(language)
                ? ApplicationConstants.DefaultLanguage
                : language.Trim();
        }

        // Prefer an explicit language; otherwise read global/per-game settings (appId 0 = global).
        public static string Resolve(string language, ulong appId = 0)
        {
            if (!string.IsNullOrWhiteSpace(language))
                return language.Trim();

            return Resolve(appId);
        }

        public static string Resolve(ulong appId = 0)
        {
            try
            {
                string fromSettings = ServiceLocator.EmulatorConfigService?.GetLanguageForAchievements(appId);
                if (!string.IsNullOrWhiteSpace(fromSettings))
                    return fromSettings.Trim();
            }
            catch
            {
                // Tests or early startup without a configured service.
            }

            return ApplicationConstants.DefaultLanguage;
        }
    }
}
