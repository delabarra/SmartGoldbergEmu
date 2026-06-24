using System;
using System.Windows.Forms;
using SmartGoldbergEmu.Helpers;

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
                logger.LogMessage("Performing startup checks...");
                bool success = EmulatorUpdateService.CheckAndInstallMissingFilesWithUI(
                    logger,
                    action => action());
                logger.LogMessage(success
                    ? "Startup checks completed successfully"
                    : "Startup checks completed with warnings");
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
    }
}
