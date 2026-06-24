using System;
using System.Diagnostics;
using System.IO;

namespace SmartGoldbergEmu.Helpers
{
    public static class DirectoryJunctionHelper
    {
        public static bool IsDirectoryReparsePoint(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                return false;
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;
        }

        public static void RemoveLinkIfPresent(string linkPath)
        {
            if (!Directory.Exists(linkPath))
                return;
            Directory.Delete(linkPath, recursive: !IsDirectoryReparsePoint(linkPath));
        }

        public static bool TryCreateDirectoryJunction(string junctionPath, string targetPath, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(junctionPath) || string.IsNullOrWhiteSpace(targetPath))
            {
                error = "Junction or target path is empty.";
                return false;
            }

            if (!Directory.Exists(targetPath))
            {
                error = "Target directory does not exist.";
                return false;
            }

            if (Directory.Exists(junctionPath) || File.Exists(junctionPath))
            {
                error = "Junction path already exists.";
                return false;
            }

            string junctionFull = Path.GetFullPath(junctionPath);
            string targetFull = Path.GetFullPath(targetPath);
            if (string.Equals(junctionFull, targetFull, StringComparison.OrdinalIgnoreCase))
            {
                error = "Junction path cannot equal the target.";
                return false;
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c mklink /J \"" + junctionFull + "\" \"" + targetFull + "\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                    {
                        error = "Failed to start cmd.exe for mklink.";
                        return false;
                    }

                    string stdOut = process.StandardOutput.ReadToEnd();
                    string stdErr = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                    {
                        error = string.IsNullOrWhiteSpace(stdErr) ? stdOut.Trim() : stdErr.Trim();
                        if (string.IsNullOrWhiteSpace(error))
                            error = "mklink /J exited with code " + process.ExitCode;
                        return false;
                    }
                }

                if (!Directory.Exists(junctionFull) || !IsDirectoryReparsePoint(junctionFull))
                {
                    error = "mklink reported success but the junction path is missing or not a reparse point.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}
