using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Data.Managers;
using Sitecore.Data.Templates;
using Sitecore.MarkdownForAI.Core;
using Sitecore.MarkdownForAI.Rendering;
using Sitecore.Web.UI.WebControls;

namespace Sitecore.MarkdownForAI.Sources
{
    /// <summary>
    /// Builds Markdown straight from item fields for structured content (articles, news,
    /// FAQs). No rendering runs, so headers, footers and sidebars never appear.
    /// <code>
    /// &lt;mapping template="{GUID}" title="Title" summary="Summary" body="Body|More Body" meta="Author|Published Date|Topics" inherited="true" /&gt;
    /// </code>
    /// </summary>
    public class FieldMappingSource : IMarkdownSource
    {
        private readonly List<FieldMapping> _mappings = new List<FieldMapping>();

        public IEnumerable<FieldMapping> Mappings
        {
            get { return _mappings; }
        }

        /// <summary>Called by the config factory (<c>hint="raw:AddMapping"</c>).</summary>
        public void AddMapping(XmlNode node)
        {
            FieldMapping mapping = FieldMapping.FromXml(node);
            if (mapping != null)
            {
                _mappings.Add(mapping);
            }
        }

        public void AddMapping(FieldMapping mapping)
        {
            if (mapping != null)
            {
                _mappings.Add(mapping);
            }
        }

        public bool CanHandle(Item item)
        {
            return Find(item) != null;
        }

        public MarkdownDocument TryCreate(Item item, Uri pageUrl)
        {
            FieldMapping mapping = Find(item);
            if (mapping == null)
            {
                return null;
            }

            MarkdownDocument document = ItemMetadata.CreateDocument(item, pageUrl);

            string title = Value(item, mapping.TitleField) ?? ItemMetadata.Title(item);
            string summary = Value(item, mapping.SummaryField) ?? ItemMetadata.Description(item);
            ICollection<string> frontMatter = MarkdownSettings.FrontMatterFields;
            if (frontMatter.Contains("title"))
            {
                document.Set("title", title);
            }

            if (frontMatter.Contains("description"))
            {
                document.Set("description", summary);
            }

            foreach (string field in mapping.MetaFields)
            {
                IList<string> values = ItemMetadata.FrontMatterValues(item, field);
                if (values.Count == 1)
                {
                    document.Set(ItemMetadata.Key(field), values[0]);
                }
                else if (values.Count > 1)
                {
                    document.Set(ItemMetadata.Key(field), values);
                }
            }

            MarkdownConverter converter = Converters.Create(pageUrl);
            var body = new StringBuilder();
            if (!string.IsNullOrEmpty(title))
            {
                body.Append("# ").Append(title).Append("\n\n");
            }
            if (!string.IsNullOrEmpty(summary) && mapping.SummaryInBody)
            {
                body.Append(summary).Append("\n\n");
            }

            foreach (string field in mapping.BodyFields)
            {
                if (item.Fields[field] == null || string.IsNullOrWhiteSpace(item[field]))
                {
                    continue;
                }

                // FieldRenderer runs renderField, which expands internal links and media URLs.
                string markdown = converter.Convert(FieldRenderer.Render(item, field));
                if (markdown.Length > 0)
                {
                    body.Append(markdown).Append("\n\n");
                }
            }

            document.Body = body.ToString();
            return document;
        }

        private FieldMapping Find(Item item)
        {
            if (item == null || _mappings.Count == 0)
            {
                return null;
            }

            FieldMapping exact = _mappings.FirstOrDefault(m => m.TemplateId == item.TemplateID);
            if (exact != null)
            {
                return exact;
            }

            Template template = TemplateManager.GetTemplate(item);
            return template == null ? null : _mappings.FirstOrDefault(m => m.Inherited && template.InheritsFrom(m.TemplateId));
        }

        private static string Value(Item item, string field)
        {
            if (string.IsNullOrEmpty(field))
            {
                return null;
            }

            string text = ItemMetadata.PlainText(item[field]);
            return text.Length == 0 ? null : text;
        }
    }

    /// <summary>One template-to-fields mapping.</summary>
    public class FieldMapping
    {
        public ID TemplateId { get; set; }

        public string TitleField { get; set; }

        public string SummaryField { get; set; }

        public bool SummaryInBody { get; set; }

        public IList<string> BodyFields { get; set; }

        public IList<string> MetaFields { get; set; }

        /// <summary>Also applies to templates that inherit from <see cref="TemplateId"/>.</summary>
        public bool Inherited { get; set; }

        public static FieldMapping FromXml(XmlNode node)
        {
            string template = Attribute(node, "template");
            if (!ID.IsID(template))
            {
                Diagnostics.Log.Warn("MarkdownForAI: field mapping ignored, template is not an ID: " + node.OuterXml, typeof(FieldMapping));
                return null;
            }

            return new FieldMapping
            {
                TemplateId = ID.Parse(template),
                TitleField = Attribute(node, "title"),
                SummaryField = Attribute(node, "summary"),
                SummaryInBody = !string.Equals(Attribute(node, "summaryInBody"), "false", StringComparison.OrdinalIgnoreCase),
                BodyFields = Split(Attribute(node, "body")),
                MetaFields = Split(Attribute(node, "meta")),
                Inherited = !string.Equals(Attribute(node, "inherited"), "false", StringComparison.OrdinalIgnoreCase)
            };
        }

        private static string Attribute(XmlNode node, string name)
        {
            XmlAttribute attribute = node.Attributes == null ? null : node.Attributes[name];
            return attribute == null ? null : attribute.Value;
        }

        private static IList<string> Split(string value)
        {
            return (value ?? string.Empty).Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(v => v.Trim()).Where(v => v.Length > 0).ToList();
        }
    }
}
