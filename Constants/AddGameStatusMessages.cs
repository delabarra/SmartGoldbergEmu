namespace SmartGoldbergEmu.Constants
{
    // User-facing status strip text for add-game collect and save (no other messages during these flows).
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

        public static string GeneratingGoldbergFiles(string gameName) =>
            $"Generating {gameName} files for Goldberg";

        public static string DownloadingAchievementIcons(string gameName, int current, int total) =>
            $"Downloading {gameName} achievement icons {current}/{total}";

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
