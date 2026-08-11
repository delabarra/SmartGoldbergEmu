using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;

namespace SmartGoldbergEmu.Forms
{
    public sealed class AppTaskDialogButton
    {
        public AppTaskDialogButton(int id, string text)
        {
            Id = id;
            Text = text ?? string.Empty;
        }

        public int Id { get; }
        public string Text { get; }
        public bool IsDefault { get; set; }
        public bool IsCancel { get; set; }
    }

    public sealed class AppTaskDialogLink
    {
        public AppTaskDialogLink(string text, string url)
        {
            Text = text ?? string.Empty;
            Url = url ?? string.Empty;
        }

        public string Text { get; }
        public string Url { get; }
    }

    public sealed class AppTaskDialogRequest
    {
        public string WindowTitle { get; set; } = ApplicationConstants.WindowTitle;
        // Plain body text only (no bold title line). Height grows with the message.
        public string Content { get; set; }
        public MessageBoxIcon Icon { get; set; } = MessageBoxIcon.None;
        // When set, overrides MessageBoxIcon (e.g. SystemIcons.Shield). Not disposed by the dialog.
        public Icon CustomIcon { get; set; }
        public string VerificationText { get; set; }
        public bool VerificationChecked { get; set; }
        public string FooterText { get; set; }
        public IList<AppTaskDialogLink> ContentLinks { get; set; }
        public IList<AppTaskDialogButton> Buttons { get; set; }
    }

    public sealed class AppTaskDialogResult
    {
        public AppTaskDialogResult(int buttonId, bool verificationChecked)
        {
            ButtonId = buttonId;
            VerificationChecked = verificationChecked;
        }

        public int ButtonId { get; }
        public bool VerificationChecked { get; }
    }

    // Task Dialog–style chrome: growing body, optional checkbox strip, button footer.
    public sealed class AppTaskDialogForm : Form
    {
        private const int FormWidth = 480;
        private const int LayoutMargin = 14;
        private const int IconSize = 32;
        private const int IconGap = 14;
        private const int ButtonWidth = 88;
        private const int ButtonHeight = 26;
        private const int ButtonGap = 8;
        // Match native Task Dialog / MessageBox command-area padding (was 12 — too tall).
        private const int BandPadY = 8;
        // Blank lines above the checkbox, owned by the checkbox slice (matches body TextRenderer line advance).
        private const int CheckStripBlankLines = 1;

        private readonly AppTaskDialogRequest _request;
        private readonly ThemeService _themeService;
        private Panel _pnlBody;
        private Panel _pnlCheckStrip;
        private Panel _pnlButtons;
        private CheckBox _chkVerification;
        private int _clickedButtonId = TaskDialogHelper.IdCancel;
        private Image _iconImage;

        private AppTaskDialogForm(AppTaskDialogRequest request, ThemeService themeService)
        {
            _request = request ?? throw new ArgumentNullException(nameof(request));
            _themeService = themeService;

            Text = string.IsNullOrWhiteSpace(request.WindowTitle)
                ? ApplicationConstants.WindowTitle
                : request.WindowTitle.Trim();
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Font;
            KeyPreview = true;
            Font = SystemFonts.MessageBoxFont;

            BuildLayout();
            ApplyThemeFromService();
            if (_themeService != null)
                _themeService.ThemeChanged += ThemeService_ThemeChanged;
        }

        public static AppTaskDialogResult Show(IWin32Window owner, AppTaskDialogRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (owner is Control c && (c.IsDisposed || c.Disposing))
                return new AppTaskDialogResult(TaskDialogHelper.IdCancel, false);

            using (var form = new AppTaskDialogForm(request, ServiceLocator.ThemeService))
            {
                if (owner != null)
                {
                    form.StartPosition = FormStartPosition.CenterParent;
                    form.ShowDialog(owner);
                }
                else
                {
                    // No owner (startup prompts): CenterParent places the dialog at the top-left.
                    form.StartPosition = FormStartPosition.CenterScreen;
                    form.ShowDialog();
                }

                bool verified = form._chkVerification != null && form._chkVerification.Checked;
                return new AppTaskDialogResult(form._clickedButtonId, verified);
            }
        }

