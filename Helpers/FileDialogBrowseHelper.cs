using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace SmartGoldbergEmu.Helpers
{
    // Isolates OpenFileDialog / FolderBrowserDialog start folders per purpose.
    // WinForms otherwise reuses one process-wide last directory across all pickers.
    public static class FileDialogBrowseHelper
    {
        public enum Purpose
        {
            GameExecutable,
            GameFolder,
            WorkingDirectory,
            CustomIcon,
            Avatar,
            Font,
            Sound,
            ModsCopyFiles,
            ModsCopyFolder,
            Shortcut,
            SavePath,
            StatsReports
        }

        private static readonly object Sync = new object();
        private static readonly Dictionary<Purpose, string> LastDirectories = new Dictionary<Purpose, string>();

        public static void ApplyInitialDirectory(FileDialog dialog, Purpose purpose, params string[] preferredDirectories)
        {
            if (dialog == null)
                return;

            dialog.RestoreDirectory = true;
            string directory = ResolveExistingDirectory(purpose, preferredDirectories);
            // Never leave InitialDirectory empty: WinForms reuses the process last-file-dialog folder.
            if (string.IsNullOrEmpty(directory))
            {
                directory = TryNormalizeExistingDirectory(
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
            }
            if (!string.IsNullOrEmpty(directory))
                dialog.InitialDirectory = directory;
        }

        public static void ApplySelectedPath(FolderBrowserDialog dialog, Purpose purpose, params string[] preferredDirectories)
        {
            if (dialog == null)
                return;

            string directory = ResolveExistingDirectory(purpose, preferredDirectories);
            if (!string.IsNullOrEmpty(directory))
                dialog.SelectedPath = directory;
        }

        public static void RememberFile(Purpose purpose, string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            try
            {
                string directory = Path.GetDirectoryName(Path.GetFullPath(filePath.Trim()));
                RememberDirectory(purpose, directory);
            }
            catch
            {
            }
        }

        public static void RememberDirectory(Purpose purpose, string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
                return;

            try
            {
                string full = Path.GetFullPath(directory.Trim());
                if (!Directory.Exists(full))
                    return;

                lock (Sync)
                    LastDirectories[purpose] = full;
            }
            catch
            {
            }
        }

        private static string ResolveExistingDirectory(Purpose purpose, string[] preferredDirectories)
        {
            if (preferredDirectories != null)
            {
                for (int i = 0; i < preferredDirectories.Length; i++)
                {
                    string existing = TryNormalizeExistingDirectory(preferredDirectories[i]);
                    if (!string.IsNullOrEmpty(existing))
                        return existing;
                }
            }

            lock (Sync)
            {
                if (LastDirectories.TryGetValue(purpose, out string remembered))
                {
                    string existing = TryNormalizeExistingDirectory(remembered);
                    if (!string.IsNullOrEmpty(existing))
                        return existing;
                }
            }

            return null;
        }

        private static string TryNormalizeExistingDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            try
            {
                string full = Path.GetFullPath(path.Trim());
                return Directory.Exists(full) ? full : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
