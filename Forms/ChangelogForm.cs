using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;

namespace SmartGoldbergEmu.Forms
{
    public partial class ChangelogForm : ThemedForm
    {
        private const int ContentTextWidth = 556;

        private ChangelogDialogContent _content;
        private string _releaseNotesMarkdown;
        private readonly List<LinkLabel> _manualDownloadLinks = new List<LinkLabel>();
        private readonly List<ReleaseNoteHyperlink> _releaseNoteLinks = new List<ReleaseNoteHyperlink>();

        public ChangelogForm()
        {
            InitializeComponent();
            ConfigureReleaseNotesView();
        }

        public ChangelogForm(ChangelogDialogContent content)
            : this(content, ServiceLocator.ThemeService)
        {
        }

        public ChangelogForm(ChangelogDialogContent content, ThemeService themeService)
            : base(themeService)
        {
            InitializeComponent();
            ConfigureReleaseNotesView();
            _content = content ?? throw new ArgumentNullException(nameof(content));
            BindContent();
        }

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static DialogResult ShowDialogIfAlive(IWin32Window owner, ChangelogDialogContent content)
        {
            if (owner is Control control && (control.IsDisposed || control.Disposing))
                return DialogResult.Cancel;

            using (var form = new ChangelogForm(content))
            {
                form.EnsureDisplayReady();
                return form.ShowDialog(owner);
            }
        }

        private void EnsureDisplayReady()
        {
            if (!IsHandleCreated)
                CreateControl();

            rtbReleaseNotes.CreateControl();
            PerformLayout();
            flpHeader.PerformLayout();
            tlpFooter.PerformLayout();
            pnlNotes.PerformLayout();
        }

        private void ConfigureReleaseNotesView()
        {
            rtbReleaseNotes.ReadOnly = true;
            rtbReleaseNotes.WordWrap = true;
            rtbReleaseNotes.ScrollBars = RichTextBoxScrollBars.Vertical;
            rtbReleaseNotes.BorderStyle = BorderStyle.FixedSingle;
            rtbReleaseNotes.DetectUrls = false;
            rtbReleaseNotes.HideSelection = false;
            rtbReleaseNotes.ShortcutsEnabled = true;
            rtbReleaseNotes.Font = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
            rtbReleaseNotes.MouseClick += RtbReleaseNotes_MouseClick;
            rtbReleaseNotes.MouseMove += RtbReleaseNotes_MouseMove;
        }

        private void BindContent()
        {
            if (_content == null)
                return;

            Text = string.IsNullOrWhiteSpace(_content.FormTitle) ? "Update Available" : _content.FormTitle;
            lblHeadline.Text = _content.Headline ?? string.Empty;

            _releaseNotesMarkdown = _content.ReleaseNotes?.Trim();

            if (string.IsNullOrWhiteSpace(_content.AdditionalInfo))
            {
                lblAdditionalInfo.Text = string.Empty;
                lblAdditionalInfo.Visible = false;
                tlpFooter.RowStyles[0] = new RowStyle(SizeType.Absolute, 0F);
            }
            else
            {
                lblAdditionalInfo.Text = _content.AdditionalInfo.Trim();
                lblAdditionalInfo.Visible = true;
                tlpFooter.RowStyles[0] = new RowStyle(SizeType.AutoSize);
            }

            if (_content.ProceedQuestion != null && string.IsNullOrWhiteSpace(_content.ProceedQuestion))
            {
                lblProceedQuestion.Text = string.Empty;
                lblProceedQuestion.Visible = false;
                tlpFooter.RowStyles[1] = new RowStyle(SizeType.Absolute, 0F);
            }
            else
            {
                lblProceedQuestion.Text = string.IsNullOrWhiteSpace(_content.ProceedQuestion)
                    ? "Do you want to proceed?"
                    : _content.ProceedQuestion;
                lblProceedQuestion.Visible = true;
                tlpFooter.RowStyles[1] = new RowStyle(SizeType.AutoSize);
            }

            btnOk.Text = string.IsNullOrWhiteSpace(_content.OkButtonText) ? "OK" : _content.OkButtonText.Trim();
            btnCancel.Visible = _content.ShowCancelButton;
            CancelButton = _content.ShowCancelButton ? btnCancel : btnOk;

            BindManualDownloadLinks(_content.ManualDownloadLinks, _content.ShowManualDownloadCaption);
            ApplyTextWrapWidths();
        }

        private void ApplyTextWrapWidths()
        {
            int textWidth = Math.Max(200, ContentTextWidth);
            lblHeadline.MaximumSize = new Size(textWidth, 0);
            lblAdditionalInfo.MaximumSize = new Size(textWidth, 0);
            lblProceedQuestion.MaximumSize = new Size(textWidth, 0);
            lblManualDownloadCaption.MaximumSize = new Size(textWidth, 0);
        }