        private void ThemeService_ThemeChanged(object sender, ThemeChangedEventArgs e)
        {
            if (IsDisposed || Disposing)
                return;
            if (InvokeRequired)
                Invoke((Action)ApplyThemeFromService);
            else
                ApplyThemeFromService();
        }

        private void ApplyThemeFromService()
        {
            _themeService?.ApplyTheme(this);
            ApplyTaskDialogChromeColors();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            UnsubscribeThemeChanged();
            base.OnFormClosed(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                UnsubscribeThemeChanged();
                if (_iconImage != null)
                {
                    _iconImage.Dispose();
                    _iconImage = null;
                }
            }

            base.Dispose(disposing);
        }

        private void UnsubscribeThemeChanged()
        {
            if (_themeService == null)
                return;
            _themeService.ThemeChanged -= ThemeService_ThemeChanged;
        }

        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                _clickedButtonId = ResolveCancelButtonId();
                DialogResult = DialogResult.Cancel;
                Close();
                return true;
            }

            return base.ProcessDialogKey(keyData);
        }

        // Native MessageBox / Task Dialog: Ctrl+C copies title, body, and button labels.
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.C))
            {
                CopyDialogTextToClipboard();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void CopyDialogTextToClipboard()
        {
            try
            {
                Clipboard.SetText(BuildClipboardText());
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning("Failed to copy dialog text: " + ex.Message);
            }
        }

        private string BuildClipboardText()
        {
            const string rule = "---------------------------";
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(rule);
            sb.AppendLine(Text ?? string.Empty);
            sb.AppendLine(rule);

            string content = (_request.Content ?? string.Empty).TrimEnd();
            if (!string.IsNullOrEmpty(content))
                sb.AppendLine(content);

            if (_request.ContentLinks != null)
            {
                foreach (AppTaskDialogLink link in _request.ContentLinks)
                {
                    if (link == null || string.IsNullOrWhiteSpace(link.Url))
                        continue;
                    sb.AppendLine(FormatContentLinkDisplayText(link));
                }
            }

            if (!string.IsNullOrWhiteSpace(_request.FooterText))
            {
                sb.AppendLine();
                sb.AppendLine(_request.FooterText.Trim());
            }

            if (!string.IsNullOrWhiteSpace(_request.VerificationText))
            {
                sb.AppendLine(rule);
                bool checkedState = _chkVerification != null && _chkVerification.Checked;
                sb.AppendLine((checkedState ? "[X] " : "[ ] ") + _request.VerificationText.Trim());
            }

            sb.AppendLine(rule);
            if (_request.Buttons != null && _request.Buttons.Count > 0)
            {
                var labels = new List<string>();
                foreach (AppTaskDialogButton b in _request.Buttons)
                {
                    if (b != null && !string.IsNullOrWhiteSpace(b.Text))
                        labels.Add(b.Text.Trim());
                }
                sb.AppendLine(string.Join("   ", labels));
            }
            else
            {
                sb.AppendLine("OK");
            }

            sb.AppendLine(rule);
            return sb.ToString();
        }

        private void BuildLayout()
        {
            int textLeft = LayoutMargin + IconSize + IconGap;
            int textWidth = FormWidth - textLeft - LayoutMargin;
            bool hasIcon = ResolveDialogIcon() != null;
            if (!hasIcon)
            {
                textLeft = LayoutMargin;
                textWidth = FormWidth - (LayoutMargin * 2);
            }

            _pnlBody = BuildBodyBand(textLeft, textWidth, hasIcon);
            _pnlCheckStrip = BuildCheckStrip(textLeft, textWidth);
            _pnlButtons = BuildButtonBand();

            int y = 0;
            _pnlBody.Location = new Point(0, y);
            y += _pnlBody.Height;

            if (_pnlCheckStrip != null)
            {
                _pnlCheckStrip.Location = new Point(0, y);
                Controls.Add(_pnlCheckStrip);
                y += _pnlCheckStrip.Height;
            }

            _pnlButtons.Location = new Point(0, y);
            y += _pnlButtons.Height;

            Controls.Add(_pnlBody);
            Controls.Add(_pnlButtons);
            ClientSize = new Size(FormWidth, y);
        }

        private Panel BuildBodyBand(int textLeft, int textWidth, bool hasIcon)
        {
            var panel = new Panel
            {
                Name = "pnlBody",
                Width = FormWidth,
                Location = new Point(0, 0)
            };

            // One blank line at the top of the body (above icon + message).
            int topPad = LayoutMargin + MeasureLineAdvance();

            if (hasIcon)
            {
                Icon stock = ResolveDialogIcon();
                _iconImage = stock.ToBitmap();
                panel.Controls.Add(new PictureBox
                {
                    Name = "picIcon",
                    Location = new Point(LayoutMargin, topPad),
                    Size = new Size(IconSize, IconSize),
                    SizeMode = PictureBoxSizeMode.StretchImage,
                    Image = _iconImage,
                    TabStop = false
                });
            }

            int y = topPad;
            string message = (_request.Content ?? string.Empty).TrimEnd();
            bool checkStripFollows = !string.IsNullOrWhiteSpace(_request.VerificationText);
            int contentBottom = y;

            if (!string.IsNullOrEmpty(message))
            {
                var lblContent = new Label
                {
                    Name = "lblContent",
                    AutoSize = false,
                    Location = new Point(textLeft, y),
                    Width = textWidth,
                    Text = message,
                    UseMnemonic = false
                };
                lblContent.Height = MeasureLabelHeight(lblContent.Text, Font, textWidth);
                if (hasIcon)
                    lblContent.Height = Math.Max(lblContent.Height, IconSize);
                panel.Controls.Add(lblContent);
                contentBottom = lblContent.Bottom;
                y = contentBottom + 8;
            }
            else if (hasIcon)
            {
                contentBottom = topPad + IconSize;
                y = contentBottom;
            }

            if (_request.ContentLinks != null)
            {
                bool addedLink = false;
                foreach (AppTaskDialogLink link in _request.ContentLinks)
                {
                    if (link == null || string.IsNullOrWhiteSpace(link.Url))
                        continue;

                    string url = link.Url.Trim();
                    string displayText;
                    int linkStart;
                    ResolveContentLinkDisplay(link.Text, url, out displayText, out linkStart);
                    if (string.IsNullOrWhiteSpace(displayText))
                        continue;

                    if (addedLink)
                        y = contentBottom + 2;

                    var lnk = new LinkLabel
                    {
                        Name = "lnkBody",
                        AutoSize = true,
                        MaximumSize = new Size(textWidth, 0),
                        Text = displayText,
                        Location = new Point(textLeft, y),
                        TabStop = true,
                        UseMnemonic = false
                    };
                    lnk.LinkArea = new LinkArea(linkStart, url.Length);
                    lnk.LinkClicked += (s, e) => OpenSafeUrl(url);
                    panel.Controls.Add(lnk);
                    contentBottom = lnk.Bottom;
                    addedLink = true;
                }

                if (addedLink)
                    y = contentBottom + 8;
            }

            if (!string.IsNullOrWhiteSpace(_request.FooterText))
            {
                var lblNote = new Label
                {
                    Name = "lblBodyNote",
                    AutoSize = false,
                    Location = new Point(textLeft, y),
                    Width = textWidth,
                    Text = _request.FooterText.Trim(),
                    UseMnemonic = false
                };
                lblNote.Height = MeasureLabelHeight(lblNote.Text, Font, textWidth);
                panel.Controls.Add(lblNote);
                contentBottom = lblNote.Bottom;
                y = contentBottom + 8;
            }

            // Checkbox slice owns one blank line above the checkbox; body stops at last content.
            int minBottom = hasIcon ? (topPad + IconSize + LayoutMargin) : (topPad + LayoutMargin);
            panel.Height = Math.Max(checkStripFollows ? contentBottom : y + LayoutMargin, minBottom);
            return panel;
        }

        // Dedicated strip above the button footer for verification checkboxes.
        // Top padding is N blank lines by default (part of this slice, not the body).
        private Panel BuildCheckStrip(int textLeft, int textWidth)
        {
            if (string.IsNullOrWhiteSpace(_request.VerificationText))
                return null;

            // Same line advance as the body label — Font.Height * N overshoots (2 looked like ~3).
            int topPad = MeasureLineAdvance() * CheckStripBlankLines;

            var panel = new Panel
            {
                Name = "pnlCheckStrip",
                Width = FormWidth
            };

            _chkVerification = new CheckBox
            {
                Name = "chkVerification",
                AutoSize = true,
                MaximumSize = new Size(FormWidth - (LayoutMargin * 2), 0),
                Text = _request.VerificationText.Trim(),
                Checked = _request.VerificationChecked,
                UseMnemonic = false,
                Location = new Point(textLeft, topPad),
                TabIndex = 0
            };
            panel.Controls.Add(_chkVerification);

            Size chkSize = _chkVerification.GetPreferredSize(new Size(FormWidth - (LayoutMargin * 2), 0));
            _chkVerification.Size = chkSize;
            panel.Height = topPad + chkSize.Height + BandPadY;
            return panel;
        }

        private Panel BuildButtonBand()
        {
            var panel = new Panel
            {
                Name = "pnlButtons",
                Width = FormWidth,
                Height = BandPadY + ButtonHeight + BandPadY
            };

            var buttons = _request.Buttons ?? new List<AppTaskDialogButton>();
            if (buttons.Count == 0)
            {
                buttons = new List<AppTaskDialogButton>
                {
                    new AppTaskDialogButton(TaskDialogHelper.IdOk, "OK") { IsDefault = true, IsCancel = true }
                };
            }

            var widths = new int[buttons.Count];
            int buttonRowWidth = 0;
            for (int i = 0; i < buttons.Count; i++)
            {
                widths[i] = MeasureButtonWidth(buttons[i].Text);
                buttonRowWidth += widths[i];
                if (i > 0)
                    buttonRowWidth += ButtonGap;
            }

            int buttonLeft = FormWidth - LayoutMargin - buttonRowWidth;
            if (buttonLeft < LayoutMargin)
                buttonLeft = LayoutMargin;

            Button defaultButton = null;
            Button cancelButton = null;
            int x = buttonLeft;
            for (int i = 0; i < buttons.Count; i++)
            {
                AppTaskDialogButton spec = buttons[i];
                // Do not store the id in Tag — ThemeService.ApplyTheme replaces Tag with "ThemedButton".
                int buttonId = spec.Id;
                var btn = new Button
                {
                    Name = "btnTaskDialog_" + buttonId,
                    Text = spec.Text,
                    Size = new Size(widths[i], ButtonHeight),
                    Location = new Point(x, BandPadY),
                    UseVisualStyleBackColor = true,
                    TabIndex = 10 + i
                };
                btn.Click += (s, e) => TaskButton_Click(buttonId);
                panel.Controls.Add(btn);
                x += widths[i] + ButtonGap;
                if (spec.IsDefault)
                    defaultButton = btn;
                if (spec.IsCancel)
                    cancelButton = btn;
            }

            if (defaultButton != null)
                AcceptButton = defaultButton;
            if (cancelButton != null)
                CancelButton = cancelButton;

            return panel;
        }

        private static string FormatContentLinkDisplayText(AppTaskDialogLink link)
        {
            if (link == null || string.IsNullOrWhiteSpace(link.Url))
                return string.Empty;

            string displayText;
            int linkStart;
            ResolveContentLinkDisplay(link.Text, link.Url.Trim(), out displayText, out linkStart);
            return displayText;
        }

        // Label stays plain text; only the URL is the clickable LinkArea.
        private static void ResolveContentLinkDisplay(
            string text,
            string url,
            out string displayText,
            out int linkStart)
        {
            string trimmedText = (text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(trimmedText)
                || string.Equals(trimmedText, url, StringComparison.OrdinalIgnoreCase))
            {
                displayText = url;
                linkStart = 0;
                return;
            }

            int embeddedUrlIndex = trimmedText.IndexOf(url, StringComparison.OrdinalIgnoreCase);
            if (embeddedUrlIndex >= 0)
            {
                displayText = trimmedText;
                linkStart = embeddedUrlIndex;
                return;
            }

            displayText = trimmedText + ": " + url;
            linkStart = trimmedText.Length + 2;
        }

        private int MeasureButtonWidth(string text)
        {
            Size size = TextRenderer.MeasureText(
                text ?? string.Empty,
                Font,
                Size.Empty,
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
            // Padding so long labels (e.g. "Launch without restoring") fit without clipping.
            int padded = size.Width + 24;
            return Math.Max(ButtonWidth, padded);
        }

        private static int MeasureLabelHeight(string text, Font font, int width)
        {
            Size size = TextRenderer.MeasureText(
                text ?? string.Empty,
                font,
                new Size(Math.Max(1, width), int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
            return Math.Max(size.Height + 2, font.Height);
        }

        // Vertical advance of one wrapped text line — same metrics as the body message label.
        private int MeasureLineAdvance()
        {
            const int probeWidth = 200;
            int oneLine = TextRenderer.MeasureText(
                "Ag",
                Font,
                new Size(probeWidth, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl).Height;
            int twoLines = TextRenderer.MeasureText(
                "Ag\nAg",
                Font,
                new Size(probeWidth, int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl).Height;
            int advance = twoLines - oneLine;
            return Math.Max(advance, 1);
        }

        private void ApplyTaskDialogChromeColors()
        {
            if (_pnlBody == null || _pnlButtons == null)
                return;

            ThemeColors colors = _themeService != null
                ? _themeService.GetThemeColors(_themeService.EffectiveTheme)
                : null;
            bool dark = colors != null && colors.Background.GetBrightness() < 0.5f;

            Color bodyBack = dark ? colors.Background : SystemColors.Window;
            Color footerBack = dark ? colors.ControlBackground : SystemColors.Control;
            Color bodyFore = dark ? colors.Foreground : SystemColors.WindowText;

            BackColor = bodyBack;
            ForeColor = bodyFore;

            // Body (white) → checkbox slice (same white, between body and footer) → button footer (grey).
            ApplyBandColors(_pnlBody, bodyBack, bodyFore);
            if (_pnlCheckStrip != null)
                ApplyBandColors(_pnlCheckStrip, bodyBack, bodyFore);
            ApplyBandColors(_pnlButtons, footerBack, bodyFore);
        }

        private static void ApplyBandColors(Panel band, Color back, Color fore)
        {
            if (band == null)
                return;

            band.BackColor = back;
            foreach (Control child in band.Controls)
            {
                if (child is Button)
                    continue;
                child.BackColor = back;
                if (!(child is LinkLabel) && !(child is PictureBox))
                    child.ForeColor = fore;
            }
        }

        private void TaskButton_Click(int buttonId)
        {
            _clickedButtonId = buttonId;
            DialogResult = DialogResult.OK;
            Close();
        }

        private int ResolveCancelButtonId()
        {
            if (_request.Buttons != null)
            {
                foreach (AppTaskDialogButton b in _request.Buttons)
                {
                    if (b != null && b.IsCancel)
                        return b.Id;
                }
            }

            return TaskDialogHelper.IdCancel;
        }

        private static void OpenSafeUrl(string url)
        {
            if (!PathValidationHelper.IsSafeUrl(url))
                return;

            try
            {
                System.Diagnostics.Process.Start(url);
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning("Failed to open AppTaskDialog link: " + ex.Message);
            }
        }

        private Icon ResolveDialogIcon()
        {
            if (_request.CustomIcon != null)
                return _request.CustomIcon;
            return MapStockIcon(_request.Icon);
        }

        private static Icon MapStockIcon(MessageBoxIcon icon)
        {
            switch (icon)
            {
                case MessageBoxIcon.Error:
                    return SystemIcons.Error;
                case MessageBoxIcon.Warning:
                    return SystemIcons.Warning;
                case MessageBoxIcon.Information:
                    return SystemIcons.Information;
                case MessageBoxIcon.Question:
                    return SystemIcons.Question;
                default:
                    return null;
            }
        }
    }
}
