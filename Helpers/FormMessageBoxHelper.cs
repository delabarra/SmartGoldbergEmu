using System.Windows.Forms;

namespace SmartGoldbergEmu.Helpers
{
    public static class FormMessageBoxHelper
    {
        public static DialogResult ShowDialogIfAlive(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            return TaskDialogHelper.Show(owner, text, caption, buttons, icon);
        }

        public static DialogResult ShowDialogIfAlive(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon, MessageBoxDefaultButton defaultButton)
        {
            return TaskDialogHelper.Show(owner, text, caption, buttons, icon, defaultButton);
        }

        public static void ShowIfAlive(IWin32Window owner, string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            TaskDialogHelper.Show(owner, text, caption, buttons, icon);
        }

        // Task Dialog with footer hyperlink support (content/footer may use <A href="url">label</A>).
        public static DialogResult ShowDialogIfAlive(
            IWin32Window owner,
            string text,
            string caption,
            MessageBoxButtons buttons,
            MessageBoxIcon icon,
            MessageBoxDefaultButton defaultButton,
            string footer,
            bool enableHyperlinks)
        {
            return TaskDialogHelper.Show(owner, text, caption, buttons, icon, defaultButton, footer, enableHyperlinks);
        }
    }
}
