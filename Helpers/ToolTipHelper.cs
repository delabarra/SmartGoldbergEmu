using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;

namespace SmartGoldbergEmu.Helpers
{
    internal static class ToolTipHelper
    {
        public static void SetIfPresent(ToolTip toolTip, Control control, string text)
        {
            if (toolTip == null || control == null || string.IsNullOrEmpty(text))
                return;
            toolTip.SetToolTip(control, text);
        }

        public static string FormatDescription(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return raw;

            var lines = raw.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            var sb = new StringBuilder();
            for (int i = 0; i < lines.Length; i++)
            {
                string piece = lines[i].Trim();
                if (piece.Length == 0)
                    continue;
                if (sb.Length > 0)
                    sb.Append(' ');
                sb.Append(piece);
            }

            string s = sb.ToString();
            while (s.IndexOf("  ", StringComparison.Ordinal) >= 0)
                s = s.Replace("  ", " ");

            s = ApplyGrammarCorrections(s);

            if (s.Length > 0 && char.IsLetter(s[0]))
                s = char.ToUpperInvariant(s[0]) + s.Substring(1);

            const int maxLen = 520;
            if (s.Length > maxLen)
                s = s.Substring(0, maxLen - 1) + "\u2026";

            return WrapText(s, 85);
        }

        private static string ApplyGrammarCorrections(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            string fixedText = text;
            fixedText = ReplaceIgnoreCase(fixedText, "intances", "instances");
            fixedText = ReplaceIgnoreCase(fixedText, "ahcievement", "achievement");
            fixedText = ReplaceIgnoreCase(fixedText, "icons is memory", "icons in memory");
            fixedText = ReplaceIgnoreCase(fixedText, "win't", "won't");
            fixedText = ReplaceIgnoreCase(fixedText, "cause performance drop", "cause a performance drop");
            fixedText = ReplaceIgnoreCase(fixedText, "the emu", "The emulator");
            fixedText = ReplaceIgnoreCase(fixedText, "steam emu", "Steam emulator");
            return fixedText;
        }

        private static string ReplaceIgnoreCase(string source, string oldValue, string newValue)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(oldValue))
                return source;

            int start = 0;
            while (true)
            {
                int idx = source.IndexOf(oldValue, start, StringComparison.OrdinalIgnoreCase);
                if (idx < 0)
                    break;

                source = source.Substring(0, idx) + newValue + source.Substring(idx + oldValue.Length);
                start = idx + newValue.Length;
            }

            return source;
        }

        private static string WrapText(string text, int maxLineLength)
        {
            if (string.IsNullOrWhiteSpace(text) || maxLineLength < 10)
                return text;

            var words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var lines = new List<string>();
            var current = new StringBuilder();

            for (int i = 0; i < words.Length; i++)
            {
                string word = words[i];
                int nextLen = current.Length == 0 ? word.Length : current.Length + 1 + word.Length;

                if (nextLen > maxLineLength && current.Length > 0)
                {
                    lines.Add(current.ToString());
                    current.Clear();
                }

                if (current.Length > 0)
                    current.Append(' ');
                current.Append(word);
            }

            if (current.Length > 0)
                lines.Add(current.ToString());

            return string.Join(Environment.NewLine, lines.ToArray());
        }
    }
}
