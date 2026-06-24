namespace SmartGoldbergEmu.Constants
{
    // games.ini per-game section — keys as written by GameDataService.SaveGameLibrary (PascalCase) and normalized keys used when parsing (lowercase). "appid" matches SteamAppManifestAcfKeys.AppId spelling only (different format).
    public static class GamesIniKeyNames
    {
        public const string GameSectionPrefix = "Game";

        public const string AppName = "appname";
        public const string AppId = "appid";
        public const string StartFolder = "startfolder";
        public const string Path = "path";
        public const string Parameters = "parameters";
        public const string WorkingDirectory = "workingdirectory";
        public const string CustomIcon = "customicon";
        public const string GameGuid = "gameguid";
        public const string GoldbergLaunchMode = "goldberglaunchmode";

        public const string AppNameWrite = "AppName";
        public const string AppIdWrite = "AppId";
        public const string StartFolderWrite = "StartFolder";
        public const string PathWrite = "Path";
        public const string ParametersWrite = "Parameters";
        public const string WorkingDirectoryWrite = "WorkingDirectory";
        public const string CustomIconWrite = "CustomIcon";
        public const string GameGuidWrite = "GameGuid";
        public const string GoldbergLaunchModeWrite = "GoldbergLaunchMode";
    }
}
