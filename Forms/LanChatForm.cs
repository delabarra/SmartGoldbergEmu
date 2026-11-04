using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Extensions;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;

namespace SmartGoldbergEmu.Forms
{
    public partial class LanChatForm : ThemedForm
    {
        private const int AvatarSize = 64;
        private const int HistoryContentPadding = 8;
        private const int EmSetRect = 0xB3;

        private readonly LanChatService _chat;
        private readonly LanPresenceService _presence;
        private readonly AppDataService _appData;
        private readonly string _peerSteamId;
        private readonly ContextMenuStrip _peerMenu;
        private bool _eventsHooked;
        private bool _showWithoutActivation;
        private bool _hasUnreadAttention;
        private string _peerDisplayName = string.Empty;
        private string _loadedAvatarPath;
        private string _placeholderLetter;
        private Color _placeholderBackColor;
        private Image _avatarImage;
        private Image _placeholderAvatar;
        private bool _sending;
        private bool _acceptingLobbyInvite;
        private bool _syncingComposeScroll;
        private bool _initialLayoutApplied;
        // Layout while Minimized uses a tiny ClientSize; defer panel/history updates until restore.
        private bool _deferredSessionRefresh;

        public string PeerSteamId => _peerSteamId;

        protected override bool ShowWithoutActivation => _showWithoutActivation;

        public LanChatForm(string peerSteamId)
            : this(
                peerSteamId,
                ResolveThemeService(),
                ServiceLocator.LanChatService,
                ServiceLocator.LanPresenceService,
                ServiceLocator.AppDataService)
        {
        }

        public LanChatForm(
            string peerSteamId,
            ThemeService themeService,
            LanChatService chat,
            LanPresenceService presence)
            : this(peerSteamId, themeService, chat, presence, ServiceLocator.AppDataService)
        {
        }

        public LanChatForm(
            string peerSteamId,
            ThemeService themeService,
            LanChatService chat,
            LanPresenceService presence,
            AppDataService appData)
            : base(ResolveThemeService(themeService))
        {
            if (string.IsNullOrWhiteSpace(peerSteamId))
                throw new ArgumentException("Peer Steam ID is required.", nameof(peerSteamId));
            _peerSteamId = peerSteamId.Trim();
            _chat = chat ?? throw new ArgumentNullException(nameof(chat));
            _presence = presence ?? throw new ArgumentNullException(nameof(presence));
            _appData = appData;
            InitializeComponent();
            _peerMenu = new ContextMenuStrip();
            _peerMenu.Opening += OnPeerMenu_Opening;
            WireProfileContextMenu();
            rtfHistory.HandleCreated += OnHistoryHandleCreated;
            rtfHistory.Resize += OnHistoryResize;
            txtCompose.MaxLength = LanChatConstants.MaxMessageChars;
            txtCompose.TextChanged += OnComposeTextChanged;
            txtCompose.Resize += OnComposeResize;
            pnlComposer.Resize += (s, e) => EnsureComposerLayout();
            UpdateComposeCharCount();
            if (_appData != null && WindowStateHelper.TryRestoreLanChatWindowState(this, _appData))
                _initialLayoutApplied = true;
            HookChatEvents();
            _chat.EnsureSessionShell(_peerSteamId);
            RefreshProfile();
            RefreshFromSession(_chat.GetSessionSnapshot(_peerSteamId));
        }

        // Restored layout wins; otherwise center on the relative form (Main / LAN Players).
        public void ApplyInitialPlacement(Form relativeTo)
        {
            if (_initialLayoutApplied)
                return;

            if (_appData != null && WindowStateHelper.TryRestoreLanChatWindowState(this, _appData))
            {
                _initialLayoutApplied = true;
                return;
            }

            if (relativeTo != null && !relativeTo.IsDisposed)
                WindowStateHelper.CenterFormOnParent(this, relativeTo);
            else
                WindowStateHelper.CenterFormOnScreen(this);

            _initialLayoutApplied = true;
        }

