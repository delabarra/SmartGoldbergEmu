using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
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

    // One verification checkbox in the strip between body and button footer.
    public sealed class AppTaskDialogVerification
    {
        public AppTaskDialogVerification(string text, bool isChecked = false)
        {
            Text = text ?? string.Empty;
            Checked = isChecked;
        }

        public string Text { get; }
        public bool Checked { get; set; }
    }

    public sealed class AppTaskDialogRequest
    {
        public string WindowTitle { get; set; } = ApplicationConstants.WindowTitle;
        // Plain body text only (no bold title line). Height grows with the message.
        public string Content { get; set; }
        public MessageBoxIcon Icon { get; set; } = MessageBoxIcon.None;
        // When set, overrides MessageBoxIcon (e.g. SystemIcons.Shield). Not disposed by the dialog.
        public Icon CustomIcon { get; set; }
        // Single-checkbox shorthand. Combined with Verifications (this one first when both are set).
        public string VerificationText { get; set; }
        public bool VerificationChecked { get; set; }
        // Additional (or sole) verification checkboxes, stacked in the check strip.
        public IList<AppTaskDialogVerification> Verifications { get; set; }
        public string FooterText { get; set; }
        public IList<AppTaskDialogLink> ContentLinks { get; set; }
        public IList<AppTaskDialogButton> Buttons { get; set; }
        // Optional client-width cap in pixels. 0 = FormWidthMax (standard Task Dialog–like).
        public int MaxClientWidth { get; set; }
        // Optional blank lines above icon/message. 0 = default (BodyTopBlankLines).
        public int TopBlankLines { get; set; }
    }

    public sealed class AppTaskDialogResult
    {
        public AppTaskDialogResult(int buttonId, IList<bool> verificationStates)
        {
            ButtonId = buttonId;
            if (verificationStates == null || verificationStates.Count == 0)
                VerificationStates = new bool[0];
            else
            {
                var copy = new bool[verificationStates.Count];
                for (int i = 0; i < verificationStates.Count; i++)
                    copy[i] = verificationStates[i];
                VerificationStates = copy;
            }
        }

        public int ButtonId { get; }
        // First checkbox when present (same as VerificationStates[0]).
        public bool VerificationChecked
        {
            get { return VerificationStates.Count > 0 && VerificationStates[0]; }
        }
        public IReadOnlyList<bool> VerificationStates { get; }

        public bool GetVerificationChecked(int index)
        {
            return index >= 0 && index < VerificationStates.Count && VerificationStates[index];
        }
    }

    // Task Dialog–style chrome: growing body, optional checkbox strip, button footer.
    public sealed class AppTaskDialogForm : ThemedForm
    {
        // Default max client width — typical Windows Task Dialog / MessageBox range (~450–550px).
        private const int FormWidthMax = 526;
        private const int FormWidthMin = 280;
        private const int LayoutMargin = 14;
        private const int IconSize = 32;
        private const int IconGap = 14;
        private const int ButtonWidth = 88;
        private const int ButtonHeight = 26;
        private const int ButtonGap = 8;
        // Match native Task Dialog / MessageBox command-area padding (was 12 — too tall).
        private const int BandPadY = 8;
        // Blank lines around the checkbox, owned by the checkbox slice (matches body TextRenderer line advance).
        // Standard Task Dialog rhythm: text → 2 lines → checkbox → 2 lines → button slice.
        private const int CheckStripBlankLinesAbove = 2;
        private const int CheckStripBlankLinesBelow = 2;
        // When there is no checkbox strip: text → 2 lines → button slice (same body chrome).
        private const int PreButtonSpacerBlankLines = 2;
        // Default blank lines at the top of the body (above icon + message). Override via TopBlankLines.
        private const int BodyTopBlankLines = 1;

        private readonly AppTaskDialogRequest _request;
        private Panel _pnlBody;
        private Panel _pnlCheckStrip;
        private Panel _pnlPreButtonSpacer;
        private Panel _pnlButtons;
        private readonly List<CheckBox> _chkVerifications = new List<CheckBox>();
        private int _clickedButtonId = TaskDialogHelper.IdCancel;
        private Image _iconImage;
        private int _clientWidth = FormWidthMax;
        // Frame→text inset (after icon column when present). Mirrored on the right so both sides match.
        private int _contentLeft = LayoutMargin;

        private AppTaskDialogForm(AppTaskDialogRequest request, ThemeService themeService)
            : base(themeService ?? ServiceLocator.ThemeService)
        {
            _request = request ?? throw new ArgumentNullException(nameof(request));

            Text = string.IsNullOrWhiteSpace(request.WindowTitle)
                ? ApplicationConstants.WindowTitle
                : request.WindowTitle.Trim();
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            // Own taskbar button so modal prompts stay findable when buried under other windows.
            ShowInTaskbar = true;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Font;
            KeyPreview = true;
            Font = SystemFonts.MessageBoxFont;

            BuildLayout();
        }

        public static AppTaskDialogResult Show(IWin32Window owner, AppTaskDialogRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (owner is Control c && (c.IsDisposed || c.Disposing))
                return new AppTaskDialogResult(TaskDialogHelper.IdCancel, null);

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

                return new AppTaskDialogResult(form._clickedButtonId, form.CollectVerificationStates());
            }
        }

        protected override void OnThemeApplied()
        {
            ApplyTaskDialogChromeColors();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_iconImage != null)
                {
                    _iconImage.Dispose();
                    _iconImage = null;
                }
            }

            base.Dispose(disposing);
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
            if (IsCopyShortcut(keyData))
            {
                CopyDialogTextToClipboard();
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        // KeyPreview backup — LinkLabel focus can skip ProcessCmdKey on some paths.
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e != null && e.Control && !e.Alt && e.KeyCode == Keys.C)
            {
                CopyDialogTextToClipboard();
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            base.OnKeyDown(e);
        }

        private static bool IsCopyShortcut(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            Keys mods = keyData & Keys.Modifiers;
            return key == Keys.C
                && (mods & Keys.Control) == Keys.Control
                && (mods & Keys.Alt) == 0;
        }

        private void CopyDialogTextToClipboard()
        {
            string plain = BuildClipboardText();
            if (string.IsNullOrEmpty(plain))
                return;

            // Plain Unicode text only. Retry — clipboard is often briefly locked by other apps.
            const int maxAttempts = 8;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    Clipboard.Clear();
                    Clipboard.SetText(plain, TextDataFormat.UnicodeText);
                    return;
                }
                catch (ExternalException)
                {
                    Thread.Sleep(15);
                }
                catch (Exception ex)
                {
                    Program.LogService?.LogWarning("Failed to copy dialog text: " + ex.Message);
                    return;
                }
            }

            Program.LogService?.LogWarning("Failed to copy dialog text: clipboard stayed locked.");
        }

        private string BuildClipboardText()
        {
            const string rule = "---------------------------";
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(rule);
            sb.AppendLine(Text ?? string.Empty);
            sb.AppendLine(rule);

            string content = NormalizeDialogText(_request.Content);
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
                sb.AppendLine(NormalizeDialogText(_request.FooterText));
            }

            List<AppTaskDialogVerification> verifications = ResolveVerifications(_request);
            if (verifications.Count > 0)
            {
                sb.AppendLine(rule);
                for (int i = 0; i < verifications.Count; i++)
                {
                    bool checkedState = i < _chkVerifications.Count && _chkVerifications[i].Checked;
                    sb.AppendLine((checkedState ? "[X] " : "[ ] ") + verifications[i].Text.Trim());
                }
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
            bool hasIcon = ResolveDialogIcon() != null;
            _contentLeft = hasIcon ? LayoutMargin + IconSize + IconGap : LayoutMargin;
            _clientWidth = MeasureClientWidth();

            // Equal frame→text padding on both sides (icon lives inside the left inset).
            int textWidth = _clientWidth - (_contentLeft * 2);

            _pnlBody = BuildBodyBand(_contentLeft, textWidth, hasIcon);
            _pnlCheckStrip = BuildCheckStrip(_contentLeft, textWidth);
            // Same 2-line gap before the button slice when there is no verification strip.
            _pnlPreButtonSpacer = _pnlCheckStrip == null ? BuildPreButtonSpacer() : null;
            _pnlButtons = BuildButtonBand();

            int y = 0;
            _pnlBody.Location = new Point(0, y);
            y += _pnlBody.Height;
            Controls.Add(_pnlBody);

            if (_pnlCheckStrip != null)
            {
                _pnlCheckStrip.Location = new Point(0, y);
                Controls.Add(_pnlCheckStrip);
                y += _pnlCheckStrip.Height;
            }
            else if (_pnlPreButtonSpacer != null)
            {
                _pnlPreButtonSpacer.Location = new Point(0, y);
                Controls.Add(_pnlPreButtonSpacer);
                y += _pnlPreButtonSpacer.Height;
            }

            _pnlButtons.Location = new Point(0, y);
            y += _pnlButtons.Height;
            Controls.Add(_pnlButtons);
            ClientSize = new Size(_clientWidth, y);
        }

        // Prefer hugging short content; grow for wide button rows; soft-wrap long prose at MaxClientWidth.
        // Text uses mirrored _contentLeft insets; buttons still use LayoutMargin at the right edge.
        private int MeasureClientWidth()
        {
            int maxText = 0;
            MaxUnwrappedLineWidth(NormalizeDialogText(_request.Content), ref maxText);
            MaxUnwrappedLineWidth(NormalizeDialogText(_request.FooterText), ref maxText);
            foreach (AppTaskDialogVerification item in ResolveVerifications(_request))
                MaxUnwrappedLineWidth(NormalizeDialogText(item.Text), ref maxText);
            if (_request.ContentLinks != null)
            {
                foreach (AppTaskDialogLink link in _request.ContentLinks)
                {
                    if (link == null || string.IsNullOrWhiteSpace(link.Url))
                        continue;
                    string display = FormatContentLinkDisplayText(link);
                    MaxUnwrappedLineWidth(display, ref maxText);
                }
            }

            int contentWidth = (_contentLeft * 2) + maxText;
            int widthCap = ResolveMaxClientWidth();
            if (contentWidth > widthCap)
                contentWidth = widthCap;

            // Button row: last button's right edge at LayoutMargin; others grow left.
            int buttonsWidth = MeasurePreferredButtonRowWidth() + (LayoutMargin * 2);
            int width = Math.Max(contentWidth, buttonsWidth);
            if (width < FormWidthMin)
                width = FormWidthMin;
            if (width > widthCap)
                width = widthCap;
            return width;
        }

        private int ResolveMaxClientWidth()
        {
            int requested = _request.MaxClientWidth;
            if (requested > 0)
                return Math.Max(FormWidthMin, requested);
            return FormWidthMax;
        }

        private int ResolveTopBlankLines()
        {
            int requested = _request.TopBlankLines;
            if (requested > 0)
                return requested;
            return BodyTopBlankLines;
        }

        private void MaxUnwrappedLineWidth(string text, ref int maxWidth)
        {
            if (string.IsNullOrEmpty(text))
                return;

            string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
            string[] lines = normalized.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.Length == 0)
                    continue;
                Size size = TextRenderer.MeasureText(
                    line,
                    Font,
                    Size.Empty,
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
                if (size.Width > maxWidth)
                    maxWidth = size.Width;
            }
        }

        private int MeasurePreferredButtonRowWidth()
        {
            var buttons = _request.Buttons;
            if (buttons == null || buttons.Count == 0)
                return MeasureButtonWidth("OK");

            int total = 0;
            for (int i = 0; i < buttons.Count; i++)
            {
                if (i > 0)
                    total += ButtonGap;
                string label = buttons[i] != null ? buttons[i].Text : string.Empty;
                total += MeasureButtonWidth(label);
            }
            return total;
        }

        private Panel BuildBodyBand(int textLeft, int textWidth, bool hasIcon)
        {
            var panel = new Panel
            {
                Name = "pnlBody",
                Width = _clientWidth,
                Location = new Point(0, 0)
            };

            // Blank lines at the top of the body (above icon + message).
            int topPad = LayoutMargin + (MeasureLineAdvance() * ResolveTopBlankLines());

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
            string message = NormalizeDialogText(_request.Content);
            bool hasFooter = !string.IsNullOrWhiteSpace(_request.FooterText);
            bool hasLinks = false;
            if (_request.ContentLinks != null)
            {
                foreach (AppTaskDialogLink link in _request.ContentLinks)
                {
                    if (link != null && !string.IsNullOrWhiteSpace(link.Url))
                    {
                        hasLinks = true;
                        break;
                    }
                }
            }

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
                lblContent.Height = MeasureBodyTextHeight(message, textWidth);
                if (hasIcon)
                    lblContent.Height = Math.Max(lblContent.Height, IconSize);
                panel.Controls.Add(lblContent);
                contentBottom = lblContent.Bottom;
                // Gap only when another body block follows — never a trailing blank line under the message.
                if (hasLinks || hasFooter)
                    y = contentBottom + MeasureLineAdvance();
                else
                    y = contentBottom;
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

                    // Link sits directly under the intro line (e.g. "Get a free … key:").
                    // Keep 2px under the previous block; AutoSize PreferredSize clips underline/descenders.
                    y = contentBottom + 2;

                    var lnk = new LinkLabel
                    {
                        Name = "lnkBody",
                        AutoSize = false,
                        Text = displayText,
                        Location = new Point(textLeft, y),
                        TabStop = true,
                        UseMnemonic = false
                    };
                    lnk.LinkArea = new LinkArea(linkStart, Math.Min(url.Length, displayText.Length - linkStart));
                    lnk.LinkClicked += (s, e) => OpenSafeUrl(url);
                    panel.Controls.Add(lnk);

                    Size pref = TextRenderer.MeasureText(
                        displayText,
                        lnk.Font,
                        new Size(Math.Max(1, textWidth), int.MaxValue),
                        TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
                    // Extra pixels for LinkLabel underline + glyph descenders (g/p/y).
                    lnk.Size = new Size(Math.Max(1, Math.Min(textWidth, pref.Width + 2)), Math.Max(pref.Height, Font.Height) + 4);
                    contentBottom = lnk.Bottom;
                    addedLink = true;
                }

                if (addedLink)
                    y = hasFooter ? contentBottom + MeasureLineAdvance() : contentBottom;
            }

            if (hasFooter)
            {
                string footer = NormalizeDialogText(_request.FooterText);
                var lblNote = new Label
                {
                    Name = "lblBodyNote",
                    AutoSize = false,
                    Location = new Point(textLeft, y),
                    Width = textWidth,
                    Text = footer,
                    UseMnemonic = false
                };
                lblNote.Height = MeasureBodyTextHeight(footer, textWidth);
                panel.Controls.Add(lblNote);
                contentBottom = lblNote.Bottom;
            }

            // Spacer / checkbox slice owns the gap above the button footer; body stops at last content.
            int minBottom = hasIcon ? (topPad + IconSize + LayoutMargin) : (topPad + LayoutMargin);
            panel.Height = Math.Max(contentBottom, minBottom);
            return panel;
        }

        // Empty body-colored band: text → 2 lines → button slice (used when there is no checkbox strip).
        private Panel BuildPreButtonSpacer()
        {
            return new Panel
            {
                Name = "pnlPreButtonSpacer",
                Width = _clientWidth,
                Height = MeasureLineAdvance() * PreButtonSpacerBlankLines
            };
        }

        // Strip trailing blank lines at EOF only. Convert to platform newlines so Label hard-breaks
        // (lone \n can collapse to a space in some WinForms/GDI paths).
        private static string NormalizeDialogText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n').TrimEnd();
            return normalized.Replace("\n", Environment.NewLine);
        }

        // Dedicated strip above the button footer for verification checkboxes.
        // Vertical padding is blank lines owned by this slice, not the body.
        private Panel BuildCheckStrip(int textLeft, int textWidth)
        {
            List<AppTaskDialogVerification> items = ResolveVerifications(_request);
            if (items.Count == 0)
                return null;

            // Same line advance as the body label — Font.Height * N overshoots.
            int line = MeasureLineAdvance();
            int topPad = line * CheckStripBlankLinesAbove;
            int bottomPad = line * CheckStripBlankLinesBelow;
            // Tight stack between rows; outer 2-line pads stay on the strip edges.
            int itemGap = Math.Max(4, line / 4);

            var panel = new Panel
            {
                Name = "pnlCheckStrip",
                Width = _clientWidth
            };

            _chkVerifications.Clear();
            int y = topPad;
            for (int i = 0; i < items.Count; i++)
            {
                AppTaskDialogVerification item = items[i];
                var chk = new CheckBox
                {
                    Name = "chkVerification" + i,
                    AutoSize = true,
                    MaximumSize = new Size(textWidth, 0),
                    Text = item.Text.Trim(),
                    Checked = item.Checked,
                    UseMnemonic = false,
                    Location = new Point(textLeft, y),
                    TabIndex = i
                };
                panel.Controls.Add(chk);
                Size chkSize = chk.GetPreferredSize(new Size(textWidth, 0));
                chk.Size = chkSize;
                _chkVerifications.Add(chk);
                y += chkSize.Height;
                if (i < items.Count - 1)
                    y += itemGap;
            }

            panel.Height = y + bottomPad;
            return panel;
        }

        // VerificationText first (when set), then Verifications — same order returned in the result.
        private static List<AppTaskDialogVerification> ResolveVerifications(AppTaskDialogRequest request)
        {
            var list = new List<AppTaskDialogVerification>();
            if (request == null)
                return list;

            if (!string.IsNullOrWhiteSpace(request.VerificationText))
                list.Add(new AppTaskDialogVerification(request.VerificationText.Trim(), request.VerificationChecked));

            if (request.Verifications != null)
            {
                foreach (AppTaskDialogVerification item in request.Verifications)
                {
                    if (item == null || string.IsNullOrWhiteSpace(item.Text))
                        continue;
                    list.Add(item);
                }
            }

            return list;
        }

        private List<bool> CollectVerificationStates()
        {
            var states = new List<bool>(_chkVerifications.Count);
            for (int i = 0; i < _chkVerifications.Count; i++)
                states.Add(_chkVerifications[i].Checked);
            return states;
        }

        private Panel BuildButtonBand()
        {
            var panel = new Panel
            {
                Name = "pnlButtons",
                Width = _clientWidth,
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
            int preferredTotal = 0;
            for (int i = 0; i < buttons.Count; i++)
            {
                widths[i] = MeasureButtonWidth(buttons[i].Text);
                preferredTotal += widths[i];
            }

            int gaps = ButtonGap * Math.Max(0, buttons.Count - 1);
            // Shrink buttons when the row would overflow the client area.
            int maxButtonsWidth = _clientWidth - (LayoutMargin * 2) - gaps;
            if (preferredTotal > maxButtonsWidth && preferredTotal > 0 && buttons.Count > 0)
            {
                int used = 0;
                for (int i = 0; i < buttons.Count; i++)
                {
                    if (i < buttons.Count - 1)
                    {
                        widths[i] = Math.Max(1, (widths[i] * maxButtonsWidth) / preferredTotal);
                        used += widths[i];
                    }
                    else
                        widths[i] = Math.Max(1, maxButtonsWidth - used);
                }
                preferredTotal = maxButtonsWidth;
            }

            // Anchor from the bottom-right: last button's right edge at LayoutMargin; others grow left.
            int buttonRowWidth = preferredTotal + gaps;
            int buttonLeft = _clientWidth - LayoutMargin - buttonRowWidth;

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

        // Prefer hard newlines as paragraphs; soft-wrap only when a line exceeds the width cap.
        private int MeasureBodyTextHeight(string text, int width)
        {
            if (string.IsNullOrEmpty(text))
                return Font.Height;

            // Match Label (GDI) line metrics — TextBoxControl overestimates (reads as a trailing blank line).
            Size size = TextRenderer.MeasureText(
                text,
                Font,
                new Size(Math.Max(1, width), int.MaxValue),
                TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
            return Math.Max(size.Height, Font.Height);
        }

        // Vertical advance of one text line — same metrics as the body message label.
        private int MeasureLineAdvance()
        {
            const int probeWidth = 200;
            TextFormatFlags flags = TextFormatFlags.WordBreak | TextFormatFlags.NoPadding;
            int oneLine = TextRenderer.MeasureText(
                "Ag",
                Font,
                new Size(probeWidth, int.MaxValue),
                flags).Height;
            int twoLines = TextRenderer.MeasureText(
                "Ag\nAg",
                Font,
                new Size(probeWidth, int.MaxValue),
                flags).Height;
            int advance = twoLines - oneLine;
            return Math.Max(advance, 1);
        }

        private void ApplyTaskDialogChromeColors()
        {
            if (_pnlBody == null || _pnlButtons == null)
                return;

            ThemeColors colors = ThemeService.GetThemeColors(ThemeService.EffectiveTheme);
            bool dark = colors.Background.GetBrightness() < 0.5f;

            Color bodyBack = dark ? colors.Background : SystemColors.Window;
            Color footerBack = dark ? colors.ControlBackground : SystemColors.Control;
            Color bodyFore = dark ? colors.Foreground : SystemColors.WindowText;

            BackColor = bodyBack;
            ForeColor = bodyFore;

            // Body (white) → checkbox or 2-line spacer (same white) → button footer (grey).
            ApplyBandColors(_pnlBody, bodyBack, bodyFore, colors);
            if (_pnlCheckStrip != null)
                ApplyBandColors(_pnlCheckStrip, bodyBack, bodyFore, colors);
            if (_pnlPreButtonSpacer != null)
                ApplyBandColors(_pnlPreButtonSpacer, bodyBack, bodyFore, colors);
            ApplyBandColors(_pnlButtons, footerBack, bodyFore, colors);
        }

        private static void ApplyBandColors(Panel band, Color back, Color fore, ThemeColors colors)
        {
            if (band == null)
                return;

            band.BackColor = back;
            foreach (Control child in band.Controls)
            {
                if (child is Button)
                    continue;
                child.BackColor = back;
                if (child is LinkLabel link)
                {
                    link.LinkColor = colors.LinkColor;
                    link.ActiveLinkColor = colors.LinkColor;
                    link.VisitedLinkColor = colors.VisitedLinkColor;
                    continue;
                }
                if (!(child is PictureBox))
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
