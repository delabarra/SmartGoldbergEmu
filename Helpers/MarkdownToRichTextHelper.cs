using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using SmartGoldbergEmu.Models;

namespace SmartGoldbergEmu.Helpers
{
    public sealed class ReleaseNoteHyperlink
    {
        public ReleaseNoteHyperlink(int start, int length, string url)
        {
            Start = start;
            Length = length;
            Url = url;
        }

        public int Start { get; }

        public int Length { get; }

        public string Url { get; }
    }

    public static class MarkdownToRichTextHelper
    {
        private const float BaseFontSize = 9f;
        private const int ContentPadding = 8;
        private const int ListIndent = 18;

        private static readonly Regex HeaderRegex = new Regex(@"^(#{1,6})\s+(.+)$", RegexOptions.Compiled);
        private static readonly Regex HorizontalRuleRegex = new Regex(@"^(\*{3,}|-{3,}|_{3,})$", RegexOptions.Compiled);
        private static readonly Regex UnorderedListRegex = new Regex(@"^[\*\-\+]\s+(.+)$", RegexOptions.Compiled);
        private static readonly Regex OrderedListRegex = new Regex(@"^\d+\.\s+(.+)$", RegexOptions.Compiled);
        private static readonly Regex BlockquoteRegex = new Regex(@"^>\s?(.*)$", RegexOptions.Compiled);
        private static readonly Regex ItalicLineRegex = new Regex(@"^_(.+)_$", RegexOptions.Compiled);
        private static readonly Regex LinkRegex = new Regex(@"\[([^\]]+)\]\(([^)]+)\)", RegexOptions.Compiled);
        private static readonly Regex HtmlAnchorRegex = new Regex(
            @"<a\s+href=""([^""]+)""[^>]*>([\s\S]*?)</a>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex InlineCodeRegex = new Regex(@"`([^`\r\n]+)`", RegexOptions.Compiled);
        private static readonly Regex HtmlCommentRegex = new Regex(@"<!--[\s\S]*?-->", RegexOptions.Compiled);
        private static readonly Regex DetailsBlockRegex = new Regex(
            @"<details>\s*<summary>[\s\S]*?</summary>\s*[\s\S]*?</details>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex AutomationMetadataLineRegex = new Regex(
            @"^Source-(?:Detanup|Alex)-(?:Release|Asset)-ID:\s*\d+\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static IList<ReleaseNoteHyperlink> Apply(RichTextBox textBox, string markdown, ThemeColors colors)
        {
            if (textBox == null)
                throw new ArgumentNullException(nameof(textBox));

            var links = new List<ReleaseNoteHyperlink>();
            var context = new RenderContext(textBox, colors, links);

            textBox.Clear();
            textBox.ForeColor = colors.Foreground;
            textBox.BackColor = colors.FieldBackground;
            textBox.Font = CreateFont("Segoe UI", BaseFontSize, FontStyle.Regular);

            if (string.IsNullOrWhiteSpace(markdown))
            {
                textBox.SelectionIndent = ContentPadding;
                textBox.SelectionRightIndent = ContentPadding;
                textBox.AppendText("(No release notes provided.)");
                textBox.Select(0, 0);
                return links;
            }

            markdown = NormalizeReleaseNotesMarkdown(markdown);
            string[] lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            var codeBlock = new System.Text.StringBuilder();
            bool inCodeBlock = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i] ?? string.Empty;

                if (line.StartsWith("```", StringComparison.Ordinal))
                {
                    if (inCodeBlock)
                    {
                        context.AppendCodeBlock(codeBlock.ToString().TrimEnd('\r', '\n'));
                        codeBlock.Clear();
                        inCodeBlock = false;
                    }
                    else
                    {
                        inCodeBlock = true;
                    }
                    continue;
                }

                if (inCodeBlock)
                {
                    codeBlock.AppendLine(line);
                    continue;
                }

                string trimmedLine = line.Trim();
                if (trimmedLine == "<!--" || trimmedLine == "-->" || AutomationMetadataLineRegex.IsMatch(trimmedLine))
                    continue;

                if (string.IsNullOrWhiteSpace(line))
                {
                    context.AppendBlankLine();
                    while (i + 1 < lines.Length && string.IsNullOrWhiteSpace(lines[i + 1]))
                        i++;
                    continue;
                }

                Match headerMatch = HeaderRegex.Match(line);
                if (headerMatch.Success)
                {
                    int level = Math.Min(6, headerMatch.Groups[1].Value.Length);
                    context.AppendHeading(level, headerMatch.Groups[2].Value);
                    continue;
                }

