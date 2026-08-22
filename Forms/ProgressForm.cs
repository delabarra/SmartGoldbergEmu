using System;
using System.Windows.Forms;
using SmartGoldbergEmu.Services;

namespace SmartGoldbergEmu.Forms
{
    public partial class ProgressForm : ThemedForm
    {
        private int _cancelledFlag;
        private int _lastReportedPercentage;
        private Timer _autoCloseTimer;

        public ProgressForm()
            : this(ServiceLocator.ThemeService)
        {
        }

        public ProgressForm(ThemeService themeService)
            : base(themeService)
        {
            InitializeComponent();
            btnCancel.Click += OnCancel_Click;
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

            // Keep "Cancelling..." visible while background work winds down.
            if (IsCancelled)
                lblStatus.Text = "Cancelling...";
            else
                lblStatus.Text = message ?? string.Empty;
            pbarProgress.Value = clamped;
            // Startup update path pumps with DoEvents; force a paint so download updates are visible.
            lblStatus.Update();
            pbarProgress.Update();
        }

        public bool IsCancelled => System.Threading.Volatile.Read(ref _cancelledFlag) != 0;

        // After download/extract/install work finishes, only cleanup remains — do not accept cancel.
        public void DisableCancellation()
        {
            RunOnUiThread(() =>
            {
                if (IsDisposed || Disposing)
                    return;
                btnCancel.Enabled = false;
                CancelButton = null;
            });
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
            CancelButton = btnCancel;
            lblStatus.Text = "Preparing download...";
            pbarProgress.Value = 0;
            btnCancel.Text = "Cancel";
            btnCancel.Click -= OnClose_Click;
            btnCancel.Click -= OnCancel_Click;
            btnCancel.Click += OnCancel_Click;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            ReleaseUiSubscriptions();
            base.OnFormClosed(e);
        }

        // Also runs from Dispose when Close was skipped (e.g. only Hide then using-dispose).
        private void ReleaseUiSubscriptions()
        {
            StopAndDisposeAutoCloseTimer();
            btnCancel.Click -= OnCancel_Click;
            btnCancel.Click -= OnClose_Click;
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
