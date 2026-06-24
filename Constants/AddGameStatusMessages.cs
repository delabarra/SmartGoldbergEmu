namespace SmartGoldbergEmu.Constants
{
    /// <summary>
    /// User-facing status strip text for add-game collect and save (no other messages during these flows).
    /// </summary>
    public static class AddGameStatusMessages
    {
        /// <summary>Max time add-game status feedback stays on the strip before auto-clear.</summary>
        public const int StatusAutoClearDelayMs = 3000;

        public static string LookingUpData(ulong appId) =>
            $"Looking up data for {appId} in Steam Network";

        public static string RetrievingData(string gameName) =>
            $"Retrieving data for {gameName} from Steam Network";

        public static string WaitingToPreview(string gameName) =>
            $"Awaiting preview of {gameName} data...";

        public static string GeneratingGoldbergFiles(string gameName) =>
            $"Generating {gameName} files for Goldberg";

        public static string DownloadingAchievementIcons(string gameName, int current, int total) =>
            $"Downloading {gameName} achievement icons {current}/{total}";

        public static string AddedToLibrary(string gameName) =>
            $"{gameName} added to the library.";
    }
}