                if (HorizontalRuleRegex.IsMatch(line))
                {
                    context.AppendHorizontalRule();
                    continue;
                }

                Match blockquoteMatch = BlockquoteRegex.Match(line);
                if (blockquoteMatch.Success)
                {
                    context.AppendBlockquote(blockquoteMatch.Groups[1].Value);
                    continue;
                }

                Match italicLineMatch = ItalicLineRegex.Match(trimmedLine);
                if (italicLineMatch.Success)
                {
                    context.AppendSubtitle(italicLineMatch.Groups[1].Value);
                    continue;
                }

                Match unorderedMatch = UnorderedListRegex.Match(line);
                if (unorderedMatch.Success)
                {
                    context.AppendBulletItem(unorderedMatch.Groups[1].Value);
                    continue;
                }

                Match orderedMatch = OrderedListRegex.Match(line);
                if (orderedMatch.Success)
                {
                    context.AppendOrderedItem(line.TrimStart());
                    continue;
                }

                context.AppendBodyLine(line);
            }

            if (inCodeBlock)
                context.AppendCodeBlock(codeBlock.ToString().TrimEnd('\r', '\n'));

            ApplyDocumentPadding(textBox);
            textBox.Select(0, 0);
            return links;
        }

        private sealed class RenderContext
        {
            private readonly RichTextBox _textBox;
            private readonly ThemeColors _colors;
            private readonly IList<ReleaseNoteHyperlink> _links;

            public RenderContext(RichTextBox textBox, ThemeColors colors, IList<ReleaseNoteHyperlink> links)
            {
                _textBox = textBox;
                _colors = colors;
                _links = links;
                BaseFont = textBox.Font ?? SystemFonts.MessageBoxFont;
                CodeFont = CreateFont("Consolas", BaseFontSize, FontStyle.Regular);
                MutedColor = _colors.ControlForeground;
            }

            public Font BaseFont { get; }

            public Font CodeFont { get; }

            public Color MutedColor { get; }

            public void AppendBlankLine()
            {
                if (_textBox.TextLength == 0)
                    return;

                string text = _textBox.Text;
                if (text.EndsWith(Environment.NewLine + Environment.NewLine, StringComparison.Ordinal))
                    return;

                if (!text.EndsWith(Environment.NewLine, StringComparison.Ordinal))
                    FinishLine();
                FinishLine();
            }

            public void AppendHeading(int level, string text)
            {
                float size = level == 1 ? 12f : level == 2 ? 11f : level == 3 ? 10f : BaseFontSize + 0.5f;
                using (var headingFont = CreateFont("Segoe UI", size, FontStyle.Bold))
                {
                    ResetParagraphIndent();
                    AppendInline(text, headingFont, _colors.Foreground);
                    FinishLine();
                }
            }

            public void AppendSubtitle(string text)
            {
                ResetParagraphIndent();
                using (var subtitleFont = CreateFont(BaseFont, FontStyle.Italic))
                {
                    AppendInline(text, subtitleFont, MutedColor);
                }

                FinishLine();
            }

            public void AppendHorizontalRule()
            {
                ResetParagraphIndent();
                _textBox.SelectionColor = _colors.Border;
                _textBox.SelectionFont = BaseFont;
                _textBox.AppendText(new string('\u2500', 44));
                FinishLine();
                _textBox.SelectionColor = _colors.Foreground;
            }

            public void AppendBlockquote(string text)
            {
                ApplyListIndent();
                AppendPlain("\u2502 ", _colors.Border, BaseFont);
                AppendInline(text, BaseFont, MutedColor);
                FinishLine();
                ResetParagraphIndent();
            }

            public void AppendBulletItem(string text)
            {
                ApplyListIndent();
                AppendPlain("\u2022 ", _colors.Foreground, BaseFont);
                AppendInline(text, BaseFont, _colors.Foreground);
                FinishLine();
                ResetParagraphIndent();
            }

            public void AppendOrderedItem(string text)
            {
                ApplyListIndent();
                AppendInline(text, BaseFont, _colors.Foreground);
                FinishLine();
                ResetParagraphIndent();
            }

            public void AppendBodyLine(string text)
            {
                ResetParagraphIndent();
                AppendInline(text, BaseFont, _colors.Foreground);
                FinishLine();
            }

            public void AppendCodeBlock(string text)
            {
                ResetParagraphIndent();
                ApplyListIndent();
                foreach (string codeLine in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
                {
                    _textBox.SelectionFont = CodeFont;
                    _textBox.SelectionColor = _colors.Foreground;
                    _textBox.SelectionBackColor = _colors.ControlBackground;
                    _textBox.AppendText(codeLine);
                    FinishLine();
                }

                _textBox.SelectionBackColor = _colors.FieldBackground;
                _textBox.SelectionFont = BaseFont;
                ResetParagraphIndent();
            }

            private void FinishLine()
            {
                _textBox.AppendText(Environment.NewLine);
            }

            private void ResetParagraphIndent()
            {
                _textBox.SelectionIndent = ContentPadding;
                _textBox.SelectionHangingIndent = 0;
                _textBox.SelectionRightIndent = ContentPadding;
            }

            private void ApplyListIndent()
            {
                _textBox.SelectionIndent = ContentPadding + ListIndent;
                _textBox.SelectionHangingIndent = ListIndent;
                _textBox.SelectionRightIndent = ContentPadding;
            }

            private void AppendPlain(string text, Color color, Font font)
            {
                _textBox.SelectionFont = font;
                _textBox.SelectionColor = color;
                _textBox.AppendText(text);
            }

            private void AppendInline(string text, Font baseFont, Color textColor)
            {
                if (string.IsNullOrEmpty(text))
                    return;

                int index = 0;
                foreach (Match match in InlineCodeRegex.Matches(text))
                {
                    if (match.Index > index)
                        AppendInlineNonCode(text.Substring(index, match.Index - index), baseFont, textColor);

                    _textBox.SelectionFont = CodeFont;
                    _textBox.SelectionColor = textColor;
                    _textBox.SelectionBackColor = _colors.ControlBackground;
                    _textBox.AppendText(match.Groups[1].Value);
                    _textBox.SelectionBackColor = _colors.FieldBackground;
                    _textBox.SelectionFont = baseFont;
                    index = match.Index + match.Length;
                }

                if (index < text.Length)
                    AppendInlineNonCode(text.Substring(index), baseFont, textColor);
            }

            private void AppendInlineNonCode(string text, Font baseFont, Color textColor)
            {
                text = ConvertHtmlAnchorsToMarkdown(text);
                text = StripMarkdownLinksToPlaceholders(text, out var linkSegments);
                AppendStyledSegments(text, baseFont, textColor, linkSegments);
            }

            private void AppendStyledSegments(
                string text,
                Font baseFont,
                Color textColor,
                List<LinkSegment> linkSegments)
            {
                int index = 0;
                foreach (Match match in Regex.Matches(
                    text,
                    @"%%LINK(\d+)%%|\*\*([^*]+)\*\*|(?<!\*)\*([^*]+)\*(?!\*)|_([^_]+)_|(https?://[^\s<>()\""\]]+)"))
                {
                    if (match.Index > index)
                    {
                        _textBox.SelectionFont = baseFont;
                        _textBox.SelectionColor = textColor;
                        _textBox.AppendText(text.Substring(index, match.Index - index));
                    }

                    if (match.Groups[1].Success)
                    {
                        int linkIndex = int.Parse(match.Groups[1].Value);
                        if (linkIndex >= 0 && linkIndex < linkSegments.Count)
                            AppendHyperlink(linkSegments[linkIndex], baseFont);
                        else
                            _textBox.AppendText(match.Value);
                    }
                    else if (match.Groups[2].Success)
                    {
                        AppendStyledText(match.Groups[2].Value, baseFont, textColor, FontStyle.Bold);
                    }
                    else if (match.Groups[3].Success || match.Groups[4].Success)
                    {
                        string value = match.Groups[3].Success ? match.Groups[3].Value : match.Groups[4].Value;
                        AppendStyledText(value, baseFont, textColor, FontStyle.Italic);
                    }
                    else if (match.Groups[5].Success)
                    {
                        string url = TrimUrlSuffix(match.Groups[5].Value);
                        AppendHyperlink(new LinkSegment { Label = url, Url = url }, baseFont);
                    }

                    index = match.Index + match.Length;
                }

                if (index < text.Length)
                {
                    _textBox.SelectionFont = baseFont;
                    _textBox.SelectionColor = textColor;
                    _textBox.AppendText(text.Substring(index));
                }
            }

            private void AppendStyledText(string value, Font baseFont, Color textColor, FontStyle style)
            {
                using (var styledFont = CreateFont(baseFont, style))
                {
                    _textBox.SelectionFont = styledFont;
                    _textBox.SelectionColor = textColor;
                    _textBox.AppendText(value);
                }

                _textBox.SelectionFont = baseFont;
            }

            private void AppendHyperlink(LinkSegment segment, Font baseFont)
            {
                string url = segment.Url ?? string.Empty;
                string label = string.IsNullOrWhiteSpace(segment.Label) ? url : segment.Label;
                if (string.IsNullOrWhiteSpace(url) || !PathValidationHelper.IsSafeUrl(url))
                {
                    _textBox.SelectionFont = baseFont;
                    _textBox.SelectionColor = _colors.Foreground;
                    _textBox.AppendText(label);
                    return;
                }

                using (var linkFont = CreateFont(baseFont, FontStyle.Underline))
                {
                    int start = _textBox.TextLength;
                    _textBox.SelectionFont = linkFont;
                    _textBox.SelectionColor = _colors.LinkColor;
                    _textBox.AppendText(label);
                    _links.Add(new ReleaseNoteHyperlink(start, label.Length, url));
                }

                _textBox.SelectionFont = baseFont;
                _textBox.SelectionColor = _colors.Foreground;
            }
        }

        private static void ApplyDocumentPadding(RichTextBox textBox)
        {
            if (textBox.TextLength == 0)
                return;

            textBox.Select(0, textBox.TextLength);
            if (textBox.SelectionIndent < ContentPadding)
                textBox.SelectionIndent = ContentPadding;
            if (textBox.SelectionRightIndent < ContentPadding)
                textBox.SelectionRightIndent = ContentPadding;
        }

        private static Font CreateFont(string familyName, float size, FontStyle style)
        {
            return new Font(familyName, size, style, GraphicsUnit.Point);
        }

        private static Font CreateFont(Font baseFont, FontStyle style)
        {
            return new Font(baseFont.FontFamily, baseFont.Size, baseFont.Style | style, baseFont.Unit);
        }

        private static readonly Regex ExcessBlankLinesRegex = new Regex(@"\n{3,}", RegexOptions.Compiled);

        private static string NormalizeReleaseNotesMarkdown(string markdown)
        {
            if (string.IsNullOrEmpty(markdown))
                return markdown;

            markdown = HtmlCommentRegex.Replace(markdown, string.Empty);
            markdown = DetailsBlockRegex.Replace(markdown, string.Empty);
            markdown = markdown.Replace("\r\n", "\n").Replace('\r', '\n');
            markdown = ExcessBlankLinesRegex.Replace(markdown, "\n\n");
            return markdown.Trim();
        }

        private static string ConvertHtmlAnchorsToMarkdown(string text)
        {
            return HtmlAnchorRegex.Replace(text, match =>
            {
                string label = WebUtility.HtmlDecode(match.Groups[2].Value?.Trim() ?? string.Empty);
                string url = match.Groups[1].Value?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(url))
                    return label;
                if (string.IsNullOrEmpty(label))
                    label = url;
                return "[" + label + "](" + url + ")";
            });
        }

        private sealed class LinkSegment
        {
            public string Label { get; set; }

            public string Url { get; set; }
        }

        private static string StripMarkdownLinksToPlaceholders(string text, out List<LinkSegment> segments)
        {
            var linkSegments = new List<LinkSegment>();
            string result = LinkRegex.Replace(text, match =>
            {
                linkSegments.Add(new LinkSegment
                {
                    Label = match.Groups[1].Value,
                    Url = NormalizeLinkUrl(match.Groups[2].Value)
                });
                return "%%LINK" + (linkSegments.Count - 1) + "%%";
            });
            segments = linkSegments;
            return result;
        }

        private static string NormalizeLinkUrl(string rawUrl)
        {
            if (string.IsNullOrWhiteSpace(rawUrl))
                return string.Empty;

            string url = rawUrl.Trim();
            if (url.StartsWith("//", StringComparison.Ordinal))
                return "https:" + url;
            if (url.StartsWith("/", StringComparison.Ordinal))
                return "https://github.com" + url;
            if (url.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                return "https://" + url;

            return url;
        }

        private static string TrimUrlSuffix(string url)
        {
            while (url.Length > 0)
            {
                char last = url[url.Length - 1];
                if (last == '.' || last == ',' || last == ';' || last == ')' || last == ']')
                    url = url.Substring(0, url.Length - 1);
                else
                    break;
            }

            return url;
        }
    }
}
