namespace SmartGoldbergEmu.Models
{
    public enum SteamlessApplyOutcome
    {
        Success,
        NotInstalled,
        ExecutablePathInvalid,
        AlreadyApplied,
        CannotProcess,
        UnpackedFileMissing,
        FileReplaceFailed,
        Unexpected
    }
}
