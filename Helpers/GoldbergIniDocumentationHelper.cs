using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Helpers
{
    /// <summary>
    /// Builds Goldberg-style INI output: header, section spacing, and per-key lines.
    /// </summary>
    public static class GoldbergIniDocumentationHelper
    {
        public static void AppendIniHeader(StringBuilder content)
        {
            content.AppendLine("# ############################################################################## #");
            content.AppendLine("# you do not have to specify everything, pick and choose the options you need only");
            content.AppendLine("# ############################################################################## #");
        }

        public static void AppendSection(StringBuilder content, string section)
        {
            if (content.Length > 0 && content[content.Length - 1] != '\n')
                content.AppendLine();
            if (content.Length > 0)
                content.AppendLine();
            content.AppendLine("[" + section + "]");
        }

        /// <summary>
        /// Comment lines immediately after a section header (before the first key=value), e.g. [app::paths] block intro.
        /// </summary>
        public static IReadOnlyList<string> GetSectionHeaderCommentLines(
            string exampleIniPath,
            string sectionName,
            IDictionary<string, string[]> cache)
        {
            var lines = GetCachedLines(exampleIniPath, cache);
            if (lines == null)
                return Array.Empty<string>();

            string currentSection = null;
            for (int i = 0; i < lines.Length; i++)
            {
                var t = lines[i].Trim();
                if (t.Length >= 2 && t[0] == '[' && t[t.Length - 1] == ']')
                {
                    currentSection = t.Substring(1, t.Length - 2);
                    if (!string.Equals(currentSection, sectionName, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var block = new List<string>();
                    for (int j = i + 1; j < lines.Length; j++)
                    {
                        var u = lines[j].Trim();
                        if (u.Length == 0)
                            continue;
                        if (u.StartsWith("#", StringComparison.Ordinal))
                        {
                            block.Add(u);
                            continue;
                        }

                        if (u.Contains("=") && !u.StartsWith("#", StringComparison.Ordinal))
                            break;
                    }

                    return block;
                }
            }

            return Array.Empty<string>();
        }

        /// <summary>
        /// All consecutive # comment lines immediately above <paramref name="key"/>= in <paramref name="sectionName"/>.
        /// Lines are returned as in the example (trimmed), including the leading #.
        /// </summary>
        /// <summary>
        /// Consecutive # lines immediately after <paramref name="key"/>= (e.g. <c># format: ID=name</c> after <c>unlock_all</c>).
        /// </summary>
        public static IReadOnlyList<string> GetConsecutiveCommentLinesAfterKey(
            string exampleIniPath,
            string sectionName,
            string key,
            IDictionary<string, string[]> cache)
        {
            if (string.IsNullOrEmpty(key))
                return Array.Empty<string>();

            var lines = GetCachedLines(exampleIniPath, cache);
            if (lines == null)
                return Array.Empty<string>();

            string currentSection = null;
            for (int i = 0; i < lines.Length; i++)
            {
                var t = lines[i].Trim();
                if (t.Length >= 2 && t[0] == '[' && t[t.Length - 1] == ']')
                {
                    currentSection = t.Substring(1, t.Length - 2);
                    continue;
                }

                if (!string.Equals(currentSection, sectionName, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (t.StartsWith("#", StringComparison.Ordinal))
                    continue;
                if (!TrySplitKeyValue(t, out var lineKey, out _))
                    continue;
                if (!lineKey.Equals(key, StringComparison.OrdinalIgnoreCase))
                    continue;

                var block = new List<string>();
                for (int j = i + 1; j < lines.Length; j++)
                {
                    var u = lines[j].Trim();
                    if (u.Length == 0)
                        continue;
                    if (u.StartsWith("#", StringComparison.Ordinal))
                    {
                        block.Add(u);
                        continue;
                    }

                    break;
                }

                return block;
            }

            return Array.Empty<string>();
        }

        public static IReadOnlyList<string> GetCommentLinesBeforeKey(
            string exampleIniPath,
            string sectionName,
            string key,
            IDictionary<string, string[]> cache)
        {
            if (string.IsNullOrEmpty(key))
                return Array.Empty<string>();

            var lines = GetCachedLines(exampleIniPath, cache);
            if (lines == null)
                return Array.Empty<string>();

            string currentSection = null;
            for (int i = 0; i < lines.Length; i++)
            {
                var t = lines[i].Trim();
                if (t.Length >= 2 && t[0] == '[' && t[t.Length - 1] == ']')
                {
                    currentSection = t.Substring(1, t.Length - 2);
                    continue;
                }

                if (!string.Equals(currentSection, sectionName, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (t.StartsWith("#", StringComparison.Ordinal))
                    continue;
                if (!TrySplitKeyValue(t, out var lineKey, out _))
                    continue;
                if (!lineKey.Equals(key, StringComparison.OrdinalIgnoreCase))
                    continue;

                var block = new List<string>();
                for (int j = i - 1; j >= 0; j--)
                {
                    var u = lines[j].Trim();
                    if (u.Length == 0)
                        continue;
                    if (!u.StartsWith("#", StringComparison.Ordinal))
                        break;
                    block.Add(u);
                }

                block.Reverse();
                return block;
            }

            return Array.Empty<string>();
        }

        /// <summary>
        /// Writes example comment block (if any) then <c>key=value</c>. If no example comments, writes <paramref name="fallbackDocs"/> as # lines.
        /// </summary>
        public static void AppendOptionWithExampleOrFallback(
            StringBuilder sb,
            string exampleIniPath,
            string section,
            string key,
            string value,
            IDictionary<string, string[]> cache,
            params string[] fallbackDocs)
        {
            var fromExample = GetCommentLinesBeforeKey(exampleIniPath, section, key, cache);
            if (fromExample != null && fromExample.Count > 0)
            {
                foreach (var line in fromExample)
                    sb.AppendLine(line);
                sb.AppendLine(key + "=" + (value ?? string.Empty));
                return;
            }

            if (fallbackDocs != null)
            {
                foreach (var doc in fallbackDocs)
                {
                    if (!string.IsNullOrWhiteSpace(doc))
                        sb.AppendLine("# " + doc.Trim());
                }
            }

            sb.AppendLine(key + "=" + (value ?? string.Empty));
        }

        public static void WriteIniFile(string outputPath, IEnumerable<IniLine> lines)
        {
            var cache = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            var sb = new StringBuilder();
            AppendIniHeader(sb);

            foreach (var line in lines ?? Enumerable.Empty<IniLine>())
            {
                if (line == null)
                    continue;

                if (line.Type == IniLineType.Section)
                {
                    var sectionName = line.Section;
                    if (string.IsNullOrEmpty(sectionName) && !string.IsNullOrEmpty(line.RawLine))
                    {
                        var rt = line.RawLine.Trim();
                        if (rt.Length >= 2 && rt[0] == '[' && rt[rt.Length - 1] == ']')
                            sectionName = rt.Substring(1, rt.Length - 2);
                    }

                    if (!string.IsNullOrEmpty(sectionName))
                        AppendSection(sb, sectionName);
                    else
                        sb.AppendLine(line.RawLine);
                }
                else if (line.Type == IniLineType.KeyValue && !string.IsNullOrEmpty(line.Section) && !string.IsNullOrEmpty(line.Key))
                {
                    AppendOptionWithExampleOrFallback(sb, null, line.Section, line.Key, line.Value, cache);
                }
            }

            File.WriteAllText(outputPath, sb.ToString());
        }

        private static string[] GetCachedLines(string exampleIniPath, IDictionary<string, string[]> cache)
        {
            if (string.IsNullOrEmpty(exampleIniPath) || !File.Exists(exampleIniPath) || cache == null)
                return null;

            if (!cache.TryGetValue(exampleIniPath, out var lines))
            {
                lines = File.ReadAllLines(exampleIniPath);
                cache[exampleIniPath] = lines;
            }

            return lines;
        }

        private static bool TrySplitKeyValue(string trimmedLine, out string key, out string value)
        {
            key = null;
            value = null;
            var eq = trimmedLine.IndexOf('=');
            if (eq <= 0)
                return false;
            key = trimmedLine.Substring(0, eq).Trim();
            value = eq + 1 < trimmedLine.Length ? trimmedLine.Substring(eq + 1).Trim() : string.Empty;
            return !string.IsNullOrEmpty(key);
        }
    }
}
