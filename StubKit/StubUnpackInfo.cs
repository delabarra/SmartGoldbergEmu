namespace SmartGoldbergEmu.StubKit
{
    public sealed class StubUnpackInfo
    {
        public StubVariant Variant { get; set; }
        public string VariantName { get; set; }
        public string Summary { get; set; }
        public bool UsedEncryption { get; set; }
        public BindAction BindAction { get; set; }
        public bool UsedTlsOepOverride { get; set; }
        public uint NewEntryPointRva { get; set; }
        public string ErrorMessage { get; set; }
    }
}
