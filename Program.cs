using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Forms;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;

namespace SmartGoldbergEmu
{
    internal static class Program
    {
        private static Mutex _mutex = null;
        private const string MutexName = ApplicationConstants.MutexName;
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        private const int SW_RESTORE = 9;

        public static ILogService LogService => BootstrapService.LogService;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            BootstrapService.EnsureLoggingInitialized();
            InstallGlobalExceptionHandlers();

            if (LaunchSessionCleanupService.TryRunWatcherFromCommandLine(args, LogService))
                return;
            _mutex = new Mutex(true, MutexName, out bool createdNew);

            if (!createdNew)
            {
                try
                {
                    _mutex.Dispose();
                }
                catch
                {
                }
                _mutex = null;

                // Another instance is already running focus it
                FocusExistingWindow();

                // URI
                if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
                {
                    if (UriProtocolService.IsValidUri(args[0]))
                    {
                        try
                        {
                            string tempDir = PathConstants.LocalAppDataPerUserDirectory;
                            Directory.CreateDirectory(tempDir);
                            string uriFile = Path.Combine(tempDir, $"{PathConstants.LauncherUriProtocolPendingFilePrefix}{DateTime.Now.Ticks}{PathConstants.LauncherUriProtocolPendingFileExtension}");
                            File.WriteAllText(uriFile, args[0], System.Text.Encoding.UTF8);
                        }
                        catch (Exception ex)
                        {
                            LogService?.LogWarning($"Failed to write URI temp file for inter-instance communication: {ex.Message}");
                        }
                    }
                }
                return;
            }

            try
            {
                ulong? appIdToLaunch = null;
                if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
                {
                    var parseResult = UriProtocolService.ParseRunCommand(args[0]);
                    if (parseResult.Success)
                    {
                        appIdToLaunch = parseResult.AppId;
                        LogService?.LogMessage($"URI protocol launch detected: AppId {appIdToLaunch}");
                    }
                    else
                    {
                        LogService?.LogWarning($"Invalid URI protocol argument: {args[0]} - {parseResult.ErrorMessage}");
                    }
                }

                // Bootstrap: logging, TLS, config, URI protocol, WinForms init
                if (!BootstrapService.Initialize())
                {
                    return;
                }

                TryRestoreSteamClientRegistryForLifecycle();
                TryReconcileOrphanedLaunchSessions();

                LauncherUpdateService.CheckForUpdatesWithUISync(BootstrapService.LogService, isStartup: true);

                if (!InitializationService.PerformStartupChecks())
                {
                    BootstrapService.LogService?.LogMessage("Startup checks failed - exiting application");
                    return;
                }

                if (!EmulatorUpdateService.GoldbergFilesCheckSync())
                    EmulatorUpdateService.CheckForUpdatesWithUISync(BootstrapService.LogService, isStartup: true);

                // A modal loop or DoEvents before Application.Run (startup Goldberg download, update prompts) leaves a plain
                // SynchronizationContext that WinForms does not replace; MainForm awaits would then resume on the thread pool.
                if (!(SynchronizationContext.Current is WindowsFormsSynchronizationContext))
                    SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

                BootstrapService.LogService.LogMessage("Starting main application form...");
                var mainForm = new Forms.MainForm();
                mainForm.PendingAppIdLaunch = appIdToLaunch;

                Application.Run(mainForm);

                TryRestoreSteamClientRegistryForLifecycle();
                // Safety net if MainForm close path did not run (resources are disposed idempotently).
                ServiceLocator.DisposeApplicationResources();
                BootstrapService.LogShutdown();
            }
            finally
            {
                _mutex?.ReleaseMutex();
                _mutex?.Dispose();
            }
        }

        // Must run before any control is created (SetUnhandledExceptionMode throws afterwards).
        private static void InstallGlobalExceptionHandlers()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += OnUiThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }

        private static void OnUiThreadException(object sender, ThreadExceptionEventArgs e)
        {
            LogService?.LogError("Unhandled UI exception", e.Exception);
            try
            {
                MessageBox.Show(
                    ErrorDisplayHelper.SanitizeForUser("The last action", e.Exception) + "\n\nDetails were written to the log.",
                    Application.ProductName,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            catch
            {
            }
        }

        // Exceptions on non-UI threads still terminate the process; log first so the cause is not lost.
        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            LogService?.LogError(
                $"Unhandled exception (terminating: {e.IsTerminating})",
                e.ExceptionObject as Exception);
        }

        private static void OnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            LogService?.LogError("Unobserved task exception", e.Exception);
            e.SetObserved();
        }

        private static void TryRestoreSteamClientRegistryForLifecycle()
        {
            try
            {
                ServiceLocator.GameLaunchService.RestoreSteamClientRegistryForApplicationLifecycle();
            }
            catch (Exception ex)
            {
                BootstrapService.LogService?.LogWarning($"Steam client registry lifecycle restore skipped: {ex.Message}");
            }
        }

        private static void TryReconcileOrphanedLaunchSessions()
        {
            try
            {
                ServiceLocator.LaunchSessionCleanupService.ReconcileAllOrphanedSessions();
            }
            catch (Exception ex)
            {
                BootstrapService.LogService?.LogWarning($"Orphaned launch session cleanup skipped: {ex.Message}");
            }
        }

        private static void FocusExistingWindow()
        {
            try
            {
                Process currentProcess = Process.GetCurrentProcess();
                string processName = currentProcess.ProcessName;
                IntPtr hWnd = IntPtr.Zero;

                foreach (Process process in Process.GetProcessesByName(processName))
                {
                    try
                    {
                        if (process.Id != currentProcess.Id && process.MainWindowHandle != IntPtr.Zero)
                        {
                            hWnd = process.MainWindowHandle;
                            break;
                        }
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }

                if (hWnd != IntPtr.Zero)
                {
                    if (IsIconic(hWnd))
                    {
                        ShowWindow(hWnd, SW_RESTORE);
                    }
                    
                    SetForegroundWindow(hWnd);
                }
            }
            catch (Exception ex)
            {
                LogService?.LogWarning($"Failed to focus existing window: {ex.Message}");
            }
        }
    }
}

