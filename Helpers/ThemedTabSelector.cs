using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Helpers
{
    // Hides native TabControl headers and drives selection from a themed button strip.
    internal sealed class ThemedTabSelector : IDisposable
    {
        internal const string HeaderHiddenTag = "TabHeaderHidden";
        internal const string SelectorButtonTag = "TabSelectorButton";
        internal const string SelectorButtonSelectedTag = "TabSelectorButtonSelected";

        private const float MaxFontSize = 8f;
        private const float MinFontSize = 6.5f;
        private const int ButtonHeight = 22;
        private const int ButtonGap = 2;
        private const int HorizontalTextPadding = 8;

        private readonly Form _host;
        private readonly TabControl _tabControl;
        private readonly Panel _strip;
        private readonly List<Button> _buttons = new List<Button>();
        private ThemeColors _colors;
        private int _reservedStripHeight;
        private bool _syncing;
        private bool _layingOut;
        private bool _disposed;

        public event EventHandler StripHeightChanged;

        public int StripHeight
        {
            get { return _strip != null && !_strip.IsDisposed ? _strip.Height : 0; }
        }

        private ThemedTabSelector(Form host, TabControl tabControl)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _tabControl = tabControl ?? throw new ArgumentNullException(nameof(tabControl));
            _strip = new Panel
            {
                Name = "pnlTabSelector",
                Margin = Padding.Empty,
                Padding = new Padding(2, 2, 2, 2),
                TabStop = false
            };
        }

        public static ThemedTabSelector Attach(Form host, TabControl tabControl)
        {
            if (host == null)
                throw new ArgumentNullException(nameof(host));
            if (tabControl == null)
                throw new ArgumentNullException(nameof(tabControl));
            if (tabControl.Parent != host)
                throw new InvalidOperationException("TabControl must be a direct child of the host form.");

            var selector = new ThemedTabSelector(host, tabControl);
            selector.Initialize();
            return selector;
        }

        public static bool IsHeaderHidden(TabControl tabControl)
        {
            return tabControl != null && tabControl.Tag != null
                && string.Equals(tabControl.Tag.ToString(), HeaderHiddenTag, StringComparison.Ordinal);
        }

        public static bool IsSelectorButton(Button button)
        {
            if (button == null || button.Tag == null)
                return false;
            string tag = button.Tag.ToString();
            return string.Equals(tag, SelectorButtonTag, StringComparison.Ordinal)
                || string.Equals(tag, SelectorButtonSelectedTag, StringComparison.Ordinal);
        }

        public static bool IsSelectorButtonSelected(Button button)
        {
            return button != null && button.Tag != null
                && string.Equals(button.Tag.ToString(), SelectorButtonSelectedTag, StringComparison.Ordinal);
        }

        public void ApplyColors(ThemeColors colors)
        {
            if (_disposed || colors == null || _strip.IsDisposed)
                return;

            _colors = colors;
            _strip.BackColor = colors.ControlBackground;
            for (int i = 0; i < _buttons.Count; i++)
                ApplyButtonColors(_buttons[i], colors);
        }

        private void Initialize()
        {
            HideNativeHeaders();

            _strip.Location = new Point(_tabControl.Left, _tabControl.Top);
            _strip.Width = _tabControl.Width;
            _strip.Height = _strip.Padding.Vertical + ButtonHeight;
            _strip.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            BuildButtons();
            LayoutButtonsOneLine();

            _host.Controls.Add(_strip);
            int tabChildIndex = _host.Controls.GetChildIndex(_tabControl);
            _host.Controls.SetChildIndex(_strip, tabChildIndex);

            ApplyStripHeightToTabControl(_strip.Height);

            _tabControl.SelectedIndexChanged += TabControl_SelectedIndexChanged;
            _tabControl.ControlAdded += TabControl_PagesChanged;
            _tabControl.ControlRemoved += TabControl_PagesChanged;
            _strip.SizeChanged += Strip_SizeChanged;
            _strip.Layout += Strip_Layout;

            SyncSelectionFromTabControl();
        }

        private void HideNativeHeaders()
        {
            _tabControl.Appearance = TabAppearance.FlatButtons;
            _tabControl.ItemSize = new Size(0, 1);
            _tabControl.SizeMode = TabSizeMode.Fixed;
            _tabControl.Multiline = true;
            _tabControl.Tag = HeaderHiddenTag;
        }

        private void BuildButtons()
        {
            for (int i = 0; i < _buttons.Count; i++)
            {
                Button old = _buttons[i];
                old.Click -= SelectorButton_Click;
                _strip.Controls.Remove(old);
                old.Dispose();
            }
            _buttons.Clear();

            FontFamily family = (_tabControl.Font ?? Control.DefaultFont).FontFamily;
            for (int i = 0; i < _tabControl.TabPages.Count; i++)
            {
                TabPage page = _tabControl.TabPages[i];
                var button = new Button
                {
                    Name = "btnTabSelector" + i,
                    Text = page.Text,
                    AutoSize = false,
                    FlatStyle = FlatStyle.Flat,
                    UseVisualStyleBackColor = false,
                    Font = new Font(family, MaxFontSize, FontStyle.Regular),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Margin = Padding.Empty,
                    Padding = Padding.Empty,
                    Tag = SelectorButtonTag,
                    TabStop = true,
                    Cursor = Cursors.Hand,
                    Height = ButtonHeight
                };
                button.FlatAppearance.BorderSize = 1;
                button.Click += SelectorButton_Click;
                _buttons.Add(button);
                _strip.Controls.Add(button);
            }
        }

        private void SelectorButton_Click(object sender, EventArgs e)
        {
            var button = sender as Button;
            if (button == null || _syncing)
                return;

            int index = _buttons.IndexOf(button);
            if (index < 0 || index >= _tabControl.TabCount)
                return;

            if (_tabControl.SelectedIndex != index)
                _tabControl.SelectedIndex = index;
            else
                SyncSelectionFromTabControl();
        }

        private void TabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            SyncSelectionFromTabControl();
        }

        private void TabControl_PagesChanged(object sender, ControlEventArgs e)
        {
            if (_disposed || !(e.Control is TabPage))
                return;

            BuildButtons();
            LayoutButtonsOneLine();
            SyncSelectionFromTabControl();
        }

        private void Strip_Layout(object sender, LayoutEventArgs e)
        {
            LayoutButtonsOneLine();
        }

        private void Strip_SizeChanged(object sender, EventArgs e)
        {
            LayoutButtonsOneLine();
        }

        private void LayoutButtonsOneLine()
        {
            if (_disposed || _layingOut || _strip.IsDisposed || _buttons.Count == 0)
                return;

            _layingOut = true;
            try
            {
                int availableWidth = Math.Max(1, _strip.ClientSize.Width - _strip.Padding.Horizontal);
                if (availableWidth <= 1 && _strip.Width > 0)
                    availableWidth = Math.Max(1, _strip.Width - _strip.Padding.Horizontal);

                int gapTotal = ButtonGap * Math.Max(0, _buttons.Count - 1);
                int contentWidth = Math.Max(1, availableWidth - gapTotal);

                FontFamily family = _buttons[0].Font.FontFamily;
                float fontSize = MaxFontSize;
                int[] widths = new int[_buttons.Count];

                while (true)
                {
                    using (var measureFont = new Font(family, fontSize, FontStyle.Regular))
                    {
                        int total = 0;
                        for (int i = 0; i < _buttons.Count; i++)
                        {
                            Size textSize = TextRenderer.MeasureText(
                                _buttons[i].Text,
                                measureFont,
                                new Size(int.MaxValue, ButtonHeight),
                                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                            widths[i] = textSize.Width + HorizontalTextPadding;
                            total += widths[i];
                        }

                        if (total <= contentWidth || fontSize <= MinFontSize)
                        {
                            if (total < contentWidth)
                                DistributeExtraWidth(widths, contentWidth - total);
                            else if (total > contentWidth)
                                ShrinkWidthsToFit(widths, total, contentWidth);

                            ApplyFontToButtons(family, fontSize);
                            break;
                        }
                    }

                    fontSize -= 0.5f;
                    if (fontSize < MinFontSize)
                        fontSize = MinFontSize;
                }

                int x = _strip.Padding.Left;
                int y = _strip.Padding.Top;
                for (int i = 0; i < _buttons.Count; i++)
                {
                    _buttons[i].SetBounds(x, y, Math.Max(1, widths[i]), ButtonHeight);
                    x += widths[i] + ButtonGap;
                }

                int stripHeight = _strip.Padding.Vertical + ButtonHeight;
                if (_strip.Height != stripHeight)
                    _strip.Height = stripHeight;

                ApplyStripHeightToTabControl(_strip.Height);
            }
            finally
            {
                _layingOut = false;
            }
        }

        private void ApplyFontToButtons(FontFamily family, float fontSize)
        {
            for (int i = 0; i < _buttons.Count; i++)
            {
                Font old = _buttons[i].Font;
                _buttons[i].Font = new Font(family, fontSize, FontStyle.Regular);
                if (old != null && !old.IsSystemFont)
                    old.Dispose();
            }
        }

        private static void DistributeExtraWidth(int[] widths, int extra)
        {
            if (extra <= 0 || widths.Length == 0)
                return;

            int baseAdd = extra / widths.Length;
            int remain = extra % widths.Length;
            for (int i = 0; i < widths.Length; i++)
            {
                widths[i] += baseAdd;
                if (i < remain)
                    widths[i]++;
            }
        }

        private static void ShrinkWidthsToFit(int[] widths, int total, int contentWidth)
        {
            if (total <= contentWidth || widths.Length == 0)
                return;

            // Proportional shrink so every button stays on one line when text is still wide at MinFontSize.
            for (int i = 0; i < widths.Length; i++)
            {
                widths[i] = Math.Max(1, (int)Math.Floor(widths[i] * (double)contentWidth / total));
            }

            int sum = 0;
            for (int i = 0; i < widths.Length; i++)
                sum += widths[i];
            int fix = contentWidth - sum;
            for (int i = 0; fix != 0 && i < widths.Length; i++)
            {
                if (fix > 0)
                {
                    widths[i]++;
                    fix--;
                }
                else if (widths[i] > 1)
                {
                    widths[i]--;
                    fix++;
                }
            }
        }

        private void ApplyStripHeightToTabControl(int stripHeight)
        {
            int delta = stripHeight - _reservedStripHeight;
            if (delta == 0)
                return;

            _tabControl.Top += delta;
            _tabControl.Height = Math.Max(1, _tabControl.Height - delta);
            _reservedStripHeight = stripHeight;

            EventHandler handler = StripHeightChanged;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        private void SyncSelectionFromTabControl()
        {
            if (_disposed || _syncing)
                return;

            _syncing = true;
            try
            {
                int selected = _tabControl.SelectedIndex;
                for (int i = 0; i < _buttons.Count; i++)
                {
                    bool isSelected = i == selected;
                    _buttons[i].Tag = isSelected ? SelectorButtonSelectedTag : SelectorButtonTag;
                    if (_colors != null)
                        ApplyButtonColors(_buttons[i], _colors);
                    else
                        _buttons[i].Invalidate();
                }
            }
            finally
            {
                _syncing = false;
            }
        }

        private static void ApplyButtonColors(Button button, ThemeColors colors)
        {
            if (button == null || button.IsDisposed)
                return;

            bool selected = IsSelectorButtonSelected(button);
            if (selected)
            {
                button.BackColor = colors.Highlight;
                button.ForeColor = colors.HighlightText;
                button.FlatAppearance.BorderColor = colors.Highlight;
                button.FlatAppearance.MouseOverBackColor = colors.Highlight;
                button.FlatAppearance.MouseDownBackColor = colors.Highlight;
            }
            else
            {
                button.BackColor = colors.ControlBackground;
                button.ForeColor = colors.ControlForeground;
                button.FlatAppearance.BorderColor = colors.Border;
                button.FlatAppearance.MouseOverBackColor = colors.Background;
                button.FlatAppearance.MouseDownBackColor = colors.Background;
            }
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.UseVisualStyleBackColor = false;
            button.TextAlign = ContentAlignment.MiddleCenter;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            _tabControl.SelectedIndexChanged -= TabControl_SelectedIndexChanged;
            _tabControl.ControlAdded -= TabControl_PagesChanged;
            _tabControl.ControlRemoved -= TabControl_PagesChanged;
            _strip.Layout -= Strip_Layout;
            _strip.SizeChanged -= Strip_SizeChanged;

            for (int i = 0; i < _buttons.Count; i++)
            {
                _buttons[i].Click -= SelectorButton_Click;
                _buttons[i].Dispose();
            }
            _buttons.Clear();

            if (!_strip.IsDisposed)
            {
                _host.Controls.Remove(_strip);
                _strip.Dispose();
            }
        }
    }
}
