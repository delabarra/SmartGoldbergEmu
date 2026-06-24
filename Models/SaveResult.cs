namespace SmartGoldbergEmu.Models
{
    /// <summary>
    /// Represents the result of a save operation.
    /// </summary>
    public class SaveResult
    {
        /// <summary>
        /// Gets whether the save operation was successful.
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// Gets the error message if the save failed, or null if successful.
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// Gets the number of settings that were successfully saved.
        /// </summary>
        public int SettingsSaved { get; }

        /// <summary>
        /// Gets the number of settings that failed to save.
        /// </summary>
        public int SettingsFailed { get; }

        private SaveResult(bool isSuccess, string errorMessage, int settingsSaved = 0, int settingsFailed = 0)
        {
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
            SettingsSaved = settingsSaved;
            SettingsFailed = settingsFailed;
        }

        /// <summary>
        /// Creates a successful save result.
        /// </summary>
        public static SaveResult Success(int settingsSaved = 0) => new SaveResult(true, null, settingsSaved, 0);

        /// <summary>
        /// Creates a failed save result with an error message.
        /// </summary>
        public static SaveResult Failure(string errorMessage, int settingsSaved = 0, int settingsFailed = 0) => 
            new SaveResult(false, errorMessage, settingsSaved, settingsFailed);
    }
}
