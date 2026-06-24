using System;
using SmartGoldbergEmu.Constants;

namespace SmartGoldbergEmu.Models
{
    /// <summary>
    /// Represents a launch option for a game, extracted from Steam app product info (PICS KeyValue tree).
    /// </summary>
    public class LaunchOption
    {
        /// <summary>
        /// Gets or sets the description of the launch option (e.g., "Default", "Game", "Editor").
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets the executable path or name for this launch option.
        /// </summary>
        public string Executable { get; set; }

        /// <summary>
        /// Gets or sets the type of launch option (e.g., "default", "config", "betakey").
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets the launch parameters/arguments for this launch option.
        /// If set, these parameters will override the game's default parameters.
        /// </summary>
        public string Parameters { get; set; }

        /// <summary>
        /// Gets or sets the working directory for this launch option.
        /// If set, this working directory will override the game's default working directory.
        /// This is relative to the base game folder.
        /// </summary>
        public string WorkingDir { get; set; }

        /// <summary>
        /// Steam branch from PICS <c>config/BetaKey</c> when present; indicates a beta/dev depot branch.
        /// </summary>
        public string BetaKey { get; set; }

        /// <summary>
        /// PICS <c>config/osarch</c> when present (e.g. 32, 64, arm64). Used to restrict the entry to matching host architecture.
        /// </summary>
        public string OsArch { get; set; }

        /// <summary>
        /// When <see cref="ApplicationSettings.FullLaunchOptions"/> is off, these entries are hidden and excluded from auto-pick;
        /// launch falls back to the game's settings executable if nothing remains after filtering.
        /// </summary>
        public bool IsHiddenWhenFullLaunchOptionsOff()
        {
            // User-defined options should stay visible in the UI, but still get the "[user]" tag.
            // Tag rendering is based on this "hidden when full launch options are off" flag,
            // so we intentionally mark user options as hidden for tag purposes.
            if (!string.IsNullOrEmpty(Type) && Type.Equals(SteamPicsKeyNames.LaunchOptionTypeUser, StringComparison.OrdinalIgnoreCase))
                return true;

            if (!string.IsNullOrEmpty(BetaKey))
                return true;
            return IsRestrictedLaunchOptionType(Type);
        }

        /// <summary>
        /// PICS <c>type</c> values treated as config/beta/dev tooling when full launch options are off.
        /// </summary>
        public static bool IsRestrictedLaunchOptionType(string type)
        {
            if (string.IsNullOrEmpty(type))
                return false;
            return type.Equals(SteamPicsKeyNames.LaunchOptionTypeConfig, StringComparison.OrdinalIgnoreCase) ||
                   type.Equals(SteamPicsKeyNames.LaunchOptionTypeBetaKey, StringComparison.OrdinalIgnoreCase) ||
                   type.Equals(SteamPicsKeyNames.LaunchOptionTypeBeta, StringComparison.OrdinalIgnoreCase) ||
                   type.Equals(SteamPicsKeyNames.LaunchOptionTypeDev, StringComparison.OrdinalIgnoreCase) ||
                   type.Equals(SteamPicsKeyNames.LaunchOptionTypeDeveloper, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Sets <see cref="AppSettings.IsBetaBranch"/> and <see cref="AppSettings.BranchName"/> from this PICS launch option
        /// (<c>config/BetaKey</c> and <c>type</c>), matching Game Settings when a Steam launch option is applied.
        /// </summary>
        public static void ApplyBetaBranchToAppSettings(LaunchOption opt, AppSettings app)
        {
            if (opt == null || app == null)
                return;

            // Custom user launch options are explicit executable/arguments overrides and
            // should not force branch/app settings changes.
            if (!string.IsNullOrEmpty(opt.Type) && opt.Type.Equals(SteamPicsKeyNames.LaunchOptionTypeUser, StringComparison.OrdinalIgnoreCase))
                return;

            if (!string.IsNullOrEmpty(opt.BetaKey))
            {
                app.IsBetaBranch = true;
                app.BranchName = opt.BetaKey.Trim();
                return;
            }

            string t = opt.Type != null ? opt.Type.Trim() : string.Empty;
            if (t.Equals(SteamPicsKeyNames.LaunchOptionTypeBetaKey, StringComparison.OrdinalIgnoreCase) ||
                t.Equals(SteamPicsKeyNames.LaunchOptionTypeBeta, StringComparison.OrdinalIgnoreCase))
            {
                app.IsBetaBranch = true;
                app.BranchName = SteamPicsKeyNames.SteamDefaultBranchName;
                return;
            }

            app.IsBetaBranch = false;
            app.BranchName = SteamPicsKeyNames.SteamDefaultBranchName;
        }
    }
}