        public void SetOpenWithoutActivation(bool value)
        {
            _showWithoutActivation = value;
        }

        public void FocusComposer()
        {
            if (IsDisposed || Disposing)
                return;

            ClearUnreadAttention();
            if (!txtCompose.Enabled)
                return;

            try
            {
                if (!ContainsFocus)
                    Activate();
                txtCompose.Focus();
                txtCompose.Select(txtCompose.TextLength, 0);
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        public void NotifyAttention()
        {
            if (IsDisposed || Disposing)
                return;

            if (ContainsFocus && WindowState != FormWindowState.Minimized)
            {
                ClearUnreadAttention();
                return;
            }

            _hasUnreadAttention = true;
            UpdateWindowTitle();
            FlashWindowHelper.FlashUntilForeground(this);
            try
            {
                string soundPath = Path.Combine(
                    PathConstants.GlobalSoundsPath,
                    PathConstants.SteamClientUiFriendNotificationWav);
                if (!WavPreviewPlaybackHelper.TryPlayFile(soundPath))
                    System.Media.SystemSounds.Asterisk.Play();
            }
            catch (Exception)
            {
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (!_showWithoutActivation)
                BeginInvoke(new Action(FocusComposer));
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            // Minimized: Win11 can spuriously activate during FlashWindowEx; wait for restore.
            // ContainsFocus can lag one tick after Activated — defer the clear check.
            if (WindowState == FormWindowState.Minimized)
                return;
            if (_deferredSessionRefresh)
                BeginInvoke(new Action(ApplyDeferredSessionRefresh));
            BeginInvoke(new Action(TryClearUnreadAttentionIfInteractive));
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (WindowState == FormWindowState.Minimized)
                return;

            // Taskbar restore often Activates while still Minimized, then restores without a second Activated.
            if (_hasUnreadAttention)
                BeginInvoke(new Action(TryClearUnreadAttentionIfInteractive));

            if (_deferredSessionRefresh)
                BeginInvoke(new Action(ApplyDeferredSessionRefresh));
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_appData != null)
                WindowStateHelper.SaveLanChatWindowState(this, _appData);
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            FlashWindowHelper.Stop(this);
            UnhookChatEvents();
            LanChatUiHost.NotifyClosed(this);
            DetachProfileContextMenu();
            if (_peerMenu != null)
            {
                _peerMenu.Opening -= OnPeerMenu_Opening;
                _peerMenu.Dispose();
            }
            DisposeAvatarImage();
            base.OnFormClosed(e);
        }

        private void WireProfileContextMenu()
        {
            foreach (Control control in EnumerateProfileControls())
                control.ContextMenuStrip = _peerMenu;
        }

        private void DetachProfileContextMenu()
        {
            foreach (Control control in EnumerateProfileControls())
            {
                if (ReferenceEquals(control.ContextMenuStrip, _peerMenu))
                    control.ContextMenuStrip = null;
            }
        }

        private IEnumerable<Control> EnumerateProfileControls()
        {
            Control[] profileControls = { pnlProfile, picAvatar, lblName, lblPersona, lblSteamId };
            for (int i = 0; i < profileControls.Length; i++)
            {
                Control control = profileControls[i];
                if (control != null && !control.IsDisposed)
                    yield return control;
            }
        }

        private void OnPeerMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            LanPeerContextMenuHelper.ClearItems(_peerMenu);
            LanPeerContextMenuHelper.PopulatePeerMenu(
                _peerMenu,
                this,
                _peerSteamId,
                _presence,
                _appData,
                new LanPeerContextMenuHelper.Options
                {
                    IncludeMessage = false,
                    IncludeRemoveFromPlayersSeen = true,
                    OpenChatOnAllowAgain = false,
                    AfterLocalChange = RefreshProfile
                });

            if (_peerMenu.Items.Count == 0)
                e.Cancel = true;
        }

        private void TryClearUnreadAttentionIfInteractive()
        {
            if (IsDisposed || Disposing || !_hasUnreadAttention)
                return;
            if (WindowState == FormWindowState.Minimized)
                return;
            if (!ContainsFocus && !ReferenceEquals(ActiveForm, this))
                return;
            ClearUnreadAttention();
        }

        private void ClearUnreadAttention()
        {
            if (!_hasUnreadAttention)
                return;
            _hasUnreadAttention = false;
            FlashWindowHelper.Stop(this);
            UpdateWindowTitle();
        }

        private void HookChatEvents()
        {
            if (_eventsHooked)
                return;
            _chat.SessionUpdated += OnSessionUpdated;
            _presence.PeersChanged += OnPeersChanged;
            _eventsHooked = true;
        }

        private void UnhookChatEvents()
        {
            if (!_eventsHooked)
                return;
            try
            {
                _chat.SessionUpdated -= OnSessionUpdated;
            }
            catch (ObjectDisposedException)
            {
            }

            try
            {
                _presence.PeersChanged -= OnPeersChanged;
            }
            catch (ObjectDisposedException)
            {
            }

            _eventsHooked = false;
        }

        private void OnPeersChanged(object sender, EventArgs e)
        {
            if (IsDisposed || Disposing)
                return;
            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action(RefreshProfile));
                }
                catch (ObjectDisposedException)
                {
                }

