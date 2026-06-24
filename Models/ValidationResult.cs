namespace SmartGoldbergEmu.Models
{
    /// <summary>
    /// Represents the result of a validation operation.
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// Gets whether the validation passed.
        /// </summary>
        public bool IsValid { get; }

        /// <summary>
        /// Gets whether the validation was successful (alias for IsValid for consistency with other result types).
        /// </summary>
        public bool IsSuccess => IsValid;

        /// <summary>
        /// Gets the error message if validation failed, or null if valid.
        /// </summary>
        public string ErrorMessage { get; }

        private ValidationResult(bool isValid, string errorMessage)
        {
            IsValid = isValid;
            ErrorMessage = errorMessage;
        }

        /// <summary>
        /// Creates a successful validation result.
        /// </summary>
        public static ValidationResult Success() => new ValidationResult(true, null);

        /// <summary>
        /// Creates a failed validation result with an error message.
        /// </summary>
        public static ValidationResult Failure(string errorMessage) => new ValidationResult(false, errorMessage);
    }
}

