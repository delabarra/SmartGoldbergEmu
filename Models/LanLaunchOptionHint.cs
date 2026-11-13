using System;

namespace SmartGoldbergEmu.Models
{
    // Compact launch-option identity for LAN invite packets. Guest matches against local Steam/user options.
    public sealed class LanLaunchOptionHint
    {
        // Host used library Path (SkipLauncher / no option). Guest skips the picker and launches Path.
        public bool PreferLibraryPath { get; set; }

        public string Description { get; set; }

        public string Executable { get; set; }

        public string Type { get; set; }

        public string OsArch { get; set; }

        public string BetaKey { get; set; }

        public bool HasMatchableFields
        {
            get
            {
                if (PreferLibraryPath)
                    return true;
                return !string.IsNullOrWhiteSpace(Executable)
                    || !string.IsNullOrWhiteSpace(Description);
            }
        }

        public static LanLaunchOptionHint FromLaunchOption(LaunchOption option)
        {
            if (option == null)
            {
                return new LanLaunchOptionHint
                {
                    PreferLibraryPath = true
                };
            }

            return new LanLaunchOptionHint
            {
                PreferLibraryPath = false,
                Description = option.Description ?? string.Empty,
                Executable = option.Executable ?? string.Empty,
                Type = option.Type ?? string.Empty,
                OsArch = option.OsArch ?? string.Empty,
                BetaKey = option.BetaKey ?? string.Empty
            };
        }

        public static string NormalizeExecutableKey(string executable)
        {
            if (string.IsNullOrWhiteSpace(executable))
                return string.Empty;

            string value = executable.Trim().Replace('/', '\\');
            while (value.StartsWith(".\\", StringComparison.Ordinal))
                value = value.Substring(2);
            return value.TrimStart('\\');
        }
    }
}
