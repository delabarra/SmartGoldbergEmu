using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;
using SmartGoldbergEmu.Forms;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Helpers
{
    public enum DuplicateGameAction
    {
        Cancel,
        Edit
    }

    // Edit = open local entry; Cancel = abort add. (Re-collect/update-same-GUID may return later.)
    public static class DuplicateGameDialogHelper
    {
        private const int IdEdit = 100;

        public static DuplicateGameAction Show(IWin32Window owner, GameConfig duplicateGame, bool matchedByExecutable)
        {
            if (duplicateGame == null)
                return DuplicateGameAction.Cancel;

            if (owner is Control c && (c.IsDisposed || c.Disposing))
                return DuplicateGameAction.Cancel;

            string name = string.IsNullOrWhiteSpace(duplicateGame.AppName)
                ? "Unknown"
                : duplicateGame.AppName.Trim();
            string gameLine = duplicateGame.AppId + " - " + name;
            string reason = matchedByExecutable
                ? "A game with this executable path is already in your library"
                : "A game with App ID " + duplicateGame.AppId + " and this path is already in your library";

            var body = new StringBuilder();
            body.Append(reason);
            body.Append(":\n\n");
            body.Append(gameLine);

            var request = new AppTaskDialogRequest
            {
                Content = body.ToString(),
                Icon = MessageBoxIcon.Warning,
                Buttons = new List<AppTaskDialogButton>
                {
                    new AppTaskDialogButton(IdEdit, "Edit") { IsDefault = true },
                    new AppTaskDialogButton(TaskDialogHelper.IdCancel, "Cancel") { IsCancel = true }
                }
            };

            AppTaskDialogResult result = AppTaskDialogForm.Show(owner, request);
            if (result.ButtonId == IdEdit)
                return DuplicateGameAction.Edit;
            return DuplicateGameAction.Cancel;
        }
    }
}
