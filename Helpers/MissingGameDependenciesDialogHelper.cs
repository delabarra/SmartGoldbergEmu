using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using SmartGoldbergEmu.Forms;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Helpers
{
    public enum MissingGameDependenciesChoice
    {
        Cancel = 0,
        Skip = 1,
        Install = 2
    }

    // Themed prompt before launch when Steam redistributables / install scripts are still pending.
    public static class MissingGameDependenciesDialogHelper
    {
        private const int MaxListItems = 8;
        private const int IdInstall = 100;
        private const int IdSkip = 101;

        public static MissingGameDependenciesChoice Show(
            IWin32Window owner,
            string gameName,
            IReadOnlyList<PendingGameDependency> pending)
        {
            if (pending == null || pending.Count == 0)
                return MissingGameDependenciesChoice.Skip;

            if (owner is Control c && (c.IsDisposed || c.Disposing))
                return MissingGameDependenciesChoice.Cancel;

            var request = new AppTaskDialogRequest
            {
                Content = BuildContent(gameName, pending),
                Icon = MessageBoxIcon.Warning,
                Buttons = new List<AppTaskDialogButton>
                {
                    new AppTaskDialogButton(IdInstall, "Install") { IsDefault = true },
                    new AppTaskDialogButton(IdSkip, "Skip"),
                    new AppTaskDialogButton(TaskDialogHelper.IdCancel, "Cancel") { IsCancel = true }
                }
            };

            AppTaskDialogResult result = AppTaskDialogForm.Show(owner, request);
            if (result.ButtonId == IdInstall)
                return MissingGameDependenciesChoice.Install;
            if (result.ButtonId == IdSkip)
                return MissingGameDependenciesChoice.Skip;
            return MissingGameDependenciesChoice.Cancel;
        }

        private static string BuildContent(string gameName, IReadOnlyList<PendingGameDependency> pending)
        {
            string titleName = string.IsNullOrWhiteSpace(gameName) ? "this game" : gameName.Trim();
            var body = new StringBuilder();
            body.Append(pending.Count == 1
                ? titleName + " needs this runtime installed before it can run:"
                : titleName + " needs these runtimes installed before it can run:");
            body.Append("\n\n");

            var lines = pending
                .Select(p => string.IsNullOrWhiteSpace(p.DisplayName) ? "Unknown runtime" : p.DisplayName.Trim())
                .Take(MaxListItems)
                .ToList();
            if (pending.Count > MaxListItems)
                lines.Add("and " + (pending.Count - MaxListItems) + " more");

            for (int i = 0; i < lines.Count; i++)
            {
                if (i > 0)
                    body.Append('\n');
                body.Append("• ");
                body.Append(lines[i]);
            }

            body.Append("\n\n");
            body.Append(
                "These are normal Windows game prerequisites (for example Visual C++, .NET, OpenAL, or DirectX). "
                + "Install runs quietly after you accept; Windows may still ask for administrator approval. "
                + "Skip launches without installing.");
            return body.ToString();
        }
    }
}
