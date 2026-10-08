namespace SmartGoldbergEmu.Constants
{
    public static class AssetConstants
    {
        public const string GithubBaseUrl = "https://github.com/Detanup01/gbe_fork/raw/refs/heads/dev/post_build/steam_settings.EXAMPLE";

        public const string GithubRawBaseUrl = "https://raw.githubusercontent.com/Detanup01/gbe_fork/refs/heads/dev/post_build/steam_settings.EXAMPLE";

        // Steam zip entry installed as overlay_achievement_notification.wav.
        public const string SteamClientAchievementSoundInnerPath = "steamui/sounds/desktop_toast_default.wav";

        // Steam zip entry installed as overlay_friend_notification.wav.
        public const string SteamClientFriendSoundInnerPath = "steamui/sounds/recording_highlight.wav";

        public const string AccountAvatarUrl = GithubBaseUrl + "/account_avatar.jpg";

        public const string FontRobotoUrl = GithubBaseUrl + "/fonts.EXAMPLE/Roboto-Medium.ttf";

        public const string ControllerGlyphsBaseUrl = GithubBaseUrl + "/controller.EXAMPLE/glyphs/";
    }
}