        protected override void OnThemeApplied()
        {
            if (_content != null)
                RenderReleaseNotes();
        }

        private void RenderReleaseNotes()
        {
            if (rtbReleaseNotes == null)
                return;

            ThemeColors colors = ThemeService.GetThemeColors(ThemeService.EffectiveTheme);
            _releaseNoteLinks.Clear();
            IList<ReleaseNoteHyperlink> links = MarkdownToRichTextHelper.Apply(rtbReleaseNotes, _releaseNotesMarkdown, colors);
            _releaseNoteLinks.AddRange(links);
        }

        private void BindManualDownloadLinks(IList<UpdateManualDownloadLink> links, bool showCaption)
        {
            flpManualLinks.Controls.Clear();
            _manualDownloadLinks.Clear();

            if (links != null)
            {
                foreach (var link in links)
                {
                    if (link == null || string.IsNullOrWhiteSpace(link.Url))
                        continue;

                    string url = link.Url.Trim();
                    string prefix = string.IsNullOrWhiteSpace(link.Label) ? string.Empty : link.Label.Trim() + ": ";
                    string displayText = prefix + url;

                    var linkLabel = new LinkLabel
                    {
                        AutoSize = true,
                        Text = displayText,
                        Tag = url,
                        Margin = new Padding(0, 0, 0, 2),
                        TabStop = true,
                        MaximumSize = new Size(ContentTextWidth, 0)
                    };
                    linkLabel.LinkArea = new LinkArea(prefix.Length, url.Length);
                    linkLabel.LinkClicked += OnManualDownloadLink_LinkClicked;
                    flpManualLinks.Controls.Add(linkLabel);
                    _manualDownloadLinks.Add(linkLabel);
                }
            }

            bool hasLinks = flpManualLinks.Controls.Count > 0;
            bool showCaptionRow = hasLinks && showCaption;
            lblManualDownloadCaption.Visible = showCaptionRow;
            flpManualLinks.Visible = hasLinks;
            tlpFooter.RowStyles[2] = showCaptionRow
                ? new RowStyle(SizeType.AutoSize)
                : new RowStyle(SizeType.Absolute, 0F);
            tlpFooter.RowStyles[3] = hasLinks
                ? new RowStyle(SizeType.AutoSize)
                : new RowStyle(SizeType.Absolute, 0F);
        }

        private void RtbReleaseNotes_MouseClick(object sender, MouseEventArgs e)
        {
            if (rtbReleaseNotes.SelectionLength > 0)
                return;

            string url = TryGetHyperlinkUrlAtPoint(e.Location);
            if (url != null)
                OpenReleaseNoteUrl(url);
        }

        private void RtbReleaseNotes_MouseMove(object sender, MouseEventArgs e)
        {
            string url = TryGetHyperlinkUrlAtPoint(e.Location);
            rtbReleaseNotes.Cursor = url != null ? Cursors.Hand : Cursors.Default;
        }

        private string TryGetHyperlinkUrlAtPoint(Point location)
        {
            int index = rtbReleaseNotes.GetCharIndexFromPosition(location);
            if (index < 0)
                return null;

            if (index >= rtbReleaseNotes.TextLength && rtbReleaseNotes.TextLength > 0)
                index = rtbReleaseNotes.TextLength - 1;

            foreach (ReleaseNoteHyperlink link in _releaseNoteLinks)
            {
                if (index >= link.Start && index < link.Start + link.Length)
                    return link.Url;
            }

            return null;
        }

        private void OpenReleaseNoteUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;

            url = url.Trim();
            if (!PathValidationHelper.IsSafeUrl(url))
            {
                FormMessageBoxHelper.ShowIfAlive(this, "Invalid URL format detected.", "Invalid URL", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                Process.Start(url);
            }
            catch (Exception ex)
            {
                Program.LogService?.LogError($"Failed to open release note link: {ex.Message}", ex);
                FormMessageBoxHelper.ShowIfAlive(this, "Failed to open link.", "Could Not Open Link", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnManualDownloadLink_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            var url = (sender as LinkLabel)?.Tag as string;
            if (string.IsNullOrEmpty(url))
                return;

            e.Link.Visited = true;
            OpenReleaseNoteUrl(url);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Activate();
            BringToFront();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            rtbReleaseNotes.MouseClick -= RtbReleaseNotes_MouseClick;
            rtbReleaseNotes.MouseMove -= RtbReleaseNotes_MouseMove;
            foreach (var link in _manualDownloadLinks)
                link.LinkClicked -= OnManualDownloadLink_LinkClicked;
            base.OnFormClosed(e);
        }
    }
}
