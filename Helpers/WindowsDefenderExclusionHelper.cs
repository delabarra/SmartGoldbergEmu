using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace SmartGoldbergEmu.Helpers
{
    // Batches Add/Remove-MpPreference ExclusionPath in one elevated PowerShell (one UAC).
    public static class WindowsDefenderExclusionHelper
    {
        public static string BuildMpPreferenceScript(IReadOnlyList<string> paths, bool add)
        {
            if (paths == null || paths.Count == 0)
                throw new ArgumentException("At least one exclusion path is required.", nameof(paths));

            var sb = new StringBuilder();
            string cmd = add ? "Add-MpPreference" : "Remove-MpPreference";
            for (int i = 0; i < paths.Count; i++)
            {
                string safePath = SanitizeExclusionPath(paths[i]);
                if (i > 0)
                    sb.Append("; ");
                sb.Append(cmd).Append(" -ExclusionPath '").Append(safePath).Append('\'');
            }

            return sb.ToString();
        }

        public static bool TryApplyExclusions(IReadOnlyList<string> paths, bool add)
        {
            try
            {
                string inner = BuildMpPreferenceScript(paths, add);
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"" + inner + "\"",
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using (var process = Process.Start(psi))
                {
                    if (process == null)
                        return false;
                    process.WaitForExit();
                    return process.ExitCode == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        public static string SanitizeExclusionPath(string path)
        {
            string safePath = (path ?? string.Empty).Replace("'", "''");
            if (safePath.IndexOfAny(new[] { '\r', '\n', '`', ';', '|', '&' }) >= 0)
                throw new InvalidOperationException("Defender exclusion path contains invalid characters.");
            return safePath;
        }
    }
}
