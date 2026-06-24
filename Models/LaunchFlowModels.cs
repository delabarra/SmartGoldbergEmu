namespace SmartGoldbergEmu.Models
{
    public class ResolvedLaunchCommand
    {
        public string ExecutablePath { get; set; }
        public string Arguments { get; set; }
        public string WorkingDirectory { get; set; }

        public string ToCommandLine()
        {
            var exe = string.IsNullOrEmpty(ExecutablePath) ? "" : (ExecutablePath.Contains(" ") ? "\"" + ExecutablePath + "\"" : ExecutablePath);
            var args = Arguments ?? "";
            return string.IsNullOrEmpty(exe) ? args : (string.IsNullOrEmpty(args) ? exe : exe + " " + args);
        }
    }

    /// <summary>
    /// Result from showing launch options form.
    /// When LaunchOption is set, it overrides the game launch with Executable, Parameters, WorkingDir, and beta branch for that launch.
    /// </summary>
    public class LaunchOptionResult
    {
        /// <summary>
        /// Gets or sets the selected launch option, or null if skipped or cancelled.
        /// When set, overrides the game launch: Executable (process path), Parameters (arguments), WorkingDir, and configs.app.ini branch fields.
        /// </summary>
        public LaunchOption LaunchOption { get; set; }

        /// <summary>
        /// Gets or sets whether the user chose to skip the launcher.
        /// </summary>
        public bool SkipLauncher { get; set; }

        /// <summary>
        /// Gets or sets whether the user cancelled the dialog.
        /// </summary>
        public bool Cancelled { get; set; }
    }
}
