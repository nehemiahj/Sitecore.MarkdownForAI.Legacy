using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.MarkdownForAI.Core;

namespace Sitecore.MarkdownForAI.Rendering
{
    /// <summary>Reads titles, summaries and front matter values from items.</summary>
    public static class ItemMetadata
    {
        private static readonly Regex Tags = new Regex("<[^>]*>", RegexOptions.Compiled);
        private static readonly Regex Spaces = new Regex(@"\s+", RegexOptions.Compiled);

        /// <summary>
        /// Title fields, then the display name. Null for "*": a wildcard item serves many URLs, and no title
        /// is better than a wrong one (the page's own heading is still in the body).
        /// </summary>
        public static string Title(Item item)
        {
            string title = FirstValue(item, MarkdownSettings.TitleFields) ?? item.DisplayName;
            return title == "*" ? null : title;
        }

        public static string Description(Item item)
        {
            return FirstValue(item, MarkdownSettings.DescriptionFields);
        }

        /// <summary>
        /// Front matter shared by every source: title, description, url, language, last_updated, each only
        /// when listed in <c>MarkdownForAI.FrontMatterFields</c>.
        /// </summary>
        public static MarkdownDocument CreateDocument(Item item, Uri pageUrl)
        {
            var document = new MarkdownDocument();
            if (item == null)
            {
                return document;
            }

            ICollection<string> fields = MarkdownSettings.FrontMatterFields;
            if (fields.Contains("title"))
            {
                document.Set("title", Title(item));
            }

            if (fields.Contains("description"))
            {
                document.Set("description", Description(item));
            }

            if (fields.Contains("url"))
            {
                document.Set("url", pageUrl == null ? null : pageUrl.AbsoluteUri);
            }

            if (fields.Contains("language"))
            {
                document.Set("language", item.Language.Name);
            }

            DateTime updated = item.Statistics.Updated;
            if (updated > DateTime.MinValue && fields.Contains("last_updated"))
            {
                document.Set("last_updated", updated.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            }

            return document;
        }

        /// <summary>First non-empty field among <paramref name="fieldNames"/>, as plain text.</summary>
        public static string FirstValue(Item item, IEnumerable<string> fieldNames)
        {
            foreach (string name in fieldNames)
            {
                Field field = item.Fields[name];
                if (field == null)
                {
                    continue;
                }

                string text = PlainText(field.Value);
                if (text.Length > 0)
                {
                    return text;
                }
            }

            return null;
        }

        /// <summary>Formats a field for front matter: reference fields become display names, dates ISO, HTML plain text.</summary>
        public static IList<string> FrontMatterValues(Item item, string fieldName)
        {
            Field field = item.Fields[fieldName];
            if (field == null || string.IsNullOrWhiteSpace(field.Value))
            {
                return new List<string>();
            }

            CustomField custom = FieldTypeManager.GetField(field);

            var multilist = custom as MultilistField;
            if (multilist != null)
            {
                return multilist.GetItems().Select(i => i.DisplayName).ToList();
            }

            var reference = custom as ReferenceField;
            if (reference != null)
            {
                return reference.TargetItem == null ? new List<string>() : new List<string> { reference.TargetItem.DisplayName };
            }

            var lookup = custom as LookupField;
            if (lookup != null)
            {
                return lookup.TargetItem == null ? new List<string>() : new List<string> { lookup.TargetItem.DisplayName };
            }

            var date = custom as DateField;
            if (date != null)
            {
                return date.DateTime == DateTime.MinValue ? new List<string>() : new List<string> { date.DateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) };
            }

            var checkbox = custom as CheckboxField;
            if (checkbox != null)
            {
                return new List<string> { checkbox.Checked ? "true" : "false" };
            }

            return new List<string> { PlainText(field.Value) };
        }

        public static string PlainText(string html)
        {
            if (string.IsNullOrEmpty(html))
            {
                return string.Empty;
            }

            return Spaces.Replace(WebUtility.HtmlDecode(Tags.Replace(html, " ")), " ").Trim();
        }

        /// <summary>Front matter key for a field name: lower case, spaces to underscores.</summary>
        public static string Key(string fieldName)
        {
            return Spaces.Replace(fieldName.Trim().ToLowerInvariant(), "_");
        }
    }
}
