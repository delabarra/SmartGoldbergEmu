using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.LanKit;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Services
{
    // Orchestrates LAN Join/Invite: Detanup invite sidecars + LanKit lobby discover + GameLaunchService.
    public sealed class LanGameJoinService : IDisposable
    {
        // Returned when the guest declines close+relaunch; UI restores the pending invite quietly.
        public const string RelaunchCancelledMessage = "RELAUNCH_CANCELLED";

        // Titles that never publish Steam lobby id / rich-presence connect (custom matchmaking only).
        private static readonly HashSet<ulong> SteamLobbyJoinDenylist = new HashSet<ulong>
        {
            512900UL, // Streets of Rogue — IP:port multiplayer, no +connect_lobby
        };

        private readonly GameDataService _gameData;
        private readonly GameLaunchService _launch;
        private readonly LaunchOptionService _launchOptions;
        private readonly GoldbergFilesService _goldbergFiles;
        private readonly LanPresenceService _presence;
        private readonly ILogService _logger;
        private readonly object _readinessGate = new object();
        private System.Threading.Timer _readinessTimer;
        private int _readinessBusy;
        private bool _disposed;

        public LanGameJoinService()
            : this(
                ServiceLocator.GameDataService,
                ServiceLocator.GameLaunchService,
                ServiceLocator.LaunchOptionService,
                ServiceLocator.GoldbergFilesService,
                ServiceLocator.LanPresenceService,
                ServiceLocator.LogService)
        {
        }

        public LanGameJoinService(
            GameDataService gameData,
            GameLaunchService launch,
            LaunchOptionService launchOptions,
            GoldbergFilesService goldbergFiles,
            LanPresenceService presence,
            ILogService logger)
        {
            _gameData = gameData ?? throw new ArgumentNullException(nameof(gameData));
            _launch = launch ?? throw new ArgumentNullException(nameof(launch));
            _launchOptions = launchOptions ?? throw new ArgumentNullException(nameof(launchOptions));
            _goldbergFiles = goldbergFiles ?? throw new ArgumentNullException(nameof(goldbergFiles));
            _presence = presence ?? throw new ArgumentNullException(nameof(presence));
            _logger = logger;

            _presence.OnlineStateChanged += OnPresenceOnlineStateChanged;
            if (_presence.IsOnline)
                StartLobbyReadinessMonitor();
        }

        public void Dispose()
        {
            lock (_readinessGate)
            {
                if (_disposed)
                    return;
                _disposed = true;
            }

            try
            {
                _presence.OnlineStateChanged -= OnPresenceOnlineStateChanged;
            }
            catch
            {
            }

            StopLobbyReadinessMonitor();
            _presence.SetLocalJoinArguments(null);
        }

        // AppId only when the peer advertises Steam lobby/connect join args on presence.
        public static ulong GetJoinableAppId(LanPresencePeer peer)
        {
            if (peer == null)
                return 0;
            if (peer.IsPlaying && peer.AppId != 0 && peer.HasJoinableLobby)
                return peer.AppId;
            return 0;
        }

        // Override On/Off wins; Default uses denylist; unknown AppIds are treated as capable.
        public static bool SupportsSteamLobbyJoin(ulong appId, GameConfig game = null)
        {
            if (appId == 0)
                return false;

            LanSteamJoinSupport support = game != null && game.AppId == appId
                ? game.LanSteamJoinSupport
                : LanSteamJoinSupport.Default;

            if (support == LanSteamJoinSupport.On)
                return true;
            if (support == LanSteamJoinSupport.Off)
                return false;

            return !SteamLobbyJoinDenylist.Contains(appId);
        }

        public static string GetUnsupportedSteamLobbyJoinMessage(ulong appId, string gameName = null)
        {
            string title = string.IsNullOrWhiteSpace(gameName) ? ("App " + appId) : gameName.Trim();
            return title
                + " does not expose Steam lobby or connect join args for SGE Invite/Join. "
                + "Use the game's own multiplayer (for example IP and port).";
        }

        public GameConfig TryGetLibraryGame(ulong appId)
        {
            if (appId == 0)
                return null;
            try
            {
                return _gameData.GetGameByAppId(appId);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug("LAN join: library lookup failed: " + ex.Message);
                return null;
            }
        }

        // Merge host Steam64 into auto_accept_invite, LanKit-discover join args, then launch.
        public async Task<ValidationResult> JoinPeerGameAsync(
            LanPresencePeer peer,
            IWin32Window owner,
            CancellationToken cancellationToken)
        {
            if (peer == null || string.IsNullOrWhiteSpace(peer.SteamId))
                return ValidationResult.Failure("Player is missing.");

            ulong appId = GetJoinableAppId(peer);
            if (appId == 0)
                return ValidationResult.Failure("That player is not in a joinable game.");

            GameConfig game = TryGetLibraryGame(appId);
            if (game == null)
                return ValidationResult.Failure("That game is not in your library (App " + appId + ").");

            if (!SupportsSteamLobbyJoin(appId, game))
                return ValidationResult.Failure(GetUnsupportedSteamLobbyJoinMessage(appId, game.AppName));

            if (!peer.HasJoinableLobby)
            {
                return ValidationResult.Failure(
                    "That player is not in a joinable lobby yet. Ask them to create or enter the multiplayer lobby first.");
            }

            SaveResult accept = _goldbergFiles.EnsureSteamIdInAutoAcceptInvite(appId, peer.SteamId);
            if (!accept.IsSuccess)
                return ValidationResult.Failure(accept.ErrorMessage ?? "Could not update auto_accept_invite.txt.");

            TrySeedPeerBroadcastIp(appId, peer.RemoteAddress, "join");

            return await LaunchLibraryGameAsync(
                    game,
                    peer.SteamId,
                    appId,
                    owner,
                    cancellationToken,
                    knownJoinArguments: peer.JoinArguments,
                    peer.RemoteAddress)
                .ConfigureAwait(true);
        }

        // UDP invite with host-probed join args. Do not write auto_send_invites.txt (Goldberg overlay
        // auto-invite on friend-seen is redundant with +connect_lobby and can upset fragile hosts).
        public Task<ValidationResult> InvitePeerToLocalGameAsync(LanPresencePeer peer, CancellationToken cancellationToken)
        {
            if (peer == null || string.IsNullOrWhiteSpace(peer.SteamId))
                return Task.FromResult(ValidationResult.Failure("Player is missing."));
            if (!peer.IsPresent)
                return Task.FromResult(ValidationResult.Failure("That player is not online."));

            LanPresencePeer self = _presence.GetLocalSelfSnapshot();
            if (self == null || !self.IsPlaying || self.AppId == 0)
                return Task.FromResult(ValidationResult.Failure("Launch a game first, then invite."));

            GameConfig localGame = TryGetLibraryGame(self.AppId);
            if (!SupportsSteamLobbyJoin(self.AppId, localGame))
            {
                return Task.FromResult(ValidationResult.Failure(
                    GetUnsupportedSteamLobbyJoinMessage(self.AppId, self.GameName ?? localGame?.AppName)));
            }

            if (!self.HasJoinableLobby)
            {
                return Task.FromResult(ValidationResult.Failure(
                    "No joinable lobby yet. Create or enter the multiplayer lobby in-game, then invite again."));
            }

            if (cancellationToken.IsCancellationRequested)
                return Task.FromResult(ValidationResult.Failure("Invite cancelled."));

            // Best-effort: already-running hosts may have already loaded broadcasts, but keep the file ready.
            TrySeedPeerBroadcastIp(self.AppId, peer.RemoteAddress, "invite");

            _logger?.LogMessage(
                "LAN invite: sending nudge for AppId " + self.AppId
                + " to " + peer.SteamId
                + " with join args (no auto_send_invites).");

            return Task.FromResult(
                _presence.SendGameInvite(peer.SteamId, self.AppId, self.GameName, self.JoinArguments));
        }

        // After Accept on an inbound invite: merge inviter into auto_accept, discover + launch.
        public async Task<ValidationResult> AcceptInviteAsync(
            LanGameInviteEventArgs invite,
            IWin32Window owner,
            CancellationToken cancellationToken)
        {
            if (invite == null || invite.AppId == 0)
                return ValidationResult.Failure("Invite is invalid.");
            if (string.IsNullOrWhiteSpace(invite.FromSteamId))
                return ValidationResult.Failure("Invite sender is missing.");

            GameConfig game = TryGetLibraryGame(invite.AppId);
            if (game == null)
                return ValidationResult.Failure("That game is not in your library (App " + invite.AppId + ").");

            if (!SupportsSteamLobbyJoin(invite.AppId, game))
                return ValidationResult.Failure(GetUnsupportedSteamLobbyJoinMessage(invite.AppId, game.AppName));

            SaveResult accept = _goldbergFiles.EnsureSteamIdInAutoAcceptInvite(invite.AppId, invite.FromSteamId);
            if (!accept.IsSuccess)
                return ValidationResult.Failure(accept.ErrorMessage ?? "Could not update auto_accept_invite.txt.");

            string hostIp = null;
            LanPresencePeer hostPeer;
            if (_presence.TryGetPeer(invite.FromSteamId, out hostPeer) && hostPeer != null)
                hostIp = hostPeer.RemoteAddress;
            TrySeedPeerBroadcastIp(invite.AppId, hostIp, "accept");

            string knownJoin = invite.JoinArguments;
            return await LaunchLibraryGameAsync(
                    game, invite.FromSteamId, invite.AppId, owner, cancellationToken, knownJoin, hostIp)
                .ConfigureAwait(true);
        }

        private async Task<ValidationResult> LaunchLibraryGameAsync(
            GameConfig game,
            string hostSteamId,
            ulong discoverAppId,
            IWin32Window owner,
            CancellationToken cancellationToken,
            string knownJoinArguments = null,
            string hostBroadcastIp = null)
        {
            if (game == null)
                return ValidationResult.Failure("Game is missing.");

            if (!GameFolderPathHelper.TryResolveStoredExecutable(game, out _))
                return ValidationResult.Failure(GameFolderPathHelper.GetMissingStoredExecutableMessage(game));

            ValidationResult prerequisite = _launch.ValidateEmulatorFilesPrerequisite(game, requireLaunchModeBinaries: true);
            if (!prerequisite.IsValid)
                return prerequisite;

            if (cancellationToken.IsCancellationRequested)
                return ValidationResult.Failure("Launch cancelled.");

            string joinArgs = null;
            if (!string.IsNullOrWhiteSpace(knownJoinArguments))
            {
                joinArgs = knownJoinArguments.Trim();
            }
            else
            {
                uint appId32 = discoverAppId > uint.MaxValue ? 0u : (uint)discoverAppId;
                if (appId32 == 0)
                    return ValidationResult.Failure("AppId is invalid for lobby discovery.");

                _logger?.LogMessage(
                    "LAN join: peer probe for host Steam64 " + hostSteamId + " AppId " + appId32
                    + (string.IsNullOrWhiteSpace(hostBroadcastIp) ? string.Empty : (" hostIp=" + hostBroadcastIp.Trim()))
                    + ".");

                string probeIp = hostBroadcastIp;
                LanDiscoverResult discover = await Task.Run(
                        () => DiscoverJoinResult(hostSteamId, appId32, probeIp),
                        cancellationToken)
                    .ConfigureAwait(true);

                if (cancellationToken.IsCancellationRequested)
                    return ValidationResult.Failure("Launch cancelled.");

                // Host is visible on Goldberg LAN but not in a Steam lobby / no connect string yet.
                if (discover != null && discover.Status == LanDiscoverStatus.PeerSeenWithoutJoin)
                {
                    string notInLobby =
                        "The other player is on the emulator network, but they are not in a joinable lobby yet. "
                        + "Being in the game is not enough — they must create or enter the multiplayer lobby first, "
                        + "then invite you again (or you Join again).";
                    if (!SupportsSteamLobbyJoin(discoverAppId, game))
                        notInLobby += " " + GetUnsupportedSteamLobbyJoinMessage(discoverAppId, game.AppName);
                    return ValidationResult.Failure(notInLobby);
                }

                if (discover != null
                    && discover.Status == LanDiscoverStatus.JoinArgsFound
                    && discover.Join != null
                    && !string.IsNullOrWhiteSpace(discover.Join.ConnectArguments))
                {
                    joinArgs = discover.Join.ConnectArguments.Trim();
                }
                else
                {
                    return ValidationResult.Failure(
                        "Could not discover lobby/connect args for that player on the emulator network. "
                        + "Ask them to invite again while in a lobby, or use the in-game overlay Join.");
                }
            }

            // +connect_lobby only applies at process start — close+relaunch when already in-session.
            if (_launch.IsGameRunning(game.AppId, game))
            {
                string title = string.IsNullOrWhiteSpace(game.AppName)
                    ? ("App " + game.AppId)
                    : game.AppName.Trim();
                DialogResult answer = DialogResult.No;
                if (owner != null)
                {
                    answer = MessageBox.Show(
                        owner,
                        title + " is already running. Relaunch it now to join the lobby?",
                        "LAN lobby invite",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);
                }

                if (answer != DialogResult.Yes)
                {
                    return ValidationResult.Failure(RelaunchCancelledMessage);
                }

                if (cancellationToken.IsCancellationRequested)
                    return ValidationResult.Failure("Launch cancelled.");

                ValidationResult closed = _launch.TryCloseRunningGame(game.AppId);
                if (!closed.IsValid)
                    return closed;

                if (cancellationToken.IsCancellationRequested)
                    return ValidationResult.Failure("Launch cancelled.");
            }

            var launchResult = await _launchOptions
                .ShowLaunchOptionsAsync(game, owner, cancellationToken)
                .ConfigureAwait(true);
            if (launchResult.Cancelled || cancellationToken.IsCancellationRequested)
                return ValidationResult.Failure("Launch cancelled.");

            LaunchOption baseOption = launchResult.SkipLauncher ? null : launchResult.LaunchOption;
            if (baseOption == null && !string.IsNullOrWhiteSpace(game.Parameters))
            {
                baseOption = new LaunchOption
                {
                    Description = "LAN join",
                    Parameters = game.Parameters.Trim()
                };
            }

            LaunchOption launchOption = MergeJoinArguments(baseOption, joinArgs);
            _logger?.LogMessage(
                "LAN join launch args for AppId " + game.AppId + ": "
                + (launchOption != null && !string.IsNullOrWhiteSpace(launchOption.Parameters)
                    ? launchOption.Parameters
                    : "(none)"));

            ValidationResult launched = _launch.LaunchGame(game, useEmulator: true, launchOption: launchOption);
            if (!launched.IsValid)
            {
                _logger?.LogError(
                    "LAN join launch failed: AppId=" + game.AppId + ", name=" + game.AppName + ": " + launched.ErrorMessage);
            }

            return launched;
        }

        private void OnPresenceOnlineStateChanged(object sender, EventArgs e)
        {
            if (_presence.IsOnline)
                StartLobbyReadinessMonitor();
            else
            {
                StopLobbyReadinessMonitor();
                _presence.SetLocalJoinArguments(null);
            }
        }

        private void StartLobbyReadinessMonitor()
        {
            lock (_readinessGate)
            {
                if (_disposed)
                    return;
                if (_readinessTimer != null)
                    return;
                _readinessTimer = new System.Threading.Timer(
                    OnLobbyReadinessTick,
                    null,
                    1500,
                    LanPresenceConstants.LobbyReadinessIntervalMs);
            }
        }

        private void StopLobbyReadinessMonitor()
        {
            System.Threading.Timer timer;
            lock (_readinessGate)
            {
                timer = _readinessTimer;
                _readinessTimer = null;
            }

            try
            {
                timer?.Dispose();
            }
            catch
            {
            }
        }

        private void OnLobbyReadinessTick(object state)
        {
            if (Interlocked.CompareExchange(ref _readinessBusy, 1, 0) != 0)
                return;

            try
            {
                if (_disposed || !_presence.IsOnline)
                    return;

                LanPresencePeer self = _presence.GetLocalSelfSnapshot();
                if (self == null || !self.IsPlaying || self.AppId == 0)
                {
                    _presence.SetLocalJoinArguments(null);
                    return;
                }

                GameConfig game = TryGetLibraryGame(self.AppId);
                if (!SupportsSteamLobbyJoin(self.AppId, game))
                {
                    _presence.SetLocalJoinArguments(null);
                    return;
                }

                if (!TryParseSteam64(self.SteamId, out ulong selfSteam64))
                {
                    _presence.SetLocalJoinArguments(null);
                    return;
                }

                uint appId32 = self.AppId > uint.MaxValue ? 0u : (uint)self.AppId;
                if (appId32 == 0)
                {
                    _presence.SetLocalJoinArguments(null);
                    return;
                }

                // Ephemeral client looking for our own game Steam64 on 127.0.0.1 + LAN mesh.
                LanDiscoverResult discover;
                LanLobbyDiscoverer.TryDiscoverReadiness(selfSteam64, appId32, out discover, _logger, "127.0.0.1");
                if (discover != null
                    && discover.Status == LanDiscoverStatus.JoinArgsFound
                    && discover.Join != null
                    && !string.IsNullOrWhiteSpace(discover.Join.ConnectArguments))
                {
                    string join = discover.Join.ConnectArguments.Trim();
                    if (!string.Equals(self.JoinArguments ?? string.Empty, join, StringComparison.Ordinal))
                    {
                        _logger?.LogMessage(
                            "LAN lobby ready: AppId " + appId32 + " join args=" + join);
                    }

                    _presence.SetLocalJoinArguments(join);
                }
                else
                {
                    _presence.SetLocalJoinArguments(null);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogDebug("LAN lobby readiness probe failed: " + ex.Message);
            }
            finally
            {
                Interlocked.Exchange(ref _readinessBusy, 0);
            }
        }

        private LanDiscoverResult DiscoverJoinResult(string hostSteamId, uint appId, string hostBroadcastIp)
        {
            if (!TryParseSteam64(hostSteamId, out ulong steam64))
                return LanDiscoverResult.Empty;

            LanDiscoverResult result;
            LanLobbyDiscoverer.TryDiscover(steam64, appId, out result, _logger, hostBroadcastIp);
            return result ?? LanDiscoverResult.Empty;
        }

        private void TrySeedPeerBroadcastIp(ulong appId, string remoteAddress, string reason)
        {
            if (appId == 0 || string.IsNullOrWhiteSpace(remoteAddress))
                return;

            SaveResult seeded = _goldbergFiles.EnsureIpInCustomBroadcasts(appId, remoteAddress);
            if (!seeded.IsSuccess)
            {
                _logger?.LogWarning(
                    "LAN " + reason + ": could not seed custom_broadcasts for AppId " + appId
                    + ": " + (seeded.ErrorMessage ?? "unknown error"));
                return;
            }

            if (seeded.SettingsSaved > 0)
            {
                _logger?.LogMessage(
                    "LAN " + reason + ": added " + remoteAddress.Trim()
                    + " to custom_broadcasts for AppId " + appId + ".");
            }
        }

        // Append LanKit connect/+connect_lobby onto launch parameters without dropping existing ones.
        private static LaunchOption MergeJoinArguments(LaunchOption selected, string joinArguments)
        {
            if (string.IsNullOrWhiteSpace(joinArguments))
                return selected;

            string join = joinArguments.Trim();
            if (selected == null)
            {
                return new LaunchOption
                {
                    Description = "LAN join",
                    Parameters = join
                };
            }

            string existing = selected.Parameters ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(existing)
                && existing.IndexOf(join, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return selected;
            }

            string merged = string.IsNullOrWhiteSpace(existing)
                ? join
                : (existing.Trim() + " " + join);

            return new LaunchOption
            {
                Description = selected.Description,
                Executable = selected.Executable,
                Type = selected.Type,
                Parameters = merged,
                WorkingDir = selected.WorkingDir,
                BetaKey = selected.BetaKey,
                OsArch = selected.OsArch
            };
        }

        private static bool TryParseSteam64(string value, out ulong steamId)
        {
            steamId = 0;
            if (string.IsNullOrWhiteSpace(value))
                return false;
            return ulong.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out steamId)
                && steamId != 0;
        }
    }
}
