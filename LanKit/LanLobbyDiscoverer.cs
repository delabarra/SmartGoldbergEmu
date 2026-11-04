using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Constants;

namespace SmartGoldbergEmu.LanKit
{
    // Headless lobby_connect discovery against Goldberg steam_api64 (emu LAN friends).
    public static class LanLobbyDiscoverer
    {
        // Goldberg announces on BROADCAST_INTERVAL (5s). Need longer than one interval + TCP/friend sync.
        private const int PrimaryPasses = 48;
        // Host readiness polls — shorter so the exclusive LanKit lock is not held for ~12s every cycle.
        private const int ReadinessPasses = 20;
        private const int CallbackSleepMs = 250;
        private const int DefaultListenPort = 47584;

        // Public-individual Steam64 base (universe 1, type 1, instance 1).
        private const ulong SteamId64IndividualBase = 76561197960265728UL;

        private static readonly object Gate = new object();

        public static bool TryDiscover(ulong steamId64, uint appId, out LanJoinArgs result, ILogService log = null, string extraBroadcastIp = null)
        {
            LanDiscoverResult full;
            bool ok = TryDiscover(steamId64, appId, out full, log, extraBroadcastIp);
            result = full != null && full.Join != null ? full.Join : LanJoinArgs.Empty;
            return ok;
        }

        // Host presence poll: same discover path, fewer passes (see ReadinessPasses).
        public static bool TryDiscoverReadiness(
            ulong steamId64,
            uint appId,
            out LanDiscoverResult result,
            ILogService log = null,
            string extraBroadcastIp = null)
        {
            return TryDiscover(steamId64, appId, out result, log, extraBroadcastIp, ReadinessPasses, "readiness");
        }

        public static bool TryDiscover(
            ulong steamId64,
            uint appId,
            out LanDiscoverResult result,
            ILogService log = null,
            string extraBroadcastIp = null)
        {
            return TryDiscover(steamId64, appId, out result, log, extraBroadcastIp, PrimaryPasses, "ephemeral+game");
        }

