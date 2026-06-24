using System;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Services
{
    // Parsing and URI creation only; registry install lives in UriProtocolRegistryService.
    public static class UriProtocolService
    {
        public static bool IsValidUri(string argument)
        {
            if (string.IsNullOrWhiteSpace(argument))
                return false;
            return argument.StartsWith(ApplicationConstants.UriProtocolAuthorityPrefix, StringComparison.OrdinalIgnoreCase);
        }

        public static UriParseResult ParseRunCommand(string uriArgument)
        {
            // Check for null/empty
            if (string.IsNullOrWhiteSpace(uriArgument))
                return UriParseResult.FailureResult("URI argument is empty");

            // Normalize to lowercase
            string normalizedUri = uriArgument.ToLowerInvariant();

            if (!normalizedUri.StartsWith(ApplicationConstants.UriProtocolCommandPrefix))
                return UriParseResult.FailureResult($"Invalid URI. Expected prefix '{ApplicationConstants.UriProtocolCommandPrefix}'");

            string appIdString = normalizedUri.Substring(ApplicationConstants.UriProtocolCommandPrefix.Length);
            if (string.IsNullOrWhiteSpace(appIdString))
                return UriParseResult.FailureResult($"AppID is missing. Expected format: {ApplicationConstants.UriProtocolCommandPrefix}appid");

            // Parse App ID
            if (!ulong.TryParse(appIdString, out ulong appId))
                return UriParseResult.FailureResult($"Invalid AppID '{appIdString}'. AppID must be a valid number.");

            // AppId must be a valid non-zero Steam app id.
            if (appId == 0)
                return UriParseResult.FailureResult("AppID must be a valid non-zero Steam App ID.");

            return UriParseResult.SuccessResult(appId);
        }

        public static string CreateRunUri(ulong appId)
        {
            return $"{ApplicationConstants.UriProtocolCommandPrefix}{appId}";
        }
    }
}
