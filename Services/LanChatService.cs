using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.JsonKit;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Services
{
    // LAN sticky-note chat: multiplexed on presence UDP, consent-gated, no side effects from text.
    public sealed class LanChatService : IDisposable
    {
        private readonly object _gate = new object();
        private readonly ILogService _logger;
        private readonly LanPresenceService _presence;
        private readonly AppDataService _appData;
        private readonly Dictionary<string, PeerSession> _sessions =
            new Dictionary<string, PeerSession>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<long>> _inviteTimestamps =
            new Dictionary<string, List<long>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<long>> _messageTimestamps =
            new Dictionary<string, List<long>>(StringComparer.Ordinal);
        private readonly HashSet<string> _blockedIds =
            new HashSet<string>(StringComparer.Ordinal);
        // Accepted once — skip the first-message gate across restarts until blocked.
        private readonly HashSet<string> _trustedIds =
            new HashSet<string>(StringComparer.Ordinal);
        private bool _hooked;
        private bool _disposed;

        public event EventHandler<LanChatSessionEventArgs> SessionUpdated;

        public LanChatService()
            : this(ServiceLocator.LogService, ServiceLocator.LanPresenceService, ServiceLocator.AppDataService)
        {
        }

        public LanChatService(ILogService logger, LanPresenceService presence, AppDataService appData)
        {
            _logger = logger;
            _presence = presence ?? throw new ArgumentNullException(nameof(presence));
            _appData = appData ?? throw new ArgumentNullException(nameof(appData));
            ReloadIdLists();
            EnsureHooked();
        }

        public void EnsureHooked()
        {
            lock (_gate)
            {
                if (_disposed || _hooked)
                    return;
                _presence.ChatPacketReceived += OnChatPacketReceived;
                _hooked = true;
            }
        }

        public void ReloadBlockedIds()
        {
            ReloadIdLists();
        }

        public void ReloadTrustedIds()
        {
            ReloadIdLists();
        }

        private void ReloadIdLists()
        {
            HashSet<string> blocked = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> trusted = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                ApplicationSettings settings = _appData.LoadApplicationSettings();
                FillSteamIdSet(blocked, settings != null ? settings.LanChatBlockedSteamIds : null);
                FillSteamIdSet(trusted, settings != null ? settings.LanChatTrustedSteamIds : null);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug("LAN chat id list load failed: " + ex.Message);
            }

            lock (_gate)
            {
                _blockedIds.Clear();
                foreach (string id in blocked)
                    _blockedIds.Add(id);
                _trustedIds.Clear();
                foreach (string id in trusted)
                    _trustedIds.Add(id);
            }
        }

        private static void FillSteamIdSet(HashSet<string> target, IList<string> source)
        {
            if (target == null || source == null)
                return;
            for (int i = 0; i < source.Count; i++)
            {
                string id = LanWireHelper.SanitizeSteamId(source[i]);
                if (!string.IsNullOrEmpty(id))
                    target.Add(id);
            }
        }

        public bool IsChatBlocked(string steamId)
        {
            steamId = LanWireHelper.SanitizeSteamId(steamId);
            if (string.IsNullOrEmpty(steamId))
                return false;
            lock (_gate)
                return _blockedIds.Contains(steamId);
        }

        public LanChatSessionSnapshot GetSessionSnapshot(string peerSteamId)
        {
            peerSteamId = LanWireHelper.SanitizeSteamId(peerSteamId);
            if (string.IsNullOrEmpty(peerSteamId))
                return CreateEmptySnapshot(peerSteamId);

            lock (_gate)
            {
                PeerSession session;
                if (!_sessions.TryGetValue(peerSteamId, out session))
                {
                    if (_blockedIds.Contains(peerSteamId))
                        return CreateBlockedSnapshot(peerSteamId);
                    if (_trustedIds.Contains(peerSteamId))
                        return CreateTrustedShellSnapshot(peerSteamId);
                    return CreateEmptySnapshot(peerSteamId);
                }

                return CloneSnapshot(session);
            }
        }

        // Opens/returns a local session shell so the UI can compose the first message.
        public LanChatSessionSnapshot EnsureSessionShell(string peerSteamId)
        {
            peerSteamId = LanWireHelper.SanitizeSteamId(peerSteamId);
            if (string.IsNullOrEmpty(peerSteamId))
                return CreateEmptySnapshot(peerSteamId);

            LanChatSessionSnapshot snapshot;
            lock (_gate)
            {
                if (_blockedIds.Contains(peerSteamId))
                    return CreateBlockedSnapshot(peerSteamId);

                PeerSession session = GetOrCreateSessionUnlocked(peerSteamId);
                snapshot = CloneSnapshot(session);
            }

            return snapshot;
        }

        public bool TrySendMessage(string peerSteamId, string text, out string error)
        {
            error = null;
            peerSteamId = LanWireHelper.SanitizeSteamId(peerSteamId);
            text = SanitizeChatText(text);
            if (string.IsNullOrEmpty(peerSteamId))
            {
                error = "Unknown player.";
                return false;
            }

            if (string.IsNullOrEmpty(text))
            {
                error = "Message is empty.";
                return false;
            }

            if (!_presence.IsOnline)
            {
                error = "Go online in LAN Players to chat.";
                return false;
            }

            if (_presence.IsLocalBusy)
            {
                error = "You are Busy. Change status to send chat.";
                return false;
            }

            string localId = GetLocalSteamId();
            if (string.IsNullOrEmpty(localId) || string.Equals(localId, peerSteamId, StringComparison.Ordinal))
            {
                error = "Cannot chat with this player.";
                return false;
            }

            if (!_presence.IsPeerPresent(peerSteamId))
            {
                error = "Player is not present on the LAN.";
                return false;
            }

            string messageId = NewMessageId();
            DateTime utc = DateTime.UtcNow;
            bool asRequest;
            byte[] payload;

            lock (_gate)
            {
                if (_blockedIds.Contains(peerSteamId))
                {
                    error = "This player is blocked.";
                    return false;
                }

                PeerSession session = GetOrCreateSessionUnlocked(peerSteamId);
                if (session.Status == LanChatSessionStatus.IncomingPending)
                {
                    error = "Accept or ignore their message first.";
                    return false;
                }

                if (session.Status == LanChatSessionStatus.OutgoingPending)
                {
                    error = "Waiting for them to accept.";
                    return false;
                }

                if (session.SoftIgnoreUntilUtc > utc)
                {
                    error = "Wait a moment before messaging again.";
                    return false;
                }

                // Trusted peers skip the first-message gate forever (until blocked).
                if (_trustedIds.Contains(peerSteamId)
                    && session.Status != LanChatSessionStatus.Accepted)
                {
                    session.Status = LanChatSessionStatus.Accepted;
                    session.PendingInviteMessageId = null;
                    session.StatusDetail = string.Empty;
                }

                asRequest = session.Status != LanChatSessionStatus.Accepted;
                string rateKey = peerSteamId;
                if (asRequest)
                {
                    if (!LanWireHelper.TryConsumeRate(
                        _inviteTimestamps,
                        rateKey,
                        LanChatConstants.InviteMaxPerPeer,
                        LanChatConstants.InviteWindowMs))
                    {
                        error = "Too many chat requests. Try again later.";
                        return false;
                    }
                }
                else
                {
                    if (!LanWireHelper.TryConsumeRate(
                        _messageTimestamps,
                        rateKey,
                        LanChatConstants.MessageMaxPerPeer,
                        LanChatConstants.MessageWindowMs))
                    {
                        error = "Sending too fast. Try again shortly.";
                        return false;
                    }
                }

                string type = asRequest ? LanChatConstants.MessageRequest : LanChatConstants.MessageText;
                payload = BuildPacket(type, localId, peerSteamId, messageId, text, utc, null);
                if (payload == null)
                {
                    error = "Message is too large.";
                    return false;
                }

                var localMessage = new LanChatMessage
                {
                    MessageId = messageId,
                    FromSteamId = localId,
                    Text = text,
                    TimestampUtc = utc,
                    IsLocal = true,
                    Kind = LanChatMessageKind.Text
                };
                session.Messages.Add(localMessage);
                TrimMessagesUnlocked(session);

                if (asRequest)
                {
                    session.Status = LanChatSessionStatus.OutgoingPending;
                    session.PendingInviteMessageId = messageId;
                    session.StatusDetail = "Waiting for them to accept…";
                }
                else
                {
                    session.StatusDetail = string.Empty;
                }
            }

            // Broadcast (not unicast): same firewall lesson as avatar replies.
            if (!_presence.TrySendBroadcast(payload))
            {
                error = "Could not send on the LAN.";
                return false;
            }

            RaiseSessionUpdated(peerSteamId);
            return true;
        }

        // Local chat line after a successful lobby invite UDP send (no wire chat packet).
        public void RecordGameInviteSent(string peerSteamId, ulong appId, string gameName)
        {
            peerSteamId = LanWireHelper.SanitizeSteamId(peerSteamId);
            if (string.IsNullOrEmpty(peerSteamId) || appId == 0)
                return;

            string localId = GetLocalSteamId();
            if (string.IsNullOrEmpty(localId))
                return;

            string displayGame = FormatInviteGameName(appId, gameName);
            DateTime utc = DateTime.UtcNow;
            lock (_gate)
            {
                if (_blockedIds.Contains(peerSteamId))
                    return;

                PeerSession session = GetOrCreateSessionUnlocked(peerSteamId);
                // One "You sent…" per silent invite window; further clicks wait for a cooldown reply.
                if (HasRecentLocalInviteNoteUnlocked(
                        session,
                        LanChatMessageKind.GameInviteSent,
                        LanPresenceConstants.InviteAcceptWindowMs,
                        utc))
                    return;

                session.Messages.Add(new LanChatMessage
                {
                    MessageId = NewMessageId(),
                    FromSteamId = localId,
                    // Short history note only — Accept/Decline live on the form panel.
                    Text = "You sent an invitation to join " + displayGame + ".",
                    TimestampUtc = utc,
                    IsLocal = true,
                    Kind = LanChatMessageKind.GameInviteSent
                });
                TrimMessagesUnlocked(session);
            }

            RaiseSessionUpdated(peerSteamId);
        }

        // Local chat line when the peer silently rejected a repeat lobby invite (cooldown packet).
        public void RecordGameInviteCooldown(string peerSteamId, int remainingSeconds)
        {
            peerSteamId = LanWireHelper.SanitizeSteamId(peerSteamId);
            if (string.IsNullOrEmpty(peerSteamId))
                return;

            if (remainingSeconds < 1)
                remainingSeconds = 1;

            string localId = GetLocalSteamId();
            if (string.IsNullOrEmpty(localId))
                return;

            string wait = remainingSeconds == 1
                ? "1 second"
                : (remainingSeconds.ToString() + " seconds");
            DateTime utc = DateTime.UtcNow;
            lock (_gate)
            {
                if (_blockedIds.Contains(peerSteamId))
                    return;

                PeerSession session = GetOrCreateSessionUnlocked(peerSteamId);
                string text = "Try again in " + wait + ".";
                // Refresh the trailing cooldown line instead of stacking one per spam click.
                bool refreshed = false;
                if (session.Messages.Count > 0)
                {
                    LanChatMessage last = session.Messages[session.Messages.Count - 1];
                    if (last != null
                        && last.Kind == LanChatMessageKind.GameInviteCooldown
                        && last.IsLocal
                        && (utc - last.TimestampUtc).TotalMilliseconds < LanPresenceConstants.InviteAcceptWindowMs)
                    {
                        last.Text = text;
                        last.TimestampUtc = utc;
                        refreshed = true;
                    }
                }

                if (!refreshed)
                {
                    session.Messages.Add(new LanChatMessage
                    {
                        MessageId = NewMessageId(),
                        FromSteamId = localId,
                        Text = text,
                        TimestampUtc = utc,
                        IsLocal = true,
                        Kind = LanChatMessageKind.GameInviteCooldown
                    });
                    TrimMessagesUnlocked(session);
                }
            }

            RaiseSessionUpdated(peerSteamId);
        }

        // Inbound lobby invite from presence (already sanitized/rate-limited). Display only;
        // Accept pulls join args from session state, never from message Text.
        public void RecordGameInviteReceived(LanGameInviteEventArgs invite)
        {
            if (invite == null || invite.AppId == 0)
                return;

            string peerSteamId = LanWireHelper.SanitizeSteamId(invite.FromSteamId);
            if (string.IsNullOrEmpty(peerSteamId))
                return;

            string join = SanitizeJoinArguments(invite.JoinArguments);
            string displayGame = FormatInviteGameName(invite.AppId, invite.GameName);

            DateTime utc = DateTime.UtcNow;
            string messageId = NewMessageId();
            lock (_gate)
            {
                if (_blockedIds.Contains(peerSteamId))
                    return;

                PeerSession session = GetOrCreateSessionUnlocked(peerSteamId);
                if (!string.IsNullOrWhiteSpace(invite.FromDisplayName))
                    session.PeerDisplayName = SanitizeChatText(invite.FromDisplayName);

                // Latest invite wins — avoid stacking Accept targets from the same peer.
                session.PendingGameInvite = new PendingGameInvite
                {
                    AppId = invite.AppId,
                    GameName = SanitizeInviteGameName(invite.GameName),
                    JoinArguments = join,
                    FromSteamId = peerSteamId,
                    FromDisplayName = session.PeerDisplayName ?? string.Empty,
                    MessageId = messageId
                };

                session.Messages.Add(new LanChatMessage
                {
                    MessageId = messageId,
                    FromSteamId = peerSteamId,
                    // Short history note only — Accept/Decline + wording live on the form panel.
                    Text = "sent an invitation to join " + displayGame + ".",
                    TimestampUtc = utc,
                    IsLocal = false,
                    Kind = LanChatMessageKind.GameInvite
                });
                TrimMessagesUnlocked(session);
            }

            RaiseSessionUpdated(peerSteamId);
        }

        // Removes pending payload and returns a copy for GameLaunchService. Never parses chat Text.
        public bool TryTakePendingGameInvite(string peerSteamId, out LanGameInviteEventArgs invite, out string error)
        {
            invite = null;
            error = null;
            peerSteamId = LanWireHelper.SanitizeSteamId(peerSteamId);
            if (string.IsNullOrEmpty(peerSteamId))
            {
                error = "Unknown player.";
                return false;
            }

            lock (_gate)
            {
                PeerSession session;
                if (!_sessions.TryGetValue(peerSteamId, out session)
                    || session.PendingGameInvite == null
                    || session.PendingGameInvite.AppId == 0)
                {
                    error = "No pending lobby invite.";
                    return false;
                }

                PendingGameInvite pending = session.PendingGameInvite;
                session.PendingGameInvite = null;
                invite = new LanGameInviteEventArgs
                {
                    FromSteamId = pending.FromSteamId,
                    FromDisplayName = pending.FromDisplayName ?? string.Empty,
                    AppId = pending.AppId,
                    GameName = pending.GameName ?? string.Empty,
                    JoinArguments = pending.JoinArguments ?? string.Empty
                };
            }

            RaiseSessionUpdated(peerSteamId);
            return true;
        }

        // After a successful Accept launch (or confirmed relaunch). Not used when relaunch is cancelled.
        public void RecordGameInviteAccepted(string peerSteamId)
        {
            peerSteamId = LanWireHelper.SanitizeSteamId(peerSteamId);
            if (string.IsNullOrEmpty(peerSteamId))
                return;

            string localId = GetLocalSteamId() ?? string.Empty;
            lock (_gate)
            {
                PeerSession session;
                if (!_sessions.TryGetValue(peerSteamId, out session))
                    return;

                session.Messages.Add(new LanChatMessage
                {
                    MessageId = NewMessageId(),
                    FromSteamId = localId,
                    Text = "Accepted lobby invite.",
                    TimestampUtc = DateTime.UtcNow,
                    IsLocal = true,
                    Kind = LanChatMessageKind.GameInviteResult
                });
                TrimMessagesUnlocked(session);
            }

            RaiseSessionUpdated(peerSteamId);
        }

        // Put Accept payload back when relaunch was declined or launch failed before start.
        public void RestorePendingGameInvite(LanGameInviteEventArgs invite)
        {
            if (invite == null || invite.AppId == 0)
                return;

            string peerSteamId = LanWireHelper.SanitizeSteamId(invite.FromSteamId);
            if (string.IsNullOrEmpty(peerSteamId))
                return;

            lock (_gate)
            {
                if (_blockedIds.Contains(peerSteamId))
                    return;

                PeerSession session = GetOrCreateSessionUnlocked(peerSteamId);
                session.PendingGameInvite = new PendingGameInvite
                {
                    AppId = invite.AppId,
                    GameName = SanitizeInviteGameName(invite.GameName),
                    JoinArguments = SanitizeJoinArguments(invite.JoinArguments),
                    FromSteamId = peerSteamId,
                    FromDisplayName = string.IsNullOrWhiteSpace(invite.FromDisplayName)
                        ? (session.PeerDisplayName ?? string.Empty)
                        : SanitizeChatText(invite.FromDisplayName),
                    MessageId = NewMessageId()
                };
            }

            RaiseSessionUpdated(peerSteamId);
        }

        public bool TryDeclineGameInvite(string peerSteamId, out string error)
        {
            error = null;
            peerSteamId = LanWireHelper.SanitizeSteamId(peerSteamId);
            if (string.IsNullOrEmpty(peerSteamId))
            {
                error = "Unknown player.";
                return false;
            }

            string localId = GetLocalSteamId() ?? string.Empty;
            lock (_gate)
            {
                PeerSession session;
                if (!_sessions.TryGetValue(peerSteamId, out session)
                    || session.PendingGameInvite == null)
                {
                    error = "No pending lobby invite.";
                    return false;
                }

                session.PendingGameInvite = null;
                session.Messages.Add(new LanChatMessage
                {
                    MessageId = NewMessageId(),
                    FromSteamId = localId,
                    Text = "Declined lobby invite.",
                    TimestampUtc = DateTime.UtcNow,
                    IsLocal = true,
                    Kind = LanChatMessageKind.GameInviteResult
                });
                TrimMessagesUnlocked(session);
            }

            RaiseSessionUpdated(peerSteamId);
            return true;
        }

        public bool TryAccept(string peerSteamId, out string error)
        {
            return TryDecide(peerSteamId, LanChatConstants.DecisionAccept, persistBlock: false, out error);
        }

        public bool TryIgnore(string peerSteamId, out string error)
        {
            return TryDecide(peerSteamId, LanChatConstants.DecisionIgnore, persistBlock: false, out error);
        }

        public bool TryIgnoreUser(string peerSteamId, out string error)
        {
            return TryDecide(peerSteamId, LanChatConstants.DecisionBlock, persistBlock: true, out error);
        }

        public ValidationResult SetChatBlocked(string peerSteamId, bool blocked)
        {
            peerSteamId = LanWireHelper.SanitizeSteamId(peerSteamId);
            if (string.IsNullOrEmpty(peerSteamId))
                return ValidationResult.Failure("Steam ID is required.");

            ValidationResult save = _appData.SetLanChatBlockedSteamId(peerSteamId, blocked);
            if (!save.IsValid)
                return save;

            lock (_gate)
            {
                if (blocked)
                {
                    _blockedIds.Add(peerSteamId);
                    _trustedIds.Remove(peerSteamId);
                    PeerSession session;
                    if (_sessions.TryGetValue(peerSteamId, out session))
                    {
                        session.Status = LanChatSessionStatus.Blocked;
                        session.PendingInviteMessageId = null;
                        session.PendingGameInvite = null;
                        session.StatusDetail = "Chat from this player is blocked.";
                        session.Messages.Clear();
                    }
                }
                else
                {
                    _blockedIds.Remove(peerSteamId);
                    PeerSession session;
                    if (_sessions.TryGetValue(peerSteamId, out session))
                    {
                        session.Status = LanChatSessionStatus.None;
                        session.StatusDetail = string.Empty;
                    }
                }
            }

            RaiseSessionUpdated(peerSteamId);
            return ValidationResult.Success();
        }

        private bool TryDecide(string peerSteamId, string decision, bool persistBlock, out string error)
        {
            error = null;
            peerSteamId = LanWireHelper.SanitizeSteamId(peerSteamId);
            if (string.IsNullOrEmpty(peerSteamId))
            {
                error = "Unknown player.";
                return false;
            }

            string localId = GetLocalSteamId();
            string inviteId;
            byte[] payload;
            LanChatSessionStatus previousStatus;
            string previousPendingInviteMessageId;
            string previousStatusDetail;
            DateTime previousSoftIgnoreUntilUtc;
            List<LanChatMessage> previousMessages;
            bool wasBlocked;
            bool wasTrusted;

            lock (_gate)
            {
                PeerSession session;
                if (!_sessions.TryGetValue(peerSteamId, out session)
                    || session.Status != LanChatSessionStatus.IncomingPending)
                {
                    error = "No pending chat request.";
                    return false;
                }

                inviteId = session.PendingInviteMessageId ?? string.Empty;

                payload = BuildPacket(
                    LanChatConstants.MessageDecision,
                    localId,
                    peerSteamId,
                    inviteId,
                    null,
                    DateTime.UtcNow,
                    decision);

                previousStatus = session.Status;
                previousPendingInviteMessageId = session.PendingInviteMessageId;
                previousStatusDetail = session.StatusDetail;
                previousSoftIgnoreUntilUtc = session.SoftIgnoreUntilUtc;
                previousMessages = new List<LanChatMessage>(session.Messages);
                wasBlocked = _blockedIds.Contains(peerSteamId);
                wasTrusted = _trustedIds.Contains(peerSteamId);

                if (string.Equals(decision, LanChatConstants.DecisionAccept, StringComparison.Ordinal))
                {
                    session.Status = LanChatSessionStatus.Accepted;
                    session.PendingInviteMessageId = null;
                    session.StatusDetail = string.Empty;
                    _trustedIds.Add(peerSteamId);
                }
                else if (string.Equals(decision, LanChatConstants.DecisionIgnore, StringComparison.Ordinal))
                {
                    session.Status = LanChatSessionStatus.None;
                    session.PendingInviteMessageId = null;
                    session.SoftIgnoreUntilUtc = DateTime.UtcNow.AddMilliseconds(LanChatConstants.SoftIgnoreCooldownMs);
                    session.StatusDetail = "Ignored. They can try again later.";
                    session.Messages.Clear();
                }
                else
                {
                    session.Status = LanChatSessionStatus.Blocked;
                    session.PendingInviteMessageId = null;
                    session.StatusDetail = "Chat from this player is blocked.";
                    session.Messages.Clear();
                    _blockedIds.Add(peerSteamId);
                    _trustedIds.Remove(peerSteamId);
                }
            }

            string persistenceError = null;
            if (string.Equals(decision, LanChatConstants.DecisionAccept, StringComparison.Ordinal))
            {
                ValidationResult trustSave = _appData.SetLanChatTrustedSteamId(peerSteamId, true);
                if (!trustSave.IsValid)
                    persistenceError = trustSave.ErrorMessage ?? "Could not save trusted chat peer.";
            }

            if (persistenceError == null && persistBlock)
            {
                ValidationResult save = _appData.SetLanChatBlockedSteamId(peerSteamId, true);
                if (!save.IsValid)
                    persistenceError = save.ErrorMessage ?? "Could not save block list.";
            }

            if (persistenceError != null)
            {
                lock (_gate)
                {
                    PeerSession session;
                    if (_sessions.TryGetValue(peerSteamId, out session))
                    {
                        session.Status = previousStatus;
                        session.PendingInviteMessageId = previousPendingInviteMessageId;
                        session.StatusDetail = previousStatusDetail;
                        session.SoftIgnoreUntilUtc = previousSoftIgnoreUntilUtc;
                        session.Messages.Clear();
                        for (int i = 0; i < previousMessages.Count; i++)
                            session.Messages.Add(previousMessages[i]);
                    }

                    if (wasBlocked)
                        _blockedIds.Add(peerSteamId);
                    else
                        _blockedIds.Remove(peerSteamId);

                    if (wasTrusted)
                        _trustedIds.Add(peerSteamId);
                    else
                        _trustedIds.Remove(peerSteamId);
                }

                error = persistenceError;
                RaiseSessionUpdated(peerSteamId);
                return false;
            }

            if (payload != null)
                _presence.TrySendBroadcast(payload);

            RaiseSessionUpdated(peerSteamId);
            return true;
        }

        private void OnChatPacketReceived(object sender, LanChatPacketEventArgs e)
        {
            if (e == null || e.Root == null || e.Remote == null)
                return;

            try
            {
                HandleIncoming(e.Root, e.Remote);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug("LAN chat handle failed: " + ex.Message);
            }
        }

        private void HandleIncoming(JsonObject root, IPEndPoint remote)
        {
            int version = root["v"] != null ? root["v"].ToInt32() : 0;
            if (version != LanPresenceConstants.ProtocolVersion)
                return;

            string type = root["t"] != null ? root["t"].ToString() : null;
            if (!LanChatConstants.IsChatType(type))
                return;

            string fromId = LanWireHelper.SanitizeSteamId(root["sid"] != null ? root["sid"].ToString() : null);
            string toId = LanWireHelper.SanitizeSteamId(root["to"] != null ? root["to"].ToString() : null);
            string localId = GetLocalSteamId();
            if (string.IsNullOrEmpty(fromId) || string.IsNullOrEmpty(localId))
                return;
            if (string.Equals(fromId, localId, StringComparison.Ordinal))
                return;
            if (!string.IsNullOrEmpty(toId) && !string.Equals(toId, localId, StringComparison.Ordinal))
                return;
            if (!_presence.IsPresentPeerFromEndpoint(fromId, remote))
                return;

            string messageId = SanitizeMessageId(root["mid"] != null ? root["mid"].ToString() : null);
            if (string.IsNullOrEmpty(messageId))
                return;

            // Busy: drop all inbound chat; decline invites so the sender is not left pending.
            if (_presence.IsLocalBusy)
            {
                if (string.Equals(type, LanChatConstants.MessageRequest, StringComparison.Ordinal))
                {
                    byte[] decline = BuildPacket(
                        LanChatConstants.MessageDecision,
                        localId,
                        fromId,
                        messageId,
                        null,
                        DateTime.UtcNow,
                        LanChatConstants.DecisionIgnore);
                    if (decline != null)
                        _presence.TrySendBroadcast(decline);
                }

                return;
            }

            if (string.Equals(type, LanChatConstants.MessageRequest, StringComparison.Ordinal))
                HandleIncomingRequest(fromId, messageId, root);
            else if (string.Equals(type, LanChatConstants.MessageDecision, StringComparison.Ordinal))
                HandleIncomingDecision(fromId, messageId, root);
            else if (string.Equals(type, LanChatConstants.MessageText, StringComparison.Ordinal))
                HandleIncomingText(fromId, messageId, root);
        }

        private void HandleIncomingRequest(string fromId, string messageId, JsonObject root)
        {
            string text = SanitizeChatText(root["txt"] != null ? root["txt"].ToString() : null);
            if (string.IsNullOrEmpty(text))
                return;

            DateTime utc = ParseTimestamp(root["ts"]);
            bool raise = false;
            bool sendMutualAccept = false;

            lock (_gate)
            {
                if (_blockedIds.Contains(fromId))
                    return;

                if (!LanWireHelper.TryConsumeRate(
                    _inviteTimestamps,
                    fromId,
                    LanChatConstants.InviteMaxPerPeer,
                    LanChatConstants.InviteWindowMs))
                    return;

                PeerSession session = GetOrCreateSessionUnlocked(fromId);
                if (session.SeenMessageIds.Contains(messageId))
                    return;

                bool trusted = _trustedIds.Contains(fromId);
                bool wasOutgoingPending = session.Status == LanChatSessionStatus.OutgoingPending;
                bool needsAcceptReply = wasOutgoingPending
                    || (trusted && session.Status != LanChatSessionStatus.Accepted);

                if (session.Status == LanChatSessionStatus.Accepted || trusted || wasOutgoingPending)
                {
                    // Already chatting, trusted, or mutual invite — treat as an accepted message.
                    AppendRemoteMessageUnlocked(session, messageId, fromId, text, utc);
                    session.Status = LanChatSessionStatus.Accepted;
                    session.PendingInviteMessageId = null;
                    session.StatusDetail = string.Empty;
                    if (wasOutgoingPending)
                        _trustedIds.Add(fromId);
                    sendMutualAccept = needsAcceptReply;
                    raise = true;
                }
                else if (session.Status == LanChatSessionStatus.IncomingPending)
                {
                    // Keep the first pending invite; drop duplicates/spam.
                    return;
                }
                else
                {
                    if (session.SoftIgnoreUntilUtc > DateTime.UtcNow)
                        return;

                    RememberMessageIdUnlocked(session, messageId);
                    session.Messages.Clear();
                    session.Messages.Add(new LanChatMessage
                    {
                        MessageId = messageId,
                        FromSteamId = fromId,
                        Text = text,
                        TimestampUtc = utc,
                        IsLocal = false
                    });
                    session.Status = LanChatSessionStatus.IncomingPending;
                    session.PendingInviteMessageId = messageId;
                    session.StatusDetail =
                        "LAN identity is not verified. Accept only if you trust this player.";
                    raise = true;
                }
            }

            if (sendMutualAccept)
            {
                PersistTrust(fromId);
                byte[] acceptPayload = BuildPacket(
                    LanChatConstants.MessageDecision,
                    GetLocalSteamId(),
                    fromId,
                    messageId,
                    null,
                    DateTime.UtcNow,
                    LanChatConstants.DecisionAccept);
                if (acceptPayload != null)
                    _presence.TrySendBroadcast(acceptPayload);
            }

            if (raise)
                RaiseSessionUpdated(fromId);
        }

        private void HandleIncomingDecision(string fromId, string messageId, JsonObject root)
        {
            string decision = root["d"] != null ? root["d"].ToString() : null;
            if (string.IsNullOrWhiteSpace(decision))
                return;
            decision = decision.Trim();

            lock (_gate)
            {
                PeerSession session;
                if (!_sessions.TryGetValue(fromId, out session))
                    return;
                if (session.Status != LanChatSessionStatus.OutgoingPending)
                    return;
                if (!string.IsNullOrEmpty(session.PendingInviteMessageId)
                    && !string.Equals(session.PendingInviteMessageId, messageId, StringComparison.Ordinal)
                    && !string.IsNullOrEmpty(messageId))
                {
                    // Still accept if mid empty/mismatch on older clients — only when pending.
                }

                if (string.Equals(decision, LanChatConstants.DecisionAccept, StringComparison.Ordinal))
                {
                    session.Status = LanChatSessionStatus.Accepted;
                    session.PendingInviteMessageId = null;
                    session.StatusDetail = string.Empty;
                    _trustedIds.Add(fromId);
                }
                else if (string.Equals(decision, LanChatConstants.DecisionBlock, StringComparison.Ordinal))
                {
                    session.Status = LanChatSessionStatus.None;
                    session.PendingInviteMessageId = null;
                    session.StatusDetail = "They blocked chat.";
                }
                else
                {
                    session.Status = LanChatSessionStatus.None;
                    session.PendingInviteMessageId = null;
                    session.StatusDetail = "They ignored the request.";
                }
            }

            if (string.Equals(decision, LanChatConstants.DecisionAccept, StringComparison.Ordinal))
                PersistTrust(fromId);

            RaiseSessionUpdated(fromId);
        }

        private void HandleIncomingText(string fromId, string messageId, JsonObject root)
        {
            string text = SanitizeChatText(root["txt"] != null ? root["txt"].ToString() : null);
            if (string.IsNullOrEmpty(text))
                return;

            DateTime utc = ParseTimestamp(root["ts"]);
            bool raise = false;
            bool persistTrust = false;

            lock (_gate)
            {
                if (_blockedIds.Contains(fromId))
                    return;

                if (!LanWireHelper.TryConsumeRate(
                    _messageTimestamps,
                    fromId,
                    LanChatConstants.MessageMaxPerPeer,
                    LanChatConstants.MessageWindowMs))
                    return;

                PeerSession session = GetOrCreateSessionUnlocked(fromId);
                bool trusted = _trustedIds.Contains(fromId);
                // OutgoingPending + inbound text = they already accepted (or were trusted); finish handshake.
                if (session.Status != LanChatSessionStatus.Accepted
                    && session.Status != LanChatSessionStatus.OutgoingPending
                    && !trusted)
                    return;

                if (session.Status == LanChatSessionStatus.OutgoingPending || !trusted)
                    persistTrust = true;

                session.Status = LanChatSessionStatus.Accepted;
                session.PendingInviteMessageId = null;
                session.StatusDetail = string.Empty;
                _trustedIds.Add(fromId);

                if (session.SeenMessageIds.Contains(messageId))
                    return;

                AppendRemoteMessageUnlocked(session, messageId, fromId, text, utc);
                raise = true;
            }

            if (persistTrust)
                PersistTrust(fromId);

            if (raise)
                RaiseSessionUpdated(fromId);
        }

        private void PersistTrust(string peerSteamId)
        {
            peerSteamId = LanWireHelper.SanitizeSteamId(peerSteamId);
            if (string.IsNullOrEmpty(peerSteamId))
                return;

            lock (_gate)
                _trustedIds.Add(peerSteamId);

            try
            {
                _appData.SetLanChatTrustedSteamId(peerSteamId, true);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug("LAN chat trust persist failed: " + ex.Message);
            }
        }

        private PeerSession GetOrCreateSessionUnlocked(string peerSteamId)
        {
            PeerSession session;
            if (!_sessions.TryGetValue(peerSteamId, out session))
            {
                session = new PeerSession
                {
                    PeerSteamId = peerSteamId,
                    PeerDisplayName = ResolveDisplayName(peerSteamId),
                    Status = _trustedIds.Contains(peerSteamId)
                        ? LanChatSessionStatus.Accepted
                        : LanChatSessionStatus.None
                };
                _sessions[peerSteamId] = session;
            }
            else
            {
                string name = ResolveDisplayName(peerSteamId);
                if (!string.IsNullOrEmpty(name))
                    session.PeerDisplayName = name;
                if (_trustedIds.Contains(peerSteamId)
                    && session.Status == LanChatSessionStatus.None)
                {
                    session.Status = LanChatSessionStatus.Accepted;
                    session.StatusDetail = string.Empty;
                }
            }

            return session;
        }

        private void RaiseSessionUpdated(string peerSteamId)
        {
            LanChatSessionSnapshot snapshot = GetSessionSnapshot(peerSteamId);
            EventHandler<LanChatSessionEventArgs> handler = SessionUpdated;
            if (handler == null)
                return;
            try
            {
                handler(this, new LanChatSessionEventArgs(snapshot));
            }
            catch (Exception ex)
            {
                _logger?.LogDebug("LAN chat SessionUpdated handler error: " + ex.Message);
            }
        }

        private string GetLocalSteamId()
        {
            try
            {
                LanPresencePeer self = _presence.GetLocalSelfSnapshot();
                return LanWireHelper.SanitizeSteamId(self != null ? self.SteamId : null);
            }
            catch
            {
                return string.Empty;
            }
        }

        private string ResolveDisplayName(string steamId)
        {
            LanPresencePeer peer;
            if (_presence.TryGetPeer(steamId, out peer)
                && peer != null
                && !string.IsNullOrWhiteSpace(peer.DisplayName))
            {
                return peer.DisplayName.Trim();
            }

            return steamId ?? string.Empty;
        }

        private LanChatSessionSnapshot CreateBlockedSnapshot(string peerSteamId)
        {
            return new LanChatSessionSnapshot
            {
                PeerSteamId = peerSteamId,
                PeerDisplayName = ResolveDisplayName(peerSteamId),
                Status = LanChatSessionStatus.Blocked,
                Messages = new List<LanChatMessage>(),
                StatusDetail = "Chat from this player is blocked."
            };
        }

        private LanChatSessionSnapshot CreateTrustedShellSnapshot(string peerSteamId)
        {
            return new LanChatSessionSnapshot
            {
                PeerSteamId = peerSteamId,
                PeerDisplayName = ResolveDisplayName(peerSteamId),
                Status = LanChatSessionStatus.Accepted,
                Messages = new List<LanChatMessage>(),
                StatusDetail = string.Empty
            };
        }

        private static LanChatSessionSnapshot CreateEmptySnapshot(string peerSteamId)
        {
            return new LanChatSessionSnapshot
            {
                PeerSteamId = peerSteamId ?? string.Empty,
                PeerDisplayName = peerSteamId ?? string.Empty,
                Status = LanChatSessionStatus.None,
                Messages = new List<LanChatMessage>(),
                StatusDetail = string.Empty
            };
        }

        private static void AppendRemoteMessageUnlocked(
            PeerSession session,
            string messageId,
            string fromId,
            string text,
            DateTime utc)
        {
            RememberMessageIdUnlocked(session, messageId);
            session.Messages.Add(new LanChatMessage
            {
                MessageId = messageId,
                FromSteamId = fromId,
                Text = text,
                TimestampUtc = utc,
                IsLocal = false,
                Kind = LanChatMessageKind.Text
            });
            TrimMessagesUnlocked(session);
        }

        private LanChatSessionSnapshot CloneSnapshot(PeerSession session)
        {
            var messages = new List<LanChatMessage>(session.Messages.Count);
            for (int i = 0; i < session.Messages.Count; i++)
            {
                LanChatMessage m = session.Messages[i];
                messages.Add(new LanChatMessage
                {
                    MessageId = m.MessageId,
                    FromSteamId = m.FromSteamId,
                    Text = m.Text,
                    TimestampUtc = m.TimestampUtc,
                    IsLocal = m.IsLocal,
                    Kind = m.Kind
                });
            }

            LanPendingGameInviteSnapshot pendingGame = null;
            if (session.PendingGameInvite != null && session.PendingGameInvite.AppId != 0)
            {
                pendingGame = new LanPendingGameInviteSnapshot
                {
                    AppId = session.PendingGameInvite.AppId,
                    GameName = session.PendingGameInvite.GameName ?? string.Empty,
                    HasJoinArguments = !string.IsNullOrEmpty(session.PendingGameInvite.JoinArguments)
                };
            }

            return new LanChatSessionSnapshot
            {
                PeerSteamId = session.PeerSteamId,
                PeerDisplayName = session.PeerDisplayName,
                Status = session.Status,
                PendingInviteMessageId = session.PendingInviteMessageId,
                StatusDetail = session.StatusDetail ?? string.Empty,
                PendingGameInvite = pendingGame,
                Messages = messages
            };
        }

        private static bool HasRecentLocalInviteNoteUnlocked(
            PeerSession session,
            LanChatMessageKind kind,
            int windowMs,
            DateTime utc)
        {
            if (session == null || session.Messages == null || windowMs <= 0)
                return false;

            for (int i = session.Messages.Count - 1; i >= 0; i--)
            {
                LanChatMessage m = session.Messages[i];
                if (m == null || !m.IsLocal || m.Kind != kind)
                    continue;
                return (utc - m.TimestampUtc).TotalMilliseconds < windowMs;
            }

            return false;
        }

        private static string FormatInviteGameName(ulong appId, string gameName)
        {
            string cleaned = SanitizeInviteGameName(gameName);
            if (!string.IsNullOrEmpty(cleaned))
                return cleaned;
            return "App " + appId;
        }

        private static string SanitizeInviteGameName(string value)
        {
            string cleaned = SanitizeChatText(value);
            if (cleaned.Length > LanPresenceConstants.MaxGameNameChars)
                cleaned = cleaned.Substring(0, LanPresenceConstants.MaxGameNameChars);
            return cleaned;
        }

        // Re-sanitize join args stored for Accept; never show raw value in chat UI.
        private static string SanitizeJoinArguments(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var sb = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c < 32 || c == 127)
                    continue;
                sb.Append(c);
            }

            string cleaned = sb.ToString().Trim();
            if (cleaned.Length > LanPresenceConstants.MaxJoinArgumentsChars)
                cleaned = cleaned.Substring(0, LanPresenceConstants.MaxJoinArgumentsChars);
            return cleaned;
        }

        private static byte[] BuildPacket(
            string type,
            string fromId,
            string toId,
            string messageId,
            string text,
            DateTime utc,
            string decision)
        {
            var obj = new JsonObject();
            obj["v"] = new JsonNumber(LanPresenceConstants.ProtocolVersion);
            obj["t"] = new JsonString(type);
            obj["sid"] = new JsonString(fromId ?? string.Empty);
            obj["to"] = new JsonString(toId ?? string.Empty);
            obj["mid"] = new JsonString(messageId ?? string.Empty);
            obj["ts"] = new JsonNumber(new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeSeconds());
            if (!string.IsNullOrEmpty(text))
                obj["txt"] = new JsonString(text);
            if (!string.IsNullOrEmpty(decision))
                obj["d"] = new JsonString(decision);

            string json = obj.ToString();
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            if (bytes.Length > LanPresenceConstants.MaxPacketBytes)
                return null;
            return bytes;
        }

        private static DateTime ParseTimestamp(JsonValue value)
        {
            if (value == null)
                return DateTime.UtcNow;
            long unix = value.ToInt64();
            if (unix <= 0)
                return DateTime.UtcNow;
            try
            {
                return DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;
            }
            catch
            {
                return DateTime.UtcNow;
            }
        }

        private static string NewMessageId()
        {
            return Guid.NewGuid().ToString("N");
        }

        private static void RememberMessageIdUnlocked(PeerSession session, string messageId)
        {
            session.SeenMessageIds.Add(messageId);
            session.SeenMessageIdOrder.Enqueue(messageId);
            while (session.SeenMessageIdOrder.Count > LanChatConstants.MaxRecentMessageIds)
            {
                string old = session.SeenMessageIdOrder.Dequeue();
                session.SeenMessageIds.Remove(old);
            }
        }

        private static void TrimMessagesUnlocked(PeerSession session)
        {
            const int maxKeep = 100;
            while (session.Messages.Count > maxKeep)
                session.Messages.RemoveAt(0);
        }

        private static string SanitizeMessageId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            value = value.Trim();
            if (value.Length > LanChatConstants.MaxMessageIdChars)
                return string.Empty;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                bool ok = (c >= '0' && c <= '9')
                    || (c >= 'a' && c <= 'z')
                    || (c >= 'A' && c <= 'Z');
                if (!ok)
                    return string.Empty;
            }

            return value;
        }

        // Plain text only: keep tab/newline for pasted configs; strip other controls.
        public static string SanitizeChatText(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var sb = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '\n' || c == '\r' || c == '\t')
                {
                    if (c == '\r')
                        continue;
                    sb.Append(c);
                    continue;
                }

                if (c < 32 || c == 127)
                    continue;
                sb.Append(c);
            }

            string cleaned = sb.ToString().Trim();
            if (cleaned.Length > LanChatConstants.MaxMessageChars)
                cleaned = cleaned.Substring(0, LanChatConstants.MaxMessageChars);
            return cleaned;
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                    return;
                _disposed = true;
                if (_hooked)
                {
                    try
                    {
                        _presence.ChatPacketReceived -= OnChatPacketReceived;
                    }
                    catch
                    {
                    }

                    _hooked = false;
                }

                _sessions.Clear();
            }
        }

        private sealed class PeerSession
        {
            public string PeerSteamId;
            public string PeerDisplayName;
            public LanChatSessionStatus Status;
            public string PendingInviteMessageId;
            public string StatusDetail = string.Empty;
            public DateTime SoftIgnoreUntilUtc = DateTime.MinValue;
            // Lobby join payload for Accept — not derived from message Text.
            public PendingGameInvite PendingGameInvite;
            public readonly List<LanChatMessage> Messages = new List<LanChatMessage>();
            public readonly HashSet<string> SeenMessageIds = new HashSet<string>(StringComparer.Ordinal);
            public readonly Queue<string> SeenMessageIdOrder = new Queue<string>();
        }

        private sealed class PendingGameInvite
        {
            public ulong AppId;
            public string GameName;
            public string JoinArguments;
            public string FromSteamId;
            public string FromDisplayName;
            public string MessageId;
        }
    }
}
