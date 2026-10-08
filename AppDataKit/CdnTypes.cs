namespace AppDataKit
{
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
