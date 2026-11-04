using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Forms;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;

namespace SmartGoldbergEmu.Helpers
{
    // Discover missing Steam deps, prompt Install/Skip/Cancel, optionally install with ProgressForm.
    public static class GameDependencyLaunchHelper
    {
        // false = user cancelled (abort launch). true = continue launch (installed or skipped).
        public static bool TryHandleMissingDependencies(
            GameConfig game,
            string gameInstallFolder,
            IWin32Window owner,
            ITaskReportService taskReport,
            SteamInstallScriptService installScripts = null)
        {
            if (game == null)
                return true;

            SteamInstallScriptService service = installScripts ?? ServiceLocator.SteamInstallScriptService;
            List<PendingGameDependency> pending;
            try
            {
                pending = service.DiscoverPendingDependencies(game, gameInstallFolder);
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService?.LogWarning(
                    "Dependency discovery failed (continuing launch): " + ex.Message);
                return true;
            }

            if (pending == null || pending.Count == 0)
                return true;

            MissingGameDependenciesChoice choice = MissingGameDependenciesDialogHelper.Show(
                owner,
                game.AppName,
                pending);

            if (choice == MissingGameDependenciesChoice.Cancel)
            {
                taskReport?.SetMessageWithAutoClear("Launch cancelled.");
                return false;
            }

            if (choice == MissingGameDependenciesChoice.Skip)
            {
                ServiceLocator.LogService?.LogMessage(
                    "Install scripts: user skipped " + pending.Count + " missing dependenc"
                    + (pending.Count == 1 ? "y" : "ies")
                    + " for AppId " + game.AppId + ".");
                taskReport?.SetMessageWithAutoClear("Launching without installing dependencies.");
                return true;
            }

            return RunInstallWithProgressForm(owner, taskReport, service, pending);
        }

        private static bool RunInstallWithProgressForm(
            IWin32Window owner,
            ITaskReportService taskReport,
            SteamInstallScriptService service,
            IReadOnlyList<PendingGameDependency> pending)
        {
            using (var progressForm = new ProgressForm())
            {
                progressForm.Text = pending.Count == 1
                    ? "Installing dependency"
                    : "Installing dependencies";
                progressForm.Reset();
                progressForm.UpdateProgress(
                    pending.Count == 1 ? "Preparing dependency…" : "Preparing dependencies…",
                    0);
                progressForm.Show(owner);
                Application.DoEvents();

                ValidationResult install;
                try
                {
                    // Installers WaitForExit; keep ProgressForm pumping on the UI thread.
                    Task<ValidationResult> installTask = Task.Run(() =>
                        service.InstallPendingDependencies(
                            pending,
                            (status, pct) =>
                            {
                                progressForm.UpdateProgress(
                                    string.IsNullOrWhiteSpace(status) ? "Installing…" : status,
                                    pct);
                            },
                            () => progressForm.IsCancelled));

                    while (!installTask.Wait(50))
                        Application.DoEvents();

                    install = installTask.GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    try { progressForm.Close(); } catch { }
                    ServiceLocator.LogService?.LogWarning(
                        "Install scripts: dependency install failed: " + ex.Message);
                    taskReport?.SetMessageWithAutoClear(
                        "Some dependencies could not be installed. Launching anyway.",
                        TaskReportKind.Warning);
                    return true;
                }

                if (progressForm.IsCancelled)
                {
                    progressForm.ShowCancellationAndClose("Dependency install cancelled");
                    WaitForProgressFormClose(progressForm);
                    taskReport?.SetMessageWithAutoClear("Launch cancelled.");
                    return false;
                }

                if (!install.IsValid)
                {
                    ServiceLocator.LogService?.LogWarning(
                        "Install scripts: dependency install finished with errors: " + install.ErrorMessage);
                    progressForm.ShowSuccessAndClose("Finished with errors — launching anyway");
                    WaitForProgressFormClose(progressForm);
                    return true;
                }

                progressForm.ShowSuccessAndClose(
                    pending.Count == 1 ? "Dependency installed" : "Dependencies installed");
                WaitForProgressFormClose(progressForm);
                return true;
            }
        }

        private static void WaitForProgressFormClose(ProgressForm progressForm)
        {
            if (progressForm == null || progressForm.IsDisposed)
                return;

            while (progressForm.Visible && !progressForm.IsDisposed)
            {
                Application.DoEvents();
                System.Threading.Thread.Sleep(15);
            }
        }
    }
}
