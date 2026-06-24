namespace SmartGoldbergEmu.Models
{
    /// <summary>
    /// Represents the result of parsing a URI.
    /// </summary>
    public class UriParseResult
    {
        /// <summary>
        /// Gets whether the parsing succeeded.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// Gets whether the parsing was successful (alias for Success for consistency with other result types).
        /// </summary>
        public bool IsSuccess => Success;

        /// <summary>
        /// Gets the parsed App ID if successful, or 0 if failed.
        /// </summary>
        public ulong AppId { get; }

        /// <summary>
        /// Gets the error message if parsing failed, or null if successful.
        /// </summary>
        public string ErrorMessage { get; }

        private UriParseResult(bool success, ulong appId, string errorMessage)
        {
            Success = success;
            AppId = appId;
            ErrorMessage = errorMessage;
        }

        /// <summary>
        /// Creates a successful parse result.
        /// </summary>
        public static UriParseResult SuccessResult(ulong appId) => new UriParseResult(true, appId, null);

        /// <summary>
        /// Creates a failed parse result with an error message.
        /// </summary>
        public static UriParseResult FailureResult(string errorMessage) => new UriParseResult(false, 0, errorMessage);
    }
}

