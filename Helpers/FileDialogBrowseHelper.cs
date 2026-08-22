using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;

namespace SmartGoldbergEmu.Helpers
{
    // Isolates OpenFileDialog / FolderBrowserDialog start folders per purpose.
    // WinForms otherwise reuses one process-wide last directory across all pickers.
    // Last folders are persisted in ui_settings.ini so they survive app restarts.
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
        private static bool _loadedFromDisk;

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
                EnsureLoadedFromDisk();

                string full = Path.GetFullPath(directory.Trim());
                if (!Directory.Exists(full))
                    return;

                lock (Sync)
                    LastDirectories[purpose] = full;

                PersistDirectory(purpose, full);
            }
            catch
            {
            }
        }

        private static string ResolveExistingDirectory(Purpose purpose, string[] preferredDirectories)
        {
            EnsureLoadedFromDisk();

            // Prefer call-site seeds (e.g. current game folder text), not install/copy destinations.
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

            return TryNormalizeExistingDirectory(GetPurposeDefaultDirectory(purpose));
        }

        // First-open defaults only; remembered paths from ui_settings.ini win once set.
        private static string GetPurposeDefaultDirectory(Purpose purpose)
        {
            switch (purpose)
            {
                case Purpose.Avatar:
                    return TryEnsureDirectory(PathConstants.GlobalSettingsPath);
                case Purpose.Font:
                    return TryEnsureDirectory(PathConstants.GlobalFontsPath);
                case Purpose.Sound:
                    return TryEnsureDirectory(PathConstants.GlobalSoundsPath);
                case Purpose.Shortcut:
                    return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                default:
                    return null;
            }
        }

        private static string TryEnsureDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            try
            {
                Directory.CreateDirectory(path);
                return path;
            }
            catch
            {
                return null;
            }
        }

        private static void EnsureLoadedFromDisk()
        {
            if (_loadedFromDisk)
                return;

            lock (Sync)
            {
                if (_loadedFromDisk)
                    return;

                try
                {
                    string path = PathConstants.UiSettingsFilePath;
                    if (File.Exists(path))
                    {
                        var iniService = new IniFileService();
                        IniFile uiIni = iniService.ParseFile(path);
                        foreach (Purpose purpose in Enum.GetValues(typeof(Purpose)))
                        {
                            string value = iniService.GetValue(
                                uiIni,
                                ApplicationConstants.SettingSectionBrowseFolders,
                                purpose.ToString());
                            string existing = TryNormalizeExistingDirectory(value);
                            if (!string.IsNullOrEmpty(existing))
                                LastDirectories[purpose] = existing;
                        }
                    }
                }
                catch
                {
                }

                _loadedFromDisk = true;
            }
        }

        private static void PersistDirectory(Purpose purpose, string directory)
        {
            try
            {
                string path = PathConstants.UiSettingsFilePath;
                string uiSettingsDirectory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(uiSettingsDirectory))
                    Directory.CreateDirectory(uiSettingsDirectory);

                var iniService = new IniFileService();
                IniFile uiIni = File.Exists(path)
                    ? iniService.ParseFile(path)
                    : new IniFile();

                iniService.SetValue(
                    uiIni,
                    ApplicationConstants.SettingSectionBrowseFolders,
                    purpose.ToString(),
                    directory);
                iniService.WriteFile(uiIni, path);
            }
            catch
            {
            }
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
