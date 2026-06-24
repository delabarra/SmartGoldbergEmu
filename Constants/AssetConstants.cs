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
        /// Default account avatar image URL.
        /// </summary>
        public const string DefaultAvatarUrl = GithubRawBaseUrl + "/account_avatar.EXAMPLE.jpg";

        /// <summary>
        /// Sound file: achievement notification.
        /// </summary>
        public const string SoundAchievementUrl = GithubBaseUrl + "/sounds.EXAMPLE/overlay_achievement_notification.wav";

        /// <summary>
        /// Sound file: friend notification.
        /// </summary>
        public const string SoundFriendUrl = GithubBaseUrl + "/sounds.EXAMPLE/overlay_friend_notification.wav";

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
