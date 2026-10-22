namespace SmartGoldbergEmu.Models
{
    public class ResolvedLaunchCommand
    {
        public string ExecutablePath { get; set; }
        public string Arguments { get; set; }
        public string WorkingDirectory { get; set; }
    }

    public class LaunchOptionResult
    {
        // Null when skipped or cancelled; when set, overrides executable, parameters, working dir, and configs.app.ini branch for this launch.
        public LaunchOption LaunchOption { get; set; }

        public bool SkipLauncher { get; set; }

        public bool Cancelled { get; set; }
    }
}
