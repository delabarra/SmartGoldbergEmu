using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;
using SmartGoldbergEmu.Services;

namespace SmartGoldbergEmu.Forms
{
    public partial class LanPlayersForm : ThemedForm
    {
        private const int AvatarDisplaySize = 40;
        private const int RowHeightTwoLine = 56;
        private const int RowHeightThreeLine = 72;

        private readonly LanPresenceService _presence;
        private readonly AppDataService _appData;
        private readonly List<Image> _rowAvatars = new List<Image>();
        private readonly ContextMenuStrip _playerMenu;
        private bool _eventsHooked;
        private bool _initialLayoutApplied;
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
            _playerMenu = new ContextMenuStrip();
            _playerMenu.Opening += OnPlayerMenu_Opening;
            if (WindowStateHelper.TryRestoreLanListWindowState(this, _appData))
                _initialLayoutApplied = true;
            HookPresenceEvents();
            UpdateOnlineButton();
            RefreshPeerList();
        }

        public void ApplyInitialPlacement()
        {
            if (_initialLayoutApplied)
                return;

            if (WindowStateHelper.TryRestoreLanListWindowState(this, _appData))
            {
                _initialLayoutApplied = true;
                return;
            }

            PositionBesideOwner();
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

        private void PositionBesideOwner()
        {
            if (Owner == null)
                return;

            Rectangle ownerBounds = Owner.Bounds;
            int x = ownerBounds.Right - 8;
            int y = ownerBounds.Top + 40;
            Rectangle wa = Screen.FromControl(Owner).WorkingArea;
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

        private void OnPresencePeersChanged(object sender, EventArgs e)
        {
            if (IsDisposed || Disposing)
                return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action(RefreshPeerList));
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
                BeginInvoke(new Action(() =>
                {
                    UpdateOnlineButton();
                    RefreshPeerList();
                }));
                return;
            }

            UpdateOnlineButton();
            RefreshPeerList();
        }