        private static bool TryDiscover(
            ulong steamId64,
            uint appId,
            out LanDiscoverResult result,
            ILogService log,
            string extraBroadcastIp,
            int passes,
            string label)
        {
            result = LanDiscoverResult.Empty;
            if (steamId64 == 0 || appId == 0)
            {
                log?.LogWarning("LanKit: discover aborted (steamId or appId is 0).");
                return false;
            }

            if (!Environment.Is64BitProcess)
            {
                log?.LogWarning("LanKit: discovery requires a 64-bit process.");
                return false;
            }

            string dllPath = PathConstants.CombineGoldbergStandardSteamApiPath(useX64: true);
            if (string.IsNullOrEmpty(dllPath) || !File.Exists(dllPath))
            {
                log?.LogWarning("LanKit: steam_api64.dll is missing under goldberg/SteamApi (run Goldberg Update).");
                return false;
            }

            if (!Monitor.TryEnter(Gate, TimeSpan.FromSeconds(90)))
            {
                log?.LogWarning("LanKit: discovery lock timed out.");
                return false;
            }

            string probeRoot = null;
            try
            {
                string gameFolder = PathConstants.GetGameFolder(appId);
                bool gameFolderExists = !string.IsNullOrEmpty(gameFolder) && Directory.Exists(gameFolder);
                string steamSettings = gameFolderExists
                    ? Path.Combine(gameFolder, PathConstants.SteamSettingsFolderName)
                    : null;
                bool steamSettingsExists = !string.IsNullOrEmpty(steamSettings) && Directory.Exists(steamSettings);

                int listenPort = ResolveListenPort(steamSettings, steamSettingsExists);
                string meshInfo = DescribeMeshSidecars(steamSettings, steamSettingsExists, listenPort);
                string startMsg =
                    "LanKit: start discover (" + label + ") targetSteam64=" + steamId64
                    + " appId=" + appId
                    + " passes=" + passes
                    + " dllOk=1"
                    + " gameFolder=" + (gameFolderExists ? "1" : "0")
                    + " steam_settings=" + (steamSettingsExists ? "1" : "0")
                    + " extraIp=" + (string.IsNullOrWhiteSpace(extraBroadcastIp) ? "-" : extraBroadcastIp.Trim())
                    + " " + meshInfo;
                if (string.Equals(label, "readiness", StringComparison.Ordinal))
                    log?.LogDebug(startMsg);
                else
                    log?.LogMessage(startMsg);

                probeRoot = TryCreateEphemeralProbeRoot(
                    steamId64, listenPort, steamSettings, steamSettingsExists, log, extraBroadcastIp);
                if (string.IsNullOrEmpty(probeRoot))
                {
                    log?.LogWarning("LanKit: could not create ephemeral probe steam_settings.");
                    return false;
                }

                string probeGse = EnsureTrailingSlash(probeRoot);

                // Stock steam_api64 is NOT built with LOBBY_CONNECT — that AppId rejects other games'
                // user-status callbacks. Same AppId + ephemeral identity is the working path.
                // custom_broadcasts includes 127.0.0.1 so same-PC host probe can reach the game
                // (UDP broadcast often never loops back to another local process).
                if (TryDiscoverOnce(
                        dllPath,
                        steamId64,
                        appId,
                        appId,
                        probeGse,
                        label,
                        passes,
                        out LanDiscoverResult gameApp,
                        log)
                    && gameApp.Status == LanDiscoverStatus.JoinArgsFound)
                {
                    result = gameApp;
                    return true;
                }

                result = gameApp ?? LanDiscoverResult.Empty;
                // Readiness polls expect PeerSeenWithoutJoin until the host enters a lobby — keep quiet.
                bool quietMiss = string.Equals(label, "readiness", StringComparison.Ordinal);
                if (result.Status == LanDiscoverStatus.PeerSeenWithoutJoin)
                {
                    if (!quietMiss)
                    {
                        log?.LogWarning(
                            "LanKit: Steam64 " + steamId64 + " is on the emu network for AppId " + appId
                            + " but has no lobby id / connect string yet (friends max=" + result.MaxFriendsSeen + "). "
                            + "They must create or enter the in-game lobby before Join/Accept can attach +connect_lobby. "
                            + "Some titles never publish Steam lobby/connect (custom matchmaking only).");
                    }
                }
                else if (!quietMiss)
                {
                    log?.LogWarning(
                        "LanKit: no emu-network peers for Steam64 " + steamId64 + " AppId " + appId
                        + " (friends max=" + result.MaxFriendsSeen + "). Check listen_port (" + listenPort
                        + "), firewall UDP for that port, and that the host game is running with Goldberg.");
                }

                return false;
            }
            finally
            {
                TryDeleteDirectory(probeRoot);
                Monitor.Exit(Gate);
            }
        }

        private static LanDiscoverResult Prefer(LanDiscoverResult current, LanDiscoverResult candidate)
        {
            if (candidate == null)
                return current ?? LanDiscoverResult.Empty;
            if (current == null || current.Status == LanDiscoverStatus.None)
                return candidate;
            if (candidate.Status == LanDiscoverStatus.JoinArgsFound)
                return candidate;
            if (current.Status != LanDiscoverStatus.JoinArgsFound
                && candidate.Status == LanDiscoverStatus.PeerSeenWithoutJoin)
                return candidate;
            if (candidate.MaxFriendsSeen > current.MaxFriendsSeen)
                return candidate;
            return current;
        }

