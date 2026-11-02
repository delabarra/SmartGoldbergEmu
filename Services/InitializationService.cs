using System;
using System.Collections.Generic;
using System.Windows.Forms;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Services
{
    public static class InitializationService
    {
        public static bool PerformStartupChecks()
        {
            var logger = BootstrapService.LogService;
            if (logger == null)
                return false;

            try
            {
                logger.LogDebug("Performing startup checks...");
                bool success = EmulatorUpdateService.CheckAndInstallMissingFilesWithUI(
                    logger,
                    action => action());
                if (success)
                {
                    TryEnsureColdClientForLibraryAtStartup(logger);
                    TryEnsureColdClientDllFoldersAtStartup(logger);
                }

                logger.LogMessage(success
                    ? "Startup checks complete."
                    : "Startup checks completed with warnings.");
                return success;
            }
            catch (Exception ex)
            {
                logger.LogError("Error during startup checks", ex);
                FormMessageBoxHelper.ShowIfAlive(
                    null,
                    $"An error occurred during startup checks:\n\n{ex.Message}",
                    "Startup Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return false;
            }
        }

        // Runs before MainForm; cancel/failure still allows the app to open (Goldberg missing-files skip style).
        private static void TryEnsureColdClientForLibraryAtStartup(ILogService logger)
        {
            if (!ColdClientInstallService.ColdClientFilesCheckSync())
                return;
            if (!ServiceLocator.GameDataService.AnyGameUsesLaunchMode(GoldbergLaunchMode.ColdClient))
                return;

            logger?.LogWarning(
                "ColdClient files are missing under goldberg\\ColdClientLoader, but one or more games use ColdClient mode.");
            ColdClientInstallService.TryEnsureInstalledWithUiSync(null, logger);
        }

        private static void TryEnsureColdClientDllFoldersAtStartup(ILogService logger)
        {
            List<GameConfig> games;
            try
            {
                games = ServiceLocator.GameDataService.LoadGameLibrary();
            }
            catch (Exception ex)
            {
                logger?.LogDebug("Could not load game library to ensure ColdClient DLL folders: " + ex.Message);
                return;
            }

            if (games == null || games.Count == 0)
                return;

            foreach (GameConfig game in games)
            {
                if (game == null || game.LaunchMode != GoldbergLaunchMode.ColdClient)
                    continue;

                GoldbergFilesService.TryEnsureColdClientDllFolders(game.AppId);
            }
        }
    }
}
