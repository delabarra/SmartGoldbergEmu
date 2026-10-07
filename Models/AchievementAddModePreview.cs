using AppDataKit;

namespace SmartGoldbergEmu.Models
{
    public sealed class AchievementAddModePreview
    {
        public AchievementPreviewKind Kind { get; set; }

        // Preview rows for the settings dialog (icon URLs stripped).
        public string PreviewJson { get; set; }

        // Set only for RealList: the schema the preview was built from, with icon URLs intact.
        public AchievementsSection Schema { get; set; }
        public string Language { get; set; }
    }
}