        private static bool TryDiscoverOnce(
            string dllPath,
            ulong steamId64,
            uint targetAppIdForMatch,
            uint steamAppIdEnv,
            string gseAppPath,
            string label,
            int passes,
            out LanDiscoverResult result,
            ILogService log)
        {
            result = new LanDiscoverResult
            {
                Status = LanDiscoverStatus.None,
                Join = LanJoinArgs.Empty,
                MaxFriendsSeen = 0
            };

            string previousAppId = Environment.GetEnvironmentVariable("SteamAppId");
            string previousGameId = Environment.GetEnvironmentVariable("SteamGameId");
            string previousGseAppPath = Environment.GetEnvironmentVariable("GseAppPath");
            try
            {
                string appIdText = steamAppIdEnv.ToString(CultureInfo.InvariantCulture);
                Environment.SetEnvironmentVariable("SteamAppId", appIdText);
                Environment.SetEnvironmentVariable("SteamGameId", appIdText);
                Environment.SetEnvironmentVariable("GseAppPath", gseAppPath);

                bool quiet = string.Equals(label, "readiness", StringComparison.Ordinal);
                if (quiet)
                {
                    log?.LogDebug(
                        "LanKit[" + label + "]: init SteamAppId=" + appIdText
                        + " passes=" + passes);
                }
                else
                {
                    log?.LogMessage(
                        "LanKit[" + label + "]: init SteamAppId=" + appIdText
                        + " GseAppPath=" + (string.IsNullOrEmpty(gseAppPath) ? "(null)" : "ephemeral")
                        + " passes=" + passes);
                }

                using (var api = new SteamApiNative())
                {
                    api.Load(dllPath);
                    if (!api.Init())
                    {
                        log?.LogWarning("LanKit[" + label + "]: SteamAPI_Init failed.");
                        return false;
                    }

                    if (!quiet)
                        log?.LogMessage("LanKit[" + label + "]: SteamAPI_Init ok.");

                    try
                    {
                        int lastFriendCount = -1;
                        bool sawPeerWithoutJoin = false;
                        string lastScanFragment = null;
                        for (int i = 0; i < passes; i++)
                        {
                            api.RunCallbacks();
                            bool found = api.TryFindJoinArgs(
                                steamId64,
                                targetAppIdForMatch,
                                out LanJoinArgs join,
                                out int friendCount,
                                out bool peerSeenWithoutJoin,
                                out LanFriendScanStats scanStats);

                            if (friendCount > result.MaxFriendsSeen)
                                result.MaxFriendsSeen = friendCount;
                            if (peerSeenWithoutJoin)
                                sawPeerWithoutJoin = true;

                            string scanFragment = scanStats != null ? scanStats.ToLogFragment() : ("friends=" + friendCount);
                            bool scanChanged = !string.Equals(scanFragment, lastScanFragment, StringComparison.Ordinal);
                            if (scanChanged)
                                lastScanFragment = scanFragment;

                            if (found && join != null && join.Kind != LanJoinKind.None
                                && !string.IsNullOrWhiteSpace(join.ConnectArguments))
                            {
                                result.Status = LanDiscoverStatus.JoinArgsFound;
                                result.Join = join;
                                log?.LogMessage(
                                    "LanKit[" + label + "]: discovered " + join.Kind
                                    + " for " + steamId64 + ": " + join.ConnectArguments
                                    + " (pass=" + (i + 1) + ", " + scanFragment + ")");
                                return true;
                            }

                            if (!quiet && (friendCount != lastFriendCount || (scanChanged && friendCount > 0)))
                            {
                                lastFriendCount = friendCount;
                                log?.LogMessage(
                                    "LanKit[" + label + "]: looking for " + steamId64
                                    + " AppId " + targetAppIdForMatch
                                    + " pass=" + (i + 1) + "/" + passes
                                    + " " + scanFragment);
                            }

                            Thread.Sleep(CallbackSleepMs);
                        }

                        if (sawPeerWithoutJoin)
                            result.Status = LanDiscoverStatus.PeerSeenWithoutJoin;

                        if (quiet)
                        {
                            log?.LogDebug(
                                "LanKit[" + label + "]: finished " + passes + " passes, status="
                                + result.Status + ", friends max=" + result.MaxFriendsSeen + ".");
                        }
                        else
                        {
                            log?.LogMessage(
                                "LanKit[" + label + "]: finished " + passes + " passes, status="
                                + result.Status + ", friends max=" + result.MaxFriendsSeen
                                + (lastScanFragment != null ? ", lastScan=" + lastScanFragment : string.Empty) + ".");
                        }

                        return false;
                    }
                    finally
                    {
                        api.Shutdown();
                    }
                }
            }
            catch (Exception ex)
            {
                log?.LogWarning("LanKit[" + label + "]: discovery failed: " + ex.Message);
                return false;
            }
            finally
            {
                try
                {
                    Environment.SetEnvironmentVariable("SteamAppId", previousAppId);
                    Environment.SetEnvironmentVariable("SteamGameId", previousGameId);
                    Environment.SetEnvironmentVariable("GseAppPath", previousGseAppPath);
                }
                catch
                {
                }
            }
        }