        private void OnOnlineToggle_Click(object sender, EventArgs e)
        {
            bool nextOnline = !_presence.IsOnline;
            try
            {
                ApplicationSettings settings = _appData.LoadApplicationSettings();
                settings.LanPlayersOnline = nextOnline;
                ValidationResult save = _appData.SaveApplicationSettings(settings);
                if (!save.IsValid)
                {
                    FormMessageBoxHelper.ShowIfAlive(
                        this,
                        save.ErrorMessage ?? "Could not save LAN online preference.",
                        "LAN Players",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                Program.LogService?.LogWarning("LAN Players: failed to persist online preference: " + ex.Message);
            }

            _presence.SetOnline(nextOnline);
            UpdateOnlineButton();
            RefreshPeerList();
        }

        private void UpdateOnlineButton()
        {
            bool online = _presence.IsOnline;
            btnOnlineToggle.Text = online ? "Go Offline" : "Go Online";
            Text = online ? "LAN Players" : "LAN Players (Offline)";
        }

        private void RefreshPeerList()
        {
            ThemeColors colors = ThemeService.GetThemeColors(ThemeService.EffectiveTheme);
            LanPresencePeer self = _presence.GetLocalSelfSnapshot();
            IReadOnlyList<LanPresencePeer> peers = _presence.IsOnline
                ? _presence.GetPeersSnapshot()
                : Array.Empty<LanPresencePeer>();

            HashSet<string> favorites = LoadFavoriteSet();

            flpLanPlayers.SuspendLayout();
            try
            {
                ClearPlayerRows();

                lblEmpty.Visible = false;
                flpLanPlayers.Visible = true;

                int rowWidth = Math.Max(200, flpLanPlayers.ClientSize.Width - 24);

                flpLanPlayers.Controls.Add(BuildPlayerRow(
                    self, colors, rowWidth, isSelf: true, channelOnline: _presence.IsOnline, isFavorite: false));

                if (!_presence.IsOnline)
                {
                    flpLanPlayers.Controls.Add(BuildSectionLabel("You are offline.", colors, rowWidth));
                    UpdateGamesFoundStatus(0);
                    return;
                }

                var favoritePresent = new List<LanPresencePeer>();
                var onlineOthers = new List<LanPresencePeer>();
                var lastSeen = new List<LanPresencePeer>();

                for (int i = 0; i < peers.Count; i++)
                {
                    LanPresencePeer peer = peers[i];
                    bool fav = favorites.Contains(peer.SteamId ?? string.Empty);
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

                UpdateGamesFoundStatus(CountUniqueGamesFound(self, favoritePresent, onlineOthers));
            }
            finally
            {
                flpLanPlayers.ResumeLayout(true);
            }
        }

        private void UpdateGamesFoundStatus(int uniqueGames)
        {
            if (uniqueGames == 1)
                lblGamesFound.Text = "1 game found";
            else
                lblGamesFound.Text = uniqueGames + " games found";
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
                Font = new Font("Segoe UI", 8.25F, FontStyle.Regular),
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
            string name = string.IsNullOrWhiteSpace(peer.DisplayName) ? peer.SteamId : peer.DisplayName;
            if (isFavorite && !isSelf)
                name = "★ " + name;

            string statusLine = FormatPersonaStatusLine(peer, isSelf, channelOnline);
            string gameLine = FormatGameStatusLine(peer, channelOnline);
            bool showGame = !string.IsNullOrEmpty(gameLine);
            int rowHeight = showGame ? RowHeightThreeLine : RowHeightTwoLine;

            Color statusColor = colors.StatusTextSecondary;
            if (channelOnline && peer.IsPresent && peer.PersonaState != LanPersonaState.Away)
                statusColor = colors.StatusTextSecondary;
            if (channelOnline && peer.IsPlaying)
                statusColor = colors.SuccessColor;

            var row = new Panel
            {
                Width = rowWidth,
                Height = rowHeight,
                Margin = new Padding(0, 0, 0, 2),
                BackColor = isSelf ? colors.ControlBackground : colors.ListViewBackground,
                Padding = new Padding(8, 6, 8, 6),
                Tag = peer.SteamId ?? string.Empty,
                ContextMenuStrip = _playerMenu
            };
            row.MouseDown += OnPlayerRow_MouseDown;

            var pic = new PictureBox
            {
                Size = new Size(AvatarDisplaySize, AvatarDisplaySize),
                Location = new Point(8, (rowHeight - AvatarDisplaySize) / 2),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = colors.ControlBackground,
                Tag = peer.SteamId ?? string.Empty,
                ContextMenuStrip = _playerMenu
            };
            pic.MouseDown += OnPlayerRow_MouseDown;
            Image avatar = TryLoadAvatar(peer.AvatarFilePath) ?? CreatePlaceholderAvatar(colors);
            _rowAvatars.Add(avatar);
            pic.Image = avatar;

            int textLeft = 8 + AvatarDisplaySize + 10;
            int textWidth = rowWidth - AvatarDisplaySize - 30;

            var lblName = new Label
            {
                AutoSize = false,
                Location = new Point(textLeft, 6),
                Size = new Size(textWidth, 18),
                Text = name,
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
                ForeColor = colors.ListViewForeground,
                BackColor = Color.Transparent,
                AutoEllipsis = true,
                Tag = peer.SteamId ?? string.Empty,
                ContextMenuStrip = _playerMenu
            };
            lblName.MouseDown += OnPlayerRow_MouseDown;

            var lblStatus = new Label
            {
                AutoSize = false,
                Location = new Point(textLeft, 24),
                Size = new Size(textWidth, 16),
                Text = statusLine,
                Font = new Font("Segoe UI", 8.25F, FontStyle.Regular),
                ForeColor = (!channelOnline || !peer.IsPresent) ? colors.StatusTextSecondary : statusColor,
                BackColor = Color.Transparent,
                AutoEllipsis = true,
                Tag = peer.SteamId ?? string.Empty,
                ContextMenuStrip = _playerMenu
            };
            lblStatus.MouseDown += OnPlayerRow_MouseDown;

            row.Controls.Add(pic);
            row.Controls.Add(lblName);
            row.Controls.Add(lblStatus);

            if (showGame)
            {
                var lblGame = new Label
                {
                    AutoSize = false,
                    Location = new Point(textLeft, 42),
                    Size = new Size(textWidth, 16),
                    Text = gameLine,
                    Font = new Font("Segoe UI", 8.25F, FontStyle.Regular),
                    ForeColor = colors.SuccessColor,
                    BackColor = Color.Transparent,
                    AutoEllipsis = true,
                    Tag = peer.SteamId ?? string.Empty,
                    ContextMenuStrip = _playerMenu
                };
                lblGame.MouseDown += OnPlayerRow_MouseDown;
                row.Controls.Add(lblGame);
            }

            return row;
        }

        private void OnPlayerRow_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;

            Control c = sender as Control;
            string steamId = null;
            while (c != null && string.IsNullOrEmpty(steamId))
            {
                steamId = c.Tag as string;
                c = c.Parent;
            }

            _contextSteamId = steamId ?? string.Empty;
            LanPresencePeer self = _presence.GetLocalSelfSnapshot();
            _contextIsSelf = string.Equals(_contextSteamId, self.SteamId, StringComparison.Ordinal);
        }

        private void OnPlayerMenu_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _playerMenu.Items.Clear();
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

                // Check preferred (saved) status; auto-Away from idle does not change the menu check.
                var status = new ToolStripMenuItem("Status");
                LanPersonaState current = settings != null ? settings.LanPersonaState : LanPersonaState.Online;
                status.DropDownItems.Add(CreatePersonaStatusItem("Online", LanPersonaState.Online, current));
                status.DropDownItems.Add(CreatePersonaStatusItem("Away", LanPersonaState.Away, current));
                status.DropDownItems.Add(CreatePersonaStatusItem("Hidden", LanPersonaState.Hidden, current));
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
            RefreshPeerList();
        }

        private ToolStripMenuItem CreatePersonaStatusItem(string text, LanPersonaState state, LanPersonaState current)
        {
            var item = new ToolStripMenuItem(text)
            {
                Checked = state == current,
                CheckOnClick = false
            };
            item.Click += (s, e) => SetPersonaState(state);
            return item;
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
            UpdateOnlineButton();
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
                c.Dispose();
            }

            for (int i = 0; i < _rowAvatars.Count; i++)
            {
                if (_rowAvatars[i] != null)
                    _rowAvatars[i].Dispose();
            }

            _rowAvatars.Clear();
        }

