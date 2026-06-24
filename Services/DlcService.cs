using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SmartGoldbergEmu.Abstractions;
using SmartGoldbergEmu.Helpers;
using SmartGoldbergEmu.Models;
using SteamKit;

namespace SmartGoldbergEmu.Services
{
    public class DlcService
    {
        private readonly SteamProductInfoService _steamProductInfo;
        private readonly ILogService _logger;
        private readonly ITaskReportService _taskReportService;

        public DlcService(SteamProductInfoService steamProductInfo = null, ILogService logger = null, ITaskReportService feedbackService = null)
        {
            _steamProductInfo = steamProductInfo ?? ServiceLocator.SteamProductInfoService;
            _logger = logger ?? ServiceLocator.LogService;
            _taskReportService = feedbackService;
        }

        #region DLC list text helpers

        public static string BuildDlcListText(Dictionary<long, string> dlcData)
        {
            if (dlcData == null || dlcData.Count == 0)
                return string.Empty;

            var dlcList = dlcData
                .Select(kvp => kvp.Key + " - " + kvp.Value)
                .OrderBy(s => s)
                .ToList();

            return string.Join(Environment.NewLine, dlcList);
        }

        public static string BuildDlcListTextWithPreferredNames(Dictionary<long, string> dlcData, Dictionary<long, string> preferredNames)
        {
            if (dlcData == null || dlcData.Count == 0)
                return string.Empty;

            var rows = new List<string>();
            foreach (var kvp in dlcData)
            {
                string name = kvp.Value;
                if (preferredNames != null && preferredNames.TryGetValue(kvp.Key, out string preferred) && !string.IsNullOrWhiteSpace(preferred))
                    name = preferred;
                rows.Add(kvp.Key + " - " + (name ?? string.Empty));
            }

            return string.Join(Environment.NewLine, rows.OrderBy(x => x));
        }

