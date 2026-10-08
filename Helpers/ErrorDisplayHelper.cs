using System;

namespace SmartGoldbergEmu.Helpers
{
    public static class ErrorDisplayHelper
    {
        private const int MaxUserMessageLength = 60;

        // Hides paths and long technical messages from the user; log the full exception separately.
        public static string SanitizeForUser(string context, Exception ex)
        {
            var msg = ex?.Message ?? string.Empty;
            if (string.IsNullOrWhiteSpace(msg))
                return $"{context} failed. See log for details.";
            if (msg.Contains("\\") || msg.Contains("/") || msg.Length > MaxUserMessageLength)
                return $"{context} failed. See log for details.";
            return $"{context} failed: {msg.Trim()}";
        }
    }
}
