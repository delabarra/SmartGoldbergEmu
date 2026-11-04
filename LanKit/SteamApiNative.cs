using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace SmartGoldbergEmu.LanKit
{
    // Dynamic load of Goldberg steam_api64 flat exports (lobby_connect-equivalent discovery).
    // Module stays loaded for the process lifetime — FreeLibrary mid-session races emu worker threads.
    internal sealed class SteamApiNative : IDisposable
    {
        private const uint LoadWithAlteredSearchPath = 0x00000008;
        private const int FriendFlagImmediate = 0x04;
        private const int FriendFlagAll = unchecked((int)0xFFFF);

        private static readonly object ModuleGate = new object();
        private static IntPtr SharedModule;
        private static string SharedModulePath;

        private bool _initialized;
        private bool _disposed;

        private SteamApiInitDelegate _init;
        private SteamApiShutdownDelegate _shutdown;
        private SteamApiRunCallbacksDelegate _runCallbacks;
        private SteamFriendsAccessorDelegate _steamFriends;
        private FriendsGetFriendCountDelegate _getFriendCount;
        private FriendsGetFriendByIndexDelegate _getFriendByIndex;
        private FriendsGetFriendRichPresenceDelegate _getFriendRichPresence;
        private FriendsGetFriendGamePlayedDelegate _getFriendGamePlayed;

        private delegate byte SteamApiInitDelegate();
        private delegate void SteamApiShutdownDelegate();
        private delegate void SteamApiRunCallbacksDelegate();
        private delegate IntPtr SteamFriendsAccessorDelegate();
        private delegate int FriendsGetFriendCountDelegate(IntPtr self, int friendFlags);
        private delegate ulong FriendsGetFriendByIndexDelegate(IntPtr self, int friendIndex, int friendFlags);
        private delegate IntPtr FriendsGetFriendRichPresenceDelegate(IntPtr self, ulong steamIdFriend, [MarshalAs(UnmanagedType.LPStr)] string key);
        private delegate byte FriendsGetFriendGamePlayedDelegate(IntPtr self, ulong steamIdFriend, ref FriendGameInfoNative info);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibraryExW(string lpFileName, IntPtr hFile, uint dwFlags);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        public void Load(string steamApi64FullPath)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(steamApi64FullPath))
                throw new ArgumentException("steam_api64 path is required.", nameof(steamApi64FullPath));

            EnsureSharedModule(Path.GetFullPath(steamApi64FullPath));
            BindExports();
        }

        public bool Init()
        {
            ThrowIfDisposed();
            EnsureExports();
            if (_initialized)
                return true;
            if (_init() == 0)
                return false;
            _initialized = true;
            return true;
        }

        public void RunCallbacks()
        {
            ThrowIfDisposed();
            EnsureExports();
            if (_initialized)
                _runCallbacks();
        }

        public void Shutdown()
        {
            ThrowIfDisposed();
            if (!_initialized)
                return;
            try
            {
                _shutdown();
            }
            finally
            {
                _initialized = false;
            }
        }

        // Scans emu-LAN friends. Sets peerSeenWithoutJoin when the target (or same-AppId peer) is present but has no join args yet.
        public bool TryFindJoinArgs(
            ulong targetSteamId64,
            uint targetAppId,
            out LanJoinArgs args,
            out int friendCount,
            out bool peerSeenWithoutJoin,
            out LanFriendScanStats scanStats)
        {
            args = LanJoinArgs.Empty;
            friendCount = 0;
            peerSeenWithoutJoin = false;
            scanStats = new LanFriendScanStats();
            ThrowIfDisposed();
            EnsureExports();
            if (!_initialized)
                return false;

            IntPtr friends = _steamFriends();
            if (friends == IntPtr.Zero)
                return false;

            int flags = FriendFlagAll;
            friendCount = _getFriendCount(friends, flags);
            if (friendCount <= 0)
            {
                flags = FriendFlagImmediate;
                friendCount = _getFriendCount(friends, flags);
            }

            scanStats.FriendCount = friendCount;

            var appIds = new System.Collections.Generic.HashSet<uint>();
            var ips = new System.Collections.Generic.List<string>(4);
            var ports = new System.Collections.Generic.List<string>(4);

            LanJoinArgs appIdFallback = null;
            for (int i = 0; i < friendCount; i++)
            {
                ulong steamId = _getFriendByIndex(friends, i, flags);
                if (steamId == 0)
                    continue;

                bool isTarget = targetSteamId64 != 0 && steamId == targetSteamId64;
                if (isTarget)
                    scanStats.TargetSeen++;

                uint reportedAppId = 0;
                ulong lobbyId = 0;
                uint gameIp = 0;
                ushort gamePort = 0;
                var info = new FriendGameInfoNative();
                bool playing = _getFriendGamePlayed(friends, steamId, ref info) != 0;
                if (playing)
                {
                    scanStats.PlayingCount++;
                    reportedAppId = (uint)(info.GameId & 0xFFFFFF);
                    lobbyId = info.SteamIdLobby;
                    gameIp = info.GameIp;
                    gamePort = info.GamePort;
                    appIds.Add(reportedAppId);
                }

                IntPtr connectPtr = _getFriendRichPresence(friends, steamId, "connect");
                string connect = PtrToUtf8(connectPtr);

                bool hasConnect = !string.IsNullOrWhiteSpace(connect);
                bool hasLobby = lobbyId != 0;
                bool sameApp = targetAppId != 0 && reportedAppId == targetAppId;
                bool hasJoin = hasConnect || hasLobby;

                if (hasLobby)
                    scanStats.WithLobbyCount++;
                if (hasConnect)
                    scanStats.WithConnectCount++;
                if (sameApp)
                    scanStats.SameAppCount++;
                if (sameApp && hasJoin)
                    scanStats.SameAppWithJoinCount++;
                if (isTarget && hasJoin)
                    scanStats.TargetWithJoinCount++;

                if ((isTarget || sameApp) && ips.Count < 4)
                {
                    if (gameIp != 0)
                        ips.Add(FormatIpv4(gameIp));
                    if (gamePort != 0)
                        ports.Add(gamePort.ToString());
                    else if (info.QueryPort != 0)
                        ports.Add("q" + info.QueryPort);
                }

                if (!hasJoin)
                {
                    if (isTarget || sameApp)
                        peerSeenWithoutJoin = true;
                    continue;
                }

                LanJoinArgs candidate;
                if (hasConnect)
                {
                    candidate = new LanJoinArgs
                    {
                        Kind = LanJoinKind.RichPresenceConnect,
                        ConnectArguments = connect.Trim(),
                        AppId = reportedAppId
                    };
                }
                else
                {
                    candidate = new LanJoinArgs
                    {
                        Kind = LanJoinKind.ConnectLobby,
                        ConnectArguments = "+connect_lobby " + lobbyId,
                        AppId = reportedAppId
                    };
                }

                if (isTarget)
                {
                    args = candidate;
                    FinalizeScanStats(scanStats, appIds, ips, ports);
                    return true;
                }

                if (appIdFallback == null && sameApp)
                    appIdFallback = candidate;
            }

            FinalizeScanStats(scanStats, appIds, ips, ports);

            if (appIdFallback != null)
            {
                args = appIdFallback;
                return true;
            }

            return false;
        }

        private static void FinalizeScanStats(
            LanFriendScanStats stats,
            System.Collections.Generic.HashSet<uint> appIds,
            System.Collections.Generic.List<string> ips,
            System.Collections.Generic.List<string> ports)
        {
            if (stats == null)
                return;
            if (appIds != null && appIds.Count > 0)
                stats.DistinctAppIds = string.Join(",", appIds);
            if (ips != null && ips.Count > 0)
                stats.SampleIps = string.Join(",", ips);
            if (ports != null && ports.Count > 0)
                stats.SamplePorts = string.Join(",", ports);
        }

        // FriendGameInfo IP is a host-order IPv4 dword (Steam packing).
        private static string FormatIpv4(uint hostOrderIp)
        {
            return ((hostOrderIp >> 24) & 0xFF) + "."
                + ((hostOrderIp >> 16) & 0xFF) + "."
                + ((hostOrderIp >> 8) & 0xFF) + "."
                + (hostOrderIp & 0xFF);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            try
            {
                Shutdown();
            }
            catch
            {
            }

            _disposed = true;
        }

        private static void EnsureSharedModule(string fullPath)
        {
            lock (ModuleGate)
            {
                if (SharedModule != IntPtr.Zero)
                {
                    if (!string.Equals(SharedModulePath, fullPath, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            "steam_api64 is already loaded from a different path.");
                    }

                    return;
                }

                IntPtr module = LoadLibraryExW(fullPath, IntPtr.Zero, LoadWithAlteredSearchPath);
                if (module == IntPtr.Zero)
                {
                    int err = Marshal.GetLastWin32Error();
                    throw new InvalidOperationException(
                        "LoadLibraryEx failed for steam_api64.dll (Win32 " + err + ").");
                }

                SharedModule = module;
                SharedModulePath = fullPath;
            }
        }

        private void BindExports()
        {
            if (_init != null)
                return;

            _init = LoadDelegate<SteamApiInitDelegate>("SteamAPI_Init");
            _shutdown = LoadDelegate<SteamApiShutdownDelegate>("SteamAPI_Shutdown");
            _runCallbacks = LoadDelegate<SteamApiRunCallbacksDelegate>("SteamAPI_RunCallbacks");
            _steamFriends = TryLoadDelegate<SteamFriendsAccessorDelegate>("SteamAPI_SteamFriends_v017")
                ?? LoadDelegate<SteamFriendsAccessorDelegate>("SteamAPI_SteamFriends_v018");
            _getFriendCount = LoadDelegate<FriendsGetFriendCountDelegate>("SteamAPI_ISteamFriends_GetFriendCount");
            _getFriendByIndex = LoadDelegate<FriendsGetFriendByIndexDelegate>("SteamAPI_ISteamFriends_GetFriendByIndex");
            _getFriendRichPresence = LoadDelegate<FriendsGetFriendRichPresenceDelegate>("SteamAPI_ISteamFriends_GetFriendRichPresence");
            _getFriendGamePlayed = LoadDelegate<FriendsGetFriendGamePlayedDelegate>("SteamAPI_ISteamFriends_GetFriendGamePlayed");
        }

        private void EnsureExports()
        {
            if (SharedModule == IntPtr.Zero || _init == null)
                throw new InvalidOperationException("steam_api64 is not loaded.");
        }

        private T LoadDelegate<T>(string exportName) where T : class
        {
            T del = TryLoadDelegate<T>(exportName);
            if (del == null)
                throw new InvalidOperationException("Missing export: " + exportName);
            return del;
        }

        private T TryLoadDelegate<T>(string exportName) where T : class
        {
            IntPtr proc = GetProcAddress(SharedModule, exportName);
            if (proc == IntPtr.Zero)
                return null;
            return Marshal.GetDelegateForFunctionPointer(proc, typeof(T)) as T;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SteamApiNative));
        }

        private static string PtrToUtf8(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
                return string.Empty;
            int len = 0;
            while (Marshal.ReadByte(ptr, len) != 0)
                len++;
            if (len == 0)
                return string.Empty;
            byte[] bytes = new byte[len];
            Marshal.Copy(ptr, bytes, 0, len);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
