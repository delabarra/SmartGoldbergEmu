using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartGoldbergEmu.Services
{
    // DLC list text parse/format helpers. Network fetch lives in AppDataKitBridgeService.
    public class DlcService
    {
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

        private static string FormatDlcPlaceholder(long dlcId) => "DLC " + dlcId;
    }
}
