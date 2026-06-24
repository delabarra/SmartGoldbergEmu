using System;
using System.Diagnostics;
using System.Windows.Forms;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;

namespace SmartGoldbergEmu.Forms
{
    public partial class AboutForm : Form
    {
        private readonly ThemeService _themeService;

        public AboutForm() : this(ServiceLocator.ThemeService)
        {
        }

        public AboutForm(ThemeService themeService)
        {
            InitializeComponent();
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            lblVersion.Text = "Version " + ApplicationVersionHelper.GetDisplayVersion();

            linkRepository.Tag = LauncherReleaseConstants.GetRepositoryWebUrl();
            linkRepository.LinkClicked += OnAboutLink_LinkClicked;

            ApplyTheme();
            _themeService.ThemeChanged += ThemeService_ThemeChanged;
        }

        private void OnAboutLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            var url = (sender as LinkLabel)?.Tag as string;
            if (string.IsNullOrEmpty(url))
                return;

            e.Link.Visited = true;

            if (!PathValidationHelper.IsSafeUrl(url))
            {
                FormMessageBoxHelper.ShowIfAlive(this, "Invalid URL format detected.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                Process.Start(url);
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError($"Failed to open link: {ex.Message}", ex);
                FormMessageBoxHelper.ShowIfAlive(this, "Failed to open link.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ApplyTheme() => _themeService.ApplyTheme(this);

        private void ThemeService_ThemeChanged(object sender, ThemeChangedEventArgs e)
        {
            if (IsDisposed || Disposing)
                return;
            if (InvokeRequired)
                Invoke((Action)ApplyTheme);
            else
                ApplyTheme();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _themeService.ThemeChanged -= ThemeService_ThemeChanged;
            base.OnFormClosed(e);
        }
    }
}
