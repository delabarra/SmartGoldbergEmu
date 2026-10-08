namespace SmartGoldbergEmu.Models
{
    public class ValidationResult
    {
        public bool IsValid { get; }

        // Alias for IsValid, matching other result types.
        public bool IsSuccess => IsValid;

        public string ErrorMessage { get; }

        private ValidationResult(bool isValid, string errorMessage)
        {
            IsValid = isValid;
            ErrorMessage = errorMessage;
        }

        public static ValidationResult Success() => new ValidationResult(true, null);

        public static ValidationResult Failure(string errorMessage) => new ValidationResult(false, errorMessage);
    }
}

