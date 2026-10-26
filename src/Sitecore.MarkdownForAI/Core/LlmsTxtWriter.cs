using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sitecore.MarkdownForAI.Core
{
    /// <summary>One page listed in llms.txt.</summary>
    public sealed class LlmsEntry
    {
        public string Title { get; set; }

        /// <summary>Absolute URL of the Markdown version of the page.</summary>
        public string Url { get; set; }

        public string Summary { get; set; }

        public string Section { get; set; }

        /// <summary>Listed under the "Optional" section, which agents may skip.</summary>
        public bool Optional { get; set; }

        /// <summary>Full Markdown body, used only for llms-full.txt.</summary>
        public string Markdown { get; set; }
    }

    /// <summary>Writes llms.txt and llms-full.txt following the llmstxt.org format.</summary>
    public static class LlmsTxtWriter
    {
        public const string DefaultSection = "Pages";
        public const string OptionalSection = "Optional";

        public static string WriteIndex(string title, string summary, IEnumerable<string> notes, IEnumerable<LlmsEntry> entries)
        {
            var sb = new StringBuilder();
            WriteHeader(sb, title, summary, notes);

            List<LlmsEntry> list = entries.ToList();
            foreach (IGrouping<string, LlmsEntry> section in GroupBySection(list.Where(e => !e.Optional)))
            {
                WriteSection(sb, section.Key, section);
            }

            List<LlmsEntry> optional = list.Where(e => e.Optional).ToList();
            if (optional.Count > 0)
            {
                WriteSection(sb, OptionalSection, optional);
            }

            return sb.ToString().TrimEnd() + "\n";
        }

        /// <summary>Concatenates every page; stops before <paramref name="maxChars"/> and notes how many were left out.</summary>
        public static string WriteFull(string title, string summary, IEnumerable<LlmsEntry> entries, int maxChars)
        {
            var sb = new StringBuilder();
            WriteHeader(sb, title, summary, null);

            List<LlmsEntry> list = entries.ToList();
            int written = 0;
            foreach (LlmsEntry entry in list)
            {
                var page = new StringBuilder();
                page.Append("---\n\n");
                page.Append("# ").Append(OneLine(entry.Title)).Append("\n\n");
                page.Append("Source: ").Append(entry.Url).Append("\n\n");
                string body = string.IsNullOrWhiteSpace(entry.Markdown) ? OneLine(entry.Summary) : entry.Markdown.Trim();
                if (body.Length > 0)
                {
                    page.Append(body).Append("\n\n");
                }

                if (maxChars > 0 && sb.Length + page.Length > maxChars && written > 0)
                {
                    sb.Append("---\n\n");
                    sb.Append("Truncated: ").Append(list.Count - written).Append(" more pages are listed in llms.txt.\n");
                    return sb.ToString();
                }

                sb.Append(page);
                written++;
            }

            return sb.ToString().TrimEnd() + "\n";
        }

        private static void WriteHeader(StringBuilder sb, string title, string summary, IEnumerable<string> notes)
        {
            sb.Append("# ").Append(OneLine(title)).Append("\n\n");
            if (!string.IsNullOrWhiteSpace(summary))
            {
                sb.Append("> ").Append(OneLine(summary)).Append("\n\n");
            }

            if (notes != null)
            {
                foreach (string note in notes.Where(n => !string.IsNullOrWhiteSpace(n)))
                {
                    sb.Append(note.Trim()).Append("\n\n");
                }
            }
        }

        private static IEnumerable<IGrouping<string, LlmsEntry>> GroupBySection(IEnumerable<LlmsEntry> entries)
        {
            // GroupBy keeps the order in which sections first appear.
            return entries.GroupBy(e => string.IsNullOrWhiteSpace(e.Section) ? DefaultSection : e.Section.Trim(), StringComparer.OrdinalIgnoreCase);
        }

        private static void WriteSection(StringBuilder sb, string name, IEnumerable<LlmsEntry> entries)
        {
            sb.Append("## ").Append(OneLine(name)).Append("\n\n");
            foreach (LlmsEntry entry in entries)
            {
                sb.Append("- [").Append(OneLine(entry.Title).Replace("[", "\\[").Replace("]", "\\]")).Append("](").Append(entry.Url).Append(')');
                string summary = OneLine(entry.Summary);
                if (summary.Length > 0)
                {
                    sb.Append(": ").Append(summary);
                }

                sb.Append('\n');
            }

            sb.Append('\n');
        }

        private static string OneLine(string value)
        {
            return string.Join(" ", (value ?? string.Empty).Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
