namespace SmartGoldbergEmu.Models
{
    public class SaveResult
    {
        public bool IsSuccess { get; }

        public string ErrorMessage { get; }

        public int SettingsSaved { get; }

        public int SettingsFailed { get; }

        private SaveResult(bool isSuccess, string errorMessage, int settingsSaved = 0, int settingsFailed = 0)
        {
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
            SettingsSaved = settingsSaved;
            SettingsFailed = settingsFailed;
        }

        public static SaveResult Success(int settingsSaved = 0) => new SaveResult(true, null, settingsSaved, 0);

        public static SaveResult Failure(string errorMessage, int settingsSaved = 0, int settingsFailed = 0) => 
            new SaveResult(false, errorMessage, settingsSaved, settingsFailed);
    }
}
