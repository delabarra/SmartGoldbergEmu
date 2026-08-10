using System;
using System.Drawing;
using System.Windows.Forms;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Helpers
{
    public enum DuplicateGameAction
    {
        Cancel,
        Edit,
        Update
    }

    // Edit = open local entry; Update = re-collect Steam data then save over the same GUID; Cancel = abort.
    public static class DuplicateGameDialogHelper
    {
        public static DuplicateGameAction Show(IWin32Window owner, GameConfig duplicateGame, bool matchedByExecutable)
        {
            if (duplicateGame == null)
                return DuplicateGameAction.Cancel;

            if (owner is Control c && (c.IsDisposed || c.Disposing))
                return DuplicateGameAction.Cancel;

            string name = string.IsNullOrWhiteSpace(duplicateGame.AppName)
                ? "Unknown"
                : duplicateGame.AppName.Trim();
            string reason = matchedByExecutable
                ? "A game with this executable path is already in your library"
                : $"A game with App ID {duplicateGame.AppId} and this path is already in your library";

            using (var dialog = new DuplicateGameChoiceForm(reason, name))
            {
                DialogResult result = owner != null
                    ? dialog.ShowDialog(owner)
                    : dialog.ShowDialog();

                if (result == DialogResult.Yes)
                    return DuplicateGameAction.Edit;
                if (result == DialogResult.Retry)
                    return DuplicateGameAction.Update;
                return DuplicateGameAction.Cancel;
            }
        }

        private sealed class DuplicateGameChoiceForm : Form
        {
            public DuplicateGameChoiceForm(string reason, string gameName)
            {
                Text = "Game Already in Library";
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                ShowInTaskbar = false;
                StartPosition = FormStartPosition.CenterParent;
                AutoScaleMode = AutoScaleMode.Font;
                ClientSize = new Size(420, 168);

                var lblMessage = new Label
                {
                    AutoSize = false,
                    Location = new Point(14, 14),
                    Size = new Size(392, 72),
                    Text = $"{reason}:\r\n\r\n{gameName}\r\n\r\nEdit the existing entry, update it with fresh Steam data (keeps your custom launch options), or cancel."
                };

                var btnEdit = new Button
                {
                    Text = "Edit",
                    DialogResult = DialogResult.Yes,
                    Location = new Point(116, 112),
                    Size = new Size(88, 28)
                };
                var btnUpdate = new Button
                {
                    Text = "Update",
                    DialogResult = DialogResult.Retry,
                    Location = new Point(210, 112),
                    Size = new Size(88, 28)
                };
                var btnCancel = new Button
                {
                    Text = "Cancel",
                    DialogResult = DialogResult.Cancel,
                    Location = new Point(304, 112),
                    Size = new Size(88, 28)
                };

                Controls.Add(lblMessage);
                Controls.Add(btnEdit);
                Controls.Add(btnUpdate);
                Controls.Add(btnCancel);

                AcceptButton = btnUpdate;
                CancelButton = btnCancel;
            }
        }
    }
}
