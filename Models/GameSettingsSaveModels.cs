using System;
using System.Threading.Tasks;
using SmartGoldbergEmu.Abstractions;

namespace SmartGoldbergEmu.Models
{
    public sealed class GameSettingsSaveRequest
    {
        public GameConfig GameConfig { get; set; }
        public bool IsEditMode { get; set; }
        public OnlineAppData Metadata { get; set; }
        public string CustomStatsRawJson { get; set; }
        public ITaskReportService TaskReportService { get; set; }
        public Func<GameSettingsSnapshot> BuildSnapshot { get; set; }
        public Func<GameSettingsSnapshot, string> ResolveAchievementLanguage { get; set; }
        public Action SaveDlcAndPaths { get; set; }
        public Action SaveAdditionalGoldbergFiles { get; set; }
        public Action OnAssetsDownloaded { get; set; }
        public Action OnSuccessfulSaveCompleted { get; set; }

        /// <summary>
        /// When true, child save work must not change the status strip (add-game save uses fixed messages only).
        /// </summary>
        public bool SuppressStatusMessages { get; set; }
    }

    public sealed class GameSettingsSaveResult
    {
        public bool IsSuccess { get; }
        public bool HasCustomStatsJsonError { get; }
        public string ErrorMessage { get; }

        private GameSettingsSaveResult(bool isSuccess, bool hasCustomStatsJsonError, string errorMessage)
        {
            IsSuccess = isSuccess;
            HasCustomStatsJsonError = hasCustomStatsJsonError;
            ErrorMessage = errorMessage ?? string.Empty;
        }

        public static GameSettingsSaveResult Success() =>
            new GameSettingsSaveResult(isSuccess: true, hasCustomStatsJsonError: false, errorMessage: string.Empty);

        public static GameSettingsSaveResult Failure(string errorMessage) =>
            new GameSettingsSaveResult(isSuccess: false, hasCustomStatsJsonError: false, errorMessage: errorMessage);

        public static GameSettingsSaveResult InvalidCustomStatsJson() =>
            new GameSettingsSaveResult(isSuccess: false, hasCustomStatsJsonError: true, errorMessage: "Invalid custom stats JSON.");
    }
}
