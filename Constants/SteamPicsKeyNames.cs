namespace SmartGoldbergEmu.Constants
{
    // Steam PICS / KeyValue node names (SteamKit). App "common" section matches PathConstants.SteamAppsCommonDirectoryName.
    public static class SteamPicsKeyNames
    {
        public const string AppInfo = "appinfo";
        public const string Config = "config";
        public const string Extended = "extended";
        public const string Name = "name";
        public const string Type = "type";
        public const string SupportedLanguages = "supported_languages";
        public const string Languages = "languages";
        public const string Supported = "supported";
        public const string InstallDir = "installdir";
        public const string Dlc = "dlc";
        public const string ListOfDlc = "listofdlc";

        public const string Launch = "launch";
        public const string LaunchOverride = "launch_override";
        public const string Executable = "executable";
        public const string Arguments = "arguments";
        public const string CommandLine = "commandline";
        public const string Description = "description";
        public const string WorkingDir = "workingdir";
        public const string OsList = "oslist";

        public const string Stats = "stats";
        public const string Depots = "depots";
        public const string Leaderboards = "leaderboards";
        public const string Achievements = "achievements";
        public const string Branches = "branches";
        public const string BuildId = "buildid";
        public const string TimeUpdated = "timeupdated";
        public const string PwdRequired = "pwdrequired";
        public const string Default = "default";
        public const string Global = "global";
        public const string SortMethod = "sortmethod";
        public const string DisplayType = "displaytype";

        public const string Packages = "packages";
        public const string Subs = "subs";

        public const string LibraryAssetsFull = "library_assets_full";
        public const string LibraryCapsule = "library_capsule";
        public const string LibraryLogo = "library_logo";
        public const string Image = "image";
        public const string English = "english";
        public const string HeaderImage = "header_image";
        public const string Icon = "icon";
        public const string ClientIcon = "clienticon";

        // Launch entry -> config child names (PICS)
        public const string BetaKey = "BetaKey";
        public const string OsArch = "osarch";

        // Launch entry "type" field values (Steam appinfo). "default" matches stats child name constant Default.
        public const string LaunchOptionTypeUser = "user";
        public const string LaunchOptionTypeConfig = "config";
        public const string LaunchOptionTypeBetaKey = "betakey";
        public const string LaunchOptionTypeBeta = "beta";
        public const string LaunchOptionTypeDev = "dev";
        public const string LaunchOptionTypeDeveloper = "developer";
        public const string SteamDefaultBranchName = "public";
    }
}
