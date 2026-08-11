using System;
using System.Windows.Forms;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;

namespace SmartGoldbergEmu.Forms
{
    public partial class ForkSelectForm : Form
    {
        private readonly ThemeService _themeService;
        private readonly bool _forceExplicitChoice;
        private readonly GoldbergForkSource _forkWhenOpened;
        private readonly GoldbergReleaseChannel _channelWhenOpened;

        public ForkSelectForm()
            : this(forceExplicitChoice: false)
        {
        }

        // forceExplicitChoice: no fork pre-selected; OK stays disabled until the user picks one (first-time download).
        public ForkSelectForm(bool forceExplicitChoice)
            : this(forceExplicitChoice, ServiceLocator.ThemeService)
        {
        }

        public ForkSelectForm(bool forceExplicitChoice, ThemeService themeService)
        {
            InitializeComponent();
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            _forceExplicitChoice = forceExplicitChoice;
            _forkWhenOpened = ServiceLocator.AppDataService.GetGoldbergForkSource();
            _channelWhenOpened = ServiceLocator.AppDataService.GetGoldbergReleaseChannel();

            ConfigureForkOptionsLayout();

            if (forceExplicitChoice)
            {
                chkUpdateFilesOnOk.Visible = false;
                ClearForkSelection();
                btnOK.Enabled = false;
                WireForkRadioEvents(RadioFork_CheckedChanged);
            }
            else
            {
                ApplySavedSelection(_forkWhenOpened, _channelWhenOpened);
                WireForkRadioEvents(ForkChoice_CheckedChanged);
                SyncUpdateFilesCheckboxForForkChange();
            }

            ApplyTheme();
            _themeService.ThemeChanged += ThemeService_ThemeChanged;
        }

        private void ConfigureForkOptionsLayout()
        {
#if DEBUG
            rbDetanup.Visible = false;
            rbAlex.Visible = false;
            rbDetanupRepack.Visible = true;
            rbDetanupUpstream.Visible = true;
            rbAlexRepack.Visible = true;
            rbAlexUpstream.Visible = true;

            // Designer sizes are for the 2-option release layout; expand and reflow the footer.
            const int groupHeight = 130;
            const int gapAfterGroup = 15;
            int footerY = grpFork.Top + groupHeight + gapAfterGroup;
            grpFork.Size = new System.Drawing.Size(grpFork.Width, groupHeight);
            chkUpdateFilesOnOk.Location = new System.Drawing.Point(chkUpdateFilesOnOk.Left, footerY);
            // Bottom-anchored buttons keep their margin when ClientSize grows.
            const int buttonBottomMargin = 12;
            ClientSize = new System.Drawing.Size(
                ClientSize.Width,
                footerY + Math.Max(chkUpdateFilesOnOk.Height, btnOK.Height) + buttonBottomMargin);
#else
            rbDetanup.Visible = true;
            rbAlex.Visible = true;
            rbDetanupRepack.Visible = false;
            rbDetanupUpstream.Visible = false;
            rbAlexRepack.Visible = false;
            rbAlexUpstream.Visible = false;
#endif
        }

        private void WireForkRadioEvents(EventHandler handler)
        {
            foreach (RadioButton radio in EnumerateForkRadios())
                radio.CheckedChanged += handler;
        }

        private RadioButton[] EnumerateForkRadios()
        {
#if DEBUG
            return new[] { rbDetanupRepack, rbDetanupUpstream, rbAlexRepack, rbAlexUpstream };
#else
            return new[] { rbDetanup, rbAlex };
#endif
        }

        private void ClearForkSelection()
        {
            foreach (RadioButton radio in EnumerateForkRadios())
                radio.Checked = false;
        }

