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
        // A *_o.exe backup already exists; refuse to overwrite it during session unpack.
        ExistingBackupConflict,
        RestoreFailed,
        Unexpected
    }
}
