using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;
using Timer = System.Windows.Forms.Timer;

namespace SmartGoldbergEmu.Forms
{
    public partial class LanPlayersForm : ThemedForm
    {
        // Compact list rows (chat header uses 64px).
        private const int AvatarDisplaySize = 48;
        private const int RowHeight = 78;
        private const int NameLineHeight = 18;
        private const int StatusLineHeight = 16;
        private const int IdLineHeight = 16;
        private const int LastSeenRefreshIntervalMs = 30000;
        private const int RowWidthScrollbarPad = 24;

        private readonly LanPresenceService _presence;
        private readonly AppDataService _appData;
        private readonly List<Image> _rowAvatars = new List<Image>();
        private readonly ContextMenuStrip _playerMenu;
        private Timer _lastSeenRefreshTimer;
        private Font _sectionFont;
        private Font _nameFont;
        private Font _metaFont;
        private Image _placeholderAvatar;
        private Color _placeholderBackColor;
        private bool _eventsHooked;
        private bool _initialLayoutApplied;
        private bool _rebuildingRows;
        private string _contextSteamId;
        private bool _contextIsSelf;

        public LanPlayersForm()
            : this(ResolveThemeService(), ServiceLocator.LanPresenceService, ServiceLocator.AppDataService)
        {
        }

        public LanPlayersForm(ThemeService themeService, LanPresenceService presence, AppDataService appData)
            : base(ResolveThemeService(themeService))
        {
            _presence = presence ?? throw new ArgumentNullException(nameof(presence));
            _appData = appData ?? throw new ArgumentNullException(nameof(appData));
            InitializeComponent();
            EnsureRowFonts();
            flpLanPlayers.SizeChanged += OnFlpLanPlayers_SizeChanged;
            _playerMenu = new ContextMenuStrip();
            _playerMenu.Opening += OnPlayerMenu_Opening;
            if (WindowStateHelper.TryRestoreLanListWindowState(this, _appData))
                _initialLayoutApplied = true;
            HookPresenceEvents();
            EnsureLastSeenRefreshTimer();
            UpdateWindowTitle();
            RefreshPeerList();
        }

        public void ApplyInitialPlacement()
        {
            ApplyInitialPlacement(Owner);
        }

        public void ApplyInitialPlacement(Form relativeTo)
        {
            if (_initialLayoutApplied)
                return;

            if (WindowStateHelper.TryRestoreLanListWindowState(this, _appData))
            {
                _initialLayoutApplied = true;
                return;
            }

            PositionBesideOwner(relativeTo);
            _initialLayoutApplied = true;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            ApplyInitialPlacement();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            RefreshPeerList();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            WindowStateHelper.SaveLanListWindowState(this, _appData);
            base.OnFormClosing(e);
        }

        protected override void OnThemeApplied()
        {
            base.OnThemeApplied();
            RefreshPeerList();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (flpLanPlayers != null)
                flpLanPlayers.SizeChanged -= OnFlpLanPlayers_SizeChanged;
            UnhookPresenceEvents();
            StopAndDisposeLastSeenRefreshTimer();
            _playerMenu.Opening -= OnPlayerMenu_Opening;
            _playerMenu.Dispose();
            ClearPlayerRows();
            DisposeRowFontsAndPlaceholder();
            base.OnFormClosed(e);
        }

        private void PositionBesideOwner(Form relativeTo)
        {
            if (relativeTo == null)
                return;

            Rectangle ownerBounds = relativeTo.Bounds;
            int x = ownerBounds.Right - 8;
            int y = ownerBounds.Top + 40;
            Rectangle wa = Screen.FromControl(relativeTo).WorkingArea;
            if (x + Width > wa.Right)
                x = ownerBounds.Left - Width + 8;
            if (x < wa.Left)
                x = wa.Left;
            if (y + Height > wa.Bottom)
                y = Math.Max(wa.Top, wa.Bottom - Height);
            StartPosition = FormStartPosition.Manual;
            Location = new Point(x, y);
        }

        private void HookPresenceEvents()
        {
            if (_eventsHooked || DesignTimeHelper.IsDesignTime)
                return;
            _presence.PeersChanged += OnPresencePeersChanged;
            _presence.OnlineStateChanged += OnPresenceOnlineStateChanged;
            _eventsHooked = true;
        }

