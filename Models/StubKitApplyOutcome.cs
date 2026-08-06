namespace SmartGoldbergEmu.Models
{
    public enum StubKitApplyOutcome
    {
        Success,
        Restored,
        ExecutablePathInvalid,
        NoStubFound,
        CannotRemove,
        UnpackFailed,
        FileReplaceFailed,
        BackupMissing,
        RestoreFailed,
        Unexpected
    }
}
