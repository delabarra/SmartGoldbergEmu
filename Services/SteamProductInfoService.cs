using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SmartGoldbergEmu.JsonKit;
using SmartGoldbergEmu.Constants;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;
using SteamKit;

namespace SmartGoldbergEmu.Services
{
    public sealed class SteamProductInfoService : IDisposable
    {
        private const uint EResultOk = 1;
        private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        private static readonly TimeSpan[] SessionEstablishAttemptBudgets =
        {
            TimeSpan.FromSeconds(25),
            TimeSpan.FromSeconds(22),
        };
        private const int MaxLinkedPackagesToFetch = 32;

        private static readonly HashSet<string> PackageSectionNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            SteamPicsKeyNames.Packages,
            SteamPicsKeyNames.Subs
        };

        private readonly SemaphoreSlim _sessionLock = new SemaphoreSlim(1, 1);
        private bool _disposed;

        private SteamClient _client;
        private bool _loggedOn;

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            try
            {
                _sessionLock.Wait();
                try
                {
                    TeardownClient();
                }
                finally
                {
                    try
                    {
                        _sessionLock.Release();
                    }
                    catch
                    {
                    }
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService?.LogWarning($"SteamProductInfoService dispose: {ex.Message}");
            }

            try
            {
                _sessionLock.Dispose();
            }
            catch
            {
            }
        }

        public async Task<bool> IsAppDataAvailableAsync(string appId, CancellationToken ct = default)
        {
            return await GetAppPicsRootOrFetchAsync(appId, null, ct).ConfigureAwait(false) != null;
        }

