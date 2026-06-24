using System;
using System.Windows.Forms;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;

namespace SmartGoldbergEmu.Forms
{
    public partial class SteamlessOptionsForm : Form
    {
        private readonly ThemeService _themeService;

        public SteamlessCliOptions SelectedOptions { get; private set; }

        public SteamlessOptionsForm()
        {
            InitializeComponent();
            _themeService = ServiceLocator.ThemeService;
        }

        public static bool TryShow(string gameName, string executablePath, IWin32Window owner, out SteamlessCliOptions options)
        {
            options = null;
            using (var form = new SteamlessOptionsForm(gameName, executablePath))
            {
                if (form.ShowDialog(owner) != DialogResult.OK)
                    return false;

                options = form.SelectedOptions;
                return true;
            }
        }

        private SteamlessOptionsForm(string gameName, string executablePath)
        {
            InitializeComponent();

            _themeService = ServiceLocator.ThemeService;
            lblTitle.Text = "Steamless on " + (gameName ?? "game");
            lblExecutable.Text = "Executable: " + (executablePath ?? string.Empty);
            ApplyOptionsToForm(SteamlessCliOptions.Default);

            ApplyTheme();
            _themeService.ThemeChanged += ThemeService_ThemeChanged;
        }

        private void btnApply_Click(object sender, EventArgs e)
        {
            SelectedOptions = ReadOptionsFromForm();
        }

        private void ApplyOptionsToForm(SteamlessCliOptions options)
        {
            if (options == null)
                return;

            chkKeepBind.Checked = options.KeepBindSection;
            chkKeepStub.Checked = options.KeepDosStub;
            chkDumpPayload.Checked = options.DumpStubPayload;
            chkDumpDrmp.Checked = options.DumpSteamDrmpDll;
            chkRealign.Checked = options.RealignSections;
            chkRecalcChecksum.Checked = options.RecalculateChecksum;
            chkExperimental.Checked = options.UseExperimental;
        }

        private SteamlessCliOptions ReadOptionsFromForm()
        {
            return new SteamlessCliOptions
            {
                KeepBindSection = chkKeepBind.Checked,
                KeepDosStub = chkKeepStub.Checked,
                DumpStubPayload = chkDumpPayload.Checked,
                DumpSteamDrmpDll = chkDumpDrmp.Checked,
                RealignSections = chkRealign.Checked,
                RecalculateChecksum = chkRecalcChecksum.Checked,
                UseExperimental = chkExperimental.Checked
            };
        }

        private void ApplyTheme() => _themeService.ApplyTheme(this);

        private void ThemeService_ThemeChanged(object sender, ThemeChangedEventArgs e)
        {
            if (IsDisposed || Disposing)
                return;
            if (InvokeRequired)
                BeginInvoke(new Action(ApplyTheme));
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
