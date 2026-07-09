namespace SmartGoldbergEmu.Constants
{
    /// <summary>
    /// URLs for default Goldberg assets hosted on gbe_fork (post_build template tree).
    /// Source: https://github.com/Detanup01/gbe_fork/tree/dev/post_build
    /// </summary>
    public static class AssetConstants
    {
        /// <summary>
        /// Base URL for GitHub raw downloads (github.com/.../raw/...).
        /// </summary>
        public const string GithubBaseUrl = "https://github.com/Detanup01/gbe_fork/raw/refs/heads/dev/post_build/steam_settings.EXAMPLE";

        /// <summary>
        /// Base URL for raw content (raw.githubusercontent.com).
        /// </summary>
        public const string GithubRawBaseUrl = "https://raw.githubusercontent.com/Detanup01/gbe_fork/refs/heads/dev/post_build/steam_settings.EXAMPLE";

        /// <summary>
        /// Inner zip path for Steam's desktop toast sound.
        /// Mapped to overlay_achievement_notification.wav.
        /// </summary>
        public const string SteamClientAchievementSoundInnerPath = "steamui/sounds/desktop_toast_default.wav";

        /// <summary>
        /// Inner zip path for Steam's recording highlight sound.
        /// Mapped to overlay_friend_notification.wav.
        /// </summary>
        public const string SteamClientFriendSoundInnerPath = "steamui/sounds/recording_highlight.wav";

        /// <summary>
        /// Font file: Roboto-Medium.
        /// </summary>
        public const string FontRobotoUrl = GithubBaseUrl + "/fonts.EXAMPLE/Roboto-Medium.ttf";

        /// <summary>
        /// Controller glyph paths (append filename to base).
        /// </summary>
        public const string ControllerGlyphsBaseUrl = GithubBaseUrl + "/controller.EXAMPLE/glyphs/";
    }
}
