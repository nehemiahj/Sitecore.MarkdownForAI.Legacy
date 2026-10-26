using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Sitecore.MarkdownForAI.Core
{
    /// <summary>
    /// Small, forgiving HTML parser. It is not a full HTML5 tree builder; it handles what
    /// CMS output contains in practice: unclosed p/li/td, void elements, self-closing XHTML
    /// tags, comments, entities and raw-text elements such as script and style.
    /// </summary>
    public static class HtmlParser
    {
        private static readonly HashSet<string> VoidElements = new HashSet<string>(StringComparer.Ordinal)
        {
            "area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "param", "source", "track", "wbr"
        };

        private static readonly HashSet<string> RawTextElements = new HashSet<string>(StringComparer.Ordinal)
        {
            "script", "style", "textarea", "title", "xmp", "noscript", "template"
        };

        // Start tags that implicitly close an open <p>.
        private static readonly HashSet<string> ClosesParagraph = new HashSet<string>(StringComparer.Ordinal)
        {
            "address", "article", "aside", "blockquote", "details", "div", "dl", "fieldset", "figcaption", "figure",
            "footer", "form", "h1", "h2", "h3", "h4", "h5", "h6", "header", "hr", "main", "nav", "ol", "p", "pre",
            "section", "table", "ul"
        };

        // Elements that stop the search for an open <p> to close.
        private static readonly HashSet<string> ParagraphScope = new HashSet<string>(StringComparer.Ordinal)
        {
            "div", "li", "td", "th", "blockquote", "section", "article", "aside", "main", "header", "footer", "body",
            "html", "dd", "dt", "figure", "button", "table", "nav", "form"
        };

        private static readonly HashSet<string> ListScope = new HashSet<string>(StringComparer.Ordinal) { "ul", "ol", "menu" };
        private static readonly HashSet<string> DefinitionScope = new HashSet<string>(StringComparer.Ordinal) { "dl" };
        private static readonly HashSet<string> RowScope = new HashSet<string>(StringComparer.Ordinal) { "table", "thead", "tbody", "tfoot" };
        private static readonly HashSet<string> CellScope = new HashSet<string>(StringComparer.Ordinal) { "tr", "table" };
        private static readonly HashSet<string> TableScope = new HashSet<string>(StringComparer.Ordinal) { "table" };
        private static readonly HashSet<string> SelectScope = new HashSet<string>(StringComparer.Ordinal) { "select", "datalist" };

        public static HtmlNode Parse(string html)
        {
            var document = new HtmlNode(HtmlNodeKind.Document);
            if (string.IsNullOrEmpty(html))
            {
                return document;
            }

            var stack = new List<HtmlNode> { document };
            var text = new StringBuilder();
            int i = 0;
            int length = html.Length;

            while (i < length)
            {
                char c = html[i];
                if (c == '<' && i + 1 < length)
                {
                    char next = html[i + 1];

                    if (next == '!')
                    {
                        FlushText(stack, text);
                        if (StartsWith(html, i, "<!--"))
                        {
                            int end = html.IndexOf("-->", i + 4, StringComparison.Ordinal);
                            string comment = end < 0 ? html.Substring(i + 4) : html.Substring(i + 4, end - i - 4);
                            Current(stack).AppendChild(new HtmlNode(HtmlNodeKind.Comment, text: comment));
                            i = end < 0 ? length : end + 3;
                        }
                        else if (StartsWith(html, i, "<![CDATA["))
                        {
                            int end = html.IndexOf("]]>", i + 9, StringComparison.Ordinal);
                            string data = end < 0 ? html.Substring(i + 9) : html.Substring(i + 9, end - i - 9);
                            Current(stack).AppendChild(new HtmlNode(HtmlNodeKind.Text, text: data));
                            i = end < 0 ? length : end + 3;
                        }
                        else
                        {
                            int end = html.IndexOf('>', i);
                            i = end < 0 ? length : end + 1;
                        }

                        continue;
                    }

                    if (next == '?')
                    {
                        FlushText(stack, text);
                        int end = html.IndexOf('>', i);
                        i = end < 0 ? length : end + 1;
                        continue;
                    }

                    if (next == '/')
                    {
                        int nameStart = i + 2;
                        int nameEnd = ReadName(html, nameStart);
                        if (nameEnd > nameStart)
                        {
                            FlushText(stack, text);
                            string name = html.Substring(nameStart, nameEnd - nameStart).ToLowerInvariant();
                            int end = html.IndexOf('>', nameEnd);
                            i = end < 0 ? length : end + 1;
                            CloseElement(stack, name);
                            continue;
                        }
                    }
                    else if (IsAsciiLetter(next))
                    {
                        FlushText(stack, text);
                        i = ReadStartTag(html, i, stack);
                        continue;
                    }
                }

                text.Append(c);
                i++;
            }

            FlushText(stack, text);
            return document;
        }

        private static int ReadStartTag(string html, int start, List<HtmlNode> stack)
        {
            int length = html.Length;
            int nameStart = start + 1;
            int nameEnd = ReadName(html, nameStart);
            string name = html.Substring(nameStart, nameEnd - nameStart).ToLowerInvariant();
            var element = new HtmlNode(HtmlNodeKind.Element, name);
            bool selfClosing = false;
            int i = nameEnd;

            while (i < length)
            {
                char c = html[i];
                if (char.IsWhiteSpace(c))
                {
                    i++;
                    continue;
                }

                if (c == '>')
                {
                    i++;
                    break;
                }

                if (c == '/')
                {
                    if (i + 1 < length && html[i + 1] == '>')
                    {
                        selfClosing = true;
                        i += 2;
                        break;
                    }

                    i++;
                    continue;
                }

                int attrStart = i;
                while (i < length && !char.IsWhiteSpace(html[i]) && html[i] != '=' && html[i] != '>' && html[i] != '/')
                {
                    i++;
                }

                string attrName = html.Substring(attrStart, i - attrStart).ToLowerInvariant();
                while (i < length && char.IsWhiteSpace(html[i]))
                {
                    i++;
                }

                string value = string.Empty;
                if (i < length && html[i] == '=')
                {
                    i++;
                    while (i < length && char.IsWhiteSpace(html[i]))
                    {
                        i++;
                    }

                    if (i < length && (html[i] == '"' || html[i] == '\''))
                    {
                        char quote = html[i];
                        int valueEnd = html.IndexOf(quote, i + 1);
                        if (valueEnd < 0)
                        {
                            valueEnd = length;
                        }

                        value = html.Substring(i + 1, valueEnd - i - 1);
                        i = Math.Min(length, valueEnd + 1);
                    }
                    else
                    {
                        int valueStart = i;
                        while (i < length && !char.IsWhiteSpace(html[i]) && html[i] != '>')
                        {
                            i++;
                        }

                        value = html.Substring(valueStart, i - valueStart);
                    }
                }

                if (attrName.Length > 0 && !element.Attributes.ContainsKey(attrName))
                {
                    element.Attributes[attrName] = WebUtility.HtmlDecode(value);
                }
            }

            OpenElement(stack, element);

            if (VoidElements.Contains(name) || selfClosing)
            {
                stack.RemoveAt(stack.Count - 1);
                return i;
            }

            if (RawTextElements.Contains(name))
            {
                int close = html.IndexOf("</" + name, i, StringComparison.OrdinalIgnoreCase);
                int contentEnd = close < 0 ? length : close;
                string raw = html.Substring(i, contentEnd - i);
                if (raw.Length > 0)
                {
                    bool decode = name == "textarea" || name == "title";
                    element.AppendChild(new HtmlNode(HtmlNodeKind.Text, text: decode ? WebUtility.HtmlDecode(raw) : raw));
                }

                stack.RemoveAt(stack.Count - 1);
                if (close < 0)
                {
                    return length;
                }

                int end = html.IndexOf('>', close);
                return end < 0 ? length : end + 1;
            }

            return i;
        }

        private static void OpenElement(List<HtmlNode> stack, HtmlNode element)
        {
            string name = element.Name;

            if (ClosesParagraph.Contains(name))
            {
                CloseOpen(stack, "p", ParagraphScope);
            }

            switch (name)
            {
                case "li":
                    CloseOpen(stack, "li", ListScope);
                    break;
                case "dt":
                case "dd":
                    CloseOpen(stack, "dt", DefinitionScope);
                    CloseOpen(stack, "dd", DefinitionScope);
                    break;
                case "tr":
                    CloseOpen(stack, "tr", RowScope);
                    break;
                case "td":
                case "th":
                    CloseOpen(stack, "td", CellScope);
                    CloseOpen(stack, "th", CellScope);
                    break;
                case "thead":
                case "tbody":
                case "tfoot":
                    CloseOpen(stack, "thead", TableScope);
                    CloseOpen(stack, "tbody", TableScope);
                    CloseOpen(stack, "tfoot", TableScope);
                    break;
                case "option":
                    CloseOpen(stack, "option", SelectScope);
                    break;
            }

            Current(stack).AppendChild(element);
            stack.Add(element);
        }

        private static void CloseOpen(List<HtmlNode> stack, string name, HashSet<string> scope)
        {
            for (int k = stack.Count - 1; k > 0; k--)
            {
                HtmlNode node = stack[k];
                if (node.Name == name)
                {
                    stack.RemoveRange(k, stack.Count - k);
                    return;
                }

                if (scope.Contains(node.Name))
                {
                    return;
                }
            }
        }

        private static void CloseElement(List<HtmlNode> stack, string name)
        {
            if (VoidElements.Contains(name))
            {
                return;
            }

            for (int k = stack.Count - 1; k > 0; k--)
            {
                if (stack[k].Name == name)
                {
                    stack.RemoveRange(k, stack.Count - k);
                    return;
                }
            }
        }

        private static void FlushText(List<HtmlNode> stack, StringBuilder text)
        {
            if (text.Length == 0)
            {
                return;
            }

            Current(stack).AppendChild(new HtmlNode(HtmlNodeKind.Text, text: WebUtility.HtmlDecode(text.ToString())));
            text.Clear();
        }

        private static HtmlNode Current(List<HtmlNode> stack)
        {
            return stack[stack.Count - 1];
        }

        private static int ReadName(string html, int start)
        {
            int i = start;
            while (i < html.Length && (char.IsLetterOrDigit(html[i]) || html[i] == '-' || html[i] == ':' || html[i] == '_'))
            {
                i++;
            }

            return i;
        }

        private static bool IsAsciiLetter(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        }

        private static bool StartsWith(string html, int index, string value)
        {
            return index + value.Length <= html.Length && string.CompareOrdinal(html, index, value, 0, value.Length) == 0;
        }
    }
}
