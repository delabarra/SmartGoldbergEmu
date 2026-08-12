using System;
using System.Windows.Forms;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;

namespace SmartGoldbergEmu.Forms
{
    // Applies theme after the HWND exists (before Visible) and owns ThemeChanged subscribe/unsubscribe.
    public class ThemedForm : Form
    {
        private readonly ThemeService _themeService;
        private bool _themeBound;

        protected ThemeService ThemeService => _themeService;

        protected ThemedForm()
            : this(ServiceLocator.ThemeService)
        {
        }

        protected ThemedForm(ThemeService themeService)
        {
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (DesignTimeHelper.IsDesignTime)
                return;
            EnsureThemeBound();
            ApplyTheme();
        }

        protected void ApplyTheme()
        {
            if (DesignTimeHelper.IsDesignTime || IsDisposed || Disposing)
                return;
            _themeService.ApplyTheme(this);
            OnThemeApplied();
        }

        protected virtual void OnThemeApplied()
        {
        }

        private void EnsureThemeBound()
        {
            if (_themeBound)
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
            if (!_themeBound)
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
