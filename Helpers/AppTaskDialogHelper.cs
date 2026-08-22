using System.Collections.Generic;
using System.Windows.Forms;
using SmartGoldbergEmu.Forms;

namespace SmartGoldbergEmu.Helpers
{
    // Thin OK-only prompts on AppTaskDialogForm (info / guidance / soft notices).
    public static class AppTaskDialogHelper
    {
        public static void ShowOk(IWin32Window owner, string content, MessageBoxIcon icon)
        {
            AppTaskDialogForm.Show(
                owner,
                new AppTaskDialogRequest
                {
                    Content = content ?? string.Empty,
                    Icon = icon,
                    Buttons = new List<AppTaskDialogButton>
                    {
                        new AppTaskDialogButton(TaskDialogHelper.IdOk, "OK")
                        {
                            IsDefault = true,
                            IsCancel = true
                        }
                    }
                });
        }
    }
}
