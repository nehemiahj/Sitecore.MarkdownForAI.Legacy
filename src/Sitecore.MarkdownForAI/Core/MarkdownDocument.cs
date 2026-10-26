using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sitecore.MarkdownForAI.Core
{
    /// <summary>A Markdown body with optional YAML front matter.</summary>
    public sealed class MarkdownDocument
    {
        private readonly List<KeyValuePair<string, object>> _frontMatter = new List<KeyValuePair<string, object>>();

        public MarkdownDocument(string body = null)
        {
            Body = body ?? string.Empty;
        }

        public string Body { get; set; }

        public IEnumerable<KeyValuePair<string, object>> FrontMatter
        {
            get { return _frontMatter; }
        }

        /// <summary>Adds a scalar value. Empty values are ignored; an existing key is replaced.</summary>
        public MarkdownDocument Set(string key, string value)
        {
            return SetValue(key, string.IsNullOrWhiteSpace(value) ? null : value.Trim());
        }

        /// <summary>Adds a list value. Empty lists are ignored; an existing key is replaced.</summary>
        public MarkdownDocument Set(string key, IEnumerable<string> values)
        {
            List<string> list = values == null ? null : values.Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).ToList();
            return SetValue(key, list != null && list.Count > 0 ? list : null);
        }

        public string Get(string key)
        {
            KeyValuePair<string, object> pair = _frontMatter.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));
            return pair.Value as string;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            if (_frontMatter.Count > 0)
            {
                sb.Append("---\n");
                foreach (KeyValuePair<string, object> pair in _frontMatter)
                {
                    sb.Append(pair.Key).Append(": ");
                    var list = pair.Value as IList<string>;
                    if (list != null)
                    {
                        sb.Append('[').Append(string.Join(", ", list.Select(Quote))).Append(']');
                    }
                    else
                    {
                        sb.Append(Quote((string)pair.Value));
                    }

                    sb.Append('\n');
                }

                sb.Append("---\n\n");
            }

            sb.Append(Body.Trim()).Append('\n');
            return sb.ToString();
        }

        private MarkdownDocument SetValue(string key, object value)
        {
            _frontMatter.RemoveAll(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));
            if (value != null)
            {
                _frontMatter.Add(new KeyValuePair<string, object>(key, value));
            }

            return this;
        }

        internal static string Quote(string value)
        {
            string singleLine = string.Join(" ", (value ?? string.Empty).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)).Trim();
            return "\"" + singleLine.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }
    }
}
