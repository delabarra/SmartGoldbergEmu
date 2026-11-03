using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.JsonKit;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Services
{
    // LAN Players presence: SGE-owned UDP heartbeats (not Goldberg listen_port).
    public sealed class LanPresenceService : IDisposable
    {
        private readonly object _gate = new object();
        private readonly ILogService _logger;
        private readonly Dictionary<string, LanPresencePeer> _peers =
            new Dictionary<string, LanPresencePeer>(StringComparer.Ordinal);
        // Key = steamId|hash → UTC ticks when last requested (retry after AvatarRequestRetryMs).
        private readonly Dictionary<string, long> _avatarRequestCooldown =
            new Dictionary<string, long>(StringComparer.Ordinal);
        // Sliding-window timestamps for avatar accept (by Steam64) and reply (by remote IP).
        private readonly Dictionary<string, List<long>> _avatarAcceptTimestamps =
            new Dictionary<string, List<long>>(StringComparer.Ordinal);
        private readonly Dictionary<string, List<long>> _avatarReplyTimestamps =
            new Dictionary<string, List<long>>(StringComparer.Ordinal);

        private UdpClient _udp;
        private Timer _heartbeatTimer;
        private Timer _expireTimer;
        private byte[] _avatarJpeg;
        private string _avatarHash = string.Empty;
        private string _localSteamId = string.Empty;
        private string _localName = string.Empty;
        // Preferred persona from settings (Online / Away / Busy / Hidden). Away/Busy here are manual and sticky.
        private LanPersonaState _localPersonaState = LanPersonaState.Online;
        // Runtime overlay: only when preferred is Online and the machine is idle.
        private bool _autoAwayActive;
        private string _localCustomStatus = string.Empty;
        private ulong _localLookingForPlayAppId;
        private string _localLookingForPlayGameName = string.Empty;
        private ulong _lastAdvertisedAppId;
        private string _lastAdvertisedGameName = string.Empty;
        private bool _online;
        private bool _disposed;
        private int _receiveLoopRunning;

        public event EventHandler PeersChanged;
        public event EventHandler OnlineStateChanged;

        // Chat types (cq/cd/cm) multiplexed on this socket; LanChatService handles the payload.
        public event EventHandler<LanChatPacketEventArgs> ChatPacketReceived;

        public LanPresenceService()
            : this(ServiceLocator.LogService)
        {
        }

        public LanPresenceService(ILogService logger)
        {
            _logger = logger;
        }

        public bool IsOnline
        {
            get
            {
                lock (_gate)
                    return _online;
            }
        }

        public IReadOnlyList<LanPresencePeer> GetPeersSnapshot()
        {
            lock (_gate)
            {
                var list = new List<LanPresencePeer>(_peers.Count);
                foreach (LanPresencePeer peer in _peers.Values)
                {
                    list.Add(ClonePeer(peer));
                }

                list.Sort(ComparePeers);
                return list;
            }
        }

        // Local persona for the LAN list header (Steam-style self row).
        public LanPresencePeer GetLocalSelfSnapshot()
        {
            lock (_gate)
            {
                RefreshLocalIdentityUnlocked();
                EnsureAvatarUnlocked();

                ulong appId;
                string gameName;
                TryGetPlayingGame(out appId, out gameName);

                string avatarPath = null;
                if (!string.IsNullOrEmpty(_avatarHash))
                {
                    string cached = ResolveCachedAvatarPath(_localSteamId, _avatarHash);
                    if (!string.IsNullOrEmpty(cached) && File.Exists(cached))
                        avatarPath = cached;
                }

                if (string.IsNullOrEmpty(avatarPath) && File.Exists(PathConstants.GlobalAccountAvatarPath))
                    avatarPath = PathConstants.GlobalAccountAvatarPath;

                return new LanPresencePeer
                {
                    SteamId = _localSteamId ?? string.Empty,
                    DisplayName = _localName ?? string.Empty,
                    AppId = _online ? appId : 0,
                    GameName = _online ? (gameName ?? string.Empty) : string.Empty,
                    AvatarHash = _avatarHash ?? string.Empty,
                    AvatarFilePath = avatarPath,
                    LastSeenUtc = DateTime.UtcNow,
                    IsPresent = _online,
                    PersonaState = _online ? GetEffectivePersonaStateUnlocked() : LanPersonaState.Online,
                    CustomStatus = _online ? (_localCustomStatus ?? string.Empty) : string.Empty,
                    LookingForPlayAppId = _online ? _localLookingForPlayAppId : 0,
                    LookingForPlayGameName = _online ? (_localLookingForPlayGameName ?? string.Empty) : string.Empty
                };
            }
        }

        // Reload persona / custom status from settings.ini (after Settings save or status pick).
        public void ApplyLocalPersonaFromSettings()
        {
            lock (_gate)
            {
                if (_disposed)
                    return;
                RefreshLocalPersonaUnlocked();
                if (_online)
                    UpdateAutoAwayUnlocked();
            }

            RaisePeersChanged();
        }

        // Session-only; not written to settings.ini. appId 0 clears.
        public void SetLookingForPlay(ulong appId)
        {
            lock (_gate)
            {
                if (_disposed)
                    return;

                if (appId == 0)
                {
                    _localLookingForPlayAppId = 0;
                    _localLookingForPlayGameName = string.Empty;
                }
                else
                {
                    _localLookingForPlayAppId = appId;
                    _localLookingForPlayGameName = string.Empty;
                    try
                    {
                        GameConfig game = ServiceLocator.GameDataService.GetGameByAppId(appId);
                        if (game != null && !string.IsNullOrWhiteSpace(game.AppName))
                            _localLookingForPlayGameName = game.AppName.Trim();
                        else
                            _localLookingForPlayGameName = "App " + appId;
                    }
                    catch
                    {
                        _localLookingForPlayGameName = "App " + appId;
                    }
                }
            }

            RaisePeersChanged();
        }

        public ulong LookingForPlayAppId
        {
            get
            {
                lock (_gate)
                    return _localLookingForPlayAppId;
            }
        }

        public void StartIfEnabled()
        {
            bool enabled = true;
            try
            {
                enabled = ServiceLocator.AppDataService.LoadApplicationSettings().LanPlayersOnline;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning("LAN presence: could not load online preference: " + ex.Message);
            }

            SetOnline(enabled);
        }

        public void SetOnline(bool online)
        {
            lock (_gate)
            {
                if (_disposed)
                    return;

                if (online)
                {
                    if (_online && _udp != null)
                        return;
                    try
                    {
                        BeginOnlineUnlocked();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning("LAN presence: failed to go online: " + ex.Message);
                        StopOnlineUnlocked();
                        return;
                    }
                }
                else
                {
                    if (!_online && _udp == null)
                        return;
                    StopOnlineUnlocked();
                }
            }

            RaiseOnlineStateChanged();
            RaisePeersChanged();
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                    return;
                _disposed = true;
                StopOnlineUnlocked();
            }
        }

        private void BeginOnlineUnlocked()
        {
            RefreshLocalIdentityUnlocked();
            RefreshLocalPersonaUnlocked();
            EnsureAvatarUnlocked();

            var udp = new UdpClient();
            udp.EnableBroadcast = true;
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, LanPresenceConstants.UdpPort));

            _udp = udp;
            _online = true;
            _peers.Clear();

            if (Interlocked.Exchange(ref _receiveLoopRunning, 1) == 0)
                ThreadPool.QueueUserWorkItem(ReceiveLoop);

            _heartbeatTimer = new Timer(OnHeartbeat, null, 0, LanPresenceConstants.HeartbeatIntervalMs);
            _expireTimer = new Timer(OnExpirePeers, null, LanPresenceConstants.UiRefreshIntervalMs, LanPresenceConstants.UiRefreshIntervalMs);
            _logger?.LogDebug("LAN presence online on UDP " + LanPresenceConstants.UdpPort);
        }

        private void StopOnlineUnlocked()
        {
            _online = false;
            _autoAwayActive = false;

            Timer heartbeat = _heartbeatTimer;
            _heartbeatTimer = null;
            Timer expire = _expireTimer;
            _expireTimer = null;
            UdpClient udp = _udp;
            _udp = null;

            try
            {
                heartbeat?.Dispose();
            }
            catch
            {
            }

            try
            {
                expire?.Dispose();
            }
            catch
            {
            }

            try
            {
                udp?.Close();
            }
            catch
            {
            }

            try
            {
                udp?.Dispose();
            }
            catch
            {
            }

            _peers.Clear();
            _avatarRequestCooldown.Clear();
            _avatarAcceptTimestamps.Clear();
            _avatarReplyTimestamps.Clear();
            Interlocked.Exchange(ref _receiveLoopRunning, 0);
        }

        private void OnHeartbeat(object state)
        {
            try
            {
                UdpClient udp;
                string payload;
                bool localPresenceChanged = false;
                bool advertise;
                lock (_gate)
                {
                    if (!_online || _udp == null || _disposed)
                        return;

                    RefreshLocalIdentityUnlocked();
                    RefreshLocalPersonaUnlocked();
                    if (UpdateAutoAwayUnlocked())
                        localPresenceChanged = true;
                    EnsureAvatarUnlocked();
                    advertise = _localPersonaState != LanPersonaState.Hidden;
                    payload = advertise ? BuildPresenceJsonUnlocked() : null;
                    udp = _udp;

                    ulong appId;
                    string gameName;
                    TryGetPlayingGame(out appId, out gameName);
                    if (appId != _lastAdvertisedAppId
                        || !string.Equals(_lastAdvertisedGameName ?? string.Empty, gameName ?? string.Empty, StringComparison.Ordinal))
                    {
                        _lastAdvertisedAppId = appId;
                        _lastAdvertisedGameName = gameName ?? string.Empty;
                        localPresenceChanged = true;
                    }
                }

                if (advertise && !string.IsNullOrEmpty(payload))
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(payload);
                    if (bytes.Length <= LanPresenceConstants.MaxPacketBytes)
                        udp.Send(bytes, bytes.Length, new IPEndPoint(IPAddress.Broadcast, LanPresenceConstants.UdpPort));
                }

                if (localPresenceChanged)
                    RaisePeersChanged();
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                _logger?.LogDebug("LAN presence heartbeat failed: " + ex.Message);
            }
        }

        private void OnExpirePeers(object state)
        {
            bool changed = false;
            DateTime cutoff = DateTime.UtcNow.AddMilliseconds(-LanPresenceConstants.PeerExpireMs);
            lock (_gate)
            {
                if (!_online)
                    return;

                foreach (LanPresencePeer peer in _peers.Values)
                {
                    if (peer.IsPresent && peer.LastSeenUtc < cutoff)
                    {
                        peer.IsPresent = false;
                        peer.AppId = 0;
                        peer.GameName = string.Empty;
                        peer.LookingForPlayAppId = 0;
                        peer.LookingForPlayGameName = string.Empty;
                        changed = true;
                    }
                }
            }

            if (changed)
                RaisePeersChanged();
        }

        private void ReceiveLoop(object state)
        {
            while (true)
            {
                UdpClient udp;
                lock (_gate)
                {
                    if (!_online || _udp == null || _disposed)
                    {
                        Interlocked.Exchange(ref _receiveLoopRunning, 0);
                        return;
                    }

                    udp = _udp;
                }

                try
                {
                    IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = udp.Receive(ref remote);
                    if (data == null || data.Length == 0 || data.Length > LanPresenceConstants.MaxPacketBytes)
                        continue;

                    HandlePacket(data, remote);
                }
                catch (ObjectDisposedException)
                {
                    Interlocked.Exchange(ref _receiveLoopRunning, 0);
                    return;
                }
                catch (SocketException)
                {
                    lock (_gate)
                    {
                        if (!_online || _udp == null || _disposed)
                        {
                            Interlocked.Exchange(ref _receiveLoopRunning, 0);
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug("LAN presence receive error: " + ex.Message);
                }
            }
        }

        private void HandlePacket(byte[] data, IPEndPoint remote)
        {
            string json;
            try
            {
                json = Encoding.UTF8.GetString(data);
            }
            catch
            {
                return;
            }

            JsonObject root;
            try
            {
                root = JsonObject.Parse(json);
            }
            catch
            {
                return;
            }

            if (root == null)
                return;

            int version = root["v"] != null ? root["v"].ToInt32() : 0;
            if (version != LanPresenceConstants.ProtocolVersion)
                return;

            string type = root["t"] != null ? root["t"].ToString() : null;
            if (string.IsNullOrEmpty(type))
                return;

            if (string.Equals(type, LanPresenceConstants.MessagePresence, StringComparison.Ordinal))
                HandlePresence(root, remote);
            else if (string.Equals(type, LanPresenceConstants.MessageAvatarRequest, StringComparison.Ordinal))
                HandleAvatarRequest(root, remote);
            else if (string.Equals(type, LanPresenceConstants.MessageAvatarResponse, StringComparison.Ordinal))
                HandleAvatarResponse(root);
            else if (LanChatConstants.IsChatType(type))
                RaiseChatPacket(root, remote);
        }

        private void RaiseChatPacket(JsonObject root, IPEndPoint remote)
        {
            EventHandler<LanChatPacketEventArgs> handler = ChatPacketReceived;
            if (handler == null || root == null || remote == null)
                return;
            try
            {
                handler(this, new LanChatPacketEventArgs(root, remote));
            }
            catch (Exception ex)
            {
                _logger?.LogDebug("LAN chat packet handler error: " + ex.Message);
            }
        }

        // Same path as presence/avatar: unicast is often dropped when the peer firewall only
        // learned the port from inbound broadcasts. Chat packets carry "to" for filtering.
        public bool TrySendBroadcast(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
                return false;
            if (payload.Length > LanPresenceConstants.MaxPacketBytes)
                return false;

            UdpClient udp;
            lock (_gate)
            {
                if (!_online || _udp == null || _disposed)
                    return false;
                udp = _udp;
            }

            try
            {
                udp.Send(payload, payload.Length, new IPEndPoint(IPAddress.Broadcast, LanPresenceConstants.UdpPort));
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogDebug("LAN broadcast send failed: " + ex.Message);
                return false;
            }
        }

        public bool IsPeerPresent(string steamId)
        {
            LanPresencePeer peer;
            return TryGetPeer(steamId, out peer) && peer.IsPresent;
        }

        // Cloned peer by Steam64, present or aged-out.
        public bool TryGetPeer(string steamId, out LanPresencePeer peer)
        {
            peer = null;
            steamId = LanWireHelper.SanitizeSteamId(steamId);
            if (string.IsNullOrEmpty(steamId))
                return false;

            lock (_gate)
            {
                LanPresencePeer stored;
                if (!_peers.TryGetValue(steamId, out stored) || stored == null)
                    return false;
                peer = ClonePeer(stored);
                return true;
            }
        }

        public bool IsPresentPeerFromEndpoint(string steamId, IPEndPoint remote)
        {
            steamId = LanWireHelper.SanitizeSteamId(steamId);
            if (string.IsNullOrEmpty(steamId) || remote == null || remote.Address == null)
                return false;

            lock (_gate)
            {
                LanPresencePeer peer;
                if (!_peers.TryGetValue(steamId, out peer) || peer == null || !peer.IsPresent)
                    return false;
                if (string.IsNullOrWhiteSpace(peer.RemoteAddress))
                    return false;
                return IpAddressesEqual(peer.RemoteAddress.Trim(), remote.Address);
            }
        }

        private static bool IpAddressesEqual(string stored, IPAddress remote)
        {
            if (string.IsNullOrEmpty(stored) || remote == null)
                return false;
            if (string.Equals(stored, remote.ToString(), StringComparison.OrdinalIgnoreCase))
                return true;

            IPAddress storedAddress;
            if (!IPAddress.TryParse(stored, out storedAddress) || storedAddress == null)
                return false;

            // Normalize IPv4-mapped IPv6 (::ffff:a.b.c.d) so presence IP matches chat source.
            if (storedAddress.IsIPv4MappedToIPv6)
                storedAddress = storedAddress.MapToIPv4();
            if (remote.IsIPv4MappedToIPv6)
                remote = remote.MapToIPv4();

            return storedAddress.Equals(remote);
        }

        private void HandlePresence(JsonObject root, IPEndPoint remote)
        {
            string steamId = LanWireHelper.SanitizeSteamId(root["sid"] != null ? root["sid"].ToString() : null);
            if (string.IsNullOrEmpty(steamId))
                return;

            string localId;
            lock (_gate)
            {
                localId = _localSteamId;
            }

            if (string.Equals(steamId, localId, StringComparison.Ordinal))
                return;

            string name = SanitizeName(root["n"] != null ? root["n"].ToString() : null);
            ulong appId = 0;
            if (root["a"] != null)
            {
                long raw = root["a"].ToInt64();
                if (raw > 0)
                    appId = (ulong)raw;
            }

            string gameName = SanitizeGameName(root["g"] != null ? root["g"].ToString() : null);
            string avatarHash = SanitizeAvatarHash(root["h"] != null ? root["h"].ToString() : null);
            LanPersonaState persona = LanPersonaStateHelper.ParseWire(root["st"] != null ? root["st"].ToString() : null);
            string customStatus = SanitizeCustomStatus(root["cs"] != null ? root["cs"].ToString() : null);
            ulong lfgAppId = 0;
            if (root["lfg"] != null)
            {
                long rawLfg = root["lfg"].ToInt64();
                if (rawLfg > 0)
                    lfgAppId = (ulong)rawLfg;
            }

            string lfgName = SanitizeGameName(root["lfgn"] != null ? root["lfgn"].ToString() : null);
            if (lfgAppId == 0)
                lfgName = string.Empty;

            bool changed;
            bool needAvatar;
            string requestTargetHash;
            lock (_gate)
            {
                if (!_online)
                    return;

                LanPresencePeer peer;
                if (!_peers.TryGetValue(steamId, out peer))
                {
                    peer = new LanPresencePeer { SteamId = steamId };
                    _peers[steamId] = peer;
                    changed = true;
                }
                else
                {
                    changed = false;
                }

                if (!peer.IsPresent)
                {
                    peer.IsPresent = true;
                    changed = true;
                }

                if (!string.Equals(peer.DisplayName ?? string.Empty, name ?? string.Empty, StringComparison.Ordinal))
                {
                    peer.DisplayName = name;
                    changed = true;
                }

                if (peer.AppId != appId)
                {
                    peer.AppId = appId;
                    changed = true;
                }

                string effectiveGame = appId == 0 ? string.Empty : (gameName ?? string.Empty);
                if (!string.Equals(peer.GameName ?? string.Empty, effectiveGame, StringComparison.Ordinal))
                {
                    peer.GameName = effectiveGame;
                    changed = true;
                }

                if (peer.PersonaState != persona)
                {
                    peer.PersonaState = persona;
                    changed = true;
                }

                if (!string.Equals(peer.CustomStatus ?? string.Empty, customStatus ?? string.Empty, StringComparison.Ordinal))
                {
                    peer.CustomStatus = customStatus;
                    changed = true;
                }

                if (peer.LookingForPlayAppId != lfgAppId)
                {
                    peer.LookingForPlayAppId = lfgAppId;
                    changed = true;
                }

                string effectiveLfgName = lfgAppId == 0 ? string.Empty : (lfgName ?? string.Empty);
                if (!string.Equals(peer.LookingForPlayGameName ?? string.Empty, effectiveLfgName, StringComparison.Ordinal))
                {
                    peer.LookingForPlayGameName = effectiveLfgName;
                    changed = true;
                }

                if (!string.Equals(peer.AvatarHash ?? string.Empty, avatarHash ?? string.Empty, StringComparison.Ordinal))
                {
                    peer.AvatarHash = avatarHash;
                    peer.AvatarFilePath = null;
                    changed = true;
                }

                peer.LastSeenUtc = DateTime.UtcNow;

                if (remote != null && remote.Address != null)
                {
                    string remoteAddress = remote.Address.ToString();
                    if (!string.Equals(peer.RemoteAddress ?? string.Empty, remoteAddress, StringComparison.OrdinalIgnoreCase))
                    {
                        peer.RemoteAddress = remoteAddress;
                        changed = true;
                    }
                }

                string cachedPath = ResolveCachedAvatarPath(steamId, avatarHash);
                if (!string.IsNullOrEmpty(cachedPath) && File.Exists(cachedPath))
                {
                    if (!string.Equals(peer.AvatarFilePath ?? string.Empty, cachedPath, StringComparison.OrdinalIgnoreCase))
                    {
                        peer.AvatarFilePath = cachedPath;
                        changed = true;
                    }

                    needAvatar = false;
                    requestTargetHash = null;
                }
                else
                {
                    needAvatar = !string.IsNullOrEmpty(avatarHash);
                    requestTargetHash = avatarHash;
                }
            }

            if (needAvatar)
                TryRequestAvatar(steamId, requestTargetHash);

            if (changed)
                RaisePeersChanged();
        }

        private void TryRequestAvatar(string steamId, string hash)
        {
            string cooldownKey = steamId + "|" + hash;
            UdpClient udp;
            string payload;
            lock (_gate)
            {
                if (!_online || _udp == null || string.IsNullOrEmpty(_localSteamId))
                    return;

                long nowTicks = DateTime.UtcNow.Ticks;
                long lastTicks;
                if (_avatarRequestCooldown.TryGetValue(cooldownKey, out lastTicks))
                {
                    long ageMs = (nowTicks - lastTicks) / TimeSpan.TicksPerMillisecond;
                    if (ageMs >= 0 && ageMs < LanPresenceConstants.AvatarRequestRetryMs)
                        return;
                }

                _avatarRequestCooldown[cooldownKey] = nowTicks;

                var obj = new JsonObject();
                obj["v"] = new JsonNumber(LanPresenceConstants.ProtocolVersion);
                obj["t"] = new JsonString(LanPresenceConstants.MessageAvatarRequest);
                obj["sid"] = new JsonString(_localSteamId);
                obj["for"] = new JsonString(steamId);
                obj["want"] = new JsonString(hash);
                payload = obj.ToJsonString();
                udp = _udp;
            }

            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(payload);
                if (bytes.Length > LanPresenceConstants.MaxPacketBytes)
                    return;
                udp.Send(bytes, bytes.Length, new IPEndPoint(IPAddress.Broadcast, LanPresenceConstants.UdpPort));
            }
            catch (Exception ex)
            {
                _logger?.LogDebug("LAN presence avatar request failed: " + ex.Message);
            }
        }

        private void HandleAvatarRequest(JsonObject root, IPEndPoint remote)
        {
            string forId = LanWireHelper.SanitizeSteamId(root["for"] != null ? root["for"].ToString() : null);
            string want = SanitizeAvatarHash(root["want"] != null ? root["want"].ToString() : null);
            if (string.IsNullOrEmpty(forId) || string.IsNullOrEmpty(want))
                return;

            string remoteKey = remote != null && remote.Address != null
                ? remote.Address.ToString()
                : string.Empty;
            if (string.IsNullOrEmpty(remoteKey))
                return;

            byte[] jpeg;
            string hash;
            string localId;
            UdpClient udp;
            lock (_gate)
            {
                if (!_online || _udp == null)
                    return;
                // Hidden does not advertise; do not answer avatar pulls either.
                if (_localPersonaState == LanPersonaState.Hidden)
                    return;
                localId = _localSteamId;
                if (!string.Equals(forId, localId, StringComparison.Ordinal))
                    return;
                if (!string.Equals(want, _avatarHash, StringComparison.Ordinal))
                    return;
                if (_avatarJpeg == null || _avatarJpeg.Length == 0)
                    return;
                if (!LanWireHelper.TryConsumeRate(
                        _avatarReplyTimestamps,
                        remoteKey,
                        LanPresenceConstants.AvatarReplyMaxPerRemote,
                        LanPresenceConstants.AvatarReplyWindowMs))
                    return;

                jpeg = _avatarJpeg;
                hash = _avatarHash;
                udp = _udp;
            }

            string payload;
            try
            {
                var obj = new JsonObject();
                obj["v"] = new JsonNumber(LanPresenceConstants.ProtocolVersion);
                obj["t"] = new JsonString(LanPresenceConstants.MessageAvatarResponse);
                obj["sid"] = new JsonString(localId);
                obj["h"] = new JsonString(hash);
                obj["img"] = new JsonString(Convert.ToBase64String(jpeg));
                payload = obj.ToJsonString();
            }
            catch
            {
                return;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(payload);
            if (bytes.Length > LanPresenceConstants.MaxPacketBytes)
            {
                _logger?.LogDebug(
                    "LAN presence avatar response dropped: packet " + bytes.Length +
                    " exceeds " + LanPresenceConstants.MaxPacketBytes);
                return;
            }

            try
            {
                // Broadcast like presence heartbeats. Unicast-only replies are often dropped when
                // a host firewall allowed the inbound broadcast request asymmetrically.
                udp.Send(bytes, bytes.Length, new IPEndPoint(IPAddress.Broadcast, LanPresenceConstants.UdpPort));
            }
            catch (Exception ex)
            {
                _logger?.LogDebug("LAN presence avatar response failed: " + ex.Message);
            }
        }

        private void HandleAvatarResponse(JsonObject root)
        {
            string steamId = LanWireHelper.SanitizeSteamId(root["sid"] != null ? root["sid"].ToString() : null);
            string hash = SanitizeAvatarHash(root["h"] != null ? root["h"].ToString() : null);
            string b64 = root["img"] != null ? root["img"].ToString() : null;
            if (string.IsNullOrEmpty(steamId) || string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(b64))
                return;

            byte[] jpeg;
            try
            {
                jpeg = Convert.FromBase64String(b64);
            }
            catch
            {
                return;
            }

            if (jpeg.Length == 0 || jpeg.Length > LanPresenceConstants.MaxAvatarJpegBytes)
                return;
            if (!LooksLikeJpeg(jpeg))
                return;
            // Claimed hash must match the exact payload bytes (spoof / corruption → drop).
            if (!string.Equals(ComputeSha256Hex(jpeg), hash, StringComparison.Ordinal))
                return;

            lock (_gate)
            {
                if (!_online)
                    return;
                if (string.Equals(steamId, _localSteamId, StringComparison.Ordinal))
                    return;

                LanPresencePeer existing;
                if (!_peers.TryGetValue(steamId, out existing))
                    return;
                // Only accept the hash this peer is currently advertising.
                if (!string.Equals(existing.AvatarHash ?? string.Empty, hash, StringComparison.Ordinal))
                    return;
                if (!LanWireHelper.TryConsumeRate(
                        _avatarAcceptTimestamps,
                        steamId,
                        LanPresenceConstants.AvatarAcceptMaxPerPeer,
                        LanPresenceConstants.AvatarAcceptWindowMs))
                    return;
            }

            // Never keep peer bytes: decode → fixed canvas → our JPEG only.
            byte[] safeJpeg = ReencodeUntrustedAvatar(jpeg);
            if (safeJpeg == null || safeJpeg.Length == 0)
                return;

            string path = ResolveCachedAvatarPath(steamId, hash);
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, safeJpeg);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug("LAN presence avatar cache write failed: " + ex.Message);
                return;
            }

            bool changed = false;
            lock (_gate)
            {
                LanPresencePeer peer;
                if (_peers.TryGetValue(steamId, out peer)
                    && string.Equals(peer.AvatarHash ?? string.Empty, hash, StringComparison.Ordinal))
                {
                    peer.AvatarFilePath = path;
                    changed = true;
                }

                _avatarRequestCooldown.Remove(steamId + "|" + hash);
            }

            if (changed)
                RaisePeersChanged();
        }

        private string BuildPresenceJsonUnlocked()
        {
            ulong appId = 0;
            string gameName = string.Empty;
            TryGetPlayingGame(out appId, out gameName);

            var obj = new JsonObject();
            obj["v"] = new JsonNumber(LanPresenceConstants.ProtocolVersion);
            obj["t"] = new JsonString(LanPresenceConstants.MessagePresence);
            obj["sid"] = new JsonString(_localSteamId ?? string.Empty);
            obj["n"] = new JsonString(Truncate(_localName ?? string.Empty, LanPresenceConstants.MaxNameChars));
            obj["a"] = new JsonNumber((long)appId);
            obj["g"] = new JsonString(Truncate(gameName ?? string.Empty, LanPresenceConstants.MaxGameNameChars));
            obj["h"] = new JsonString(_avatarHash ?? string.Empty);
            obj["st"] = new JsonString(LanPersonaStateHelper.FormatWire(GetEffectivePersonaStateUnlocked()));
            obj["cs"] = new JsonString(Truncate(_localCustomStatus ?? string.Empty, LanPresenceConstants.MaxCustomStatusChars));
            obj["lfg"] = new JsonNumber((long)_localLookingForPlayAppId);
            obj["lfgn"] = new JsonString(
                Truncate(
                    _localLookingForPlayAppId == 0 ? string.Empty : (_localLookingForPlayGameName ?? string.Empty),
                    LanPresenceConstants.MaxGameNameChars));
            return obj.ToJsonString();
        }

        private void RefreshLocalIdentityUnlocked()
        {
            try
            {
                UserSettings user = ServiceLocator.GoldbergCfgService.LoadGlobalUserSettings();
                _localName = SanitizeName(user != null ? user.AccountName : null);
                _localSteamId = LanWireHelper.SanitizeSteamId(user != null ? user.AccountSteamId : null);
                if (string.IsNullOrEmpty(_localName))
                    _localName = ApplicationConstants.DefaultAccountName;
                if (string.IsNullOrEmpty(_localSteamId))
                    _localSteamId = ApplicationConstants.DefaultSteamId;
            }
            catch (Exception ex)
            {
                _logger?.LogDebug("LAN presence identity refresh failed: " + ex.Message);
                if (string.IsNullOrEmpty(_localName))
                    _localName = ApplicationConstants.DefaultAccountName;
                if (string.IsNullOrEmpty(_localSteamId))
                    _localSteamId = ApplicationConstants.DefaultSteamId;
            }
        }

        private void RefreshLocalPersonaUnlocked()
        {
            try
            {
                ApplicationSettings settings = ServiceLocator.AppDataService.LoadApplicationSettings();
                _localPersonaState = settings != null ? settings.LanPersonaState : LanPersonaState.Online;
                // Manual Away/Busy/Hidden are sticky; drop any auto-Away overlay when preferred is not Online.
                if (_localPersonaState != LanPersonaState.Online)
                    _autoAwayActive = false;
                _localCustomStatus = SanitizeCustomStatus(settings != null ? settings.LanCustomStatus : null);
                // Looking-to-play is session-only; do not reload from settings.
            }
            catch (Exception ex)
            {
                _logger?.LogDebug("LAN presence persona refresh failed: " + ex.Message);
                _localPersonaState = LanPersonaState.Online;
                _autoAwayActive = false;
                _localCustomStatus = string.Empty;
            }
        }

        // True when the auto-Away overlay flipped (UI should refresh). Does not write settings.
        private bool UpdateAutoAwayUnlocked()
        {
            bool next = false;
            if (_localPersonaState == LanPersonaState.Online)
            {
                uint idleMs = WindowsUserIdleHelper.GetIdleMilliseconds();
                next = idleMs >= (uint)LanPresenceConstants.AwayIdleMs;
            }

            if (next == _autoAwayActive)
                return false;

            _autoAwayActive = next;
            return true;
        }

        private LanPersonaState GetEffectivePersonaStateUnlocked()
        {
            if (_localPersonaState == LanPersonaState.Online && _autoAwayActive)
                return LanPersonaState.Away;
            return _localPersonaState;
        }

        private void EnsureAvatarUnlocked()
        {
            try
            {
                string sourcePath = PathConstants.GlobalAccountAvatarPath;
                if (!File.Exists(sourcePath))
                {
                    _avatarJpeg = null;
                    _avatarHash = string.Empty;
                    return;
                }

                byte[] jpeg = BuildAvatarThumbnail(sourcePath);
                if (jpeg == null || jpeg.Length == 0)
                {
                    _avatarJpeg = null;
                    _avatarHash = string.Empty;
                    _logger?.LogDebug(
                        "LAN presence: could not build shareable avatar thumbnail from " + sourcePath);
                    return;
                }

                string hash = ComputeSha256Hex(jpeg);
                if (string.Equals(hash, _avatarHash, StringComparison.Ordinal) && _avatarJpeg != null)
                    return;

                _avatarJpeg = jpeg;
                _avatarHash = hash;
                TryWriteLocalAvatarCacheUnlocked(jpeg, hash);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug("LAN presence avatar prepare failed: " + ex.Message);
                _avatarJpeg = null;
                _avatarHash = string.Empty;
            }
        }

        private void TryWriteLocalAvatarCacheUnlocked(byte[] jpeg, string hash)
        {
            if (jpeg == null || jpeg.Length == 0 || string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(_localSteamId))
                return;

            string path = ResolveCachedAvatarPath(_localSteamId, hash);
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                if (File.Exists(path))
                    return;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, jpeg);
            }
            catch (Exception ex)
            {
                _logger?.LogDebug("LAN presence local avatar cache write failed: " + ex.Message);
            }
        }

        private static void TryGetPlayingGame(out ulong appId, out string gameName)
        {
            appId = 0;
            gameName = string.Empty;
            try
            {
                if (!ServiceLocator.LaunchSessionCleanupService.TryGetPrimaryLiveLaunchAppId(out appId) || appId == 0)
                    return;

                GameConfig game = ServiceLocator.GameDataService.GetGameByAppId(appId);
                if (game != null && !string.IsNullOrWhiteSpace(game.AppName))
                    gameName = game.AppName.Trim();
                else
                    gameName = "App " + appId;
            }
            catch
            {
                appId = 0;
                gameName = string.Empty;
            }
        }

        private static byte[] BuildAvatarThumbnail(string sourcePath)
        {
            // Read into memory so the avatar file is not locked for Settings edits.
            byte[] fileBytes = File.ReadAllBytes(sourcePath);
            return EncodeAvatarThumbnail(fileBytes, requirePacketFit: true);
        }

        // Untrusted peer payload → our fixed-size JPEG only (never cache raw bytes).
        private static byte[] ReencodeUntrustedAvatar(byte[] sourceJpeg)
        {
            if (sourceJpeg == null || sourceJpeg.Length == 0)
                return null;
            return EncodeAvatarThumbnail(sourceJpeg, requirePacketFit: false);
        }

        // requirePacketFit=true: local account avatar (may be large on disk). false: untrusted peer bytes.
        private static byte[] EncodeAvatarThumbnail(byte[] fileBytes, bool requirePacketFit)
        {
            ImageCodecInfo jpegCodec = FindJpegEncoder();
            if (jpegCodec == null)
                return null;

            int maxDecode = requirePacketFit
                ? LanPresenceConstants.MaxLocalAvatarDecodeDimension
                : LanPresenceConstants.MaxAvatarDecodeDimension;

            try
            {
                using (var input = new MemoryStream(fileBytes))
                using (var source = Image.FromStream(input))
                {
                    if (source.Width < 1
                        || source.Height < 1
                        || source.Width > maxDecode
                        || source.Height > maxDecode)
                        return null;

                    using (var thumb = new Bitmap(LanPresenceConstants.AvatarPixelSize, LanPresenceConstants.AvatarPixelSize))
                    using (Graphics g = Graphics.FromImage(thumb))
                    {
                        g.Clear(Color.Black);
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.DrawImage(source, 0, 0, LanPresenceConstants.AvatarPixelSize, LanPresenceConstants.AvatarPixelSize);

                        // Drop quality until under MaxAvatarJpegBytes (and packet fit when sending).
                        long[] qualities = { 55L, 40L, 28L, 18L, 12L };
                        for (int i = 0; i < qualities.Length; i++)
                        {
                            using (var ms = new MemoryStream())
                            using (var encoderParams = new EncoderParameters(1))
                            {
                                encoderParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, qualities[i]);
                                thumb.Save(ms, jpegCodec, encoderParams);
                                byte[] bytes = ms.ToArray();
                                if (bytes.Length == 0 || bytes.Length > LanPresenceConstants.MaxAvatarJpegBytes)
                                    continue;
                                if (requirePacketFit && !AvatarPacketFits(bytes))
                                    continue;
                                return bytes;
                            }
                        }

                        return null;
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        private static bool AvatarPacketFits(byte[] jpeg)
        {
            // Approximate worst-case wrapper; reject before we ever try to send.
            int b64Len = ((jpeg.Length + 2) / 3) * 4;
            int estimated = 160 + LanPresenceConstants.MaxSteamIdChars + LanPresenceConstants.MaxAvatarHashChars + b64Len;
            return estimated <= LanPresenceConstants.MaxPacketBytes;
        }

        private static ImageCodecInfo FindJpegEncoder()
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
            for (int i = 0; i < codecs.Length; i++)
            {
                if (codecs[i].FormatID == ImageFormat.Jpeg.Guid)
                    return codecs[i];
            }

            return null;
        }

        private static string ComputeSha256Hex(byte[] data)
        {
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(data);
                var sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    sb.Append(hash[i].ToString("x2"));
                return sb.ToString();
            }
        }

        private static string ResolveCachedAvatarPath(string steamId, string hash)
        {
            if (string.IsNullOrEmpty(steamId) || string.IsNullOrEmpty(hash))
                return null;
            if (hash.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                return null;
            if (steamId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                return null;

            return Path.Combine(
                PathConstants.LanPresenceAvatarCacheDirectory,
                steamId + "_" + hash + ".jpg");
        }

        private static bool LooksLikeJpeg(byte[] data)
        {
            return data != null
                && data.Length >= 4
                && data[0] == 0xFF
                && data[1] == 0xD8
                && data[data.Length - 2] == 0xFF
                && data[data.Length - 1] == 0xD9;
        }

        private static string SanitizeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            return Truncate(StripControls(value.Trim()), LanPresenceConstants.MaxNameChars);
        }

        private static string SanitizeGameName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            return Truncate(StripControls(value.Trim()), LanPresenceConstants.MaxGameNameChars);
        }

        private static string SanitizeCustomStatus(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            return Truncate(StripControls(value.Trim()), LanPresenceConstants.MaxCustomStatusChars);
        }

        // True while preferred/effective persona is Busy (LAN chat is dropped).
        public bool IsLocalBusy
        {
            get
            {
                lock (_gate)
                    return _online && GetEffectivePersonaStateUnlocked() == LanPersonaState.Busy;
            }
        }

        private static string SanitizeAvatarHash(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;
            value = value.Trim().ToLowerInvariant();
            if (value.Length > LanPresenceConstants.MaxAvatarHashChars)
                return string.Empty;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                bool ok = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');
                if (!ok)
                    return string.Empty;
            }

            return value;
        }

        private static string StripControls(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            var sb = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c >= 32 && c != 127)
                    sb.Append(c);
            }

            return sb.ToString();
        }

        private static string Truncate(string value, int maxChars)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxChars)
                return value ?? string.Empty;
            return value.Substring(0, maxChars);
        }

        private static LanPresencePeer ClonePeer(LanPresencePeer peer)
        {
            return new LanPresencePeer
            {
                SteamId = peer.SteamId,
                DisplayName = peer.DisplayName,
                AppId = peer.AppId,
                GameName = peer.GameName,
                AvatarHash = peer.AvatarHash,
                AvatarFilePath = peer.AvatarFilePath,
                LastSeenUtc = peer.LastSeenUtc,
                RemoteAddress = peer.RemoteAddress,
                IsPresent = peer.IsPresent,
                PersonaState = peer.PersonaState,
                CustomStatus = peer.CustomStatus,
                LookingForPlayAppId = peer.LookingForPlayAppId,
                LookingForPlayGameName = peer.LookingForPlayGameName
            };
        }

        private static int ComparePeers(LanPresencePeer a, LanPresencePeer b)
        {
            int byPlaying = b.IsPlaying.CompareTo(a.IsPlaying);
            if (byPlaying != 0)
                return byPlaying;
            return string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
        }

        private void RaisePeersChanged()
        {
            EventHandler handler = PeersChanged;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }

        private void RaiseOnlineStateChanged()
        {
            EventHandler handler = OnlineStateChanged;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }
    }
}
