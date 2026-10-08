namespace SmartGoldbergEmu.Models
{
    public class ApiKeyStatus
    {
        public bool HasKey { get; set; }

        // Format check only (32 characters).
        public bool HasValidFormat { get; set; }

        // Format and network validation both passed.
        public bool IsValid { get; set; }

        public string ErrorMessage { get; set; }
    }
}