        public async Task<KeyValue> GetAppKeyValueAsync(string appId, CancellationToken ct = default)
        {
            if (!uint.TryParse(appId, out uint id) || id == 0)
                return null;

            await _sessionLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (!await EnsureLoggedOnAsync(ct).ConfigureAwait(false))
                    return null;

                PICSProductInfoResult pics = await _client.RequestProductInfo(id, 0, ct).ConfigureAwait(false);
                return PicsAppResultToKeyValue(id, pics);
            }
            finally
            {
                _sessionLock.Release();
            }
        }

        // In-memory root, then on-disk VDF export (games/{appId}/resources/{appId}.vdf), then live Steam PICS.
        public async Task<KeyValue> GetAppPicsRootOrFetchAsync(string appId, KeyValue picsAppRoot, CancellationToken ct = default)
        {
            if (picsAppRoot != null)
                return picsAppRoot;

            if (ulong.TryParse(appId, out ulong appIdNum) && appIdNum != 0)
            {
                KeyValue cached = SteamPicsKeyValueHelper.TryLoadExportedAppPicsFromValveFile(
                    PathConstants.GamesDirectory,
                    appIdNum);
                if (cached != null)
                    return cached;
            }

            return await GetAppKeyValueAsync(appId, ct).ConfigureAwait(false);
        }

        public async Task<KeyValue> WarmGameConfigAppPicsRootAsync(GameConfig game, CancellationToken ct = default)
        {
            if (game == null || game.AppId == 0)
                return null;
            string appId = game.AppId.ToString();
            KeyValue kv = await GetAppPicsRootOrFetchAsync(appId, game.AppPicsKeyValue, ct).ConfigureAwait(false);
            if (kv != null)
                game.AppPicsKeyValue = kv;
            return game.AppPicsKeyValue;
        }

        public async Task PreWarmSessionAsync(CancellationToken ct = default)
        {
            if (_disposed)
                return;

            try
            {
                await _sessionLock.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    if (!ct.IsCancellationRequested)
                        await EnsureLoggedOnAsync(ct).ConfigureAwait(false);

                    // Drop anything we just opened if the caller backed out mid-connect.
                    if (ct.IsCancellationRequested)
                        TeardownClient();
                }
                finally
                {
                    _sessionLock.Release();
                }
            }
            catch (OperationCanceledException)
            {
                // Cancelled before the lock was held; the connect attempt tears itself down.
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService?.LogWarning($"Steam session pre-warm failed: {ex.Message}");
            }
        }

        public async Task CloseSessionAsync()
        {
            if (_disposed)
                return;

            try
            {
                await _sessionLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    TeardownClient();
                }
                finally
                {
                    _sessionLock.Release();
                }
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService?.LogWarning($"Steam session close failed: {ex.Message}");
            }
        }

        public async Task<KeyValue> GetPackageKeyValueAsync(string packageId, CancellationToken ct = default)
        {
            if (!uint.TryParse(packageId, out uint pkgId) || pkgId == 0)
                return null;

            await _sessionLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (!await EnsureLoggedOnAsync(ct).ConfigureAwait(false))
                    return null;

                PICSProductInfoResult pics = await _client.RequestProductInfo(0, pkgId, ct).ConfigureAwait(false);
                return PicsPackageResultToKeyValue(pkgId, pics);
            }
            finally
            {
                _sessionLock.Release();
            }
        }

        private static KeyValue PicsAppResultToKeyValue(uint appId, PICSProductInfoResult pics)
        {
            if (pics == null)
                return null;

            if (pics.UnknownAppIds.Contains(appId))
                return null;

            PICSProductInfoItem item = pics.Apps.FirstOrDefault(a => a.ID == appId) ?? pics.Apps.FirstOrDefault();
            if (item?.Buffer == null || item.Buffer.Length == 0)
                return null;

            return item.ToKeyValue();
        }

        private static KeyValue PicsPackageResultToKeyValue(uint packageId, PICSProductInfoResult pics)
        {
            if (pics == null)
                return null;

            if (pics.UnknownPackageIds.Contains(packageId))
                return null;

            PICSProductInfoItem item = pics.Packages.FirstOrDefault(p => p.ID == packageId) ?? pics.Packages.FirstOrDefault();
            if (item?.Buffer == null || item.Buffer.Length == 0)
                return null;

            return item.ToKeyValue();
        }

        private async Task<bool> EnsureLoggedOnAsync(CancellationToken ct)
        {
            if (_client != null && _loggedOn && _client.IsConnected)
                return true;

            return await ReconnectAsync(ct).ConfigureAwait(false);
        }

        private async Task<bool> ReconnectAsync(CancellationToken ct)
        {
            uint lastOutcome = SteamLogonWaitResult.WaitTimedOut;

            for (int attempt = 0; attempt < SessionEstablishAttemptBudgets.Length; attempt++)
            {
                TeardownClient();

                (bool success, uint outcome) = await TryConnectAndLogOnAsync(ct, SessionEstablishAttemptBudgets[attempt])
                    .ConfigureAwait(false);
                if (success)
                    return true;

                lastOutcome = outcome;
                if (ct.IsCancellationRequested)
                    return false;

                if (attempt == 0 && ShouldRetrySessionEstablishment(lastOutcome))
                    continue;

                break;
            }

            ServiceLocator.LogService.LogWarning(
                $"Steam game assets: session establishment failed ({DescribeSessionEstablishFailure(lastOutcome)}).");
            return false;
        }

        private async Task<(bool success, uint outcome)> TryConnectAndLogOnAsync(CancellationToken ct, TimeSpan waitBudget)
        {
            var logonTcs = new TaskCompletionSource<uint>(TaskCreationOptions.RunContinuationsAsynchronously);
            var client = new SteamClient();

            client.OnConnected += () => client.LogOnAnonymous();
            client.OnLoggedOn += code => logonTcs.TrySetResult(code);
            client.OnConnectionFailed += _ => logonTcs.TrySetResult(SteamLogonWaitResult.ConnectionFailed);
            client.OnDisconnected += () =>
            {
                _loggedOn = false;
                logonTcs.TrySetResult(SteamLogonWaitResult.DisconnectedWhileWaiting);
            };

            _client = client;
            client.Connect();

            Task completed = await Task.WhenAny(logonTcs.Task, Task.Delay(waitBudget, ct)).ConfigureAwait(false);
            if (completed != logonTcs.Task)
            {
                TeardownClient();
                return (false, SteamLogonWaitResult.WaitTimedOut);
            }

            uint er = await logonTcs.Task.ConfigureAwait(false);
            if (er != EResultOk)
            {
                TeardownClient();
                return (false, er);
            }

            _loggedOn = true;
            return (true, er);
        }

        private static bool ShouldRetrySessionEstablishment(uint outcome)
        {
            return outcome == SteamLogonWaitResult.WaitTimedOut
                || outcome == SteamLogonWaitResult.DisconnectedWhileWaiting
                || outcome == SteamLogonWaitResult.LogonResponseParseFailed
                || outcome == SteamLogonWaitResult.ConnectionFailed;
        }

        private static string DescribeSessionEstablishFailure(uint code)
        {
            if (code == SteamLogonWaitResult.WaitTimedOut)
                return "timed out waiting for logon";
            if (code == SteamLogonWaitResult.ConnectionFailed)
                return "could not connect to a Steam CM";
            if (code == SteamLogonWaitResult.DisconnectedWhileWaiting)
                return "connection closed before logon completed";
            if (code == SteamLogonWaitResult.LogonResponseParseFailed)
                return "could not parse logon response";
            return $"Steam EResult {code}";
        }

        private void TeardownClient()
        {
            _loggedOn = false;
            try
            {
                _client?.Disconnect();
            }
            catch
            {
            }

            _client = null;
        }

        public async Task<PackageExtractionResult> ExtractPackageDataForAppAsync(string appId, CancellationToken ct = default)
        {
            KeyValue appRoot = await GetAppPicsRootOrFetchAsync(appId, null, ct).ConfigureAwait(false);
            return await ExtractPackageDataForAppAsync(appId, appRoot, ct).ConfigureAwait(false);
        }

        // Uses an in-memory app PICS root when supplied to avoid a duplicate app product-info fetch.
        public async Task<PackageExtractionResult> ExtractPackageDataForAppAsync(string appId, KeyValue existingAppRoot, CancellationToken ct = default)
        {
            var result = new PackageExtractionResult();
            try
            {
                if (existingAppRoot == null)
                    return result;

                List<uint> packageIds = CollectLinkedPackageIds(existingAppRoot);
                int n = 0;
                foreach (uint pkg in packageIds)
                {
                    if (++n > MaxLinkedPackagesToFetch)
                        break;

                    KeyValue pkgData = await GetPackageKeyValueAsync(pkg.ToString(), ct).ConfigureAwait(false);
                    if (pkgData?.Children == null || pkgData.Children.Count == 0)
                        continue;

                    foreach (string d in ExtractDepotsFromPackage(pkgData))
                    {
                        if (!result.Depots.Contains(d))
                            result.Depots.Add(d);
                    }

                    foreach (PackageBranchInfo b in ExtractBranchesFromPackage(pkgData))
                    {
                        if (!result.Branches.Any(x => x.Name == b.Name))
                            result.Branches.Add(b);
                    }

                    foreach (string aid in ExtractAppIdsFromPackage(pkgData))
                    {
                        if (!result.AppIds.Contains(aid))
                            result.AppIds.Add(aid);
                    }
                }
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService.LogError($"Error extracting game assets package data for app {appId}", ex);
            }

            return result;
        }

        // Game settings add-mode: merge app + linked-package depot ids, sorted numerically when parseable (CPU work off caller's sync context).
        public async Task<List<string>> BuildOrderedDepotIdsFromPicsAsync(string appId, KeyValue cachedAppRoot, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(appId))
                return null;

            KeyValue kv = await GetAppPicsRootOrFetchAsync(appId, cachedAppRoot, ct).ConfigureAwait(false);
            if (kv == null)
                return null;

            PackageExtractionResult pkgData = await ExtractPackageDataForAppAsync(appId, kv, ct).ConfigureAwait(false);

            return await Task.Run(() =>
            {
                AppDataExtractionResult appData = ExtractAppDataFromAppRoot(kv, appId);
                var ids = new HashSet<string>(StringComparer.Ordinal);
                if (appData.Depots != null)
                {
                    foreach (string d in appData.Depots)
                    {
                        if (!string.IsNullOrWhiteSpace(d))
                            ids.Add(d.Trim());
                    }
                }
                if (pkgData?.Depots != null)
                {
                    foreach (string d in pkgData.Depots)
                    {
                        if (!string.IsNullOrWhiteSpace(d))
                            ids.Add(d.Trim());
                    }
                }
                if (ids.Count == 0)
                    return null;
                return ids
                    .Select(x => ulong.TryParse(x, out ulong u) ? (Key: u, Text: x) : (Key: ulong.MaxValue, Text: x))
                    .OrderBy(t => t.Key)
                    .Select(t => t.Text)
                    .ToList();
            }).ConfigureAwait(false);
        }

        private static List<uint> CollectLinkedPackageIds(KeyValue appRoot)
        {
            var ids = new HashSet<uint>();
            KeyValue target = SteamPicsKeyValueHelper.ResolveAppInfoTarget(appRoot) ?? appRoot;
            CollectPackageSectionsRecursive(target, ids, 0);
            return ids.OrderBy(x => x).ToList();
        }

        private static void CollectPackageSectionsRecursive(KeyValue node, HashSet<uint> ids, int depth)
        {
            if (node?.Children == null || depth > 28)
                return;

            foreach (KeyValue child in node.Children)
            {
                if (child == null || string.IsNullOrEmpty(child.Name))
                    continue;

                if (PackageSectionNames.Contains(child.Name))
                {
                    AddNumericKeysAsPackageIds(child, ids);
                    continue;
                }

                CollectPackageSectionsRecursive(child, ids, depth + 1);
            }
        }

        private static void AddNumericKeysAsPackageIds(KeyValue section, HashSet<uint> ids)
        {
            if (section?.Children == null)
                return;

            foreach (KeyValue c in section.Children)
            {
                if (c != null && uint.TryParse(c.Name, out uint pkg) && pkg > 0)
                    ids.Add(pkg);
            }
        }

        public AppDataExtractionResult ExtractAppDataFromAppRoot(KeyValue appData, string appIdForLogging = null)
        {
            var result = new AppDataExtractionResult();
            try
            {
                if (appData == null || appData.Children == null || appData.Children.Count == 0)
                    return result;

                KeyValue targetNode = SteamPicsKeyValueHelper.ResolveAppInfoTarget(appData) ?? appData;

                KeyValue statsNode = SteamPicsKeyValueHelper.FindChild(targetNode, SteamPicsKeyNames.Stats);
                if (statsNode != null)
                    result.Stats = ExtractStats(statsNode);

                KeyValue depotsNode = SteamPicsKeyValueHelper.FindChild(targetNode, SteamPicsKeyNames.Depots);
                if (depotsNode != null)
                    result.Depots = ExtractDepotsFromAppData(depotsNode);

                KeyValue leaderboardsNode = SteamPicsKeyValueHelper.FindChild(targetNode, SteamPicsKeyNames.Leaderboards);
                if (leaderboardsNode != null)
                    result.Leaderboards = ExtractLeaderboards(leaderboardsNode);

                KeyValue achievementsNode = SteamPicsKeyValueHelper.FindChild(targetNode, SteamPicsKeyNames.Achievements);
                if (achievementsNode != null)
                    result.Achievements = ExtractAchievements(achievementsNode);
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService.LogError($"Error extracting app game assets data for app {appIdForLogging ?? "?"}", ex);
            }

            return result;
        }

        private static List<string> ExtractDepotsFromPackage(KeyValue packageData)
        {
            var depots = new List<string>();
            if (packageData == null || packageData.Children == null)
                return depots;

            try
            {
                KeyValue depotsNode = SteamPicsKeyValueHelper.FindChild(packageData, SteamPicsKeyNames.Depots);
                if (depotsNode?.Children != null)
                {
                    foreach (KeyValue depotEntry in depotsNode.Children)
                    {
                        if (depotEntry == null)
                            continue;
                        string depotId = depotEntry.Name;
                        if (!string.IsNullOrEmpty(depotId) && !depots.Contains(depotId))
                            depots.Add(depotId);
                    }
                }

                foreach (KeyValue child in packageData.Children)
                {
                    if (child == null || string.Equals(child.Name, SteamPicsKeyNames.Depots, StringComparison.OrdinalIgnoreCase) ||
                        child.Children == null || child.Children.Count == 0)
                        continue;

                    bool allNumericKeys = true;
                    foreach (KeyValue c in child.Children)
                    {
                        if (c == null || !uint.TryParse(c.Name, out _))
                        {
                            allNumericKeys = false;
                            break;
                        }
                    }

                    if (!allNumericKeys)
                        continue;

                    foreach (KeyValue depotEntry in child.Children)
                    {
                        if (depotEntry == null)
                            continue;
                        if (uint.TryParse(depotEntry.Name, out uint depotId))
                        {
                            string depotIdStr = depotId.ToString();
                            if (!depots.Contains(depotIdStr))
                                depots.Add(depotIdStr);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService.LogError("Error extracting depots from package", ex);
            }

            return depots;
        }

        private static List<PackageBranchInfo> ExtractBranchesFromPackage(KeyValue packageData)
        {
            var branches = new List<PackageBranchInfo>();
            if (packageData == null || packageData.Children == null)
                return branches;

            try
            {
                KeyValue branchesNode = SteamPicsKeyValueHelper.FindChild(packageData, SteamPicsKeyNames.Branches);
                if (branchesNode?.Children == null)
                    return branches;

                foreach (KeyValue branchEntry in branchesNode.Children)
                {
                    if (branchEntry == null)
                        continue;
                    string branchName = branchEntry.Name;
                    KeyValue branchData = branchEntry;

                    if (string.IsNullOrEmpty(branchName) || branchData.Children == null || branchData.Children.Count == 0)
                        continue;

                    var branchInfo = new PackageBranchInfo { Name = branchName };

                    KeyValue buildIdNode = SteamPicsKeyValueHelper.FindChild(branchData, SteamPicsKeyNames.BuildId);
                    if (buildIdNode != null && uint.TryParse(buildIdNode.Value, out uint buildId))
                        branchInfo.BuildId = buildId;

                    KeyValue timeUpdatedNode = SteamPicsKeyValueHelper.FindChild(branchData, SteamPicsKeyNames.TimeUpdated);
                    if (timeUpdatedNode != null && uint.TryParse(timeUpdatedNode.Value, out uint timeUpdated))
                        branchInfo.TimeUpdated = timeUpdated;

                    KeyValue descNode = SteamPicsKeyValueHelper.FindChild(branchData, SteamPicsKeyNames.Description);
                    if (descNode != null)
                        branchInfo.Description = descNode.Value ?? "";

                    KeyValue protectedNode = SteamPicsKeyValueHelper.FindChild(branchData, SteamPicsKeyNames.PwdRequired);
                    if (protectedNode != null)
                    {
                        if (uint.TryParse(protectedNode.Value, out uint p) && p != 0)
                            branchInfo.Protected = true;
                        else
                            branchInfo.Protected = protectedNode.Value == "1" ||
                                string.Equals(protectedNode.Value, "true", StringComparison.OrdinalIgnoreCase);
                    }

                    branches.Add(branchInfo);
                }
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService.LogError("Error extracting branches from package", ex);
            }

            return branches;
        }

        private static List<string> ExtractAppIdsFromPackage(KeyValue packageData)
        {
            var appIds = new List<string>();
            if (packageData == null)
                return appIds;

            try
            {
                ExtractAppIdsRecursive(packageData, appIds);
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService.LogError("Error extracting app IDs from package", ex);
            }

            return appIds.Distinct().ToList();
        }

        private static void ExtractAppIdsRecursive(KeyValue node, List<string> appIds)
        {
            if (node == null)
                return;

            if (!string.IsNullOrEmpty(node.Value))
            {
                string potentialAppId = node.Value.Trim();
                if (!string.IsNullOrEmpty(potentialAppId) &&
                    uint.TryParse(potentialAppId, out uint appId) &&
                    appId > 0 && appId < 4294967295)
                {
                    if (!appIds.Contains(potentialAppId))
                        appIds.Add(potentialAppId);
                }
            }

            if (node.Children != null)
            {
                foreach (KeyValue child in node.Children)
                    ExtractAppIdsRecursive(child, appIds);
            }
        }

        private static List<string> ExtractDepotsFromAppData(KeyValue depotsNode)
        {
            var depots = new List<string>();
            if (depotsNode == null || depotsNode.Children == null)
                return depots;

            try
            {
                foreach (KeyValue depotEntry in depotsNode.Children)
                {
                    if (depotEntry == null)
                        continue;
                    if (!uint.TryParse(depotEntry.Name, out uint depotId) || depotId == 0)
                        continue;

                    string depotIdString = depotId.ToString();
                    if (!depots.Contains(depotIdString))
                        depots.Add(depotIdString);
                }
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService.LogError("Error extracting depots from app data", ex);
            }

            return depots;
        }

        private static string ExtractStats(KeyValue statsNode)
        {
            if (statsNode == null || statsNode.Children == null)
                return null;

            try
            {
                var statsList = new List<object>();

                foreach (KeyValue statData in statsNode.Children)
                {
                    if (statData == null || string.IsNullOrEmpty(statData.Name) || statData.Children == null)
                        continue;

                    var statObj = new Dictionary<string, string> { ["name"] = statData.Name };

                    KeyValue typeNode = SteamPicsKeyValueHelper.FindChild(statData, SteamPicsKeyNames.Type);
                    statObj["type"] = typeNode != null && !string.IsNullOrEmpty(typeNode.Value) ? typeNode.Value : "int";

                    KeyValue defaultNode = SteamPicsKeyValueHelper.FindChild(statData, SteamPicsKeyNames.Default);
                    statObj["default"] = defaultNode != null && !string.IsNullOrEmpty(defaultNode.Value) ? defaultNode.Value : "0";

                    KeyValue globalNode = SteamPicsKeyValueHelper.FindChild(statData, SteamPicsKeyNames.Global);
                    statObj["global"] = globalNode != null && !string.IsNullOrEmpty(globalNode.Value) ? globalNode.Value : "0";

                    statsList.Add(statObj);
                }

                if (statsList.Count > 0)
                    return JsonConvert.SerializeObject(statsList, JsonFormatting.Indented);
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService.LogError("Error extracting stats", ex);
            }

            return null;
        }

        private static List<string> ExtractLeaderboards(KeyValue leaderboardsNode)
        {
            var leaderboards = new List<string>();
            if (leaderboardsNode == null)
                return leaderboards;

            try
            {
                if (leaderboardsNode.Children != null)
                {
                    foreach (KeyValue leaderboardData in leaderboardsNode.Children)
                    {
                        if (leaderboardData == null)
                            continue;
                        string leaderboardName = leaderboardData.Name;

                        if (string.IsNullOrEmpty(leaderboardName))
                            continue;

                        int sortMethod = 0;
                        int displayType = 0;

                        if (leaderboardData.Children != null)
                        {
                            KeyValue sortNode = SteamPicsKeyValueHelper.FindChild(leaderboardData, SteamPicsKeyNames.SortMethod);
                            if (sortNode != null && int.TryParse(sortNode.Value, out int sort))
                                sortMethod = sort;

                            KeyValue displayNode = SteamPicsKeyValueHelper.FindChild(leaderboardData, SteamPicsKeyNames.DisplayType);
                            if (displayNode != null && int.TryParse(displayNode.Value, out int display))
                                displayType = display;
                        }

                        leaderboards.Add($"{leaderboardName}={sortMethod}={displayType}");
                    }
                }
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService.LogError("Error extracting leaderboards", ex);
            }

            return leaderboards;
        }

        private static string ExtractAchievements(KeyValue achievementsNode)
        {
            if (achievementsNode == null || achievementsNode.Children == null)
                return null;

            try
            {
                var achievementsList = new List<object>();

                foreach (KeyValue achievementData in achievementsNode.Children)
                {
                    if (achievementData == null || string.IsNullOrEmpty(achievementData.Name) || achievementData.Children == null)
                        continue;

                    var achievementObj = new Dictionary<string, object> { ["name"] = achievementData.Name };

                    foreach (KeyValue prop in achievementData.Children)
                    {
                        if (prop == null || string.IsNullOrEmpty(prop.Name))
                            continue;

                        if (prop.Children != null && prop.Children.Count > 0)
                            continue;

                        if (string.IsNullOrEmpty(prop.Value))
                            continue;

                        if (uint.TryParse(prop.Value, out uint u))
                            achievementObj[prop.Name] = u;
                        else if (int.TryParse(prop.Value, out int i))
                            achievementObj[prop.Name] = i;
                        else
                            achievementObj[prop.Name] = prop.Value;
                    }

                    if (achievementObj.Count > 0)
                        achievementsList.Add(achievementObj);
                }

                if (achievementsList.Count > 0)
                    return JsonConvert.SerializeObject(achievementsList, JsonFormatting.Indented);
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService.LogError("Error extracting achievements", ex);
            }

            return null;
        }

        public bool ExportAppPicsToValveTextFile(string appId, KeyValue picsData)
        {
            if (string.IsNullOrEmpty(appId) || picsData == null)
                return false;
            return ExportAppPicsToValveTextFile(appId, picsData, GetDefaultAppPicsExportFilePath(appId));
        }

        public bool ExportAppPicsToValveTextFile(string appId, KeyValue picsData, string outputPath)
        {
            try
            {
                if (string.IsNullOrEmpty(appId) || picsData == null || string.IsNullOrEmpty(outputPath))
                    return false;

                return ExportAppPicsToValveTextFileCore(appId, picsData, outputPath);
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService.LogError($"Error exporting app {appId} game assets to Valve text", ex);
                return false;
            }
        }

        private static bool ExportAppPicsToValveTextFileCore(string appId, KeyValue picsData, string outputPath)
        {
            try
            {
                if (picsData == null)
                    return false;

                string directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                string text = SerializeKeyValueAsValveText(picsData, appId);
                File.WriteAllText(outputPath, text, Utf8WithoutBom);
                return true;
            }
            catch (Exception ex)
            {
                ServiceLocator.LogService.LogError($"Error exporting app {appId} game assets to file", ex);
                return false;
            }
        }

        private static string SerializeKeyValueAsValveText(KeyValue node, string id)
        {
            if (node == null)
                return string.Empty;

            var sb = new StringBuilder();

            if (node.Children != null && node.Children.Count > 0)
            {
                if (!string.IsNullOrEmpty(id))
                {
                    sb.AppendLine($"\"{EscapeValveTextString(id)}\"");
                    sb.AppendLine("{");
                    foreach (KeyValue child in node.Children)
                    {
                        if (child == null)
                            continue;
                        string childText = SerializeKeyValueNodeAsValveText(child, child.Name, 1);
                        if (!string.IsNullOrEmpty(childText))
                            sb.Append(childText);
                    }

                    sb.AppendLine("}");
                }
                else
                {
                    foreach (KeyValue child in node.Children)
                    {
                        if (child == null)
                            continue;
                        string childText = SerializeKeyValueNodeAsValveText(child, child.Name, 0);
                        if (!string.IsNullOrEmpty(childText))
                            sb.Append(childText);
                    }
                }
            }

            return sb.ToString();
        }

        private static string SerializeKeyValueNodeAsValveText(KeyValue node, string name, int indentLevel)
        {
            if (node == null)
                return string.Empty;

            var sb = new StringBuilder();
            string indent = new string('\t', indentLevel);

            if (node.Children != null && node.Children.Count > 0)
            {
                sb.AppendLine($"{indent}\"{EscapeValveTextString(name)}\"");
                sb.AppendLine($"{indent}{{");

                foreach (KeyValue child in node.Children)
                {
                    if (child == null)
                        continue;
                    string childText = SerializeKeyValueNodeAsValveText(child, child.Name, indentLevel + 1);
                    if (!string.IsNullOrEmpty(childText))
                        sb.Append(childText);
                }

                sb.AppendLine($"{indent}}}");
            }
            else
            {
                string valueText = string.IsNullOrEmpty(node.Value)
                    ? "\"\""
                    : $"\"{EscapeValveTextString(node.Value)}\"";
                sb.AppendLine($"{indent}\"{EscapeValveTextString(name)}\"\t\t{valueText}");
            }

            return sb.ToString();
        }

        private static string EscapeValveTextString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "";

            return input
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        private static string GetDefaultAppPicsExportFilePath(string appId)
        {
            return PathConstants.CombineGamesPerAppValveDataFilePath(PathConstants.GamesDirectory, appId);
        }
    }
}
