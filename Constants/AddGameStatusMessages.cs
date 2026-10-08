namespace SmartGoldbergEmu.Constants
{
    // User-facing status strip text for add-game collect and save; one message per pipeline step.
    public static class AddGameStatusMessages
    {
        public static string FetchingMetadata(ulong appId, string gameName = null)
        {
            string name = string.IsNullOrWhiteSpace(gameName) ? null : gameName.Trim();
            if (name != null)
                return $"Fetching metadata for {name} (appid {appId})…";
            return $"Fetching metadata for appid {appId}…";
        }

        public static string ConnectingToSteam =>
            "Connecting to Steam…";

        public static string PreparingSave(string gameName) =>
            $"Preparing {gameName} settings for save…";

        public static string AddingToLibrary(string gameName) =>
            $"Adding {gameName} to the library.";

        public static string UpdatingInLibrary(string gameName) =>
            $"Updating {gameName} in the library.";

        public static string SavingCatalogSnapshot(string gameName) =>
            $"Saving {gameName} catalog snapshot…";

        public static string GeneratingGoldbergFiles(string gameName) =>
            $"Generating {gameName} files for Goldberg";

        public static string CreatingDefaultConfigFiles(string gameName) =>
            $"Creating {gameName} default Goldberg config files…";

        public static string GeneratingMetadataFiles(string gameName) =>
            $"Generating {gameName} metadata files (branches, stats, installed app IDs)…";

        public static string WritingSteamAppIdFile(string gameName) =>
            $"Writing {PathConstants.SteamAppIdFileName} beside the {gameName} executable…";

        public static string SavingEmulatorSettings(string gameName) =>
            $"Saving {gameName} emulator settings…";

        public static string SavingDlcAndPaths(string gameName) =>
            $"Saving {gameName} DLC list and app paths…";

        public static string SavingCustomStats(string gameName) =>
            $"Saving {gameName} custom stats…";

        public static string SavingAdditionalFiles(string gameName) =>
            $"Saving {gameName} additional Goldberg files…";

        public static string GeneratingAchievements(string gameName) =>
            $"Generating {gameName} achievements…";

        public static string FetchingAchievementSchema(string gameName) =>
            $"Fetching {gameName} achievement schema from Steam…";

        public static string CreatingPlaceholderAchievement(string gameName) =>
            $"Creating {gameName} placeholder achievement…";

        public static string DownloadingAchievementIcons(string gameName, int current, int total) =>
            $"Downloading {gameName} achievement icons {current}/{total}";

        public static string WritingAchievementsFile(string gameName) =>
            $"Writing {gameName} achievements file…";

        public static string WaitingForLibraryImages(string gameName) =>
            $"Waiting for {gameName} library images…";

        // Background library image download; shown only while no pipeline step owns the strip text.
        public static string DownloadingLibraryImages(int completed, int total) =>
            $"Downloading library images {completed}/{total}";

        public static string SavingCredentials(string gameName) =>
            $"Saving {gameName} ticket and alternate SteamID…";

        public static string AddedToLibrary(string gameName) =>
            $"{gameName} added to the library.";

        public static string UpdatedInLibrary(string gameName) =>
            $"{gameName} updated in the library.";

        public static string MetadataFetchFailed =>
            "Could not fetch app data from Steam.";

        public static string MetadataFetchTimedOut =>
            "Steam timed out while fetching app data. Try again.";
    }
}
