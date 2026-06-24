using System;

namespace SmartGoldbergEmu.Helpers
{
    /// <summary>
    /// Helper for standardizing error display: log full details, show sanitized message to user.
    /// </summary>
    public static class ErrorDisplayHelper
    {
        private const int MaxUserMessageLength = 60;

        /// <summary>
        /// Returns a user-safe error message. Avoids exposing paths or overly technical details.
        /// </summary>
        /// <param name="context">Short context (e.g. "Adding game", "Saving settings").</param>
        /// <param name="ex">The exception, or null.</param>
        /// <returns>Sanitized message for status strip or similar.</returns>
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
