using System;
using SmartGoldbergEmu.Abstractions;

namespace SmartGoldbergEmu.Tests.Fakes
{
    public sealed class NullLogService : ILogService
    {
        public void LogDebug(string message)
        {
        }

        public void LogMessage(string message)
        {
        }

        public void LogWarning(string message)
        {
        }

        public void LogError(string message)
        {
        }

        public void LogError(string message, Exception exception)
        {
        }
    }
}
