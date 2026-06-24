using System;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace SmartGoldbergEmu.Helpers
{
    // Redacts paths, Steam IDs, and API keys before log output is written or shown on the console.
    public static class LogRedactionHelper
    {
        private static readonly Regex WindowsDrivePath =
            new Regex(@"(?<![A-Za-z0-9_])([A-Za-z]:\\(?:[^\\/\s""<>|]+\\)*[^\\/\s""<>|]+)", RegexOptions.Compiled);

        private static readonly Regex UncPath =
            new Regex(@"(\\\\[^\\/\s""<>|]+(?:\\[^\\/\s""<>|]+)+)", RegexOptions.Compiled);

        private static readonly Regex UsersProfileSegment =
            new Regex(@"(\\Users\\)[^\\]+(?=\\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex SteamId64 =
            new Regex(@"\b7656119[0-9]{10}\b", RegexOptions.Compiled);

        private static readonly Regex UrlApiKeyQuery =
            new Regex(@"(?<prefix>[?&](?:key|apikey|api_key)=)[^&\s""]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private const string RedactedPath = "<path>";
        private const string RedactedUser = "<user>";
        private const string RedactedSteamId = "<steamid>";
        private const string RedactedSecret = "***";

        public static string RedactForLog(string message)
        {
            if (string.IsNullOrEmpty(message))
                return message;

            string result = message;
            result = UsersProfileSegment.Replace(result, "$1" + RedactedUser);
            result = UncPath.Replace(result, RedactedPath);
            result = WindowsDrivePath.Replace(result, RedactedPath);
            result = SteamId64.Replace(result, RedactedSteamId);
            result = UrlApiKeyQuery.Replace(result, "$1" + RedactedSecret);
            return result;
        }

        public static string RedactApiKey(string message, string apiKey)
        {
            if (string.IsNullOrEmpty(message) || string.IsNullOrEmpty(apiKey))
                return RedactForLog(message);

            string escaped = Uri.EscapeDataString(apiKey);
            return RedactForLog(message)
                .Replace(apiKey, RedactedSecret)
                .Replace(escaped, RedactedSecret);
        }

        // Visual Studio debug output only; redacted like file log (not ILogService).
        public static void WriteDebug(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;
            Debug.WriteLine(RedactForLog(message));
        }
    }
}
