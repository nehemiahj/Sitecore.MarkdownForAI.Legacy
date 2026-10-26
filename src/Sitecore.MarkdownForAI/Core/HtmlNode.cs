using System;
using System.Collections.Generic;

namespace Sitecore.MarkdownForAI.Core
{
    /// <summary>Kind of node produced by <see cref="HtmlParser"/>.</summary>
    public enum HtmlNodeKind
    {
        Document,
        Element,
        Text,
        Comment
    }

    /// <summary>
    /// Minimal DOM node. The module ships its own tolerant parser so it does not depend on
    /// HtmlAgilityPack, whose version differs between Sitecore 8.0 and 10.5.
    /// </summary>
    public sealed class HtmlNode
    {
        private Dictionary<string, string> _attributes;

        public HtmlNode(HtmlNodeKind kind, string name = null, string text = null)
        {
            Kind = kind;
            Name = name;
            Text = text;
            Children = new List<HtmlNode>();
        }

        public HtmlNodeKind Kind { get; }

        /// <summary>Lower-case tag name for elements; null otherwise.</summary>
        public string Name { get; }

        /// <summary>Decoded text for text nodes, raw text for comments and raw-text elements.</summary>
        public string Text { get; set; }

        public HtmlNode Parent { get; internal set; }

        public List<HtmlNode> Children { get; }

        public IDictionary<string, string> Attributes
        {
            get
            {
                return _attributes ?? (_attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
            }
        }

        public bool IsElement(string name)
        {
            return Kind == HtmlNodeKind.Element && string.Equals(Name, name, StringComparison.Ordinal);
        }

        public string GetAttribute(string name)
        {
            string value;
            return _attributes != null && _attributes.TryGetValue(name, out value) ? value : null;
        }

        public bool HasAttribute(string name)
        {
            return _attributes != null && _attributes.ContainsKey(name);
        }

        public void AppendChild(HtmlNode child)
        {
            child.Parent = this;
            Children.Add(child);
        }

        // Cached result of the "contains block content" check used by the converter.
        internal bool? ContainsBlock { get; set; }
    }
}
