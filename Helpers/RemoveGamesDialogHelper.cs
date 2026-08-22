using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using SmartGoldbergEmu.Forms;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Helpers
{
    // Confirm removing library entries via AppTaskDialogForm (checkbox strip + custom buttons).
    public static class RemoveGamesDialogHelper
    {
        private const int MaxListItems = 5;
        private const int IdRemove = 100;

        public static (bool confirmed, bool deleteFiles) Show(IEnumerable<GameConfig> games, IWin32Window owner)
        {
            var list = games?.ToList() ?? new List<GameConfig>();
            if (list.Count == 0)
                return (false, false);

            if (owner is Control c && (c.IsDisposed || c.Disposing))
                return (false, false);

            int count = list.Count;
            var request = new AppTaskDialogRequest
            {
                Content = BuildContent(list, count),
                Icon = MessageBoxIcon.Warning,
                VerificationText = "Remove emulator settings",
                VerificationChecked = false,
                Buttons = new List<AppTaskDialogButton>
                {
                    new AppTaskDialogButton(IdRemove, "Remove") { IsDefault = true },
                    new AppTaskDialogButton(TaskDialogHelper.IdCancel, "Cancel") { IsCancel = true }
                }
            };

            AppTaskDialogResult result = AppTaskDialogForm.Show(owner, request);
            bool confirmed = result.ButtonId == IdRemove;
            return (confirmed, confirmed && result.VerificationChecked);
        }

        private static string BuildContent(List<GameConfig> games, int count)
        {
            var body = new StringBuilder();
            body.Append(count == 1
                ? "Are you sure you want to remove the following game from your library?"
                : "Are you sure you want to remove the following games from your library?");
            body.Append("\n\n");

            var lines = games.Take(MaxListItems)
                .Select(g =>
                {
                    string name = string.IsNullOrWhiteSpace(g.AppName) ? "Unknown" : g.AppName.Trim();
                    return g.AppId + " - " + name;
                })
                .ToList();
            if (count > MaxListItems)
                lines.Add("and " + (count - MaxListItems) + " more game(s)");

            for (int i = 0; i < lines.Count; i++)
            {
                if (i > 0)
                    body.Append('\n');
                body.Append(lines[i]);
            }

            return body.ToString();
        }
    }
}
