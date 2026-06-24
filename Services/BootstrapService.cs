using System;
using System.IO;
using System.Net;
using System.Windows.Forms;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Services
{
    public static class BootstrapService
    {
        public static ILogService LogService { get; private set; }

        public static bool EnsureLoggingInitialized()
        {
            if (LogService != null)
                return true;
            return InitializeLogging();
        }

        public static bool Initialize()
        {
            try
            {
                if (!EnsureLoggingInitialized())
                    return false;

                LogService.LogMessage("SmartGoldbergEmu application starting...");

                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                new LegacyImportService().RunSynchronousStartupMigration();
                EnsureMinimalConfigFiles();
                InitializeThemeFromSettings();
                EnsureUriProtocolRegistered();

                LogService.LogMessage("Bootstrap completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                HandleBootstrapFatalError(ex);
                return false;
            }
        }

        private static void HandleBootstrapFatalError(Exception ex)
        {
            if (LogService != null)
                LogService.LogError("Fatal error during bootstrap", ex);
            else
            {
                Console.WriteLine(LogRedactionHelper.RedactForLog($"Fatal error during bootstrap: {ex.Message}"));
                if (!string.IsNullOrEmpty(ex.StackTrace))
                    Console.WriteLine(LogRedactionHelper.RedactForLog(ex.StackTrace));
            }

            FormMessageBoxHelper.ShowIfAlive(
                null,
                ErrorDisplayHelper.SanitizeForUser("Application startup", ex),
                "SmartGoldbergEmu - Fatal Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }

        private static void InitializeThemeFromSettings()
        {
            try
            {
                var themeMode = ServiceLocator.AppDataService.GetThemeMode();
                ServiceLocator.ThemeService.SetTheme(themeMode, null);
                LogService?.LogDebug($"Theme initialized: {themeMode} (effective: {ServiceLocator.ThemeService.EffectiveTheme})");
            }
            catch (Exception ex)
            {
                LogService?.LogWarning($"Failed to initialize theme during bootstrap: {ex.Message}");
            }
        }

        public static void LogShutdown()
        {
            LogService?.LogMessage("SmartGoldbergEmu application shutting down...");
        }

        private static bool InitializeLogging()
        {
            try
            {
                LogService = new LogService(LoggingConfiguration.CreateDefault());
                LogService.LogMessage("Logging service initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                try
                {
                    LogService = new LogService(enableConsoleLogging: true, enableFileLogging: false);
                    LogService.LogError("Failed to initialize logging with configuration, using fallback", ex);
                    return true;
                }
                catch
                {
                    Console.WriteLine(LogRedactionHelper.RedactForLog($"Critical failure: Unable to initialize logging: {ex.Message}"));
                    return false;
                }
            }
        }

        private static void EnsureMinimalConfigFiles()
        {
            try
            {
                LogService.LogDebug("Ensuring minimal global config structure...");
                var result = ServiceLocator.AppDataService.EnsureMinimalConfigFilesExist();
                if (result.IsValid)
                    LogService.LogDebug("Minimal config structure ensured");
                else
                    LogService.LogWarning($"Failed to ensure minimal config: {result.ErrorMessage}");
            }
            catch (Exception ex)
            {
                LogService.LogError("Error ensuring minimal config files", ex);
            }
        }

        private static void EnsureUriProtocolRegistered()
        {
            try
            {
                LogService.LogDebug("Checking URI protocol registration...");
                if (UriProtocolRegistryService.IsProtocolRegistered())
                {
                    LogService.LogDebug("URI protocol is already registered");
                    return;
                }

                LogService.LogDebug("URI protocol not registered, attempting to register...");
                try
                {
                    if (UriProtocolRegistryService.RegisterProtocol())
                        LogService.LogDebug("URI protocol registered successfully");
                    else
                        LogService.LogWarning("Failed to register URI protocol. Shortcuts may not work properly.");
                }
                catch (Exception ex)
                {
                    if (ex is UnauthorizedAccessException)
                        LogService.LogWarning("Access denied when registering URI protocol. Run as administrator.");
                    else if (ex is System.Security.SecurityException)
                        LogService.LogWarning("Security exception when registering URI protocol. Run as administrator.");
                    else
                        LogService.LogWarning($"Error registering URI protocol: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                LogService.LogError("Error checking URI protocol registration", ex);
            }
        }
    }
}
