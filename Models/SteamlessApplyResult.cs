namespace SmartGoldbergEmu.Models
{
    public sealed class SteamlessApplyResult
    {
        public SteamlessApplyOutcome Outcome { get; set; }

        public bool Success => Outcome == SteamlessApplyOutcome.Success;

        // Technical detail for logs; not shown directly to the user.
        public string LogDetail { get; set; }
    }
}
