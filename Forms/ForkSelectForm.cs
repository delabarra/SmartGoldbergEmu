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

        public ForkSelectForm()
            : this(forceExplicitChoice: false)
        {
        }

        /// <param name="forceExplicitChoice">If true, no fork is pre-selected and OK stays disabled until the user picks one (first-time download flow).</param>
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

            if (forceExplicitChoice)
            {
                chkUpdateFilesOnOk.Visible = false;
                rbDetanup.Checked = false;
                rbAlex.Checked = false;
                btnOK.Enabled = false;
                rbDetanup.CheckedChanged += RadioFork_CheckedChanged;
                rbAlex.CheckedChanged += RadioFork_CheckedChanged;
            }
            else
            {
                var current = ServiceLocator.AppDataService.GetGoldbergForkSource();
                if (current == GoldbergForkSource.Alex)
                    rbAlex.Checked = true;
                else
                    rbDetanup.Checked = true;
                rbDetanup.CheckedChanged += ForkChoice_CheckedChanged;
                rbAlex.CheckedChanged += ForkChoice_CheckedChanged;
                SyncUpdateFilesCheckboxForForkChange();
            }

            ApplyTheme();
            _themeService.ThemeChanged += ThemeService_ThemeChanged;
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
            btnOK.Enabled = rbDetanup.Checked || rbAlex.Checked;
        }

        private void ForkChoice_CheckedChanged(object sender, EventArgs e)
        {
            SyncUpdateFilesCheckboxForForkChange();
        }

        private void SyncUpdateFilesCheckboxForForkChange()
        {
            if (_forceExplicitChoice || !chkUpdateFilesOnOk.Visible)
                return;
            var selected = rbAlex.Checked ? GoldbergForkSource.Alex : GoldbergForkSource.Detanup;
            var forkChangedFromSaved = selected != _forkWhenOpened;
            chkUpdateFilesOnOk.Enabled = forkChangedFromSaved;
        }

        private void OnOk_Click(object sender, EventArgs e)
        {
            if (!rbDetanup.Checked && !rbAlex.Checked)
                return;

            var fork = rbAlex.Checked ? GoldbergForkSource.Alex : GoldbergForkSource.Detanup;
            var vr = ServiceLocator.AppDataService.SetGoldbergForkSource(fork);
            if (!vr.IsValid)
            {
                FormMessageBoxHelper.ShowIfAlive(this, vr.ErrorMessage ?? "Could not save settings.", "Emulator Fork", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var downloadFromNewFork = !_forceExplicitChoice
                && fork != _forkWhenOpened
                && chkUpdateFilesOnOk.Checked;

            var ownerForm = Owner as Form;
            DialogResult = DialogResult.OK;
            Close();

            if (downloadFromNewFork && ownerForm != null && !ownerForm.IsDisposed && !ownerForm.Disposing)
                EmulatorUpdateService.BeginDownloadAndInstallOnOwnerForm(ownerForm, Program.LogService);
        }
    }
}
