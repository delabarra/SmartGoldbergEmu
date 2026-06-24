using System.Windows.Forms;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Helpers
{
    public static class DuplicateExecutableDialogHelper
    {
        public static DialogResult Show(IWin32Window owner, GameConfig duplicateGame)
        {
            if (duplicateGame == null)
                return DialogResult.Cancel;

            return FormMessageBoxHelper.ShowDialogIfAlive(
                owner,
                $"A game with this executable path already exists:\n\n{duplicateGame.AppName}\n\nWould you like to edit it instead?",
                "Duplicate Path",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Warning);
        }
    }
}