        private void ApplySavedSelection(GoldbergForkSource fork, GoldbergReleaseChannel channel)
        {
#if DEBUG
            GoldbergReleaseChannel effective = channel == GoldbergReleaseChannel.Auto
                ? GoldbergReleaseChannel.Repack
                : channel;
            if (fork == GoldbergForkSource.Alex)
            {
                if (effective == GoldbergReleaseChannel.Upstream)
                    rbAlexUpstream.Checked = true;
                else
                    rbAlexRepack.Checked = true;
            }
            else if (effective == GoldbergReleaseChannel.Upstream)
            {
                rbDetanupUpstream.Checked = true;
            }
            else
            {
                rbDetanupRepack.Checked = true;
            }
#else
            if (fork == GoldbergForkSource.Alex)
                rbAlex.Checked = true;
            else
                rbDetanup.Checked = true;
#endif
        }

        private bool TryGetSelectedFork(out GoldbergForkSource fork, out GoldbergReleaseChannel channel)
        {
#if DEBUG
            if (rbDetanupRepack.Checked)
            {
                fork = GoldbergForkSource.Detanup;
                channel = GoldbergReleaseChannel.Repack;
                return true;
            }
            if (rbDetanupUpstream.Checked)
            {
                fork = GoldbergForkSource.Detanup;
                channel = GoldbergReleaseChannel.Upstream;
                return true;
            }
            if (rbAlexRepack.Checked)
            {
                fork = GoldbergForkSource.Alex;
                channel = GoldbergReleaseChannel.Repack;
                return true;
            }
            if (rbAlexUpstream.Checked)
            {
                fork = GoldbergForkSource.Alex;
                channel = GoldbergReleaseChannel.Upstream;
                return true;
            }
#else
            if (rbAlex.Checked)
            {
                fork = GoldbergForkSource.Alex;
                channel = GoldbergReleaseChannel.Auto;
                return true;
            }
            if (rbDetanup.Checked)
            {
                fork = GoldbergForkSource.Detanup;
                channel = GoldbergReleaseChannel.Auto;
                return true;
            }
#endif
            fork = GoldbergForkSource.Detanup;
            channel = GoldbergReleaseChannel.Auto;
            return false;
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

        private void RadioFork_CheckedChanged(object sender, EventArgs e)
        {
            btnOK.Enabled = TryGetSelectedFork(out _, out _);
        }

        private void ForkChoice_CheckedChanged(object sender, EventArgs e)
        {
            SyncUpdateFilesCheckboxForForkChange();
        }

        private void SyncUpdateFilesCheckboxForForkChange()
        {
            if (_forceExplicitChoice || !chkUpdateFilesOnOk.Visible)
                return;
            if (!TryGetSelectedFork(out GoldbergForkSource selectedFork, out GoldbergReleaseChannel selectedChannel))
                return;
            bool selectionChanged = selectedFork != _forkWhenOpened
                || !GoldbergReleaseChannelIni.AreEquivalent(_channelWhenOpened, selectedChannel);
            chkUpdateFilesOnOk.Enabled = selectionChanged;
        }

        private void OnOk_Click(object sender, EventArgs e)
        {
            if (!TryGetSelectedFork(out GoldbergForkSource fork, out GoldbergReleaseChannel channel))
                return;

            var vr = ServiceLocator.AppDataService.SetGoldbergForkSource(fork, channel);
            if (!vr.IsValid)
            {
                FormMessageBoxHelper.ShowIfAlive(this, vr.ErrorMessage ?? "Could not save settings.", "Emulator Fork", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var downloadFromNewFork = !_forceExplicitChoice
                && (fork != _forkWhenOpened || !GoldbergReleaseChannelIni.AreEquivalent(_channelWhenOpened, channel))
                && chkUpdateFilesOnOk.Checked;

            var ownerForm = Owner as Form;
            DialogResult = DialogResult.OK;
            Close();

            if (downloadFromNewFork && ownerForm != null && !ownerForm.IsDisposed && !ownerForm.Disposing)
                EmulatorUpdateService.BeginDownloadAndInstallOnOwnerForm(ownerForm, Program.LogService);
        }
    }
}
