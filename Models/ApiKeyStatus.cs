namespace SmartGoldbergEmu.Models
{
    /// <summary>
    /// Represents the status of the Steam API key.
    /// </summary>
    public class ApiKeyStatus
    {
        /// <summary>
        /// Gets or sets whether an API key is configured.
        /// </summary>
        public bool HasKey { get; set; }

        /// <summary>
        /// Gets or sets whether the API key has a valid format (32 characters).
        /// </summary>
        public bool HasValidFormat { get; set; }

        /// <summary>
        /// Gets or sets whether the API key is valid (format and network validation passed).
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets or sets the error message if validation failed.
        /// </summary>
        public string ErrorMessage { get; set; }
    }
}

