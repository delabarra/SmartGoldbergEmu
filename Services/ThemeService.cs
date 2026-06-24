using System;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Helpers;

namespace SmartGoldbergEmu.Services
{
    public class ThemeService : IDisposable
    {
        private const string ThemedDataGridViewTag = "ThemedDataGridView";
        private const string ThemedButtonTag = "ThemedButton";
        private const string SoundPreviewPlayStopButtonTag = "SoundPreviewPlayStop";
        internal const string LaunchDialogButtonTag = "LaunchDialogButton";
        private const string ThemedTextBoxTag = "ThemedTextBox";

        private ThemeMode _currentTheme;
        private bool _isSystemDarkMode;
        private RegistryKey _registryKey;
        private Timer _systemThemeWatcher;
        private bool _disposed;

        public event EventHandler<ThemeChangedEventArgs> ThemeChanged;

        public ThemeMode EffectiveTheme
        {
            get
            {
                EnsureNotDisposed();
                return _currentTheme == ThemeMode.System
                    ? (_isSystemDarkMode ? ThemeMode.Dark : ThemeMode.Light)
                    : _currentTheme;
            }
        }

        public ThemeMode CurrentTheme
        {
            get
            {
                EnsureNotDisposed();
                return _currentTheme;
            }
        }

        public ThemeService()
        {
            _currentTheme = ThemeMode.System;
            _isSystemDarkMode = false;
            InitializeSystemThemeDetection();
            _systemThemeWatcher = new Timer { Interval = 1000 };
            _systemThemeWatcher.Tick += SystemThemeWatcher_Tick;
            if (_currentTheme == ThemeMode.System)
                _systemThemeWatcher.Start();
        }

        public void SetTheme(ThemeMode theme, Form form)
        {
            EnsureNotDisposed();
            _currentTheme = theme;
            ApplyTheme(form);
            if (theme == ThemeMode.System)
                _systemThemeWatcher?.Start();
            else
                _systemThemeWatcher?.Stop();
            OnThemeChanged(new ThemeChangedEventArgs(theme, EffectiveTheme));
        }

        public void ApplyTheme(Form form)
        {
            EnsureNotDisposed();
            if (form == null)
                return;

            ThemeMode effectiveTheme = EffectiveTheme;
            ThemeColors colors = GetThemeColors(effectiveTheme);
            form.BackColor = colors.Background;
            form.ForeColor = colors.Foreground;
            ApplyThemeToControls(form.Controls, colors, effectiveTheme);
            ApplyThemeToMenus(form, colors, effectiveTheme);
            ScheduleLinkColorRefreshOnLoad(form, colors);
        }

        private void ScheduleLinkColorRefreshOnLoad(Form form, ThemeColors colors)
        {
            if (form.IsHandleCreated)
                return;
            EventHandler handler = null;
            handler = (s, e) =>
            {
                var f = (Form)s;
                f.Load -= handler;
                if (_disposed || f.IsDisposed || f.Disposing)
                    return;
                ApplyLinkColorsRecursive(f.Controls, GetThemeColors(EffectiveTheme));
            };
            form.Load += handler;
        }

        private static void ApplyLinkColorsRecursive(Control.ControlCollection controls, ThemeColors colors)
        {
            foreach (Control c in controls)
            {
                if (c is LinkLabel link)
                {
                    link.LinkColor = colors.LinkColor;
                    link.ActiveLinkColor = colors.LinkColor;
                    link.VisitedLinkColor = colors.VisitedLinkColor;
                }
                if (c.HasChildren)
                    ApplyLinkColorsRecursive(c.Controls, colors);
            }
        }

        private static Color DarkenRgb(Color color, int delta)
        {
            return Color.FromArgb(
                color.A,
                Math.Max(0, Math.Min(255, color.R - delta)),
                Math.Max(0, Math.Min(255, color.G - delta)),
                Math.Max(0, Math.Min(255, color.B - delta)));
        }

        private static Color LightenRgb(Color color, int delta)
        {
            return Color.FromArgb(
                color.A,
                Math.Max(0, Math.Min(255, color.R + delta)),
                Math.Max(0, Math.Min(255, color.G + delta)),
                Math.Max(0, Math.Min(255, color.B + delta)));
        }

