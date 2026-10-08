namespace SmartGoldbergEmu.Models
{
    public class UriParseResult
    {
        public bool Success { get; }

        // Alias for Success, matching other result types.
        public bool IsSuccess => Success;

        // 0 when parsing failed.
        public ulong AppId { get; }

        public string ErrorMessage { get; }

        private UriParseResult(bool success, ulong appId, string errorMessage)
        {
            Success = success;
            AppId = appId;
            ErrorMessage = errorMessage;
        }

        public static UriParseResult SuccessResult(ulong appId) => new UriParseResult(true, appId, null);

        public static UriParseResult FailureResult(string errorMessage) => new UriParseResult(false, 0, errorMessage);
    }
}