        // Distinct Steam64 + matching listen_port so host self-probe can see the running game as a friend.
        private static string TryCreateEphemeralProbeRoot(
            ulong avoidSteamId64,
            int listenPort,
            string gameSteamSettingsDir,
            bool gameSteamSettingsExists,
            ILogService log,
            string extraBroadcastIp = null)
        {
            try
            {
                string root = Path.Combine(
                    Path.GetTempPath(),
                    "SmartGoldbergEmu",
                    "LanKitProbe",
                    Guid.NewGuid().ToString("N"));
                string settingsDir = Path.Combine(root, PathConstants.SteamSettingsFolderName);
                Directory.CreateDirectory(settingsDir);

                ulong probeId = CreateEphemeralSteamId64(avoidSteamId64);
                var userIni = new StringBuilder();
                userIni.AppendLine("[user::general]");
                userIni.AppendLine("account_name=SGE LanKit");
                userIni.AppendLine("account_steamid=" + probeId.ToString(CultureInfo.InvariantCulture));
                userIni.AppendLine();
                userIni.AppendLine("[user::saves]");
                userIni.AppendLine("local_save_path=./saves");
                File.WriteAllText(
                    Path.Combine(settingsDir, PathConstants.GoldbergUserIniFileName),
                    userIni.ToString(),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

                var mainIni = new StringBuilder();
                mainIni.AppendLine("[main::connectivity]");
                mainIni.AppendLine("listen_port=" + listenPort.ToString(CultureInfo.InvariantCulture));
                File.WriteAllText(
                    Path.Combine(settingsDir, PathConstants.GoldbergMainIniFileName),
                    mainIni.ToString(),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

                var seen = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var broadcasts = new StringBuilder();
                void AppendBroadcast(string address)
                {
                    if (string.IsNullOrWhiteSpace(address))
                        return;
                    string line = address.Trim();
                    if (!seen.Add(line))
                        return;
                    broadcasts.AppendLine(line);
                }

                AppendBroadcast("127.0.0.1");
                AppendBroadcast(extraBroadcastIp);
                if (gameSteamSettingsExists)
                {
                    string srcBroadcasts = Path.Combine(
                        gameSteamSettingsDir, PathConstants.GoldbergCustomBroadcastsFileName);
                    if (File.Exists(srcBroadcasts))
                    {
                        try
                        {
                            foreach (string raw in File.ReadLines(srcBroadcasts))
                            {
                                if (string.IsNullOrWhiteSpace(raw) || raw.TrimStart().StartsWith("#", StringComparison.Ordinal))
                                    continue;
                                AppendBroadcast(raw);
                            }
                        }
                        catch
                        {
                        }
                    }
                }

                File.WriteAllText(
                    Path.Combine(settingsDir, PathConstants.GoldbergCustomBroadcastsFileName),
                    broadcasts.ToString(),
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

                log?.LogMessage(
                    "LanKit: ephemeral probe id=" + probeId
                    + " listen_port=" + listenPort
                    + " custom_broadcasts=" + string.Join(",", seen)
                    + " root prepared.");
                return root;
            }
            catch (Exception ex)
            {
                log?.LogWarning("LanKit: ephemeral probe setup failed: " + ex.Message);
                return null;
            }
        }

        private static ulong CreateEphemeralSteamId64(ulong avoidSteamId64)
        {
            var rng = new Random();
            for (int i = 0; i < 8; i++)
            {
                uint accountId = (uint)rng.Next(1, int.MaxValue);
                ulong id = SteamId64IndividualBase + accountId;
                if (id != 0 && id != avoidSteamId64)
                    return id;
            }

            return SteamId64IndividualBase + 424242u;
        }

        private static int ResolveListenPort(string steamSettingsDir, bool steamSettingsExists)
        {
            string perGameMain = steamSettingsExists
                ? Path.Combine(steamSettingsDir, PathConstants.GoldbergMainIniFileName)
                : null;
            string globalMain = Path.Combine(PathConstants.GlobalSettingsPath, PathConstants.GoldbergMainIniFileName);
            string raw = TryReadIniValue(perGameMain, "listen_port")
                ?? TryReadIniValue(globalMain, "listen_port");
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int port)
                && port > 0
                && port <= 65535)
            {
                return port;
            }

            return DefaultListenPort;
        }

        private static string DescribeMeshSidecars(string steamSettingsDir, bool steamSettingsExists, int resolvedListenPort)
        {
            var sb = new StringBuilder();
            string perGameMain = steamSettingsExists
                ? Path.Combine(steamSettingsDir, PathConstants.GoldbergMainIniFileName)
                : null;
            string globalMain = Path.Combine(PathConstants.GlobalSettingsPath, PathConstants.GoldbergMainIniFileName);
            string perGamePort = TryReadIniValue(perGameMain, "listen_port");
            string globalPort = TryReadIniValue(globalMain, "listen_port");
            sb.Append("listen_port(resolved)=");
            sb.Append(resolvedListenPort);
            sb.Append(" listen_port(game)=");
            sb.Append(string.IsNullOrEmpty(perGamePort) ? "-" : perGamePort);
            sb.Append(" listen_port(global)=");
            sb.Append(string.IsNullOrEmpty(globalPort) ? "-" : globalPort);

            string broadcasts = steamSettingsExists
                ? Path.Combine(steamSettingsDir, PathConstants.GoldbergCustomBroadcastsFileName)
                : null;
            int broadcastLines = CountNonEmptyLines(broadcasts);
            sb.Append(" custom_broadcasts=");
            sb.Append(broadcastLines >= 0 ? broadcastLines.ToString(CultureInfo.InvariantCulture) : "missing");

            string disableLan = TryReadIniValue(perGameMain, "disable_lan_only")
                ?? TryReadIniValue(globalMain, "disable_lan_only");
            string offline = TryReadIniValue(perGameMain, "offline")
                ?? TryReadIniValue(globalMain, "offline");
            string disableNet = TryReadIniValue(perGameMain, "disable_networking")
                ?? TryReadIniValue(globalMain, "disable_networking");
            if (!string.IsNullOrEmpty(disableLan))
                sb.Append(" disable_lan_only=").Append(disableLan);
            if (!string.IsNullOrEmpty(offline))
                sb.Append(" offline=").Append(offline);
            if (!string.IsNullOrEmpty(disableNet))
                sb.Append(" disable_networking=").Append(disableNet);

            return sb.ToString();
        }

        private static string TryReadIniValue(string path, string key)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(key) || !File.Exists(path))
                return null;
            try
            {
                string prefix = key + "=";
                foreach (string raw in File.ReadLines(path))
                {
                    if (string.IsNullOrWhiteSpace(raw) || raw.TrimStart().StartsWith(";", StringComparison.Ordinal))
                        continue;
                    string line = raw.Trim();
                    if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        return line.Substring(prefix.Length).Trim();
                }
            }
            catch
            {
            }

            return null;
        }

        private static int CountNonEmptyLines(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return -1;
            try
            {
                int n = 0;
                foreach (string raw in File.ReadLines(path))
                {
                    if (!string.IsNullOrWhiteSpace(raw) && !raw.TrimStart().StartsWith("#", StringComparison.Ordinal))
                        n++;
                }

                return n;
            }
            catch
            {
                return -1;
            }
        }

        private static string EnsureTrailingSlash(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;
            if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                || path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
                return path;
            return path + Path.DirectorySeparatorChar;
        }

        private static void TryDeleteDirectory(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, recursive: true);
            }
            catch
            {
            }
        }
    }
}
