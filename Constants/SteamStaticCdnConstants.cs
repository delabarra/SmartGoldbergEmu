using System;
using System.Collections.Generic;

namespace SmartGoldbergEmu.Constants
{
    /// <summary>
    /// Known Steam static asset CDN hosts and probe targets used to rank mirrors per user.
    /// </summary>
    public static class SteamStaticCdnConstants
    {
        public const string PreferencesCacheFileName = "steam_static_cdn_preferences.json";

        public static readonly TimeSpan PreferencesCacheTtl = TimeSpan.FromDays(7);

        public const int ProbeTimeoutMs = 3000;

        /// <summary>Stable probe app for bare <c>/steam/apps/</c> mirrors.</summary>
        public const ulong ProbeAppId = 570;

        public const string ProbeBareAssetPath = "header.jpg";

        public const string ProbeStoreItemAssetsPath =
            "store_item_assets/steam/apps/570/header.jpg";

        public const string ClientSoundsPackageRelativePath =
            "client/steamui_websrc_sounds_all.zip.vz.5ba6acd8f4dfe4b93437895b20344fab5bb3ff96_3714779";

        // Steam client package manifest (Valve KeyValues); used to resolve current bins_win32 zipvz.
        public const string ClientWin32ManifestRelativePath = "client/steam_client_win32";

        // Entry name inside bins_win32 for the client Steam.dll.
        public const string ClientBinsSteamDllEntryName = "Steam.dll";

        public static readonly IReadOnlyList<string> DefaultStoreItemAssetsHosts = new[]
        {
            "shared.fastly.steamstatic.com"
        };

        public static readonly IReadOnlyList<string> DefaultSharedFastlyHosts = new[]
        {
            "shared.fastly.steamstatic.com"
        };

        public static readonly IReadOnlyList<string> DefaultSteamAppsBareHosts = new[]
        {
            "cdn.fastly.steamstatic.com",
            "cdn.akamai.steamstatic.com",
            "cdn.cloudflare.steamstatic.com",
            "steamcdn-a.akamaihd.net"
        };

        public static readonly IReadOnlyList<string> DefaultGeneralCdnHosts = new[]
        {
            "cdn.fastly.steamstatic.com",
            "cdn.akamai.steamstatic.com",
            "cdn.cloudflare.steamstatic.com",
            "cdn.steamstatic.com",
            "steamcdn-a.akamaihd.net"
        };

        public static readonly IReadOnlyList<string> DefaultClientPackageHosts = new[]
        {
            "cdn.cloudflare.steamstatic.com",
            "cdn.fastly.steamstatic.com",
            "cdn.akamai.steamstatic.com"
        };
    }
}