                return;
            }

            RefreshProfile();
        }

        private void OnSessionUpdated(object sender, LanChatSessionEventArgs e)
        {
            if (e == null || e.Session == null)
                return;
            if (!string.Equals(e.Session.PeerSteamId, _peerSteamId, StringComparison.Ordinal))
                return;

            if (IsDisposed || Disposing)
                return;

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action(() => RefreshFromSession(e.Session)));
                }
                catch (ObjectDisposedException)
                {
                }

                return;
            }

            RefreshFromSession(e.Session);
        }

        private void ApplyDeferredSessionRefresh()
        {
            if (IsDisposed || Disposing || WindowState == FormWindowState.Minimized)
                return;
            if (!_deferredSessionRefresh)
                return;
            _deferredSessionRefresh = false;
            RefreshFromSession(_chat.GetSessionSnapshot(_peerSteamId));
            EnsureComposerLayout();
        }

        private void EnsureComposerLayout()
        {
            if (pnlComposer == null || pnlComposer.IsDisposed || txtCompose == null || txtCompose.IsDisposed)
                return;

            // Re-assert fill after a minimized layout pass can leave Anchored children at 0 height.
            int sendWidth = btnSend != null && !btnSend.IsDisposed ? btnSend.Width : 82;
            int gap = 6;
            txtCompose.SetBounds(
                0,
                0,
                Math.Max(1, pnlComposer.ClientSize.Width - sendWidth - gap),
                Math.Max(1, pnlComposer.ClientSize.Height));
            if (btnSend != null && !btnSend.IsDisposed)
            {
                btnSend.Location = new Point(
                    Math.Max(0, pnlComposer.ClientSize.Width - sendWidth),
                    0);
            }

            SyncComposeScrollBars();
        }

        private void RefreshFromSession(LanChatSessionSnapshot session)
        {
            if (session == null || IsDisposed || Disposing)
                return;

            if (!string.IsNullOrWhiteSpace(session.PeerDisplayName))
                _peerDisplayName = session.PeerDisplayName.Trim();

            UpdateWindowTitle();

            // Minimized ClientSize is tiny — showing AutoSize invite/gate rows wraps labels and crushes the composer.
            if (WindowState == FormWindowState.Minimized)
            {
                _deferredSessionRefresh = true;
                return;
            }

            _deferredSessionRefresh = false;

            ThemeColors colors = ThemeService.GetThemeColors(ThemeService.EffectiveTheme);
            bool incoming = session.Status == LanChatSessionStatus.IncomingPending;
            pnlGate.Visible = incoming;
            if (incoming)
            {
                lblGateWarning.Text =
                    "This player wants to chat on your LAN. Identity is not verified. "
                    + "Read their message below, then Accept, Ignore, or Ignore user.";
                lblGateWarning.ForeColor = colors.WarningColor;
            }

            bool lobbyInvite = session.PendingGameInvite != null && session.PendingGameInvite.AppId != 0;
            pnlLobbyInvite.Visible = lobbyInvite;
            if (lobbyInvite)
            {
                string game = string.IsNullOrWhiteSpace(session.PendingGameInvite.GameName)
                    ? ("App " + session.PendingGameInvite.AppId)
                    : session.PendingGameInvite.GameName.Trim();
                int wrapWidth = pnlLobbyInvite.ClientSize.Width;
                if (wrapWidth < 100)
                    wrapWidth = Math.Max(100, ClientSize.Width - 24);
                lblLobbyInvite.MaximumSize = new Size(wrapWidth, 0);
                lblLobbyInvite.Text = "You have received an invitation to join " + game + ".";
                lblLobbyInvite.ForeColor = colors.InfoColor;
            }

            RenderHistory(session.Messages);

            bool canCompose = session.Status == LanChatSessionStatus.None
                || session.Status == LanChatSessionStatus.Accepted;
            bool blocked = session.Status == LanChatSessionStatus.Blocked;
            txtCompose.Enabled = canCompose && !blocked;
            btnSend.Enabled = canCompose && !blocked;

            if (session.Status == LanChatSessionStatus.OutgoingPending)
            {
                lblStatus.Text = string.IsNullOrEmpty(session.StatusDetail)
                    ? "Waiting for them to accept…"
                    : session.StatusDetail;
                lblStatus.ForeColor = colors.InfoColor;
            }
            else if (session.Status == LanChatSessionStatus.Blocked)
            {
                lblStatus.Text = session.StatusDetail ?? "Chat from this player is blocked.";
                lblStatus.ForeColor = colors.ErrorColor;
            }
            else if (incoming)
            {
                lblStatus.Text = session.StatusDetail ?? string.Empty;
                lblStatus.ForeColor = colors.WarningColor;
            }
            else
            {
                lblStatus.Text = session.StatusDetail ?? string.Empty;
                lblStatus.ForeColor = colors.StatusTextSecondary;
            }

            if (!string.IsNullOrEmpty(_peerDisplayName))
                lblName.Text = _peerDisplayName;

            RefreshProfile();
            EnsureComposerLayout();
        }

        protected override void OnThemeApplied()
        {
            base.OnThemeApplied();
            RefreshProfile();
            RefreshFromSession(_chat.GetSessionSnapshot(_peerSteamId));
            UpdateComposeCharCount();
        }

        private void RefreshProfile()
        {
            if (IsDisposed || Disposing)
                return;

            ThemeColors colors = ThemeService.GetThemeColors(ThemeService.EffectiveTheme);
            LanPresencePeer peer;
            _presence.TryGetPeer(_peerSteamId, out peer);
            bool channelOnline = _presence.IsOnline;
            string name = peer != null && !string.IsNullOrWhiteSpace(peer.DisplayName)
                ? peer.DisplayName.Trim()
                : (!string.IsNullOrEmpty(_peerDisplayName) ? _peerDisplayName : _peerSteamId);
            _peerDisplayName = name;

            Color nameColor;
            Color statusColor;
            LanPersonaDisplayHelper.ResolvePersonaColors(
                peer,
                colors,
                isSelf: false,
                channelOnline,
                out nameColor,
                out statusColor);

            lblName.Text = name;
            lblName.ForeColor = nameColor;
            lblSteamId.Text = "Steam64 " + _peerSteamId;
            lblSteamId.ForeColor = colors.StatusTextSecondary;
            lblPersona.Text = LanPersonaDisplayHelper.FormatPersonaStatusLine(peer, isSelf: false, channelOnline);
            lblPersona.ForeColor = statusColor;
            pnlProfile.BackColor = colors.ControlBackground;
            picAvatar.BackColor = colors.ControlBackground;

            string path = peer != null ? peer.AvatarFilePath : null;
            bool pathUnchanged = string.Equals(path ?? string.Empty, _loadedAvatarPath ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            bool hasFileAvatar = _avatarImage != null && !ReferenceEquals(_avatarImage, _placeholderAvatar);
            if (_avatarImage != null && pathUnchanged)
            {
                if (!hasFileAvatar)
                    SetAvatarImage(GetPlaceholderAvatar(colors));
            }
            else
            {
                Image loaded = LanAvatarImageHelper.TryLoadAvatar(path);
                _loadedAvatarPath = path;
                SetAvatarImage(loaded ?? GetPlaceholderAvatar(colors));
            }

            UpdateWindowTitle();
        }

        private void UpdateWindowTitle()
        {
            string name = string.IsNullOrEmpty(_peerDisplayName) ? _peerSteamId : _peerDisplayName;
            Text = _hasUnreadAttention ? ("LAN Chat — " + name + " •") : ("LAN Chat — " + name);
        }

        private string ResolveLocalDisplayName()
        {
            try
            {
                LanPresencePeer self = _presence.GetLocalSelfSnapshot();
                if (self != null && !string.IsNullOrWhiteSpace(self.DisplayName))
                    return self.DisplayName.Trim();
                if (self != null && !string.IsNullOrWhiteSpace(self.SteamId))
                    return self.SteamId.Trim();
            }
            catch (ObjectDisposedException)
            {
            }
            catch (InvalidOperationException)
            {
            }

            return "Me";
        }

        private void RenderHistory(IReadOnlyList<LanChatMessage> messages)
        {
            ThemeColors colors = ThemeService.GetThemeColors(ThemeService.EffectiveTheme);
            rtfHistory.SuspendLayout();
            rtfHistory.Clear();

            if (messages != null)
            {
                Font baseFont = rtfHistory.Font ?? Font;
                Font boldName = null;
                try
                {
                    boldName = new Font(baseFont, FontStyle.Bold);
                    for (int i = 0; i < messages.Count; i++)
                    {
                        LanChatMessage m = messages[i];
                        if (m == null || string.IsNullOrEmpty(m.Text))
                            continue;

                        DateTime local = m.TimestampUtc.Kind == DateTimeKind.Utc
                            ? m.TimestampUtc.ToLocalTime()
                            : DateTime.SpecifyKind(m.TimestampUtc, DateTimeKind.Utc).ToLocalTime();
                        string stamp = FormatTimestamp(local);
                        bool inviteResult = m.Kind == LanChatMessageKind.GameInviteResult;
                        bool inviteNote = m.Kind == LanChatMessageKind.GameInvite
                            || m.Kind == LanChatMessageKind.GameInviteSent
                            || m.Kind == LanChatMessageKind.GameInviteCooldown
                            || inviteResult;
                        // Invite lines use normal who; details/actions stay on the form panel.
                        string who = m.IsLocal
                            ? ResolveLocalDisplayName()
                            : (_peerDisplayName.Length > 0 ? _peerDisplayName : "Them");
                        Color nameColor = m.IsLocal ? colors.StatusTextAccent : colors.Foreground;
                        // Chat invite notes stay warning (yellow); form panel uses InfoColor separately.
                        Color bodyColor = inviteResult
                            ? colors.InfoColor
                            : (inviteNote
                                ? colors.WarningColor
                                : (m.IsLocal ? colors.StatusTextAccent : colors.Foreground));

                        rtfHistory.SelectionStart = rtfHistory.TextLength;
                        rtfHistory.SelectionLength = 0;
                        rtfHistory.SelectionAlignment = m.IsLocal
                            ? HorizontalAlignment.Right
                            : HorizontalAlignment.Left;
                        rtfHistory.SelectionColor = colors.StatusTextSecondary;
                        rtfHistory.SelectionFont = baseFont;
                        rtfHistory.AppendText(stamp + "  ");
                        rtfHistory.SelectionFont = boldName;
                        rtfHistory.SelectionColor = nameColor;
                        rtfHistory.AppendText(who + Environment.NewLine);

                        rtfHistory.SelectionStart = rtfHistory.TextLength;
                        rtfHistory.SelectionLength = 0;
                        rtfHistory.SelectionAlignment = m.IsLocal
                            ? HorizontalAlignment.Right
                            : HorizontalAlignment.Left;
                        rtfHistory.SelectionFont = baseFont;
                        // Local = accent (same as online persona); peer body uses field text on themed background.
                        rtfHistory.SelectionColor = bodyColor;
                        rtfHistory.AppendText(m.Text);

                        if (i < messages.Count - 1)
                        {
                            rtfHistory.SelectionStart = rtfHistory.TextLength;
                            rtfHistory.SelectionLength = 0;
                            rtfHistory.SelectionAlignment = m.IsLocal
                                ? HorizontalAlignment.Right
                                : HorizontalAlignment.Left;
                            rtfHistory.AppendText(Environment.NewLine + Environment.NewLine);
                        }
                    }
                }
                finally
                {
                    if (boldName != null)
                        boldName.Dispose();
                }
            }

            rtfHistory.SelectionStart = rtfHistory.TextLength;
            rtfHistory.ScrollToCaret();
            rtfHistory.ResumeLayout();
            ApplyHistoryContentPadding();
        }

        private void OnHistoryHandleCreated(object sender, EventArgs e)
        {
            ApplyHistoryContentPadding();
        }

        private void OnHistoryResize(object sender, EventArgs e)
        {
            ApplyHistoryContentPadding();
        }

        private void ApplyHistoryContentPadding()
        {
            if (rtfHistory == null || rtfHistory.IsDisposed || !rtfHistory.IsHandleCreated)
                return;

            Rectangle client = rtfHistory.ClientRectangle;
            if (client.Width <= HistoryContentPadding * 2 || client.Height <= HistoryContentPadding * 2)
                return;

            var rect = new NativeRect
            {
                Left = HistoryContentPadding,
                Top = HistoryContentPadding,
                Right = client.Width - HistoryContentPadding,
                Bottom = client.Height - HistoryContentPadding
            };
            SendMessage(rtfHistory.Handle, EmSetRect, IntPtr.Zero, ref rect);
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref NativeRect lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private static string FormatTimestamp(DateTime local)
        {
            DateTime today = DateTime.Now.Date;
            if (local.Date == today)
                return local.ToString("HH:mm");
            return local.ToString("yyyy-MM-dd HH:mm");
        }

        private void OnSend_Click(object sender, EventArgs e)
        {
            SendCompose();
        }

        private void OnCompose_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                SendCompose();
            }
        }

        private void OnComposeTextChanged(object sender, EventArgs e)
        {
            UpdateComposeCharCount();
            SyncComposeScrollBars();
        }

        private void UpdateComposeCharCount()
        {
            if (lblComposeChars == null || lblComposeChars.IsDisposed)
                return;

            int max = txtCompose.MaxLength > 0
                ? txtCompose.MaxLength
                : LanChatConstants.MaxMessageChars;
            int remaining = Math.Max(0, max - txtCompose.TextLength);
            lblComposeChars.Text = remaining == 1
                ? "1 character remaining"
                : remaining + " characters remaining";

            ThemeColors colors = ThemeService.GetThemeColors(ThemeService.EffectiveTheme);
            lblComposeChars.ForeColor = remaining == 0
                ? colors.WarningColor
                : colors.StatusTextSecondary;
        }

        private void OnComposeResize(object sender, EventArgs e)
        {
            SyncComposeScrollBars();
        }

        private void SyncComposeScrollBars()
        {
            if (_syncingComposeScroll || txtCompose == null || txtCompose.IsDisposed)
                return;

            ScrollBars wanted = ComposeTextOverflows() ? ScrollBars.Vertical : ScrollBars.None;
            if (txtCompose.ScrollBars == wanted)
                return;

            _syncingComposeScroll = true;
            try
            {
                txtCompose.ScrollBars = wanted;
            }
            finally
            {
                _syncingComposeScroll = false;
            }
        }

        private bool ComposeTextOverflows()
        {
            if (txtCompose.TextLength <= 0)
                return false;

            int lineHeight = Math.Max(1, txtCompose.Font.Height);
            int visibleLines = Math.Max(1, txtCompose.ClientSize.Height / lineHeight);
            int lineCount = txtCompose.GetLineFromCharIndex(txtCompose.TextLength) + 1;
            return lineCount > visibleLines;
        }

        private void SendCompose()
        {
            if (_sending || IsDisposed || Disposing)
                return;

            _sending = true;
            try
            {
                string text = txtCompose.Text;
                string error;
                if (!_chat.TrySendMessage(_peerSteamId, text, out error))
                {
                    if (!string.IsNullOrEmpty(error))
                    {
                        FormMessageBoxHelper.ShowIfAlive(
                            this,
                            error,
                            "LAN Chat",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                    }

                    FocusComposer();
                    return;
                }

                txtCompose.Clear();
                RefreshFromSession(_chat.GetSessionSnapshot(_peerSteamId));
                FocusComposer();
            }
            finally
            {
                _sending = false;
            }
        }

        private void OnAccept_Click(object sender, EventArgs e)
        {
            string error;
            if (!_chat.TryAccept(_peerSteamId, out error) && !string.IsNullOrEmpty(error))
            {
                FormMessageBoxHelper.ShowIfAlive(
                    this,
                    error,
                    "LAN Chat",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            RefreshFromSession(_chat.GetSessionSnapshot(_peerSteamId));
            FocusComposer();
        }

        private void OnLobbyAccept_Click(object sender, EventArgs e)
        {
            if (_acceptingLobbyInvite || IsDisposed || Disposing)
                return;

            LanGameInviteEventArgs invite;
            string error;
            if (!_chat.TryTakePendingGameInvite(_peerSteamId, out invite, out error))
            {
                if (!string.IsNullOrEmpty(error))
                {
                    FormMessageBoxHelper.ShowIfAlive(
                        this,
                        error,
                        "LAN lobby invite",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }

                RefreshFromSession(_chat.GetSessionSnapshot(_peerSteamId));
                return;
            }

            RefreshFromSession(_chat.GetSessionSnapshot(_peerSteamId));
            CancellationToken token = ServiceLocator.ApplicationLifetimeToken;
            _ = AcceptLobbyInviteUiAsync(invite, token)
                .ForgetFaults(Program.LogService, nameof(OnLobbyAccept_Click));
        }

        private void OnLobbyDecline_Click(object sender, EventArgs e)
        {
            string error;
            if (!_chat.TryDeclineGameInvite(_peerSteamId, out error) && !string.IsNullOrEmpty(error))
            {
                FormMessageBoxHelper.ShowIfAlive(
                    this,
                    error,
                    "LAN lobby invite",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            RefreshFromSession(_chat.GetSessionSnapshot(_peerSteamId));
        }

        private async Task AcceptLobbyInviteUiAsync(LanGameInviteEventArgs invite, CancellationToken token)
        {
            if (invite == null)
                return;

            _acceptingLobbyInvite = true;
            btnLobbyAccept.Enabled = false;
            btnLobbyDecline.Enabled = false;
            try
            {
                ValidationResult result;
                try
                {
                    result = await ServiceLocator.LanGameJoinService
                        .AcceptInviteAsync(invite, this, token)
                        .ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    Program.LogService?.LogWarning("LAN invite accept failed: " + ex.Message);
                    _chat.RestorePendingGameInvite(invite);
                    if (!IsDisposed && !Disposing)
                    {
                        FormMessageBoxHelper.ShowIfAlive(
                            this,
                            "Could not accept invite: " + ex.Message,
                            "LAN lobby invite",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }

                    return;
                }

                if (result != null && result.IsValid)
                {
                    _chat.RecordGameInviteAccepted(_peerSteamId);
                }
                else if (result != null && !result.IsValid)
                {
                    // Keep Accept/Decline available when relaunch was declined or launch failed.
                    _chat.RestorePendingGameInvite(invite);
                    if (!IsDisposed && !Disposing
                        && !string.Equals(
                            result.ErrorMessage,
                            LanGameJoinService.RelaunchCancelledMessage,
                            StringComparison.Ordinal))
                    {
                        FormMessageBoxHelper.ShowIfAlive(
                            this,
                            result.ErrorMessage ?? "Could not accept invite.",
                            "LAN lobby invite",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }
                }
            }
            finally
            {
                _acceptingLobbyInvite = false;
                if (!IsDisposed && !Disposing)
                {
                    btnLobbyAccept.Enabled = true;
                    btnLobbyDecline.Enabled = true;
                    RefreshFromSession(_chat.GetSessionSnapshot(_peerSteamId));
                }
            }
        }

        private void OnIgnore_Click(object sender, EventArgs e)
        {
            string error;
            _chat.TryIgnore(_peerSteamId, out error);
            Close();
        }

        private void OnIgnoreUser_Click(object sender, EventArgs e)
        {
            string error;
            if (!_chat.TryIgnoreUser(_peerSteamId, out error) && !string.IsNullOrEmpty(error))
            {
                FormMessageBoxHelper.ShowIfAlive(
                    this,
                    error,
                    "LAN Chat",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            Close();
        }

        private void SetAvatarImage(Image image)
        {
            Image previous = _avatarImage;
            _avatarImage = image;
            picAvatar.Image = image;
            if (previous != null && !ReferenceEquals(previous, image) && !ReferenceEquals(previous, _placeholderAvatar))
            {
                try
                {
                    previous.Dispose();
                }
                catch
                {
                }
            }
        }

        private void DisposeAvatarImage()
        {
            _loadedAvatarPath = null;
            picAvatar.Image = null;
            if (_avatarImage != null && !ReferenceEquals(_avatarImage, _placeholderAvatar))
            {
                try
                {
                    _avatarImage.Dispose();
                }
                catch
                {
                }
            }

            _avatarImage = null;
            if (_placeholderAvatar != null)
            {
                try
                {
                    _placeholderAvatar.Dispose();
                }
                catch
                {
                }

                _placeholderAvatar = null;
            }
        }

        private Image GetPlaceholderAvatar(ThemeColors colors)
        {
            string letter = FirstDisplayLetter(_peerDisplayName);
            Color bg = colors.ControlBackground;
            if (_placeholderAvatar != null
                && string.Equals(_placeholderLetter, letter, StringComparison.Ordinal)
                && _placeholderBackColor.ToArgb() == bg.ToArgb())
                return _placeholderAvatar;

            var bmp = new Bitmap(AvatarSize, AvatarSize);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(bg);
                using (var brush = new SolidBrush(colors.StatusTextSecondary))
                using (var font = new Font("Segoe UI", 16F, FontStyle.Bold))
                {
                    SizeF size = g.MeasureString(letter, font);
                    g.DrawString(
                        letter,
                        font,
                        brush,
                        (AvatarSize - size.Width) / 2f,
                        (AvatarSize - size.Height) / 2f);
                }
            }

            Image previousPlaceholder = _placeholderAvatar;
            _placeholderAvatar = bmp;
            _placeholderLetter = letter;
            _placeholderBackColor = bg;
            if (previousPlaceholder != null && !ReferenceEquals(previousPlaceholder, _avatarImage))
            {
                try
                {
                    previousPlaceholder.Dispose();
                }
                catch
                {
                }
            }

            return _placeholderAvatar;
        }

        private static string FirstDisplayLetter(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "?";

            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (char.IsWhiteSpace(c))
                    continue;
                if (char.IsSurrogate(c))
                    return "?";
                return char.ToUpperInvariant(c).ToString();
            }

            return "?";
        }

    }
}
