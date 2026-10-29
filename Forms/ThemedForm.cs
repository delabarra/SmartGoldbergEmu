using System;
using System.Drawing;
using System.Windows.Forms;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Properties;
using SmartGoldbergEmu.Services;

namespace SmartGoldbergEmu.Forms
{
    // Applies theme after the HWND exists (before Visible) and owns ThemeChanged subscribe/unsubscribe.
    public class ThemedForm : Form
    {
        private readonly ThemeService _themeService;
        private bool _themeBound;
        private bool _appIconApplied;

        protected ThemeService ThemeService => _themeService;

        // Avoid ServiceLocator (and ThemeService timers/registry) while the WinForms designer constructs the form.
        protected static ThemeService ResolveThemeService(ThemeService themeService = null)
        {
            if (DesignTimeHelper.IsDesignTime)
                return themeService;
            return themeService ?? ServiceLocator.ThemeService;
        }

        protected ThemedForm()
            : this(ResolveThemeService())
        {
        }

        protected ThemedForm(ThemeService themeService)
        {
            ApplyApplicationIcon();
            if (DesignTimeHelper.IsDesignTime)
            {
                _themeService = themeService;
                return;
            }

            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            WinFormsThemePaintHelper.EnableDoubleBuffer(this);
            ThemeColors colors = _themeService.GetThemeColors(_themeService.EffectiveTheme);
            BackColor = colors.Background;
            ForeColor = colors.Foreground;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            ApplyApplicationIcon();
            ShowIcon = true;
            if (DesignTimeHelper.IsDesignTime)
            {
                base.OnHandleCreated(e);
                return;
            }
            EnsureThemeBound();
            // Colors before child handles paint with the designer defaults (white).
            ApplyTheme();
            base.OnHandleCreated(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (WinFormsThemePaintHelper.TryFillEraseBackground(ref m, BackColor, ClientRectangle))
                return;
            base.WndProc(ref m);
        }

        protected void ApplyTheme()
        {
            if (DesignTimeHelper.IsDesignTime || IsDisposed || Disposing || _themeService == null)
                return;
            _themeService.ApplyTheme(this);
            OnThemeApplied();
        }

        protected virtual void OnThemeApplied()
        {
        }

        private void ApplyApplicationIcon()
        {
            if (_appIconApplied)
                return;
            Icon source = Resources.steam_gold_x128;
            if (source == null)
                return;
            // Clone: Form.Dispose disposes Icon and would otherwise kill the cached resource.
            Icon = (Icon)source.Clone();
            ShowIcon = true;
            _appIconApplied = true;
        }

        private void EnsureThemeBound()
        {
            if (_themeBound || _themeService == null)
                return;
            _themeBound = true;
            _themeService.ThemeChanged += ThemeService_ThemeChanged;
        }

        private void ThemeService_ThemeChanged(object sender, ThemeChangedEventArgs e)
        {
            if (IsDisposed || Disposing)
                return;
            if (InvokeRequired)
            {
                try
                {
                    Invoke((Action)ApplyTheme);
                }
                catch (ObjectDisposedException)
                {
                }
                return;
            }
            ApplyTheme();
        }

        private void UnbindTheme()
        {
            if (!_themeBound || _themeService == null)
                return;
            _themeService.ThemeChanged -= ThemeService_ThemeChanged;
            _themeBound = false;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            UnbindTheme();
            base.OnFormClosed(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                UnbindTheme();
            base.Dispose(disposing);
        }
    }
}