        public static Dictionary<long, string> ParseDlcListText(string text, Dictionary<long, string> fallbackNames = null)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var result = new Dictionary<long, string>();
            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed))
                    continue;

                long dlcId;
                string dlcName = null;
                int sepIndex = trimmed.IndexOf(" - ", StringComparison.Ordinal);
                if (sepIndex >= 0 && long.TryParse(trimmed.Substring(0, sepIndex).Trim(), out dlcId))
                {
                    dlcName = trimmed.Substring(sepIndex + 3).Trim();
                }
                else if (long.TryParse(trimmed, out dlcId))
                {
                    dlcName = FormatDlcPlaceholder(dlcId);
                }
                else
                {
                    continue;
                }

                if (string.IsNullOrEmpty(dlcName) && fallbackNames != null && fallbackNames.TryGetValue(dlcId, out string fallbackName))
                    dlcName = fallbackName;
                if (string.IsNullOrEmpty(dlcName))
                    dlcName = FormatDlcPlaceholder(dlcId);

                result[dlcId] = dlcName;
            }

            return result.Count > 0 ? result : null;
        }

        #endregion

        #region DLC fetching

        public async Task<Dictionary<long, string>> GetDlcDataAsync(
            string appId,
            Dictionary<long, string> existingDlcData = null,
            KeyValue picsAppRoot = null,
            CancellationToken cancellationToken = default,
            ITaskReportService statusReport = null)
        {
            ITaskReportService report = statusReport ?? _taskReportService;
            var allDlc = existingDlcData != null ? new Dictionary<long, string>(existingDlcData) : new Dictionary<long, string>();

            try
            {
                List<long> picsIds = await GetDlcIdsFromPicsAsync(appId, picsAppRoot, cancellationToken).ConfigureAwait(false);
                if (picsIds.Count == 0)
                {
                    if (allDlc.Count == 0)
                    {
                        report?.SetMessage("No DLC found for this game.");
                        return allDlc;
                    }

                    List<long> missingExisting = CollectIdsWithPlaceholderNames(allDlc.Keys, allDlc);
                    if (missingExisting.Count > 0)
                    {
                        report?.SetMessage($"Resolving {missingExisting.Count} missing name(s) from Steam game assets...");
                        await ResolveDlcNamesAsync(missingExisting, allDlc, cancellationToken, report).ConfigureAwait(false);
                    }

                    return allDlc;
                }

                report?.SetMessage($"Found {picsIds.Count} DLC item(s) from game assets.");
                foreach (long dlcId in picsIds)
                {
                    if (!allDlc.ContainsKey(dlcId))
                        allDlc[dlcId] = FormatDlcPlaceholder(dlcId);
                }

                List<long> missingNames = CollectIdsWithPlaceholderNames(picsIds, allDlc);
                if (missingNames.Count == 0)
                {
                    report?.SetMessage($"All {picsIds.Count} DLC item(s) already have names.");
                    return allDlc;
                }

                report?.SetMessage(
                    $"Found {picsIds.Count} DLC item(s). Resolving {missingNames.Count} missing name(s) from Steam game assets...");
                await ResolveDlcNamesAsync(missingNames, allDlc, cancellationToken, report).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                report?.SetMessage("DLC fetch cancelled.", TaskReportKind.Warning);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError($"DLC retrieval failed for app {appId}", ex);
                report?.SetMessage("Could not fetch DLC data.", TaskReportKind.Error);
            }

            report?.SetMessage($"Successfully fetched {allDlc.Count} DLC item(s).");
            return allDlc;
        }

        private static List<long> CollectIdsWithPlaceholderNames(IEnumerable<long> dlcIds, Dictionary<long, string> allDlc)
        {
            var missingNames = new List<long>();
            foreach (long dlcId in dlcIds)
            {
                if (!allDlc.TryGetValue(dlcId, out string name) ||
                    string.IsNullOrEmpty(name) ||
                    name == FormatDlcPlaceholder(dlcId))
                {
                    missingNames.Add(dlcId);
                }
            }

            return missingNames;
        }

        private async Task ResolveDlcNamesAsync(
            IReadOnlyList<long> missingNames,
            Dictionary<long, string> allDlc,
            CancellationToken cancellationToken,
            ITaskReportService report)
        {
            int totalMissing = missingNames.Count;
            report?.SetProgress(0, totalMissing);

            int completed = 0;

            IEnumerable<Task> dlcTasks = missingNames.Select(dlcId => ResolveOneDlcNameAsync(
                dlcId, allDlc, cancellationToken, report, totalMissing, () => Interlocked.Increment(ref completed)));

            await Task.WhenAll(dlcTasks).ConfigureAwait(false);
        }

        private async Task ResolveOneDlcNameAsync(
            long dlcId,
            Dictionary<long, string> allDlc,
            CancellationToken cancellationToken,
            ITaskReportService report,
            int totalMissing,
            Func<int> incrementCompleted)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string resolved;
            try
            {
                resolved = await TryResolveDlcNameFromPicsAsync(dlcId, cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrEmpty(resolved))
                {
                    _logger.LogWarning($"Could not resolve name for DLC {dlcId} from game assets, using placeholder");
                    resolved = FormatDlcPlaceholder(dlcId);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching name for DLC {dlcId} from game assets", ex);
                resolved = FormatDlcPlaceholder(dlcId);
            }

            allDlc[dlcId] = resolved;

            int done = incrementCompleted();
            report?.SetProgress(done, totalMissing);
            report?.SetMessage($"Fetching DLC names from game assets... {done}/{totalMissing}");
        }

        private static string FormatDlcPlaceholder(long dlcId) => "DLC " + dlcId;

        private async Task<string> TryResolveDlcNameFromPicsAsync(long dlcId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            KeyValue root = await _steamProductInfo.GetAppKeyValueAsync(dlcId.ToString(), cancellationToken).ConfigureAwait(false);
            if (root != null && SteamPicsKeyValueHelper.TryGetAppDisplayInfo(root, out string name, out _) && !string.IsNullOrEmpty(name))
                return name;
            return null;
        }

        private async Task<List<long>> GetDlcIdsFromPicsAsync(string appId, KeyValue picsAppRoot, CancellationToken cancellationToken = default)
        {
            var dlcIds = new List<long>();
            try
            {
                if (!ulong.TryParse(appId, out ulong appIdNum) || appIdNum == 0)
                    return dlcIds;

                KeyValue kv = picsAppRoot;
                if (kv == null)
                {
                    var picsHolder = new GameConfig { AppId = appIdNum };
                    kv = await _steamProductInfo.WarmGameConfigAppPicsRootAsync(picsHolder, cancellationToken).ConfigureAwait(false);
                }

                if (kv == null)
                {
                    _logger.LogWarning($"Game assets KeyValue is null for app {appId}");
                    return dlcIds;
                }
                SteamPicsKeyValueHelper.CollectDlcIdsFromAppRoot(kv, dlcIds);
                if (dlcIds.Count > 0)
                    _logger.LogDebug($"Game assets DLC IDs for app {appId}: [{string.Join(", ", dlcIds)}]");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error extracting DLC list from game assets for app {appId}", ex);
            }
            return dlcIds;
        }

        #endregion
    }
}