        public ThemeColors GetThemeColors(ThemeMode theme)
        {
            switch (theme)
            {
                case ThemeMode.Light:
                {
                                       return new ThemeColors
                    {
                        Background = SystemColors.Control,
                        Foreground = SystemColors.ControlText,
                        FieldBackground = SystemColors.Control,
                        ControlBackground = SystemColors.Control,
                        ControlForeground = SystemColors.ControlText,
                        MenuBackground = SystemColors.Control,
                        MenuForeground = SystemColors.ControlText,
                        StatusStripBackground = SystemColors.Control,
                        StatusStripForeground = SystemColors.ControlText,
                        StatusTextSecondary = Color.FromArgb(80, 80, 80),
                        StatusTextAccent = Color.FromArgb(0, 120, 215),
                        ListViewBackground = SystemColors.Control,
                        ListViewForeground = SystemColors.ControlText,
                        ListViewAlternate = SystemColors.Control,
                        ListViewColumnHeaderBackground = DarkenRgb(SystemColors.Control, 15),
                        Border = SystemColors.ControlDark,
                        Highlight = Color.FromArgb(0, 120, 215),
                        HighlightText = Color.White,
                        LinkColor = Color.FromArgb(0, 102, 204),
                        ImageMarginBackground = LightenRgb(SystemColors.Control, 8),
                        DisabledBackground = SystemColors.Control,
                        DisabledForeground = SystemColors.GrayText,
                        SuccessColor = Color.FromArgb(0, 150, 0),
                        ErrorColor = Color.FromArgb(200, 0, 0),
                        WarningColor = Color.FromArgb(255, 140, 0),
                        InfoColor = Color.FromArgb(0, 102, 204),
                        VisitedLinkColor = Color.FromArgb(128, 0, 128)
                    };
                }

                case ThemeMode.Dark:
                {
                    Color darkControl = Color.FromArgb(37, 37, 38);
                    return new ThemeColors
                    {
                        Background = Color.FromArgb(30, 30, 30),
                        Foreground = Color.FromArgb(240, 240, 240),
                        FieldBackground = Color.FromArgb(30, 30, 30),
                        ControlBackground = darkControl,
                        ControlForeground = Color.FromArgb(240, 240, 240),
                        MenuBackground = darkControl,
                        MenuForeground = Color.FromArgb(240, 240, 240),
                        StatusStripBackground = darkControl,
                        StatusStripForeground = Color.FromArgb(255, 255, 255),
                        StatusTextSecondary = Color.FromArgb(200, 200, 200),
                        StatusTextAccent = Color.FromArgb(150, 175, 205),
                        ListViewBackground = darkControl,
                        ListViewForeground = Color.FromArgb(240, 240, 240),
                        ListViewAlternate = Color.FromArgb(45, 45, 48),
                        ListViewColumnHeaderBackground = DarkenRgb(darkControl, 5),
                        Border = Color.FromArgb(63, 63, 70),
                        Highlight = Color.FromArgb(70, 130, 180),
                        HighlightText = Color.White,
                        LinkColor = Color.FromArgb(135, 165, 215),
                        ImageMarginBackground = LightenRgb(darkControl, 8),
                        DisabledBackground = Color.FromArgb(25, 25, 26),
                        DisabledForeground = Color.FromArgb(130, 130, 130),
                        SuccessColor = Color.FromArgb(100, 220, 100),
                        ErrorColor = Color.FromArgb(255, 120, 120),
                        WarningColor = Color.FromArgb(255, 200, 100),
                        InfoColor = Color.FromArgb(135, 165, 215),
                        VisitedLinkColor = Color.FromArgb(150, 140, 190)
                    };
                }

                default:
                    return GetThemeColors(ThemeMode.Light);
            }
        }

        public void GetFallbackMosaicArtColors(ThemeMode effectiveTheme, out Color background, out Color foreground)
        {
            EnsureNotDisposed();
            if (effectiveTheme == ThemeMode.Light)
            {
                background = Color.FromArgb(240, 240, 240);
                foreground = Color.FromArgb(64, 64, 64);
                return;
            }

            var c = GetThemeColors(effectiveTheme);
            background = c.ListViewBackground;
            foreground = c.ListViewForeground;
        }

        public bool IsSystemDarkMode()
        {
            EnsureNotDisposed();
            try
            {
                if (_registryKey != null)
                {
                    object value = _registryKey.GetValue(ApplicationConstants.WindowsAppsUseLightThemeRegistryValueName);
                    return value != null && (int)value == 0;
                }
            }
            catch (Exception ex)
            {
                LogThemeWarning($"Failed to read system theme from registry: {ex.Message}");
            }
            return false;
        }

