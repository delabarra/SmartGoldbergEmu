using System;
using SmartGoldbergEmu.Constants;

namespace SmartGoldbergEmu.Models
{
    // Launch option from Steam PICS product info, or a user-defined entry.
    public class LaunchOption
    {
        public string Description { get; set; }

        public string Executable { get; set; }

        public string Type { get; set; }

        // Overrides the game's parameters when set.
        public string Parameters { get; set; }

        // Relative to the base game folder; overrides the game's working directory when set.
        public string WorkingDir { get; set; }

        // PICS config/BetaKey; marks a beta/dev depot branch.
        public string BetaKey { get; set; }

        // PICS config/osarch (e.g. 32, 64, arm64); restricts the entry to a matching host architecture.
        public string OsArch { get; set; }

        // Hidden and excluded from auto-pick when FullLaunchOptions is off.
        public bool IsHiddenWhenFullLaunchOptionsOff()
        {
            // User options stay visible in the UI; returning true here only drives the "[user]" tag.
            if (!string.IsNullOrEmpty(Type) && Type.Equals(SteamPicsKeyNames.LaunchOptionTypeUser, StringComparison.OrdinalIgnoreCase))
                return true;

            if (!string.IsNullOrEmpty(BetaKey))
                return true;
            return IsRestrictedLaunchOptionType(Type);
        }

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

        // Must match what Game Settings writes when a Steam launch option is applied.
        public static void ApplyBetaBranchToAppSettings(LaunchOption opt, AppSettings app)
        {
            if (opt == null || app == null)
                return;

            // User launch options override only executable and arguments; they never change the branch.
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