        private void UnhookPresenceEvents()
        {
            if (!_eventsHooked)
                return;
            _presence.PeersChanged -= OnPresencePeersChanged;
            _presence.OnlineStateChanged -= OnPresenceOnlineStateChanged;
            _eventsHooked = false;
        }

        // Relative "Last seen Xm ago" text only changes with wall-clock time, not peer events.
        private void EnsureLastSeenRefreshTimer()
        {
            if (DesignTimeHelper.IsDesignTime || _lastSeenRefreshTimer != null)
                return;

            _lastSeenRefreshTimer = new Timer { Interval = LastSeenRefreshIntervalMs };
            _lastSeenRefreshTimer.Tick += OnLastSeenRefreshTimer_Tick;
            _lastSeenRefreshTimer.Start();
        }

        private void StopAndDisposeLastSeenRefreshTimer()
        {
            if (_lastSeenRefreshTimer == null)
                return;

            _lastSeenRefreshTimer.Stop();
            _lastSeenRefreshTimer.Tick -= OnLastSeenRefreshTimer_Tick;
            _lastSeenRefreshTimer.Dispose();
            _lastSeenRefreshTimer = null;
        }

        private void OnLastSeenRefreshTimer_Tick(object sender, EventArgs e)
        {
            if (IsDisposed || Disposing || !Visible || !_presence.IsOnline)
                return;
            RefreshLastSeenLabelsOnly();
        }

        // Updates relative "Last seen … ago" copy without rebuilding rows (keeps scroll / avatars stable).
        private void RefreshLastSeenLabelsOnly()
        {
            if (flpLanPlayers == null || flpLanPlayers.IsDisposed)
                return;

            IReadOnlyList<LanPresencePeer> peers = _presence.GetPeersSnapshot();
            var absentById = new Dictionary<string, DateTime>(StringComparer.Ordinal);
            for (int i = 0; i < peers.Count; i++)
            {
                LanPresencePeer peer = peers[i];
                if (peer == null || peer.IsPresent || string.IsNullOrEmpty(peer.SteamId))
                    continue;
                absentById[peer.SteamId] = peer.LastSeenUtc;
            }

            if (absentById.Count == 0)
                return;

            for (int i = 0; i < flpLanPlayers.Controls.Count; i++)
            {
                Control row = flpLanPlayers.Controls[i];
                string steamId = row.Tag as string;
                if (string.IsNullOrEmpty(steamId) || !absentById.TryGetValue(steamId, out DateTime lastSeenUtc))
                    continue;

                for (int c = 0; c < row.Controls.Count; c++)
                {
                    Label lbl = row.Controls[c] as Label;
                    if (lbl == null)
                        continue;
                    string text = lbl.Text ?? string.Empty;
                    if (!text.StartsWith("Last seen", StringComparison.Ordinal))
                        continue;
                    string next = LanPersonaDisplayHelper.FormatLastSeen(lastSeenUtc);
                    if (!string.Equals(lbl.Text, next, StringComparison.Ordinal))
                        lbl.Text = next;
                    break;
                }
            }
        }

        private void OnPresencePeersChanged(object sender, EventArgs e)
        {
            if (IsDisposed || Disposing)
                return;
            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action(RefreshPeerList));
                }
                catch (ObjectDisposedException)
                {
                }

