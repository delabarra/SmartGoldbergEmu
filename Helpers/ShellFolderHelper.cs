using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using SmartGoldbergEmu.Constants;

namespace SmartGoldbergEmu.Helpers
{
    // Opens folders and files in Explorer. Install-tree paths are restricted; user-configured paths use format-only checks.
    public static class ShellFolderHelper
    {
        private static readonly string[] AllowedBasePaths =
        {
            PathConstants.AppBaseDirectory,
            PathConstants.GamesDirectory,
            PathConstants.GoldbergDirectory,
            PathConstants.GlobalSettingsPath
        };

        public static bool TryOpenFolder(string folderPath, bool createIfMissing, out string errorMessage)
        {
            return TryOpenFolder(folderPath, createIfMissing, out errorMessage, restrictToAppInstallTree: true);
        }

        public static bool TryOpenFolder(
            string folderPath,
            bool createIfMissing,
            out string errorMessage,
            bool restrictToAppInstallTree)
        {
            errorMessage = null;
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                errorMessage = "Folder path is empty.";
                return false;
            }

            bool pathAllowed = restrictToAppInstallTree
                ? PathValidationHelper.IsSafeFilePath(folderPath, AllowedBasePaths)
                : PathValidationHelper.IsSafeFilePath(folderPath);
            if (!pathAllowed)
            {
                errorMessage = "Invalid folder path.";
                return false;
            }

            try
            {
                if (!Directory.Exists(folderPath))
                {
                    if (!createIfMissing)
                    {
                        errorMessage = "Folder does not exist:\n" + folderPath;
                        return false;
                    }

                    Directory.CreateDirectory(folderPath);
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = folderPath,
                    UseShellExecute = true
                });
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public static bool TryOpenFile(string filePath, out string errorMessage)
        {
            errorMessage = null;
            if (string.IsNullOrWhiteSpace(filePath))
            {
                errorMessage = "File path is empty.";
                return false;
            }

            if (!PathValidationHelper.IsSafeFilePath(filePath, AllowedBasePaths))
            {
                errorMessage = "Invalid file path.";
                return false;
            }

            if (!File.Exists(filePath))
            {
                errorMessage = "File does not exist:\n" + filePath;
                return false;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public static void OpenFolderForOwner(
            IWin32Window owner,
            string folderPath,
            bool createIfMissing,
            string dialogTitle,
            string errorDialogTitle = null,
            bool restrictToAppInstallTree = true)
        {
            if (TryOpenFolder(folderPath, createIfMissing, out string errorMessage, restrictToAppInstallTree))
                return;

            bool missing = errorMessage != null
                && errorMessage.StartsWith("Folder does not exist", StringComparison.Ordinal);
            string title = missing ? dialogTitle : (errorDialogTitle ?? dialogTitle);
            var icon = missing ? MessageBoxIcon.Warning : MessageBoxIcon.Error;
            FormMessageBoxHelper.ShowIfAlive(owner, errorMessage ?? "Unable to open folder.", title,
                MessageBoxButtons.OK, icon);
        }
    }
}
