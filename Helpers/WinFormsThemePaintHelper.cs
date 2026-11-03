using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SmartGoldbergEmu.Helpers
{
    // Native erase and visual styles so dark forms do not flash white before themed paint.
    internal static class WinFormsThemePaintHelper
    {
        internal const int WmEraseBkgnd = 0x0014;
        private const int LvmGetHeader = 0x1000 + 31;
        private const string DarkModeExplorerTheme = "DarkMode_Explorer";

        private static readonly PropertyInfo DoubleBufferedProperty =
            typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);

        // Win10 1809 used 19; 1903+ uses 20. We force light title bars (value 0).
        private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;
        private const int DwmwaUseImmersiveDarkMode = 20;

        [DllImport("uxtheme.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string pszSubAppName, string pszSubIdList);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public static string ExplorerThemeName(bool dark)
        {
            return dark ? DarkModeExplorerTheme : null;
        }

        public static string DisabledVisualStyleThemeName(bool dark)
        {
            return dark ? string.Empty : null;
        }

        public static void EnableDoubleBuffer(Control control)
        {
            if (control == null || control is ToolStrip)
                return;
            DoubleBufferedProperty?.SetValue(control, true, null);
        }

        public static bool TryFillEraseBackground(ref Message m, Color backColor, Rectangle client)
        {
            if (m.Msg != WmEraseBkgnd)
                return false;
            FillEraseBackground(m.WParam, client, backColor);
            m.Result = (IntPtr)1;
            return true;
        }

        public static void FillEraseBackground(IntPtr hdc, Rectangle client, Color backColor)
        {
            if (hdc == IntPtr.Zero || client.Width <= 0 || client.Height <= 0)
                return;
            using (Graphics g = Graphics.FromHdc(hdc))
            using (var brush = new SolidBrush(backColor))
                g.FillRectangle(brush, client);
        }

        // Keep native caption bars light even when the form client area is dark.
        public static void ApplyLightTitleBar(Form form)
        {
            if (form == null || form.IsDisposed || !form.IsHandleCreated)
                return;

            int useDark = 0;
            try
            {
                DwmSetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkMode, ref useDark, sizeof(int));
                DwmSetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkModeBefore20H1, ref useDark, sizeof(int));
            }
            catch
            {
            }
        }

        public static void ApplyExplorerWindowTheme(Control control, bool dark)
        {
            ApplyWindowTheme(control, ExplorerThemeName(dark), applyListViewHeader: true, subIdList: null);
        }

        // AutoScroll Panel/FlowLayoutPanel NC scrollbars ignore a bare Explorer theme; they need the ScrollBar class id.
        public static void ApplyExplorerScrollBars(Control control, bool dark)
        {
            string themeName = ExplorerThemeName(dark);
            ApplyWindowTheme(control, themeName, applyListViewHeader: false, subIdList: themeName != null ? "ScrollBar" : null);
        }

        public static void ApplyDisabledVisualStyles(Control control, bool dark)
        {
            ApplyWindowTheme(control, DisabledVisualStyleThemeName(dark), applyListViewHeader: false, subIdList: null);
        }

        private static void ApplyWindowTheme(Control control, string themeName, bool applyListViewHeader, string subIdList)
        {
            if (control == null)
                return;

            void Apply()
            {
                if (!control.IsHandleCreated || control.IsDisposed)
                    return;
                SetWindowTheme(control.Handle, themeName, subIdList);
                if (!applyListViewHeader)
                    return;
                IntPtr header = SendMessage(control.Handle, LvmGetHeader, IntPtr.Zero, IntPtr.Zero);
                if (header != IntPtr.Zero)
                    SetWindowTheme(header, themeName, null);
            }

            if (control.IsHandleCreated)
                Apply();
            else
                control.HandleCreated += (sender, e) => Apply();
        }
    }
}