        private static string FormatPersonaStatusLine(LanPresencePeer peer, bool isSelf, bool channelOnline)
        {
            if (!channelOnline)
                return "Offline";

            if (!isSelf && !peer.IsPresent)
                return FormatLastSeen(peer.LastSeenUtc);

            string state;
            if (isSelf && peer.PersonaState == LanPersonaState.Hidden)
                state = "Hidden";
            else if (peer.PersonaState == LanPersonaState.Away)
                state = "Away";
            else
                state = "Online";

            if (peer.IsLookingForPlay)
            {
                string game = !string.IsNullOrWhiteSpace(peer.LookingForPlayGameName)
                    ? peer.LookingForPlayGameName.Trim()
                    : ("App " + peer.LookingForPlayAppId);
                state = state + " · Looking to play " + game;
            }

            if (!string.IsNullOrWhiteSpace(peer.CustomStatus))
                state = state + " — " + peer.CustomStatus.Trim();

            return state;
        }

        private static string FormatGameStatusLine(LanPresencePeer peer, bool channelOnline)
        {
            if (!channelOnline || !peer.IsPresent || !peer.IsPlaying)
                return null;

            string game = !string.IsNullOrWhiteSpace(peer.GameName)
                ? peer.GameName.Trim()
                : ("App " + peer.AppId);
            return "Playing " + game;
        }

        private static string FormatLastSeen(DateTime lastSeenUtc)
        {
            TimeSpan age = DateTime.UtcNow - lastSeenUtc;
            if (age.TotalSeconds < 45)
                return "Last seen just now";
            if (age.TotalMinutes < 60)
                return "Last seen " + Math.Max(1, (int)age.TotalMinutes) + "m ago";
            if (age.TotalHours < 48)
                return "Last seen " + Math.Max(1, (int)age.TotalHours) + "h ago";
            return "Last seen " + Math.Max(1, (int)age.TotalDays) + "d ago";
        }

        private static Image TryLoadAvatar(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                using (var ms = new MemoryStream(bytes))
                using (var img = Image.FromStream(ms))
                {
                    return new Bitmap(img, new Size(AvatarDisplaySize, AvatarDisplaySize));
                }
            }
            catch
            {
                return null;
            }
        }

        private static Image CreatePlaceholderAvatar(ThemeColors colors)
        {
            var bmp = new Bitmap(AvatarDisplaySize, AvatarDisplaySize);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(colors != null ? colors.ControlBackground : Color.DimGray);
            }

            return bmp;
        }
    }
}
