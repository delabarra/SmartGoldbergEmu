namespace AppDataKit
{
    /// <summary>A Steam content server entry from <c>GetServersForSteamPipe</c>.</summary>
    public sealed class ContentServer
    {
        public string Type { get; set; } = string.Empty;
        public string Host { get; set; } = string.Empty;
        public string VHost { get; set; } = string.Empty;
        public int Port { get; set; } = 443;
        public bool UseHttps { get; set; } = true;
        public int CellId { get; set; }
        public int Load { get; set; }
        public float WeightedLoad { get; set; }
        public bool UseAsProxy { get; set; }
        public string ProxyRequestPathTemplate { get; set; } = string.Empty;

        public string EffectiveHost => string.IsNullOrEmpty(VHost) ? Host : VHost;
    }

    /// <summary>Depot reference parsed from appinfo.</summary>
    public sealed class DepotInfo
    {
        public uint DepotId { get; set; }
        public uint AppId { get; set; }
        public string Branch { get; set; } = "public";
        public ulong ManifestGid { get; set; }
        public uint? DepotFromApp { get; set; }
        public string OsList { get; set; } = string.Empty;
    }
}
