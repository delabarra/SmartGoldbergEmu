using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SmartGoldbergEmu.Abstractions;
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
        private readonly object _probeGate = new object();
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
        }

        public void Dispose()
        {
            lock (_probeGate)
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

            _presence.SetLocalJoinArguments(null);
        }

        // AppId when the peer is playing a title we can attempt Invite/Join for (LanKit runs on demand).
        public static ulong GetJoinableAppId(LanPresencePeer peer)
        {
            if (peer == null)
                return 0;
            if (peer.IsPlaying && peer.AppId != 0)
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
                + " does not support SGE Invite/Join (no Steam connect args). "
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

        // UDP invite with on-demand LanKit join args. Do not write auto_send_invites.txt (Goldberg overlay
        // auto-invite on friend-seen is redundant with +connect_lobby and can upset fragile hosts).
        public async Task<ValidationResult> InvitePeerToLocalGameAsync(
            LanPresencePeer peer,
            CancellationToken cancellationToken)
        {
            if (peer == null || string.IsNullOrWhiteSpace(peer.SteamId))
                return ValidationResult.Failure("Player is missing.");
            if (!peer.IsPresent)
                return ValidationResult.Failure("That player is not online.");

            LanPresencePeer self = _presence.GetLocalSelfSnapshot();
            if (self == null || !self.IsPlaying || self.AppId == 0)
                return ValidationResult.Failure("Launch a game first, then invite.");

            GameConfig localGame = TryGetLibraryGame(self.AppId);
            if (!SupportsSteamLobbyJoin(self.AppId, localGame))
            {
                return ValidationResult.Failure(
                    GetUnsupportedSteamLobbyJoinMessage(self.AppId, self.GameName ?? localGame?.AppName));
            }

            if (cancellationToken.IsCancellationRequested)
                return ValidationResult.Failure("Invite cancelled.");

            string joinArgs = await Task.Run(
                    () => ProbeLocalJoinArguments(self.AppId, self.SteamId),
                    cancellationToken)
                .ConfigureAwait(true);

            if (cancellationToken.IsCancellationRequested)
                return ValidationResult.Failure("Invite cancelled.");

            if (string.IsNullOrWhiteSpace(joinArgs))
            {
                return ValidationResult.Failure(
                    "No joinable game yet. Start or enter multiplayer in-game, then invite again.");
            }

            // Best-effort: already-running hosts may have already loaded broadcasts, but keep the file ready.
            TrySeedPeerBroadcastIp(self.AppId, peer.RemoteAddress, "invite");

            LanLaunchOptionHint launchHint;
            if (!_launch.TryGetLastLaunchOptionHint(self.AppId, out launchHint))
                launchHint = null;

            _logger?.LogMessage(
                "LAN invite: sending nudge for AppId " + self.AppId
                + " to " + peer.SteamId
                + " with join args"
                + (launchHint != null && launchHint.HasMatchableFields ? " + launch option hint" : string.Empty)
                + " (no auto_send_invites).");

            return _presence.SendGameInvite(
                peer.SteamId,
                self.AppId,
                self.GameName,
                joinArgs,
                launchHint);
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
                    game,
                    invite.FromSteamId,
                    invite.AppId,
                    owner,
                    cancellationToken,
                    knownJoin,
                    hostIp,
                    invite.LaunchHint)
                .ConfigureAwait(true);
        }

        private async Task<ValidationResult> LaunchLibraryGameAsync(
            GameConfig game,
            string hostSteamId,
            ulong discoverAppId,
            IWin32Window owner,
            CancellationToken cancellationToken,
            string knownJoinArguments = null,
            string hostBroadcastIp = null,
            LanLaunchOptionHint launchHint = null)
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
                    return ValidationResult.Failure("AppId is invalid for join discovery.");

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

                // Host is visible on Goldberg LAN but not in a joinable game / no connect string yet.
                if (discover != null && discover.Status == LanDiscoverStatus.PeerSeenWithoutJoin)
                {
                    string notJoinable =
                        "The other player is on the emulator network, but they are not in a joinable game yet. "
                        + "Being in the title alone is not enough — they must start or enter multiplayer first, "
                        + "then invite you again (or you Join again).";
                    if (!SupportsSteamLobbyJoin(discoverAppId, game))
                        notJoinable += " " + GetUnsupportedSteamLobbyJoinMessage(discoverAppId, game.AppName);
                    return ValidationResult.Failure(notJoinable);
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
                        "Could not discover join args for that player on the emulator network. "
                        + "Ask them to invite again while in the game, or use the in-game overlay Join.");
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
                        title + " is already running. Relaunch it now to join the game?",
                        "LAN game invite",
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

            LaunchOptionResult launchResult = await ResolveInviteLaunchOptionAsync(
                    game, launchHint, owner, cancellationToken)
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

            string depsFolder = GameFolderPathHelper.GetLaunchBaseGameFolder(game);
            if (!GameDependencyLaunchHelper.TryHandleMissingDependencies(
                    game,
                    depsFolder,
                    owner,
                    ServiceLocator.TaskReportService))
            {
                return ValidationResult.Failure("Launch cancelled.");
            }

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
            if (!_presence.IsOnline)
                _presence.SetLocalJoinArguments(null);
        }

        // One-shot LanKit self-probe for Invite; also refreshes presence `j` so peers can Join without re-probe.
        private string ProbeLocalJoinArguments(ulong appId, string selfSteamId)
        {
            lock (_probeGate)
            {
                if (_disposed)
                    return null;
            }

            if (!SupportsSteamLobbyJoin(appId, TryGetLibraryGame(appId)))
            {
                _presence.SetLocalJoinArguments(null);
                return null;
            }

            if (!TryParseSteam64(selfSteamId, out ulong selfSteam64))
            {
                _presence.SetLocalJoinArguments(null);
                return null;
            }

            uint appId32 = appId > uint.MaxValue ? 0u : (uint)appId;
            if (appId32 == 0)
            {
                _presence.SetLocalJoinArguments(null);
                return null;
            }

            LanDiscoverResult discover;
            LanLobbyDiscoverer.TryDiscoverReadiness(selfSteam64, appId32, out discover, _logger, "127.0.0.1");
            if (discover != null
                && discover.Status == LanDiscoverStatus.JoinArgsFound
                && discover.Join != null
                && !string.IsNullOrWhiteSpace(discover.Join.ConnectArguments))
            {
                string join = discover.Join.ConnectArguments.Trim();
                _logger?.LogMessage("LAN lobby ready: AppId " + appId32 + " join args=" + join);
                _presence.SetLocalJoinArguments(join);
                return join;
            }

            _presence.SetLocalJoinArguments(null);
            return null;
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

        // Prefer the host's invite launch hint when it matches locally; otherwise show the usual picker.
        private async Task<LaunchOptionResult> ResolveInviteLaunchOptionAsync(
            GameConfig game,
            LanLaunchOptionHint launchHint,
            IWin32Window owner,
            CancellationToken cancellationToken)
        {
            if (launchHint != null && launchHint.HasMatchableFields)
            {
                if (launchHint.PreferLibraryPath)
                {
                    _logger?.LogMessage(
                        "LAN join: invite launch hint prefers library Path for AppId " + game.AppId
                        + "; skipping launch options picker.");
                    return new LaunchOptionResult
                    {
                        LaunchOption = null,
                        SkipLauncher = true,
                        Cancelled = false
                    };
                }

                List<LaunchOption> allOptions = await _launchOptions
                    .ExtractLaunchOptionsIncludingUserIniAsync(game, cancellationToken)
                    .ConfigureAwait(true);
                if (cancellationToken.IsCancellationRequested)
                {
                    return new LaunchOptionResult
                    {
                        LaunchOption = null,
                        SkipLauncher = false,
                        Cancelled = true
                    };
                }

                if (allOptions == null)
                    allOptions = new List<LaunchOption>();

                // Filtered list only — do not silent-match hidden restricted extras the guest chose not to show.
                List<LaunchOption> filtered = _launchOptions.FilterLaunchOptionsForCurrentSettings(allOptions);
                LaunchOption matched;
                if (LaunchOptionService.TryMatchLaunchOptionHint(filtered, launchHint, out matched))
                {
                    _logger?.LogMessage(
                        "LAN join: matched invite launch hint for AppId " + game.AppId
                        + " -> " + (matched.Description ?? matched.Executable ?? "(unnamed)")
                        + "; skipping launch options picker.");
                    return new LaunchOptionResult
                    {
                        LaunchOption = matched,
                        SkipLauncher = false,
                        Cancelled = false
                    };
                }

                _logger?.LogMessage(
                    "LAN join: invite launch hint did not match a safe visible local option for AppId "
                    + game.AppId + "; showing launch options picker.");
            }

            return await _launchOptions
                .ShowLaunchOptionsAsync(game, owner, cancellationToken)
                .ConfigureAwait(true);
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
