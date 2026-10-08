using System;
using AppDataKit;

namespace SmartGoldbergEmu.Models
{
    // Steam schema sections fetched during add-game collect; add-save writes items.json / achievements.json from them instead of fetching again.
    public sealed class AddGamePrefetchedSchemas
    {
        public ulong AppId { get; set; }
        public ItemsSection Items { get; set; }

        // Full schema (with icon URLs) captured for the add-mode achievement preview.
        public AchievementsSection Achievements { get; set; }

        // Achievement display names and descriptions are localized; reuse only when the save language matches.
        public string AchievementsLanguage { get; set; }

        // Null when the dialog switched to another App ID, so the caller fetches live for the new app.
        public ItemsSection GetItemsFor(ulong appId)
        {
            if (appId == 0 || appId != AppId)
                return null;
            return Items;
        }

        public AchievementsSection GetAchievementsFor(ulong appId, string language)
        {
            if (appId == 0 || appId != AppId || Achievements == null)
                return null;
            if (!string.Equals(AchievementsLanguage, language, StringComparison.OrdinalIgnoreCase))
                return null;
            return Achievements;
        }
    }
}