        public void RefreshSystemTheme(Form form = null)
        {
            EnsureNotDisposed();
            bool wasDark = _isSystemDarkMode;
            _isSystemDarkMode = IsSystemDarkMode();
            if (_currentTheme != ThemeMode.System || wasDark == _isSystemDarkMode)
                return;
            if (form != null)
                ApplyTheme(form);
            OnThemeChanged(new ThemeChangedEventArgs(_currentTheme, EffectiveTheme));
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;
            if (disposing)
            {
                if (_systemThemeWatcher != null)
                {
                    _systemThemeWatcher.Tick -= SystemThemeWatcher_Tick;
                    _systemThemeWatcher.Stop();
                    _systemThemeWatcher.Dispose();
                    _systemThemeWatcher = null;
                }
                _registryKey?.Dispose();
                _registryKey = null;
            }
            _disposed = true;
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ThemeService));
        }

        private static void LogThemeWarning(string message)
        {
            try
            {
                ServiceLocator.LogService?.LogWarning(message);
            }
            catch
            {
            }
        }

        private void InitializeSystemThemeDetection()
        {
            try
            {
                _registryKey = Registry.CurrentUser.OpenSubKey(
                    ApplicationConstants.WindowsCurrentUserThemesPersonalizeRegistryKey, false);
                _isSystemDarkMode = IsSystemDarkMode();
            }
            catch (Exception ex)
            {
                _isSystemDarkMode = false;
                LogThemeWarning($"Failed to initialize system theme detection: {ex.Message}");
            }
        }

        private void SystemThemeWatcher_Tick(object sender, EventArgs e)
        {
            if (_disposed || _currentTheme != ThemeMode.System)
                return;
            try
            {
                RefreshSystemTheme();
            }
            catch (ObjectDisposedException)
            {
                _systemThemeWatcher?.Stop();
            }
            catch (Exception ex)
            {
                LogThemeWarning($"Error in system theme watcher: {ex.Message}");
            }
        }

        public void ApplyDataGridViewTheme(DataGridView dgv)
        {
            if (_disposed || dgv == null)
                return;
            ApplyThemeToDataGridView(dgv, GetThemeColors(EffectiveTheme));
            EnsureDataGridViewThemingHooks(dgv);
        }

        private void EnsureDataGridViewThemingHooks(DataGridView dgv)
        {
            if (dgv.Tag?.ToString() != ThemedDataGridViewTag)
            {
                dgv.Tag = ThemedDataGridViewTag;
                dgv.EnabledChanged += ThemedDataGridView_EnabledChanged;
            }
        }

        private static void ApplyThemeToDataGridView(DataGridView dgv, ThemeColors colors)
        {
            if (dgv == null)
                return;

            bool enabled = dgv.Enabled;
            Color cellBack = enabled ? colors.ListViewBackground : colors.DisabledBackground;
            Color cellFore = enabled ? colors.ListViewForeground : colors.DisabledForeground;

            dgv.BackgroundColor = cellBack;
            dgv.BorderStyle = BorderStyle.FixedSingle;
            dgv.GridColor = colors.Border;
            dgv.EnableHeadersVisualStyles = false;
            dgv.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            dgv.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;

            dgv.ColumnHeadersDefaultCellStyle.BackColor = colors.ControlBackground;
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = colors.ControlForeground;
            dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = colors.ControlBackground;
            dgv.ColumnHeadersDefaultCellStyle.SelectionForeColor = colors.ControlForeground;

            dgv.RowHeadersDefaultCellStyle.BackColor = colors.ControlBackground;
            dgv.RowHeadersDefaultCellStyle.ForeColor = colors.ControlForeground;
            dgv.RowHeadersDefaultCellStyle.SelectionBackColor = colors.ControlBackground;
            dgv.RowHeadersDefaultCellStyle.SelectionForeColor = colors.ControlForeground;

            dgv.DefaultCellStyle.BackColor = cellBack;
            dgv.DefaultCellStyle.ForeColor = cellFore;
            dgv.DefaultCellStyle.SelectionBackColor = colors.Highlight;
            dgv.DefaultCellStyle.SelectionForeColor = colors.HighlightText;

            dgv.AlternatingRowsDefaultCellStyle.BackColor = colors.ListViewAlternate;
            dgv.AlternatingRowsDefaultCellStyle.ForeColor = cellFore;
            dgv.AlternatingRowsDefaultCellStyle.SelectionBackColor = colors.Highlight;
            dgv.AlternatingRowsDefaultCellStyle.SelectionForeColor = colors.HighlightText;
        }

        private void ApplyThemeToControls(Control.ControlCollection controls, ThemeColors colors, ThemeMode effectiveTheme)
        {
            foreach (Control control in controls)
                ApplyThemeToControl(control, colors, effectiveTheme);
        }

        private void ApplyThemeToControl(Control control, ThemeColors colors, ThemeMode effectiveTheme)
        {
            if (control is MenuStrip menuStrip)
            {
                menuStrip.Renderer = ThemedToolStripRendererFactory.GetRenderer(effectiveTheme, colors);
                ApplyThemeToToolStripItems(menuStrip.Items, colors, effectiveTheme);
            }
            else if (control is StatusStrip statusStrip)
            {
                statusStrip.BackColor = colors.StatusStripBackground;
                statusStrip.ForeColor = colors.StatusStripForeground;
                statusStrip.Renderer = ThemedToolStripRendererFactory.GetRenderer(effectiveTheme, colors);
                ApplyThemeToStatusStripItems(statusStrip.Items, colors);
            }
            else if (control is TabControl tabControl)
            {
                tabControl.BackColor = colors.ControlBackground;
                tabControl.ForeColor = colors.ControlForeground;
            }
            else if (control is TabPage tabPage)
            {
                tabPage.BackColor = colors.Background;
                tabPage.ForeColor = colors.Foreground;
            }
            else if (control is ListView listView)
            {
                listView.BackColor = colors.ListViewBackground;
                listView.ForeColor = colors.ListViewForeground;
                listView.BorderStyle = BorderStyle.FixedSingle;
            }
            else if (control is DataGridView dataGridView)
            {
                ApplyThemeToDataGridView(dataGridView, colors);
                EnsureDataGridViewThemingHooks(dataGridView);
            }
            else if (control is ListBox listBox)
            {
                listBox.BackColor = colors.FieldBackground;
                listBox.ForeColor = colors.Foreground;
                listBox.BorderStyle = BorderStyle.FixedSingle;
            }
            else if (control is Panel panel)
            {
                panel.BackColor = colors.ControlBackground;
                panel.ForeColor = colors.ControlForeground;
            }
            else if (control is GroupBox groupBox)
            {
                groupBox.BackColor = colors.ControlBackground;
                groupBox.ForeColor = colors.ControlForeground;
                groupBox.FlatStyle = FlatStyle.Flat;
            }
            else if (control is Label label)
            {
                label.BackColor = Color.Transparent;
                label.ForeColor = GetSemanticForeColor(control, colors);
            }
            else if (control is LinkLabel linkLabel)
            {
                linkLabel.BackColor = Color.Transparent;
                linkLabel.ForeColor = colors.Foreground;
                linkLabel.LinkColor = colors.LinkColor;
                linkLabel.ActiveLinkColor = colors.LinkColor;
                linkLabel.VisitedLinkColor = colors.VisitedLinkColor;
            }
            else if (control is Button button)
            {
                if (!button.Name.StartsWith("btnColor"))
                {
                    bool isSoundPreviewPlayStop = IsSoundPreviewPlayStopButton(button);
                    bool isLaunchDialogButton = IsLaunchDialogButton(button);
                    if (button.Enabled)
                    {
                        button.BackColor = colors.ControlBackground;
                        button.ForeColor = colors.ControlForeground;
                    }
                    else
                    {
                        button.BackColor = colors.DisabledBackground;
                        button.ForeColor = colors.DisabledForeground;
                    }
                    button.FlatStyle = FlatStyle.Flat;
                    button.FlatAppearance.BorderColor = colors.Border;
                    button.FlatAppearance.BorderSize = 1;
                    button.UseVisualStyleBackColor = false;
                    if (isSoundPreviewPlayStop)
                    {
                        button.Tag = SoundPreviewPlayStopButtonTag;
                    }
                    else if (isLaunchDialogButton)
                    {
                        button.Tag = LaunchDialogButtonTag;
                    }
                    else if (button.Tag?.ToString() != ThemedButtonTag)
                    {
                        button.Tag = ThemedButtonTag;
                        button.Paint += Button_Paint;
                        button.EnabledChanged += ThemedControl_EnabledChanged;
                    }
                }
            }
            else if (control is RichTextBox richTextBox)
            {
                if (richTextBox.ReadOnly || richTextBox.Enabled)
                {
                    richTextBox.BackColor = colors.FieldBackground;
                    richTextBox.ForeColor = colors.Foreground;
                }
                else
                {
                    richTextBox.BackColor = colors.DisabledBackground;
                    richTextBox.ForeColor = colors.DisabledForeground;
                }
                richTextBox.BorderStyle = BorderStyle.FixedSingle;
            }
            else if (control is TextBox textBox)
            {
                if (!textBox.Name.StartsWith("txtDLCList") && !textBox.Name.StartsWith("color"))
                {
                    if (textBox.Enabled)
                    {
                        textBox.BackColor = colors.FieldBackground;
                        textBox.ForeColor = colors.Foreground;
                    }
                    else
                    {
                        textBox.BackColor = colors.DisabledBackground;
                        textBox.ForeColor = colors.DisabledForeground;
                    }
                    textBox.BorderStyle = BorderStyle.FixedSingle;
                    if (textBox.Tag?.ToString() != ThemedTextBoxTag)
                    {
                        textBox.Tag = ThemedTextBoxTag;
                        textBox.EnabledChanged += ThemedControl_EnabledChanged;
                    }
                }
                else
                {
                    textBox.BorderStyle = BorderStyle.FixedSingle;
                }
            }
            else if (control is ComboBox comboBox)
            {
                comboBox.BackColor = colors.FieldBackground;
                comboBox.ForeColor = colors.Foreground;
                comboBox.FlatStyle = FlatStyle.Flat;
            }
            else if (control is NumericUpDown numericUpDown)
            {
                numericUpDown.BackColor = colors.FieldBackground;
                numericUpDown.ForeColor = colors.Foreground;
                numericUpDown.BorderStyle = BorderStyle.FixedSingle;
            }
            else if (control is CheckBox checkBox)
            {
                checkBox.BackColor = Color.Transparent;
                checkBox.ForeColor = GetSemanticForeColor(control, colors);
                checkBox.FlatStyle = FlatStyle.Standard;
                checkBox.UseVisualStyleBackColor = false;
            }
            else if (control is RadioButton radioButton)
            {
                radioButton.BackColor = Color.Transparent;
                radioButton.ForeColor = colors.Foreground;
                radioButton.FlatStyle = FlatStyle.Standard;
                radioButton.UseVisualStyleBackColor = false;
            }
            else if (control is ProgressBar progressBar)
            {
                progressBar.Style = ProgressBarStyle.Continuous;
            }

            if (control.HasChildren && !(control is DataGridView))
                ApplyThemeToControls(control.Controls, colors, effectiveTheme);

            if (control.ContextMenuStrip != null)
                ApplyThemeToContextMenuStrip(control.ContextMenuStrip, colors, effectiveTheme);
        }

        private void ApplyThemeToMenus(Form form, ThemeColors colors, ThemeMode effectiveTheme)
        {
            FieldInfo[] fields = form.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            foreach (FieldInfo field in fields)
            {
                object value = field.GetValue(form);
                if (value is ContextMenuStrip contextMenuStrip)
                    ApplyThemeToContextMenuStrip(contextMenuStrip, colors, effectiveTheme);
                else if (value is MenuStrip menuStrip)
                {
                    menuStrip.Renderer = ThemedToolStripRendererFactory.GetRenderer(effectiveTheme, colors);
                    ApplyThemeToToolStripItems(menuStrip.Items, colors, effectiveTheme);
                }
            }
        }

        private void ApplyThemeToContextMenuStrip(ContextMenuStrip contextMenuStrip, ThemeColors colors, ThemeMode effectiveTheme)
        {
            if (contextMenuStrip == null)
                return;
            contextMenuStrip.Renderer = ThemedToolStripRendererFactory.GetRenderer(effectiveTheme, colors);
            ApplyThemeToToolStripItems(contextMenuStrip.Items, colors, effectiveTheme);
        }

        private static void ApplyThemeToStatusStripItems(ToolStripItemCollection items, ThemeColors colors)
        {
            foreach (ToolStripItem item in items)
            {
                if (item is ToolStripStatusLabel || item is ToolStripLabel || item is ToolStripDropDownButton)
                {
                    item.ForeColor = colors.StatusStripForeground;
                    item.BackColor = Color.Transparent;
                }
            }
        }

        private void ApplyThemeToToolStripItems(ToolStripItemCollection items, ThemeColors colors, ThemeMode effectiveTheme)
        {
            foreach (ToolStripItem item in items)
            {
                if (item is ToolStripDropDownButton dropDownButton && dropDownButton.DropDown != null)
                {
                    dropDownButton.DropDown.Renderer = ThemedToolStripRendererFactory.GetRenderer(effectiveTheme, colors);
                    ApplyThemeToToolStripItems(dropDownButton.DropDownItems, colors, effectiveTheme);
                }
                else if (item is ToolStripMenuItem menuItem && menuItem.HasDropDownItems)
                {
                    if (menuItem.DropDown != null)
                        menuItem.DropDown.Renderer = ThemedToolStripRendererFactory.GetRenderer(effectiveTheme, colors);
                    ApplyThemeToToolStripItems(menuItem.DropDownItems, colors, effectiveTheme);
                }
            }
        }

        private static Color GetSemanticForeColor(Control control, ThemeColors colors)
        {
            string tag = control.Tag as string;
            if (tag == "ErrorColor")
                return colors.ErrorColor;
            if (tag == "WarningColor")
                return colors.WarningColor;
            if (tag == "SuccessColor")
                return colors.SuccessColor;
            if (tag == "InfoColor")
                return colors.InfoColor;
            return colors.Foreground;
        }

        private void OnThemeChanged(ThemeChangedEventArgs e)
        {
            if (_disposed)
                return;
            ThemeChanged?.Invoke(this, e);
        }

        private void ThemedDataGridView_EnabledChanged(object sender, EventArgs e)
        {
            if (_disposed)
                return;
            DataGridView dgv = sender as DataGridView;
            if (dgv == null || dgv.IsDisposed)
                return;
            ApplyThemeToDataGridView(dgv, GetThemeColors(EffectiveTheme));
        }

        private void ThemedControl_EnabledChanged(object sender, EventArgs e)
        {
            if (_disposed)
                return;
            Control control = sender as Control;
            if (control == null || control.IsDisposed)
                return;

            ThemeColors colors = GetThemeColors(EffectiveTheme);

            if (control is Button button && !button.Name.StartsWith("btnColor"))
            {
                if (button.Enabled)
                {
                    button.BackColor = colors.ControlBackground;
                    button.ForeColor = colors.ControlForeground;
                }
                else
                {
                    button.BackColor = colors.DisabledBackground;
                    button.ForeColor = colors.DisabledForeground;
                }
                button.Invalidate();
            }
            else if (control is TextBox textBox && !textBox.Name.StartsWith("txtDLCList") && !textBox.Name.StartsWith("color"))
            {
                if (textBox.Enabled)
                {
                    textBox.BackColor = colors.FieldBackground;
                    textBox.ForeColor = colors.Foreground;
                }
                else
                {
                    textBox.BackColor = colors.DisabledBackground;
                    textBox.ForeColor = colors.DisabledForeground;
                }
            }
        }

        private static bool IsSoundPreviewPlayStopButton(Button button)
        {
            return button != null
                && (button.Name == "btnSound1PlayStop" || button.Name == "btnSound2PlayStop");
        }

        private static bool IsLaunchDialogButton(Button button)
        {
            return button != null && button.Tag?.ToString() == LaunchDialogButtonTag;
        }

        private void Button_Paint(object sender, PaintEventArgs e)
        {
            Button button = sender as Button;
            if (button == null || _disposed)
                return;
            if (button.Tag?.ToString() != ThemedButtonTag)
                return;

            ThemeColors colors = GetThemeColors(EffectiveTheme);
            Color backColor = button.Enabled ? colors.ControlBackground : colors.DisabledBackground;
            Color foreColor = button.Enabled ? colors.ControlForeground : colors.DisabledForeground;

            e.Graphics.Clear(backColor);
            var borderRect = new Rectangle(0, 0, button.Width - 1, button.Height - 1);
            ControlPaint.DrawBorder(e.Graphics, borderRect, colors.Border, ButtonBorderStyle.Solid);

            TextFormatFlags flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis;
            if (button.RightToLeft == RightToLeft.Yes)
                flags |= TextFormatFlags.RightToLeft;
            TextRenderer.DrawText(e.Graphics, button.Text, button.Font, button.ClientRectangle, foreColor, backColor, flags);
        }
    }
}
