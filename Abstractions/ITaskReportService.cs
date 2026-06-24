namespace SmartGoldbergEmu.Abstractions
{
    // High-level task state for status strip and progress UI; use ILogService for diagnostics.
    public interface ITaskReportService
    {
        void SetMessage(string message);
        void SetMessage(string message, TaskReportKind kind);
        void SetProgress(int current, int total);
        void SetMessageWithAutoClear(string message, TaskReportKind kind = TaskReportKind.Info, int delayMs = 3000);
    }

    public enum TaskReportKind
    {
        Info,
        Warning,
        Error
    }
}
