using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Sitecore.MarkdownForAI.Core
{
    /// <summary>
    /// Converts rendered HTML to GitHub-flavoured Markdown. Dependency free so the same
    /// assembly runs on Sitecore 8.0 to 10.5.
    /// </summary>
    public sealed class MarkdownConverter
    {
        /// <summary>
        /// Script type that carries Markdown which must be emitted as is. Used by
        /// <c>*.md.cshtml</c> views and by inline article sources.
        /// </summary>
        public const string RawMarkdownScriptType = "text/x-markdown";

        private const char HardBreak = '\u0001';

        private static readonly HashSet<string> BlockTags = new HashSet<string>(StringComparer.Ordinal)
        {
            "address", "article", "aside", "blockquote", "body", "caption", "center", "dd", "details", "dialog", "dir",
            "div", "dl", "dt", "fieldset", "figcaption", "figure", "footer", "form", "h1", "h2", "h3", "h4", "h5", "h6",
            "header", "hgroup", "hr", "html", "li", "main", "menu", "nav", "ol", "p", "pre", "section", "summary",
            "table", "tbody", "td", "tfoot", "th", "thead", "tr", "ul"
        };

        private static readonly HashSet<string> InlineTags = new HashSet<string>(StringComparer.Ordinal)
        {
            "a", "abbr", "b", "bdi", "bdo", "br", "cite", "code", "data", "del", "dfn", "em", "font", "i", "img", "ins",
            "kbd", "label", "mark", "q", "s", "samp", "small", "span", "strike", "strong", "sub", "sup", "time", "tt",
            "u", "var", "wbr"
        };

        private static readonly Regex Whitespace = new Regex(@"\s+", RegexOptions.Compiled);
        private static readonly Regex SpacesAroundBreak = new Regex(" *\u0001 *", RegexOptions.Compiled);
        private static readonly Regex OrderedListStart = new Regex(@"^(\d+)([.)])(\s|$)", RegexOptions.Compiled);
        private static readonly Regex CodeLanguageClass = new Regex(@"^language-|^lang-", RegexOptions.Compiled);
        // GitHub alert marker at the start of a blockquote, as escaped by EscapeText: \[!WARNING\]
        private static readonly Regex AlertMarker = new Regex(@"^\\?\[!(NOTE|TIP|IMPORTANT|WARNING|CAUTION)\\?\][ \t]*\n?", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly MarkdownConverterOptions _options;

        public MarkdownConverter()
            : this(new MarkdownConverterOptions())
        {
        }

        public MarkdownConverter(MarkdownConverterOptions options)
        {
            _options = options ?? new MarkdownConverterOptions();
        }

        public string Convert(string html)
        {
            return Convert(HtmlParser.Parse(html));
        }

        public string Convert(HtmlNode root)
        {
            if (root == null)
            {
                return string.Empty;
            }

            return JoinBlocks(RenderBlocks(root.Children)).Trim();
        }

        private enum BlockKind
        {
            Paragraph,
            List,
            Other
        }

        private sealed class Block
        {
            public Block(string text, BlockKind kind)
            {
                Text = text;
                Kind = kind;
            }

            public string Text { get; }

            public BlockKind Kind { get; }
        }

        // ---------------------------------------------------------------- blocks

        private List<Block> RenderBlocks(IEnumerable<HtmlNode> nodes)
        {
            var blocks = new List<Block>();
            var inline = new List<HtmlNode>();

            foreach (HtmlNode node in nodes)
            {
                if (IsSkipped(node))
                {
                    continue;
                }

                if (node.Kind == HtmlNodeKind.Element && IsBlock(node))
                {
                    FlushInline(inline, blocks);
                    RenderBlockElement(node, blocks);
                }
                else
                {
                    inline.Add(node);
                }
            }

            FlushInline(inline, blocks);
            return blocks;
        }

        private void FlushInline(List<HtmlNode> inline, List<Block> blocks)
        {
            if (inline.Count == 0)
            {
                return;
            }

            string text = FinishInline(RenderInline(inline));
            inline.Clear();
            if (text.Length > 0)
            {
                blocks.Add(new Block(EscapeLineStart(text), BlockKind.Paragraph));
            }
        }

        private void RenderBlockElement(HtmlNode node, List<Block> blocks)
        {
            if (IsRawMarkdown(node))
            {
                string raw = RawMarkdown(node);
                if (raw.Length > 0)
                {
                    blocks.Add(new Block(raw, BlockKind.Other));
                }

                return;
            }

            switch (node.Name)
            {
                case "h1":
                case "h2":
                case "h3":
                case "h4":
                case "h5":
                case "h6":
                    string heading = FinishSingleLine(RenderInline(node.Children));
                    if (heading.Length > 0)
                    {
                        int level = node.Name[1] - '0';
                        blocks.Add(new Block(new string('#', level) + " " + heading, BlockKind.Other));
                    }

                    return;

                case "hr":
                    blocks.Add(new Block("---", BlockKind.Other));
                    return;

                case "pre":
                    RenderCodeBlock(node, blocks);
                    return;

                case "blockquote":
                    string quote = RestoreAlertMarker(JoinBlocks(RenderBlocks(node.Children)));
                    if (quote.Length > 0)
                    {
                        string quoted = string.Join("\n", quote.Split('\n').Select(line => line.Length == 0 ? ">" : "> " + line));
                        blocks.Add(new Block(quoted, BlockKind.Other));
                    }

                    return;

                case "ul":
                case "ol":
                case "menu":
                case "dir":
                    string list = RenderList(node);
                    if (list.Length > 0)
                    {
                        blocks.Add(new Block(list, BlockKind.List));
                    }

                    return;

                case "table":
                    RenderTable(node, blocks);
                    return;

                case "dl":
                    RenderDefinitionList(node, blocks);
                    return;

                case "summary":
                    string summary = FinishSingleLine(RenderInline(node.Children));
                    if (summary.Length > 0)
                    {
                        blocks.Add(new Block("**" + summary + "**", BlockKind.Paragraph));
                    }

                    return;

                default:
                    blocks.AddRange(RenderBlocks(node.Children));
                    return;
            }
        }

        private void RenderCodeBlock(HtmlNode pre, List<Block> blocks)
        {
            var text = new StringBuilder();
            AppendRawText(pre, text);
            string code = text.ToString().Replace("\r\n", "\n");
            if (code.StartsWith("\n", StringComparison.Ordinal))
            {
                code = code.Substring(1);
            }

            code = code.TrimEnd('\n', ' ', '\t');
            if (code.Trim().Length == 0)
            {
                return;
            }

            string language = CodeLanguage(pre);
            if (language == null)
            {
                HtmlNode code0 = pre.Children.FirstOrDefault(c => c.IsElement("code"));
                if (code0 != null)
                {
                    language = CodeLanguage(code0);
                }
            }

            string fence = "```";
            while (code.Contains(fence))
            {
                fence += "`";
            }

            blocks.Add(new Block(fence + (language ?? string.Empty) + "\n" + code + "\n" + fence, BlockKind.Other));
        }

        private static string CodeLanguage(HtmlNode node)
        {
            string classes = node.GetAttribute("class");
            if (string.IsNullOrEmpty(classes))
            {
                return null;
            }

            foreach (string cls in classes.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (CodeLanguageClass.IsMatch(cls))
                {
                    return cls.Substring(cls.IndexOf('-') + 1);
                }
            }

            return null;
        }

        private string RenderList(HtmlNode list)
        {
            bool ordered = list.Name == "ol";
            int number = 1;
            int start;
            if (ordered && int.TryParse(list.GetAttribute("start"), NumberStyles.Integer, CultureInfo.InvariantCulture, out start))
            {
                number = start;
            }

            var output = new StringBuilder();
            int lastIndent = 0;

            foreach (HtmlNode child in list.Children)
            {
                if (IsSkipped(child))
                {
                    continue;
                }

                if (child.Kind == HtmlNodeKind.Text && child.Text.Trim().Length == 0)
                {
                    continue;
                }

                if (child.Kind == HtmlNodeKind.Element && (child.Name == "ul" || child.Name == "ol"))
                {
                    // Invalid but common: a nested list placed directly inside the parent list.
                    string nested = RenderList(child);
                    if (nested.Length > 0)
                    {
                        AppendIndented(output, nested, lastIndent);
                    }

                    continue;
                }

                IEnumerable<HtmlNode> content = child.IsElement("li") ? (IEnumerable<HtmlNode>)child.Children : new[] { child };
                List<Block> inner = RenderBlocks(content);
                if (inner.Count == 0)
                {
                    continue;
                }

                string marker = ordered ? number.ToString(CultureInfo.InvariantCulture) + ". " : "- ";
                if (ordered)
                {
                    number++;
                }

                string[] lines = JoinListItem(inner).Split('\n');
                if (output.Length > 0)
                {
                    output.Append('\n');
                }

                output.Append(marker).Append(lines[0]);
                string indent = new string(' ', marker.Length);
                for (int k = 1; k < lines.Length; k++)
                {
                    output.Append('\n');
                    if (lines[k].Length > 0)
                    {
                        output.Append(indent).Append(lines[k]);
                    }
                }

                lastIndent = marker.Length;
            }

            return output.ToString();
        }

        private static void AppendIndented(StringBuilder output, string text, int indent)
        {
            string pad = new string(' ', indent);
            foreach (string line in text.Split('\n'))
            {
                if (output.Length > 0)
                {
                    output.Append('\n');
                }

                if (line.Length > 0)
                {
                    output.Append(pad).Append(line);
                }
            }
        }

        private static string JoinListItem(List<Block> blocks)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < blocks.Count; i++)
            {
                if (i > 0)
                {
                    bool tight = blocks[i].Kind == BlockKind.List && blocks[i - 1].Kind != BlockKind.List;
                    sb.Append(tight ? "\n" : "\n\n");
                }

                sb.Append(blocks[i].Text);
            }

            return sb.ToString();
        }

        private void RenderTable(HtmlNode table, List<Block> blocks)
        {
            var rows = new List<List<HtmlNode>>();
            HtmlNode caption = null;

            foreach (HtmlNode child in table.Children)
            {
                if (child.Kind != HtmlNodeKind.Element || IsSkipped(child))
                {
                    continue;
                }

                if (child.Name == "tr")
                {
                    rows.Add(Cells(child));
                }
                else if (child.Name == "thead" || child.Name == "tbody" || child.Name == "tfoot")
                {
                    rows.AddRange(child.Children.Where(r => r.IsElement("tr") && !IsSkipped(r)).Select(Cells));
                }
                else if (child.Name == "caption")
                {
                    caption = child;
                }
            }

            rows.RemoveAll(r => r.Count == 0);
            if (rows.Count == 0)
            {
                return;
            }

            int columns = rows.Max(r => r.Count);

            // A one-column table is almost always used for layout, not data.
            if (columns == 1)
            {
                foreach (List<HtmlNode> row in rows)
                {
                    blocks.AddRange(RenderBlocks(row[0].Children));
                }

                return;
            }

            if (caption != null)
            {
                string captionText = FinishSingleLine(RenderInline(caption.Children));
                if (captionText.Length > 0)
                {
                    blocks.Add(new Block("**" + captionText + "**", BlockKind.Paragraph));
                }
            }

            var sb = new StringBuilder();
            AppendRow(sb, rows[0], columns);
            sb.Append('\n').Append('|');
            for (int c = 0; c < columns; c++)
            {
                sb.Append(" --- |");
            }

            for (int r = 1; r < rows.Count; r++)
            {
                sb.Append('\n');
                AppendRow(sb, rows[r], columns);
            }

            blocks.Add(new Block(sb.ToString(), BlockKind.Other));
        }

        private List<HtmlNode> Cells(HtmlNode row)
        {
            return row.Children.Where(c => (c.IsElement("td") || c.IsElement("th")) && !IsSkipped(c)).ToList();
        }

        private void AppendRow(StringBuilder sb, List<HtmlNode> cells, int columns)
        {
            sb.Append('|');
            for (int c = 0; c < columns; c++)
            {
                string text = c < cells.Count ? CellText(cells[c]) : string.Empty;
                sb.Append(' ').Append(text).Append(text.Length > 0 ? " |" : "|");
            }
        }

        private string CellText(HtmlNode cell)
        {
            string text = string.Join(" ", RenderBlocks(cell.Children).Select(b => b.Text));
            text = text.Replace("  \n", " ").Replace('\n', ' ').Replace("|", "\\|");
            return Whitespace.Replace(text, " ").Trim();
        }

        private void RenderDefinitionList(HtmlNode list, List<Block> blocks)
        {
            foreach (HtmlNode child in list.Children)
            {
                if (child.Kind != HtmlNodeKind.Element || IsSkipped(child))
                {
                    continue;
                }

                if (child.Name == "dt")
                {
                    string term = FinishSingleLine(RenderInline(child.Children));
                    if (term.Length > 0)
                    {
                        blocks.Add(new Block("**" + term + "**", BlockKind.Paragraph));
                    }
                }
                else
                {
                    blocks.AddRange(RenderBlocks(child.Children));
                }
            }
        }

        private static string JoinBlocks(List<Block> blocks)
        {
            return string.Join("\n\n", blocks.Select(b => b.Text));
        }

        // ---------------------------------------------------------------- inline

        private string RenderInline(IEnumerable<HtmlNode> nodes)
        {
            var sb = new StringBuilder();
            foreach (HtmlNode node in nodes)
            {
                AppendInline(node, sb);
            }

            return sb.ToString();
        }

        private void AppendInline(HtmlNode node, StringBuilder sb)
        {
            if (IsSkipped(node))
            {
                return;
            }

            if (node.Kind == HtmlNodeKind.Text)
            {
                sb.Append(EscapeText(Whitespace.Replace(node.Text ?? string.Empty, " ")));
                return;
            }

            if (node.Kind != HtmlNodeKind.Element)
            {
                return;
            }

            if (IsRawMarkdown(node))
            {
                sb.Append(' ').Append(RawMarkdown(node)).Append(' ');
                return;
            }

            switch (node.Name)
            {
                case "br":
                    sb.Append(HardBreak);
                    return;
                case "strong":
                case "b":
                    Wrap(node, "**", sb);
                    return;
                case "em":
                case "i":
                case "cite":
                case "dfn":
                case "var":
                    Wrap(node, "*", sb);
                    return;
                case "del":
                case "s":
                case "strike":
                    Wrap(node, "~~", sb);
                    return;
                case "code":
                case "kbd":
                case "samp":
                case "tt":
                    AppendInlineCode(node, sb);
                    return;
                case "a":
                    AppendLink(node, sb);
                    return;
                case "img":
                    AppendImage(node, sb);
                    return;
                case "q":
                    sb.Append('"').Append(RenderInline(node.Children)).Append('"');
                    return;
                default:
                    bool block = IsBlock(node);
                    if (block)
                    {
                        sb.Append(' ');
                    }

                    foreach (HtmlNode child in node.Children)
                    {
                        AppendInline(child, sb);
                    }

                    if (block)
                    {
                        sb.Append(' ');
                    }

                    return;
            }
        }

        private void Wrap(HtmlNode node, string marker, StringBuilder sb)
        {
            string inner = RenderInline(node.Children);
            string trimmed = inner.Trim(' ', HardBreak);
            if (trimmed.Length == 0)
            {
                sb.Append(inner);
                return;
            }

            if (trimmed.StartsWith(marker, StringComparison.Ordinal) && trimmed.EndsWith(marker, StringComparison.Ordinal) && trimmed.Length > marker.Length * 2)
            {
                // <b><strong>x</strong></b> would otherwise become ****x****.
                sb.Append(inner);
                return;
            }

            if (inner[0] == ' ')
            {
                sb.Append(' ');
            }

            sb.Append(marker).Append(trimmed).Append(marker);

            if (inner[inner.Length - 1] == ' ')
            {
                sb.Append(' ');
            }
        }

        private static void AppendInlineCode(HtmlNode node, StringBuilder sb)
        {
            var raw = new StringBuilder();
            AppendRawText(node, raw);
            string code = Whitespace.Replace(raw.ToString(), " ").Trim();
            if (code.Length == 0)
            {
                return;
            }

            if (code.Contains("`"))
            {
                sb.Append("`` ").Append(code).Append(" ``");
            }
            else
            {
                sb.Append('`').Append(code).Append('`');
            }
        }

        private void AppendLink(HtmlNode node, StringBuilder sb)
        {
            string inner = RenderInline(node.Children);
            string href = (node.GetAttribute("href") ?? string.Empty).Trim();

            if (href.Length == 0 || href.StartsWith("#", StringComparison.Ordinal) ||
                href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
            {
                sb.Append(inner);
                return;
            }

            string url = ResolveUrl(href);
            string text = inner.Replace(HardBreak, ' ').Trim();
            if (text.Length == 0)
            {
                text = EscapeText(node.GetAttribute("aria-label") ?? node.GetAttribute("title") ?? url);
            }

            if (inner.Length > 0 && inner[0] == ' ')
            {
                sb.Append(' ');
            }

            sb.Append('[').Append(Whitespace.Replace(text, " ")).Append("](").Append(EscapeUrl(url)).Append(')');

            if (inner.Length > 1 && inner[inner.Length - 1] == ' ')
            {
                sb.Append(' ');
            }
        }

        private void AppendImage(HtmlNode node, StringBuilder sb)
        {
            if (!_options.IncludeImages)
            {
                return;
            }

            string src = node.GetAttribute("src");
            if (string.IsNullOrWhiteSpace(src) || src.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                // Lazy-loading scripts often keep the real source in data-src.
                src = node.GetAttribute("data-src");
            }

            if (string.IsNullOrWhiteSpace(src) || src.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string alt = EscapeText(Whitespace.Replace(node.GetAttribute("alt") ?? string.Empty, " ").Trim());
            sb.Append("![").Append(alt).Append("](").Append(EscapeUrl(ResolveUrl(src.Trim()))).Append(')');
        }

        // ---------------------------------------------------------------- helpers

        private bool IsSkipped(HtmlNode node)
        {
            if (node.Kind == HtmlNodeKind.Comment)
            {
                return true;
            }

            if (node.Kind != HtmlNodeKind.Element)
            {
                return false;
            }

            if (IsRawMarkdown(node))
            {
                return false;
            }

            if (_options.SkipTags.Contains(node.Name))
            {
                return true;
            }

            if ((!string.IsNullOrEmpty(_options.IgnoreAttribute) && node.HasAttribute(_options.IgnoreAttribute)) ||
                node.HasAttribute("hidden") ||
                string.Equals(node.GetAttribute("aria-hidden"), "true", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string style = node.GetAttribute("style");
            if (!string.IsNullOrEmpty(style))
            {
                string compact = style.Replace(" ", string.Empty).ToLowerInvariant();
                if (compact.Contains("display:none") || compact.Contains("visibility:hidden"))
                {
                    return true;
                }
            }

            if (_options.IgnoreClasses.Count > 0)
            {
                string classes = node.GetAttribute("class");
                if (!string.IsNullOrEmpty(classes) &&
                    classes.Split(new[] { ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries).Any(_options.IgnoreClasses.Contains))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsBlock(HtmlNode node)
        {
            if (BlockTags.Contains(node.Name) || IsRawMarkdown(node))
            {
                return true;
            }

            if (InlineTags.Contains(node.Name))
            {
                return false;
            }

            // Unknown and custom elements are blocks only when they wrap block content.
            if (!node.ContainsBlock.HasValue)
            {
                node.ContainsBlock = node.Children.Any(c => c.Kind == HtmlNodeKind.Element && !IsSkipped(c) && IsBlock(c));
            }

            return node.ContainsBlock.Value;
        }

        private static bool IsRawMarkdown(HtmlNode node)
        {
            if (!node.IsElement("script"))
            {
                return false;
            }

            string type = node.GetAttribute("type");
            return string.Equals(type, RawMarkdownScriptType, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(type, "text/markdown", StringComparison.OrdinalIgnoreCase);
        }

        private static string RawMarkdown(HtmlNode node)
        {
            var raw = new StringBuilder();
            foreach (HtmlNode child in node.Children)
            {
                raw.Append(child.Text);
            }

            return Dedent(raw.ToString().Replace("<\\/script", "</script").Replace("\r\n", "\n"));
        }

        /// <summary>
        /// Removes the indentation shared by all non-empty lines. Razor output is usually
        /// indented, and four leading spaces would otherwise turn Markdown into a code block.
        /// </summary>
        internal static string Dedent(string text)
        {
            string[] lines = text.Split('\n');
            int min = int.MaxValue;
            foreach (string line in lines)
            {
                if (line.Trim().Length == 0)
                {
                    continue;
                }

                int indent = 0;
                while (indent < line.Length && (line[indent] == ' ' || line[indent] == '\t'))
                {
                    indent++;
                }

                min = Math.Min(min, indent);
            }

            if (min == int.MaxValue)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            foreach (string line in lines)
            {
                if (sb.Length > 0)
                {
                    sb.Append('\n');
                }

                sb.Append(line.Trim().Length == 0 ? string.Empty : line.Substring(min).TrimEnd());
            }

            return sb.ToString().Trim('\n');
        }

        private static void AppendRawText(HtmlNode node, StringBuilder sb)
        {
            foreach (HtmlNode child in node.Children)
            {
                if (child.Kind == HtmlNodeKind.Text)
                {
                    sb.Append(child.Text);
                }
                else if (child.IsElement("br"))
                {
                    sb.Append('\n');
                }
                else if (child.Kind == HtmlNodeKind.Element)
                {
                    AppendRawText(child, sb);
                }
            }
        }

        private static string FinishInline(string text)
        {
            text = Regex.Replace(text, "[ \t\r\n]+", " ");
            text = SpacesAroundBreak.Replace(text, HardBreak.ToString());
            text = text.Trim(' ', HardBreak);
            return text.Replace(HardBreak.ToString(), "  \n");
        }

        private static string FinishSingleLine(string text)
        {
            return Whitespace.Replace(text.Replace(HardBreak, ' '), " ").Trim();
        }

        /// <summary>
        /// Keeps GitHub alerts (<c>&gt; [!NOTE]</c>, <c>[!WARNING]</c>, ...) intact: unescapes the marker
        /// and puts it on its own line, as GitHub and most Markdown renderers expect.
        /// </summary>
        internal static string RestoreAlertMarker(string quote)
        {
            Match match = AlertMarker.Match(quote);
            if (!match.Success)
            {
                return quote;
            }

            string rest = quote.Substring(match.Length).TrimStart(' ');
            string marker = "[!" + match.Groups[1].Value.ToUpperInvariant() + "]";
            return rest.Length > 0 ? marker + "\n" + rest : marker;
        }

        internal static string EscapeText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var sb = new StringBuilder(text.Length + 8);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                switch (c)
                {
                    case '\\':
                    case '*':
                    case '`':
                    case '[':
                    case ']':
                        sb.Append('\\').Append(c);
                        break;
                    case '_':
                        bool inWord = i > 0 && i < text.Length - 1 && char.IsLetterOrDigit(text[i - 1]) && char.IsLetterOrDigit(text[i + 1]);
                        if (!inWord)
                        {
                            sb.Append('\\');
                        }

                        sb.Append(c);
                        break;
                    case '<':
                        if (i < text.Length - 1 && (char.IsLetter(text[i + 1]) || text[i + 1] == '/' || text[i + 1] == '!'))
                        {
                            sb.Append('\\');
                        }

                        sb.Append(c);
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }

            return sb.ToString();
        }

        private static string EscapeLineStart(string text)
        {
            if (text.StartsWith("#", StringComparison.Ordinal) || text.StartsWith(">", StringComparison.Ordinal) ||
                text.StartsWith("- ", StringComparison.Ordinal) || text.StartsWith("+ ", StringComparison.Ordinal) ||
                text == "-" || text == "+")
            {
                return "\\" + text;
            }

            return OrderedListStart.Replace(text, "$1\\$2$3", 1);
        }

        private string ResolveUrl(string url)
        {
            if (_options.BaseUri == null)
            {
                return url;
            }

            if (url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) || url.StartsWith("tel:", StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }

            if (url.StartsWith("//", StringComparison.Ordinal))
            {
                return _options.BaseUri.Scheme + ":" + url;
            }

            Uri absolute;
            if (url.IndexOf("://", StringComparison.Ordinal) > 0 && Uri.TryCreate(url, UriKind.Absolute, out absolute))
            {
                return url;
            }

            return Uri.TryCreate(_options.BaseUri, url, out absolute) ? absolute.AbsoluteUri : url;
        }

        private static string EscapeUrl(string url)
        {
            return url.Replace(" ", "%20").Replace("(", "%28").Replace(")", "%29");
        }
    }
}
