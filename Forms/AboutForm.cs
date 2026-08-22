using System;
using System.Diagnostics;
using System.Windows.Forms;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Services;

namespace SmartGoldbergEmu.Forms
{
    public partial class AboutForm : ThemedForm
    {
        public AboutForm() : this(ServiceLocator.ThemeService)
        {
        }

        public AboutForm(ThemeService themeService)
            : base(themeService)
        {
            InitializeComponent();
            lblVersion.Text = "Version " + ApplicationVersionHelper.GetDisplayVersion();

            linkRepository.Tag = LauncherReleaseConstants.GetRepositoryWebUrl();
            linkRepository.LinkClicked += OnAboutLink_LinkClicked;
        }

        private void OnAboutLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            var url = (sender as LinkLabel)?.Tag as string;
            if (string.IsNullOrEmpty(url))
                return;

            e.Link.Visited = true;

            if (!PathValidationHelper.IsSafeUrl(url))
            {
                FormMessageBoxHelper.ShowIfAlive(this, "Invalid URL format detected.", "Invalid URL", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                Process.Start(url);
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError($"Failed to open link: {ex.Message}", ex);
                FormMessageBoxHelper.ShowIfAlive(this, "Failed to open link.", "Could Not Open Link", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
