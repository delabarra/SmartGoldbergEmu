using System;
using System.Collections.Generic;

namespace SmartGoldbergEmu.Models
{
    public sealed class SteamStaticCdnPreferences
    {
        public string ProbedUtc { get; set; }

        public List<string> StoreItemAssetsHosts { get; set; } = new List<string>();

        public List<string> SharedFastlyHosts { get; set; } = new List<string>();

        public List<string> SteamAppsBareHosts { get; set; } = new List<string>();

        public List<string> GeneralCdnHosts { get; set; } = new List<string>();

        public List<string> ClientPackageHosts { get; set; } = new List<string>();

        public bool IsExpired(TimeSpan ttl)
        {
            if (string.IsNullOrWhiteSpace(ProbedUtc))
                return true;

            if (!DateTime.TryParse(ProbedUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var probed))
                return true;

            return DateTime.UtcNow - probed.ToUniversalTime() > ttl;
        }
    }
}