                return;
            }

            RefreshPeerList();
        }

        private void OnPresenceOnlineStateChanged(object sender, EventArgs e)
        {
            if (IsDisposed || Disposing)
                return;
            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action(() =>
                    {
                        UpdateWindowTitle();
                        RefreshPeerList();
                    }));
                }
                catch (ObjectDisposedException)
                {
                }

                return;
            }

            UpdateWindowTitle();
            RefreshPeerList();
        }

        private void UpdateWindowTitle()
        {
            Text = _presence.IsOnline ? "LAN Players" : "LAN Players (Offline)";
        }

        private int GetRowWidth()
        {
            if (flpLanPlayers == null || flpLanPlayers.IsDisposed)
                return 200;
            return Math.Max(200, flpLanPlayers.ClientSize.Width - RowWidthScrollbarPad);
        }

        private void OnFlpLanPlayers_SizeChanged(object sender, EventArgs e)
        {
            if (_rebuildingRows || IsDisposed || Disposing || flpLanPlayers == null || flpLanPlayers.IsDisposed)
                return;
            ApplyRowWidths();
        }

        private void ApplyRowWidths()
        {
            if (flpLanPlayers == null || flpLanPlayers.IsDisposed)
                return;

            int rowWidth = GetRowWidth();
            int textWidth = Math.Max(40, rowWidth - AvatarDisplaySize - 30);
            for (int i = 0; i < flpLanPlayers.Controls.Count; i++)
            {
                Control row = flpLanPlayers.Controls[i];
                if (row == null || row.IsDisposed)
                    continue;
                row.Width = rowWidth;
                for (int c = 0; c < row.Controls.Count; c++)
                {
                    Label lbl = row.Controls[c] as Label;
                    if (lbl == null)
                        continue;
                    lbl.Width = textWidth;
                }
            }
        }

        private void RefreshPeerList()
        {
            if (flpLanPlayers == null || flpLanPlayers.IsDisposed || IsDisposed || Disposing)
                return;

            ThemeColors colors = ThemeService.GetThemeColors(ThemeService.EffectiveTheme);
            LanPresencePeer self = _presence.GetLocalSelfSnapshot();
            IReadOnlyList<LanPresencePeer> peers = _presence.IsOnline
                ? _presence.GetPeersSnapshot()
                : Array.Empty<LanPresencePeer>();

            HashSet<string> favorites = LoadFavoriteSet();
            Point scroll = flpLanPlayers.AutoScrollPosition;
            _rebuildingRows = true;
            flpLanPlayers.SuspendLayout();
            try
            {
                ClearPlayerRows();

                flpLanPlayers.Visible = true;

                int rowWidth = GetRowWidth();

                flpLanPlayers.Controls.Add(BuildPlayerRow(
                    self, colors, rowWidth, isSelf: true, channelOnline: _presence.IsOnline, isFavorite: false));

                if (!_presence.IsOnline)
                {
                    flpLanPlayers.Controls.Add(BuildSectionLabel("You are offline.", colors, rowWidth));
                    UpdateFooterStatus(0, 0);
                    return;
                }

                var favoritePresent = new List<LanPresencePeer>();
                var onlineOthers = new List<LanPresencePeer>();
                var lastSeen = new List<LanPresencePeer>();

                for (int i = 0; i < peers.Count; i++)
                {
                    LanPresencePeer peer = peers[i];
                    if (peer == null || string.IsNullOrEmpty(peer.SteamId))
                        continue;
                    bool fav = favorites.Contains(peer.SteamId);
                    if (peer.IsPresent)
                    {
                        if (fav)
                            favoritePresent.Add(peer);
                        else
                            onlineOthers.Add(peer);
                    }
                    else
                    {
                        lastSeen.Add(peer);
                    }
                }

                SortPeers(favoritePresent);
                SortPeers(onlineOthers);
                SortPeersByLastSeen(lastSeen);

                if (favoritePresent.Count > 0)
                {
                    flpLanPlayers.Controls.Add(BuildSectionLabel("Favorites", colors, rowWidth));
                    for (int i = 0; i < favoritePresent.Count; i++)
                        flpLanPlayers.Controls.Add(BuildPlayerRow(
                            favoritePresent[i], colors, rowWidth, isSelf: false, channelOnline: true, isFavorite: true));
                }

                flpLanPlayers.Controls.Add(BuildSectionLabel(
                    onlineOthers.Count == 0 && favoritePresent.Count == 0
                        ? "No other players on this LAN yet"
                        : "Online",
                    colors,
                    rowWidth));

                for (int i = 0; i < onlineOthers.Count; i++)
                    flpLanPlayers.Controls.Add(BuildPlayerRow(
                        onlineOthers[i], colors, rowWidth, isSelf: false, channelOnline: true, isFavorite: false));

                if (lastSeen.Count > 0)
                {
                    flpLanPlayers.Controls.Add(BuildSectionLabel("Last seen", colors, rowWidth));
                    for (int i = 0; i < lastSeen.Count; i++)
                    {
                        bool fav = favorites.Contains(lastSeen[i].SteamId ?? string.Empty);
                        flpLanPlayers.Controls.Add(BuildPlayerRow(
                            lastSeen[i], colors, rowWidth, isSelf: false, channelOnline: true, isFavorite: fav));
                    }
                }

                int presentPlayers = 1 + favoritePresent.Count + onlineOthers.Count;
                UpdateFooterStatus(
                    CountUniqueGamesFound(self, favoritePresent, onlineOthers),
                    presentPlayers);
            }
            finally
            {
                flpLanPlayers.ResumeLayout(true);
                flpLanPlayers.AutoScrollPosition = new Point(-scroll.X, -scroll.Y);
                _rebuildingRows = false;
            }
        }

        private void UpdateFooterStatus(int uniqueGames, int presentPlayers)
        {
            string gamesText = uniqueGames == 1 ? "1 game" : uniqueGames + " games";
            string playersText = presentPlayers == 1 ? "1 player" : presentPlayers + " players";
            lblGamesFound.Text = gamesText + " · " + playersText;
        }

        // Distinct AppIds currently being played by you or present peers.
        private static int CountUniqueGamesFound(
            LanPresencePeer self,
            List<LanPresencePeer> favoritePresent,
            List<LanPresencePeer> onlineOthers)
        {
            var appIds = new HashSet<ulong>();
            AddPlayingAppId(appIds, self);
            for (int i = 0; i < favoritePresent.Count; i++)
                AddPlayingAppId(appIds, favoritePresent[i]);
            for (int i = 0; i < onlineOthers.Count; i++)
                AddPlayingAppId(appIds, onlineOthers[i]);
            return appIds.Count;
        }

        private static void AddPlayingAppId(HashSet<ulong> appIds, LanPresencePeer peer)
        {
            if (peer != null && peer.IsPresent && peer.IsPlaying)
                appIds.Add(peer.AppId);
        }

        private HashSet<string> LoadFavoriteSet()
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                ApplicationSettings settings = _appData.LoadApplicationSettings();
                if (settings.LanFavoriteSteamIds == null)
                    return set;
                for (int i = 0; i < settings.LanFavoriteSteamIds.Count; i++)
                {
                    string id = settings.LanFavoriteSteamIds[i];
                    if (!string.IsNullOrWhiteSpace(id))
                        set.Add(id.Trim());
                }
            }
            catch
            {
            }

            return set;
        }

        private static void SortPeers(List<LanPresencePeer> list)
        {
            list.Sort((a, b) =>
            {
                int byPlaying = b.IsPlaying.CompareTo(a.IsPlaying);
                if (byPlaying != 0)
                    return byPlaying;
                return string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
            });
        }

        private static void SortPeersByLastSeen(List<LanPresencePeer> list)
        {
            list.Sort((a, b) => b.LastSeenUtc.CompareTo(a.LastSeenUtc));
        }

        private Control BuildSectionLabel(string text, ThemeColors colors, int rowWidth)
        {
            return new Label
            {
                AutoSize = false,
                Width = rowWidth,
                Height = 22,
                Margin = new Padding(0, 8, 0, 2),
                Padding = new Padding(8, 4, 8, 0),
                Text = text,
                Font = _sectionFont,
                ForeColor = colors.StatusTextSecondary,
                BackColor = Color.Transparent,
                AutoEllipsis = true
            };
        }

        private Control BuildPlayerRow(
            LanPresencePeer peer,
            ThemeColors colors,
            int rowWidth,
            bool isSelf,
            bool channelOnline,
            bool isFavorite)
        {
            if (peer == null)
                peer = new LanPresencePeer();

            string name = string.IsNullOrWhiteSpace(peer.DisplayName) ? peer.SteamId : peer.DisplayName;
            if (isFavorite && !isSelf)
                name = "★ " + name;

            string statusLine = LanPersonaDisplayHelper.FormatPersonaStatusLine(peer, isSelf, channelOnline);
            string steamId = peer.SteamId ?? string.Empty;
            string idLine = steamId;
            int textBlockHeight = NameLineHeight + StatusLineHeight + IdLineHeight;
            int textTop = Math.Max(0, (RowHeight - textBlockHeight) / 2);

            LanPersonaDisplayHelper.ResolvePersonaColors(
                peer, colors, isSelf, channelOnline, out Color nameColor, out Color statusColor);

            var row = new Panel
            {
                Width = rowWidth,
                Height = RowHeight,
                Margin = new Padding(0, 0, 0, 2),
                BackColor = isSelf ? colors.ControlBackground : colors.ListViewBackground,
                Padding = new Padding(8, 0, 8, 0),
                Tag = steamId,
                ContextMenuStrip = _playerMenu
            };
            row.MouseDown += OnPlayerRow_MouseDown;
            row.MouseDoubleClick += OnPlayerRow_MouseDoubleClick;

            var pic = new PictureBox
            {
                Size = new Size(AvatarDisplaySize, AvatarDisplaySize),
                Location = new Point(8, (RowHeight - AvatarDisplaySize) / 2),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = colors.ControlBackground,
                Tag = steamId,
                ContextMenuStrip = _playerMenu
            };
            pic.MouseDown += OnPlayerRow_MouseDown;
            pic.MouseDoubleClick += OnPlayerRow_MouseDoubleClick;
            Image avatar = LanAvatarImageHelper.TryLoadAvatar(peer.AvatarFilePath, AvatarDisplaySize);
            if (avatar != null)
                _rowAvatars.Add(avatar);
            else
                avatar = GetPlaceholderAvatar(colors);
            pic.Image = avatar;

            int textLeft = 8 + AvatarDisplaySize + 10;
            int textWidth = rowWidth - AvatarDisplaySize - 30;

            var lblName = new Label
            {
                AutoSize = false,
                Location = new Point(textLeft, textTop),
                Size = new Size(textWidth, NameLineHeight),
                Text = name,
                Font = _nameFont,
                ForeColor = nameColor,
                BackColor = Color.Transparent,
                AutoEllipsis = true,
                Tag = steamId,
                ContextMenuStrip = _playerMenu
            };
            lblName.MouseDown += OnPlayerRow_MouseDown;
            lblName.MouseDoubleClick += OnPlayerRow_MouseDoubleClick;

            var lblStatus = new Label
            {
                AutoSize = false,
                Location = new Point(textLeft, textTop + NameLineHeight),
                Size = new Size(textWidth, StatusLineHeight),
                Text = statusLine,
                Font = _metaFont,
                ForeColor = statusColor,
                BackColor = Color.Transparent,
                AutoEllipsis = true,
                Tag = steamId,
                ContextMenuStrip = _playerMenu
            };
            lblStatus.MouseDown += OnPlayerRow_MouseDown;
            lblStatus.MouseDoubleClick += OnPlayerRow_MouseDoubleClick;

            var lblSteamId = new Label
            {
                AutoSize = false,
                Location = new Point(textLeft, textTop + NameLineHeight + StatusLineHeight),
                Size = new Size(textWidth, IdLineHeight),
                Text = idLine,
                Font = _metaFont,
                ForeColor = colors.StatusTextSecondary,
                BackColor = Color.Transparent,
                AutoEllipsis = true,
                Tag = steamId,
                ContextMenuStrip = _playerMenu
            };
            lblSteamId.MouseDown += OnPlayerRow_MouseDown;
            lblSteamId.MouseDoubleClick += OnPlayerRow_MouseDoubleClick;

            row.Controls.Add(pic);
            row.Controls.Add(lblName);
            row.Controls.Add(lblStatus);
            row.Controls.Add(lblSteamId);

            return row;
        }

        private void OnPlayerRow_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;

            CaptureContextPeer(sender);
        }

        private void OnPlayerRow_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            CaptureContextPeer(sender);
            if (string.IsNullOrEmpty(_contextSteamId) || _contextIsSelf)
                return;

            bool chatBlocked = false;
            try
            {
                chatBlocked = ServiceLocator.LanChatService.IsChatBlocked(_contextSteamId);
            }
            catch
            {
            }

            if (chatBlocked)
                return;

            if (!_presence.IsOnline || _presence.IsLocalBusy)
                return;

            if (!_presence.IsPeerPresent(_contextSteamId))
                return;

            LanChatUiHost.OpenChat(_contextSteamId, Owner ?? this);
        }

        private void CaptureContextPeer(object sender)
        {
            Control c = sender as Control;
            string steamId = null;
            while (c != null && string.IsNullOrEmpty(steamId))
            {
                steamId = c.Tag as string;
                c = c.Parent;
            }

            _contextSteamId = steamId ?? string.Empty;
            LanPresencePeer self = _presence.GetLocalSelfSnapshot();
            _contextIsSelf = self != null
                && !string.IsNullOrEmpty(_contextSteamId)
                && string.Equals(_contextSteamId, self.SteamId, StringComparison.Ordinal);
        }

        private void OnPlayerMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ClearMenuItems(_playerMenu);
            if (string.IsNullOrEmpty(_contextSteamId))
            {
                e.Cancel = true;
                return;
            }

            if (_contextIsSelf)
            {
                _playerMenu.Items.Add("Edit account…", null, OnEditAccount_Click);

                ApplicationSettings settings = null;
                try
                {
                    settings = _appData.LoadApplicationSettings();
                }
                catch
                {
                }

                // Preferred status when channel is online; Offline is channel off (persona kept).
                // Auto-Away from idle does not change the menu check.
                var status = new ToolStripMenuItem("Status");
                bool channelOnline = _presence.IsOnline;
                LanPersonaState current = settings != null ? settings.LanPersonaState : LanPersonaState.Online;
                status.DropDownItems.Add(CreatePersonaStatusItem("Online", LanPersonaState.Online, current, channelOnline));
                status.DropDownItems.Add(CreatePersonaStatusItem("Away", LanPersonaState.Away, current, channelOnline));
                status.DropDownItems.Add(CreatePersonaStatusItem("Busy", LanPersonaState.Busy, current, channelOnline));
                status.DropDownItems.Add(CreatePersonaStatusItem("Hidden", LanPersonaState.Hidden, current, channelOnline));
                status.DropDownItems.Add(CreateOfflineStatusItem(channelOnline));
                _playerMenu.Items.Add(status);

                if (_presence.IsOnline)
                {
                    var lfg = new ToolStripMenuItem("Looking to play");
                    PopulateLookingToPlayMenu(lfg);
                    _playerMenu.Items.Add(lfg);

                    if (_presence.LookingForPlayAppId != 0)
                        _playerMenu.Items.Add("Clear looking to play", null, OnClearLookingToPlay_Click);
                }
            }
            else
            {
                bool present = _presence.IsPeerPresent(_contextSteamId);

                bool chatBlocked = false;
                try
                {
                    chatBlocked = ServiceLocator.LanChatService.IsChatBlocked(_contextSteamId);
                }
                catch
                {
                }

                if (chatBlocked)
                {
                    _playerMenu.Items.Add("Allow chat again", null, OnAllowChatAgain_Click);
                }
                else if (present && _presence.IsOnline && !_presence.IsLocalBusy)
                {
                    _playerMenu.Items.Add("Message…", null, OnMessagePeer_Click);
                }

                bool favorite = false;
                try
                {
                    favorite = _appData.IsLanFavoriteSteamId(_contextSteamId);
                }
                catch
                {
                }

                if (favorite)
                    _playerMenu.Items.Add("Remove from favorites", null, OnToggleFavorite_Click);
                else
                    _playerMenu.Items.Add("Add to favorites", null, OnToggleFavorite_Click);
            }
        }

        private void PopulateLookingToPlayMenu(ToolStripMenuItem parent)
        {
            if (_presence.LookingForPlayAppId != 0)
            {
                parent.DropDownItems.Add("Clear looking to play", null, OnClearLookingToPlay_Click);
                parent.DropDownItems.Add(new ToolStripSeparator());
            }

            List<GameConfig> games;
            try
            {
                games = ServiceLocator.GameDataService.GetAllGames();
            }
            catch
            {
                parent.DropDownItems.Add("(Could not load games)").Enabled = false;
                return;
            }

            if (games == null || games.Count == 0)
            {
                parent.DropDownItems.Add("(No games in library)").Enabled = false;
                return;
            }

            games.Sort((a, b) => string.Compare(
                a != null ? a.AppName : null,
                b != null ? b.AppName : null,
                StringComparison.OrdinalIgnoreCase));

            ulong currentLfg = _presence.LookingForPlayAppId;
            const int maxItems = 80;
            int count = Math.Min(games.Count, maxItems);
            for (int i = 0; i < count; i++)
            {
                GameConfig game = games[i];
                if (game == null || game.AppId == 0)
                    continue;
                string title = string.IsNullOrWhiteSpace(game.AppName)
                    ? ("App " + game.AppId)
                    : game.AppName.Trim();
                var item = new ToolStripMenuItem(title)
                {
                    Checked = game.AppId == currentLfg
                };
                ulong appId = game.AppId;
                item.Click += (s, e) => SetLookingToPlay(appId);
                parent.DropDownItems.Add(item);
            }

            if (games.Count > maxItems)
                parent.DropDownItems.Add("…and more in Settings").Enabled = false;
        }

        private void OnEditAccount_Click(object sender, EventArgs e)
        {
            using (var settingsForm = new SettingsForm())
            {
                settingsForm.SetSelectedTab(0);
                settingsForm.ShowDialog(this);
            }

            _presence.ApplyLocalPersonaFromSettings();
            UpdateWindowTitle();
            RefreshPeerList();
        }

        private ToolStripMenuItem CreatePersonaStatusItem(
            string text,
            LanPersonaState state,
            LanPersonaState current,
            bool channelOnline)
        {
            var item = new ToolStripMenuItem(text)
            {
                Checked = channelOnline && state == current,
                CheckOnClick = false
            };
            item.Click += (s, e) => SetPersonaState(state);
            return item;
        }

        private ToolStripMenuItem CreateOfflineStatusItem(bool channelOnline)
        {
            var item = new ToolStripMenuItem("Offline")
            {
                Checked = !channelOnline,
                CheckOnClick = false
            };
            item.Click += (s, e) => SetChannelOffline();
            return item;
        }

        private void SetChannelOffline()
        {
            if (!_presence.IsOnline)
                return;

            try
            {
                ApplicationSettings settings = _appData.LoadApplicationSettings();
                settings.LanPlayersOnline = false;
                ValidationResult save = _appData.SaveApplicationSettings(settings);
                if (!save.IsValid)
                {
                    FormMessageBoxHelper.ShowIfAlive(
                        this,
                        save.ErrorMessage ?? "Could not go offline.",
                        "LAN Players",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning("LAN Players: failed to persist offline preference: " + ex.Message);
                return;
            }

            _presence.SetOnline(false);
            UpdateWindowTitle();
            RefreshPeerList();
        }

        private void SetPersonaState(LanPersonaState state)
        {
            try
            {
                ValidationResult result = _appData.SetLanPersonaState(state);
                if (!result.IsValid)
                {
                    FormMessageBoxHelper.ShowIfAlive(
                        this,
                        result.ErrorMessage ?? "Could not save status.",
                        "LAN Players",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning("LAN Players: failed to save persona status: " + ex.Message);
                return;
            }

            // Choosing a status while offline turns the channel on (Hidden = listen-only).
            if (!_presence.IsOnline)
            {
                try
                {
                    ApplicationSettings settings = _appData.LoadApplicationSettings();
                    settings.LanPlayersOnline = true;
                    ValidationResult onlineResult = _appData.SaveApplicationSettings(settings);
                    if (!onlineResult.IsValid)
                    {
                        FormMessageBoxHelper.ShowIfAlive(
                            this,
                            onlineResult.ErrorMessage ?? "Could not go online.",
                            "LAN Players",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Program.LogService?.LogWarning("LAN Players: failed to go online with status: " + ex.Message);
                    return;
                }

                _presence.SetOnline(true);
            }

            _presence.ApplyLocalPersonaFromSettings();
            UpdateWindowTitle();
            RefreshPeerList();
        }

        private void OnClearLookingToPlay_Click(object sender, EventArgs e)
        {
            SetLookingToPlay(0);
        }

        private void SetLookingToPlay(ulong appId)
        {
            _presence.SetLookingForPlay(appId);
            RefreshPeerList();
        }

        private void OnMessagePeer_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_contextSteamId) || _contextIsSelf)
                return;
            LanChatUiHost.OpenChat(_contextSteamId, this.Owner ?? this);
        }

        private void OnAllowChatAgain_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_contextSteamId) || _contextIsSelf)
                return;

            try
            {
                ValidationResult result = ServiceLocator.LanChatService.SetChatBlocked(_contextSteamId, false);
                if (!result.IsValid)
                {
                    FormMessageBoxHelper.ShowIfAlive(
                        this,
                        result.ErrorMessage ?? "Could not unblock chat.",
                        "LAN Chat",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning("LAN chat unblock failed: " + ex.Message);
                return;
            }

            LanChatUiHost.OpenChat(_contextSteamId, this.Owner ?? this);
        }

        private void OnToggleFavorite_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_contextSteamId) || _contextIsSelf)
                return;

            bool currentlyFavorite = false;
            try
            {
                currentlyFavorite = _appData.IsLanFavoriteSteamId(_contextSteamId);
                ValidationResult result = _appData.SetLanFavoriteSteamId(_contextSteamId, !currentlyFavorite);
                if (!result.IsValid)
                {
                    FormMessageBoxHelper.ShowIfAlive(
                        this,
                        result.ErrorMessage ?? "Could not update favorites.",
                        "LAN Players",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning("LAN Players: failed to update favorite: " + ex.Message);
                return;
            }

            RefreshPeerList();
        }

        private void ClearPlayerRows()
        {
            if (flpLanPlayers == null)
                return;

            while (flpLanPlayers.Controls.Count > 0)
            {
                Control c = flpLanPlayers.Controls[0];
                flpLanPlayers.Controls.RemoveAt(0);
                DetachRowImages(c);
                c.Dispose();
            }

            for (int i = 0; i < _rowAvatars.Count; i++)
            {
                if (_rowAvatars[i] != null)
                    _rowAvatars[i].Dispose();
            }

            _rowAvatars.Clear();
        }

        private static void DetachRowImages(Control root)
        {
            PictureBox pic = root as PictureBox;
            if (pic != null)
                pic.Image = null;

            for (int i = 0; i < root.Controls.Count; i++)
                DetachRowImages(root.Controls[i]);
        }

        private static void ClearMenuItems(ContextMenuStrip menu)
        {
            if (menu == null)
                return;

            while (menu.Items.Count > 0)
            {
                ToolStripItem item = menu.Items[0];
                menu.Items.RemoveAt(0);
                item.Dispose();
            }
        }

        private void EnsureRowFonts()
        {
            if (_sectionFont == null)
                _sectionFont = new Font("Segoe UI", 8.25F, FontStyle.Regular);
            if (_nameFont == null)
                _nameFont = new Font("Segoe UI", 9F, FontStyle.Bold);
            if (_metaFont == null)
                _metaFont = new Font("Segoe UI", 8.25F, FontStyle.Regular);
        }

        private void DisposeRowFontsAndPlaceholder()
        {
            if (_sectionFont != null)
            {
                _sectionFont.Dispose();
                _sectionFont = null;
            }

            if (_nameFont != null)
            {
                _nameFont.Dispose();
                _nameFont = null;
            }

            if (_metaFont != null)
            {
                _metaFont.Dispose();
                _metaFont = null;
            }

            if (_placeholderAvatar != null)
            {
                _placeholderAvatar.Dispose();
                _placeholderAvatar = null;
            }
        }

        private Image GetPlaceholderAvatar(ThemeColors colors)
        {
            Color bg = colors != null ? colors.ControlBackground : Color.DimGray;
            if (_placeholderAvatar != null && _placeholderBackColor.ToArgb() == bg.ToArgb())
                return _placeholderAvatar;

            if (_placeholderAvatar != null)
            {
                _placeholderAvatar.Dispose();
                _placeholderAvatar = null;
            }

            var bmp = new Bitmap(AvatarDisplaySize, AvatarDisplaySize);
            using (Graphics g = Graphics.FromImage(bmp))
                g.Clear(bg);

            _placeholderAvatar = bmp;
            _placeholderBackColor = bg;
            return _placeholderAvatar;
        }
    }
}
