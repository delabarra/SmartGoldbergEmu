namespace SmartGoldbergEmu.Models
{
    public sealed class StubKitApplyResult
    {
        public StubKitApplyOutcome Outcome { get; set; }

        public bool Success =>
            Outcome == StubKitApplyOutcome.Success || Outcome == StubKitApplyOutcome.Restored;

        // Technical detail for logs; not shown directly to the user.
        public string LogDetail { get; set; }

        public string Summary { get; set; }
    }
}
