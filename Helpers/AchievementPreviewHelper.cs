using System.Collections.Generic;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.JsonKit;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Helpers
{
    /// <summary>
    /// In-memory achievement preview JSON for add-game (before save writes disk).
    /// </summary>
    public static class AchievementPreviewHelper
    {
        public static (string name, string displayName, string description) GetDummyFields(DummyAchievementReason reason)
        {
            return GetDummyText(reason);
        }

        public static string BuildDummyPreviewJson(DummyAchievementReason reason)
        {
            var (name, displayName, description) = GetDummyText(reason);
            var achievement = new CAchievement
            {
                name = name,
                displayName = displayName,
                description = description,
                hidden = 0,
                icon = string.Empty,
                icongray = string.Empty,
                icon_gray = string.Empty
            };
            return JsonConvert.SerializeObject(new List<CAchievement> { achievement }, JsonFormatting.Indented);
        }

        private static (string name, string displayName, string description) GetDummyText(DummyAchievementReason reason)
        {
            if (reason == DummyAchievementReason.NoApiKey)
            {
                return (
                    AchievementConstants.NoApiKeyAchievementName,
                    AchievementConstants.NoApiKeyAchievementDisplayName,
                    AchievementConstants.NoApiKeyAchievementDescription);
            }

            return (
                AchievementConstants.NoAchievementsAchievementName,
                AchievementConstants.NoAchievementsAchievementDisplayName,
                AchievementConstants.NoAchievementsAchievementDescription);
        }
    }
}
