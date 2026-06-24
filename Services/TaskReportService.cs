using System;
using System.Windows.Forms;
using SmartGoldbergEmu.Abstractions;

namespace SmartGoldbergEmu.Services
{
    public class TaskReportService : ITaskReportService
    {
        private readonly ToolStripProgressBar _progressBar;
        private readonly ToolStripStatusLabel _statusLabel;
        private readonly Control _control;
        private Timer _autoClearTimer;

        public TaskReportService(ToolStripProgressBar progressBar, ToolStripStatusLabel statusLabel, Control control)
        {
            _progressBar = progressBar ?? throw new ArgumentNullException(nameof(progressBar));
            _statusLabel = statusLabel ?? throw new ArgumentNullException(nameof(statusLabel));
            _control = control ?? throw new ArgumentNullException(nameof(control));

            if (_control.InvokeRequired)
            {
                if (!ShouldSkipUpdate())
                    _control.Invoke(new Action(() => _progressBar.Visible = false));
            }
            else if (!ShouldSkipUpdate())
            {
                _progressBar.Visible = false;
            }
        }

        private bool ShouldSkipUpdate()
        {
            if (_control == null)
                return true;
            try
            {
                return _control.IsDisposed || _control.Disposing;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        }

        public void SetMessage(string message)
        {
            SetMessage(message, TaskReportKind.Info);
        }

        public void SetMessage(string message, TaskReportKind kind)
        {
            if (_control.InvokeRequired)
            {
                if (!ShouldSkipUpdate())
                    _control.BeginInvoke(new Action<string, TaskReportKind>(SetMessage), message, kind);
                return;
            }

            if (ShouldSkipUpdate())
                return;

            if (string.IsNullOrEmpty(message))
            {
                _statusLabel.Text = string.Empty;
                _progressBar.Visible = false;
                _progressBar.Value = 0;
            }
            else
            {
                string prefix = GetPrefixForKind(kind);
                _statusLabel.Text = string.IsNullOrEmpty(prefix) ? message : prefix + message;
            }
        }

        private static string GetPrefixForKind(TaskReportKind kind)
        {
            switch (kind)
            {
                case TaskReportKind.Warning: return "\u26A0 ";
                case TaskReportKind.Error: return "\u2717 ";
                default: return string.Empty;
            }
        }

        public void SetMessageWithAutoClear(string message, TaskReportKind kind = TaskReportKind.Info, int delayMs = 3000)
        {
            if (_control.InvokeRequired)
            {
                if (!ShouldSkipUpdate())
                    _control.BeginInvoke(new Action<string, TaskReportKind, int>(SetMessageWithAutoClear), message, kind, delayMs);
                return;
            }

            if (ShouldSkipUpdate())
                return;

            StopAutoClearTimer();

            _progressBar.Visible = false;
            _progressBar.Value = 0;
            SetMessage(message, kind);

            _autoClearTimer = new Timer();
            _autoClearTimer.Interval = delayMs;
            _autoClearTimer.Tick += AutoClearTimer_Tick;
            _autoClearTimer.Start();
        }

        private void AutoClearTimer_Tick(object sender, EventArgs e)
        {
            StopAutoClearTimer();
            if (ShouldSkipUpdate())
                return;
            SetMessage(string.Empty);
        }

        private void StopAutoClearTimer()
        {
            if (_autoClearTimer != null)
            {
                _autoClearTimer.Stop();
                _autoClearTimer.Tick -= AutoClearTimer_Tick;
                _autoClearTimer.Dispose();
                _autoClearTimer = null;
            }
        }

        public void SetProgress(int current, int total)
        {
            if (_control.InvokeRequired)
            {
                if (!ShouldSkipUpdate())
                    _control.BeginInvoke(new Action<int, int>(SetProgress), current, total);
                return;
            }

            if (ShouldSkipUpdate())
                return;

            if (total > 0)
            {
                int percentage = Math.Max(0, Math.Min(100, (current * 100) / total));
                _progressBar.Value = percentage;
                _progressBar.Visible = true;

                if (string.IsNullOrEmpty(_statusLabel.Text))
                {
                    _statusLabel.Text = $"Progress: {current}/{total}";
                }
            }
        }

        public void UpdateProgress(string message, int percentage)
        {
            if (_control.InvokeRequired)
            {
                if (!ShouldSkipUpdate())
                    _control.BeginInvoke(new Action<string, int>(UpdateProgress), message, percentage);
                return;
            }

            if (ShouldSkipUpdate())
                return;

            _statusLabel.Text = message ?? string.Empty;

            if (percentage > 0)
            {
                _progressBar.Value = Math.Max(0, Math.Min(100, percentage));
                _progressBar.Visible = true;
            }
            else if (percentage == 0 && string.IsNullOrEmpty(message))
            {
                _progressBar.Visible = false;
                _progressBar.Value = 0;
            }
        }

        public void StartProgress(string message = null)
        {
            if (_control.InvokeRequired)
            {
                if (!ShouldSkipUpdate())
                    _control.BeginInvoke(new Action<string>(StartProgress), message);
                return;
            }

            if (ShouldSkipUpdate())
                return;

            if (!string.IsNullOrEmpty(message))
            {
                _statusLabel.Text = message;
            }
            _progressBar.Visible = true;
            _progressBar.Value = 0;
        }

        public void CompleteProgress(string message = null)
        {
            if (_control.InvokeRequired)
            {
                if (!ShouldSkipUpdate())
                    _control.Invoke(new Action<string>(CompleteProgress), message);
                return;
            }

            if (ShouldSkipUpdate())
                return;

            _progressBar.Value = 100;

            if (message != null)
            {
                _statusLabel.Text = message;
            }
            else
            {
                _statusLabel.Text = string.Empty;
            }

            _progressBar.Visible = false;
            _progressBar.Value = 0;
        }

        public void Clear()
        {
            if (_control.InvokeRequired)
            {
                if (!ShouldSkipUpdate())
                    _control.Invoke(new Action(Clear));
                return;
            }

            if (ShouldSkipUpdate())
                return;

            StopAutoClearTimer();
            _statusLabel.Text = string.Empty;
            _progressBar.Visible = false;
            _progressBar.Value = 0;
        }
    }
}
