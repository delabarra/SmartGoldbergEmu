using System.Windows.Forms;

namespace SmartGoldbergEmu.Helpers
{
    public static class FormMessageBoxHelper
    {
        private static bool IsOwnerDead(IWin32Window owner)
        {
            return owner is Control c && (c.IsDisposed || c.Disposing);
        }

        public static DialogResult ShowDialogIfAlive(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            if (IsOwnerDead(owner))
                return DialogResult.Cancel;
            return MessageBox.Show(owner, text, caption, buttons, icon);
        }

        public static DialogResult ShowDialogIfAlive(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
        {
            if (IsOwnerDead(owner))
                return DialogResult.Cancel;
            return MessageBox.Show(owner, text, caption, buttons, icon, defaultButton);
        }

        public static void ShowIfAlive(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            if (IsOwnerDead(owner))
                return;
            MessageBox.Show(owner, text, caption, buttons, icon);
        }
    }
}
