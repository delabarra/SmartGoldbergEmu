using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SmartGoldbergEmu.Helpers
{
    // Win10 cannot recolor DWM captions; draw a gray title strip in the client area instead.
    internal sealed class DarkClientCaptionHelper : IDisposable
    {
        private const int CaptionHeight = 32;
        private const int ButtonWidth = 46;
        private const int ResizeBorder = 6;
        private const int WmNcHitTest = 0x0084;
        private const int HtClient = 1;
        private const int HtCaption = 2;
        private const int HtLeft = 10;
        private const int HtRight = 11;
        private const int HtTop = 12;
        private const int HtTopLeft = 13;
        private const int HtTopRight = 14;
        private const int HtBottom = 15;
        private const int HtBottomLeft = 16;
        private const int HtBottomRight = 17;
        private const int WmSysCommand = 0x0112;
        private const int ScMove = 0xF010;

        private readonly Form _form;
        private Panel _bar;
        private Label _title;
        private Button _btnMin;
        private Button _btnMax;
        private Button _btnClose;
        private bool _active;
        private bool _disposed;
        private FormBorderStyle _savedBorder;
        private bool _savedControlBox;
        private bool _savedMinimizeBox;
        private bool _savedMaximizeBox;
        private Color _captionColor = Color.FromArgb(45, 45, 48);
        private Color _textColor = Color.FromArgb(240, 240, 240);

        public DarkClientCaptionHelper(Form form)
        {
            _form = form ?? throw new ArgumentNullException(nameof(form));
        }

        public bool IsActive => _active;

        public void Sync(bool dark, Color captionColor, Color textColor, bool canNativeCaptionColor)
        {
            if (_disposed || _form.IsDisposed)
                return;

            _captionColor = captionColor;
            _textColor = textColor;
            bool want = dark && !canNativeCaptionColor;
            if (want)
                Enable();
            else
                Disable();

            if (_active)
                ApplyCaptionColors();
        }

        public bool TryHandleWndProc(ref Message m)
        {
            if (!_active || _disposed)
                return false;

            if (m.Msg != WmNcHitTest)
                return false;

            Point screen = new Point(m.LParam.ToInt32() & 0xFFFF, (m.LParam.ToInt32() >> 16) & 0xFFFF);
            // Sign-extend for multi-monitor negative coords.
            if (screen.X >= 0x8000)
                screen.X -= 0x10000;
            if (screen.Y >= 0x8000)
                screen.Y -= 0x10000;

            Point client = _form.PointToClient(screen);
            Rectangle bounds = _form.ClientRectangle;
            bool left = client.X < ResizeBorder;
            bool right = client.X >= bounds.Width - ResizeBorder;
            bool top = client.Y < ResizeBorder;
            bool bottom = client.Y >= bounds.Height - ResizeBorder;

            if (top && left) { m.Result = (IntPtr)HtTopLeft; return true; }
            if (top && right) { m.Result = (IntPtr)HtTopRight; return true; }
            if (bottom && left) { m.Result = (IntPtr)HtBottomLeft; return true; }
            if (bottom && right) { m.Result = (IntPtr)HtBottomRight; return true; }
            if (left) { m.Result = (IntPtr)HtLeft; return true; }
            if (right) { m.Result = (IntPtr)HtRight; return true; }
            if (top) { m.Result = (IntPtr)HtTop; return true; }
            if (bottom) { m.Result = (IntPtr)HtBottom; return true; }

            if (_bar != null && client.Y < _bar.Height && !IsOverCaptionButton(client))
            {
                m.Result = (IntPtr)HtCaption;
                return true;
            }

            m.Result = (IntPtr)HtClient;
            return true;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            Disable();
        }

        private void Enable()
        {
            if (_active)
                return;

            _savedBorder = _form.FormBorderStyle;
            _savedControlBox = _form.ControlBox;
            _savedMinimizeBox = _form.MinimizeBox;
            _savedMaximizeBox = _form.MaximizeBox;

            _form.FormBorderStyle = FormBorderStyle.None;
            _form.ControlBox = false;

            EnsureCaptionControls();
            _bar.Visible = true;
            _bar.BringToFront();
            UpdateButtonVisibility();
            ApplyCaptionColors();
            SyncTitleText();
            _form.TextChanged += OnFormTextChanged;
            _active = true;
        }

        private void Disable()
        {
            if (!_active)
                return;

            _form.TextChanged -= OnFormTextChanged;
            if (_bar != null)
                _bar.Visible = false;

            _form.FormBorderStyle = _savedBorder;
            _form.ControlBox = _savedControlBox;
            _form.MinimizeBox = _savedMinimizeBox;
            _form.MaximizeBox = _savedMaximizeBox;
            _active = false;
        }

        private void EnsureCaptionControls()
        {
            if (_bar != null)
                return;

            _bar = new Panel
            {
                Name = "pnlDarkClientCaption",
                Dock = DockStyle.Top,
                Height = CaptionHeight,
                TabStop = false
            };
            _bar.MouseDown += OnCaptionMouseDown;
            _bar.DoubleClick += OnCaptionDoubleClick;

            _title = new Label
            {
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0),
                TabStop = false
            };
            _title.MouseDown += OnCaptionMouseDown;
            _title.DoubleClick += OnCaptionDoubleClick;

            _btnClose = CreateCaptionButton("✕", OnCloseClick);
            _btnMax = CreateCaptionButton("☐", OnMaxClick);
            _btnMin = CreateCaptionButton("—", OnMinClick);

            _bar.Controls.Add(_title);
            _bar.Controls.Add(_btnMin);
            _bar.Controls.Add(_btnMax);
            _bar.Controls.Add(_btnClose);
            _form.Controls.Add(_bar);
        }

        private Button CreateCaptionButton(string text, EventHandler onClick)
        {
            var button = new Button
            {
                Text = text,
                Dock = DockStyle.Right,
                Width = ButtonWidth,
                FlatStyle = FlatStyle.Flat,
                TabStop = false,
                Cursor = Cursors.Arrow
            };
            button.FlatAppearance.BorderSize = 0;
            button.Click += onClick;
            return button;
        }

        private void ApplyCaptionColors()
        {
            if (_bar == null)
                return;

            _bar.BackColor = _captionColor;
            _title.BackColor = _captionColor;
            _title.ForeColor = _textColor;
            StyleCaptionButton(_btnMin);
            StyleCaptionButton(_btnMax);
            StyleCaptionButton(_btnClose);
            _btnClose.FlatAppearance.MouseOverBackColor = Color.FromArgb(200, 60, 60);
        }

        private void StyleCaptionButton(Button button)
        {
            if (button == null)
                return;
            button.BackColor = _captionColor;
            button.ForeColor = _textColor;
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(
                Math.Min(255, _captionColor.R + 25),
                Math.Min(255, _captionColor.G + 25),
                Math.Min(255, _captionColor.B + 25));
            button.FlatAppearance.MouseDownBackColor = Color.FromArgb(
                Math.Min(255, _captionColor.R + 40),
                Math.Min(255, _captionColor.G + 40),
                Math.Min(255, _captionColor.B + 40));
        }

        private void UpdateButtonVisibility()
        {
            if (_btnMin != null)
                _btnMin.Visible = _savedMinimizeBox;
            if (_btnMax != null)
                _btnMax.Visible = _savedMaximizeBox;
            if (_btnClose != null)
                _btnClose.Visible = _savedControlBox || true;
        }

        private void SyncTitleText()
        {
            if (_title != null)
                _title.Text = _form.Text;
        }

        private void OnFormTextChanged(object sender, EventArgs e)
        {
            SyncTitleText();
        }

        private void OnCaptionMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;
            if (_form.WindowState == FormWindowState.Maximized)
                return;

            ReleaseCapture();
            SendMessage(_form.Handle, WmSysCommand, (IntPtr)(ScMove | 0x0002), IntPtr.Zero);
        }

        private void OnCaptionDoubleClick(object sender, EventArgs e)
        {
            if (!_savedMaximizeBox)
                return;
            _form.WindowState = _form.WindowState == FormWindowState.Maximized
                ? FormWindowState.Normal
                : FormWindowState.Maximized;
            UpdateMaxButtonGlyph();
        }

        private void OnCloseClick(object sender, EventArgs e)
        {
            _form.Close();
        }

        private void OnMaxClick(object sender, EventArgs e)
        {
            _form.WindowState = _form.WindowState == FormWindowState.Maximized
                ? FormWindowState.Normal
                : FormWindowState.Maximized;
            UpdateMaxButtonGlyph();
        }

        private void OnMinClick(object sender, EventArgs e)
        {
            _form.WindowState = FormWindowState.Minimized;
        }

        private void UpdateMaxButtonGlyph()
        {
            if (_btnMax == null)
                return;
            _btnMax.Text = _form.WindowState == FormWindowState.Maximized ? "❐" : "☐";
        }

        private bool IsOverCaptionButton(Point formClientPoint)
        {
            if (_bar == null || formClientPoint.Y < 0 || formClientPoint.Y >= _bar.Height)
                return false;

            // Caption bar is docked full-width at Y=0, so form client X maps to bar X.
            Point onBar = new Point(formClientPoint.X, formClientPoint.Y);
            if (_btnClose != null && _btnClose.Visible && _btnClose.Bounds.Contains(onBar))
                return true;
            if (_btnMax != null && _btnMax.Visible && _btnMax.Bounds.Contains(onBar))
                return true;
            if (_btnMin != null && _btnMin.Visible && _btnMin.Bounds.Contains(onBar))
                return true;
            return false;
        }

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    }
}
