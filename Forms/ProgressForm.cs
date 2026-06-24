using System;
using System.Windows.Forms;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;

namespace SmartGoldbergEmu.Forms
{
    public partial class ProgressForm : Form
    {
        private int _cancelledFlag;
        private int _lastReportedPercentage;
        private readonly ThemeService _themeService;
        private Timer _autoCloseTimer;

        public ProgressForm()
            : this(ServiceLocator.ThemeService)
        {
        }

        public ProgressForm(ThemeService themeService)
        {
            InitializeComponent();
            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            btnCancel.Click += OnCancel_Click;
            ApplyTheme();
            _themeService.ThemeChanged += ThemeService_ThemeChanged;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            ApplyTheme();
        }

        public void UpdateProgress(string message, int percentage)
        {
            if (InvokeRequired)
            {
                if (IsDisposed || Disposing)
                    return;
                BeginInvoke(new Action(() => UpdateProgress(message, percentage)));
                return;
            }

            int clamped = Math.Max(0, Math.Min(100, percentage));
            if (clamped < _lastReportedPercentage)
                clamped = _lastReportedPercentage;
            else
                _lastReportedPercentage = clamped;

            lblStatus.Text = message ?? string.Empty;
            pbarProgress.Value = clamped;
        }

        public bool IsCancelled => System.Threading.Volatile.Read(ref _cancelledFlag) != 0;

        public void DisableCancel()
        {
            RunOnUiThread(() => { btnCancel.Enabled = false; });
        }

        public void ShowCancellationAndClose(string message)
        {
            ShowTerminalStateAndClose(message, 2000);
        }

        public void ShowSuccessAndClose(string message)
        {
            ShowTerminalStateAndClose(message, 1500);
        }

        private void ShowTerminalStateAndClose(string message, int closeDelayMs)
        {
            RunOnUiThread(() =>
            {
                _lastReportedPercentage = 100;
                lblStatus.Text = message ?? string.Empty;
                pbarProgress.Value = 100;
                btnCancel.Enabled = false;
                StopAndDisposeAutoCloseTimer();
                _autoCloseTimer = new Timer { Interval = closeDelayMs };
                _autoCloseTimer.Tick += AutoCloseTimer_Tick;
                _autoCloseTimer.Start();
            });
        }

        private void AutoCloseTimer_Tick(object sender, EventArgs e)
        {
            StopAndDisposeAutoCloseTimer();
            if (!IsDisposed && !Disposing)
                Close();
        }

        private void StopAndDisposeAutoCloseTimer()
        {
            if (_autoCloseTimer == null)
                return;
            _autoCloseTimer.Stop();
            _autoCloseTimer.Tick -= AutoCloseTimer_Tick;
            _autoCloseTimer.Dispose();
            _autoCloseTimer = null;
        }

        public void Reset()
        {
            System.Threading.Volatile.Write(ref _cancelledFlag, 0);
            _lastReportedPercentage = 0;
            StopAndDisposeAutoCloseTimer();
            btnCancel.Enabled = true;
            lblStatus.Text = "Preparing download...";
            pbarProgress.Value = 0;
            btnCancel.Text = "Cancel";
            btnCancel.Click -= OnClose_Click;
            btnCancel.Click -= OnCancel_Click;
            btnCancel.Click += OnCancel_Click;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            StopAndDisposeAutoCloseTimer();
            btnCancel.Click -= OnCancel_Click;
            btnCancel.Click -= OnClose_Click;
            _themeService.ThemeChanged -= ThemeService_ThemeChanged;
            base.OnFormClosed(e);
        }

        private void OnClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void OnCancel_Click(object sender, EventArgs e)
        {
            System.Threading.Volatile.Write(ref _cancelledFlag, 1);
            btnCancel.Enabled = false;
            lblStatus.Text = "Cancelling...";
        }

        private void ThemeService_ThemeChanged(object sender, ThemeChangedEventArgs e)
        {
            if (IsDisposed || Disposing)
                return;
            RunOnUiThread(ApplyTheme);
        }

        private void ApplyTheme()
        {
            _themeService.ApplyTheme(this);
        }

        private void RunOnUiThread(Action action)
        {
            if (IsDisposed || Disposing)
                return;
            if (InvokeRequired)
                Invoke(action);
            else
                action();
        }
    }
}
