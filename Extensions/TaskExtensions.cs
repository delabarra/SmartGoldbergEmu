using System;
using System.Threading.Tasks;
using SmartGoldbergEmu.Abstractions;

namespace SmartGoldbergEmu.Extensions
{
    public static class TaskExtensions
    {
        // Returns the same task so callers can write _ = FooAsync().ForgetFaults(...).

        public static Task ForgetFaults(this Task task, ILogService log, string context)
        {
            if (task == null)
                return Task.CompletedTask;
            AttachFaultObserver(task, log, context);
            return task;
        }

        public static Task<T> ForgetFaults<T>(this Task<T> task, ILogService log, string context)
        {
            if (task == null)
                return Task.FromResult(default(T));
            AttachFaultObserver(task, log, context);
            return task;
        }

        private static void AttachFaultObserver(Task task, ILogService log, string context)
        {
            task.ContinueWith(
                t =>
                {
                    if (!t.IsFaulted || t.Exception == null)
                        return;
                    var ex = t.Exception.InnerException ?? t.Exception.GetBaseException();
                    log?.LogError($"{context}: {ex.Message}", ex);
                },
                TaskScheduler.Default);
        }
    }
}
