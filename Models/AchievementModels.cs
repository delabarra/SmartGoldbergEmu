namespace SmartGoldbergEmu.Models
{
    public sealed class AchievementPreviewData
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string IconPath { get; set; }
        public string GrayIconPath { get; set; }
        public bool IsUnlocked { get; set; }
        public bool IsHidden { get; set; }
    }

    public sealed class AchievementPreviewTextResult
    {
        public string RevealKey { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Tooltip { get; set; }
    }

    /// <summary>
    /// Reason for creating a dummy achievement
    /// </summary>
    public enum DummyAchievementReason
    {
        /// <summary>
        /// No Steam WebAPI key is configured
        /// </summary>
        NoApiKey,

        /// <summary>
        /// Game has no achievements on Steam
        /// </summary>
        NoAchievements
    }
}
