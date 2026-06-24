using System;

namespace SmartGoldbergEmu.Abstractions
{
    public interface ILogService
    {
        void LogDebug(string message);
        void LogMessage(string message);
        void LogWarning(string message);
        void LogError(string message);
        void LogError(string message, Exception exception);
    }
}
