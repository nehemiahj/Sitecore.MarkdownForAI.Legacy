using System;
using System.Collections.Generic;
using System.Linq;
using Sitecore.ContentSearch;
using Sitecore.ContentSearch.SearchTypes;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Globalization;
using Sitecore.Links;
using Sitecore.MarkdownForAI.Caching;
using Sitecore.MarkdownForAI.Core;
using Sitecore.MarkdownForAI.Install;
using Sitecore.MarkdownForAI.Rendering;
using Sitecore.MarkdownForAI.Sources;
using Sitecore.MarkdownForAI.Web;
using Sitecore.Sites;

namespace Sitecore.MarkdownForAI.LlmsTxt
{
    /// <summary>
    /// Builds llms.txt and llms-full.txt for the context site from pages that inherit
    /// <c>_LlmsPage</c> and have <c>LlmsInclude</c> checked.
    /// </summary>
    public static class LlmsTxtService
    {
        public static CachedText Get(bool full)
        {
            SiteContext site = Context.Site;
            Database database = Context.Database;
            Language language = Context.Language;
            Uri baseUri = RequestUrls.SiteBaseUri();

            // Logged-in users may see items that anonymous visitors cannot; their build must not be shared.
            string cacheKey = Context.IsLoggedIn || !MarkdownSettings.IsCacheableDatabase(database.Name) ? null : string.Join("|", full ? "llms-full" : "llms", site.Name, database.Name, language.Name,
                baseUri == null ? string.Empty : baseUri.GetLeftPart(UriPartial.Authority).ToLowerInvariant());
            return MarkdownCache.GetOrCreate(cacheKey, MarkdownSettings.LlmsTxtCacheMinutes, () => Build(full, site, database, language));
        }

        private static string Build(bool full, SiteContext site, Database database, Language language)
        {
            Item start = database.GetItem(site.StartPath, language);
            string title = start == null ? site.Name : (Value(start, LlmsFields.Title) ?? site.Name);
            string summary = start == null ? null : Value(start, LlmsFields.Summary);

            List<LlmsEntry> entries = start == null ? new List<LlmsEntry>() : CollectItems(start).Select(i => ToEntry(i, full)).ToList();

            return full
                ? LlmsTxtWriter.WriteFull(title, summary, entries, MarkdownSettings.LlmsFullMaxChars)
                : LlmsTxtWriter.WriteIndex(title, summary, Notes(), entries);
        }

        private static IEnumerable<string> Notes()
        {
            string extension = MarkdownSettings.Extension;
            yield return "Each link points to the Markdown version of a page. Append `" + extension +
                         "` to any page URL on this site to get its Markdown version.";
        }

        private static IEnumerable<Item> CollectItems(Item start)
        {
            int max = MarkdownSettings.LlmsTxtMaxItems;
            IEnumerable<Item> items = string.Equals(MarkdownSettings.LlmsTxtSource, "index", StringComparison.OrdinalIgnoreCase)
                ? FromIndex(start, max)
                : FromTree(start, max);
            return items;
        }

        /// <summary>Depth-first walk in content tree order. Fine for small and medium sites.</summary>
        private static IEnumerable<Item> FromTree(Item start, int max)
        {
            var stack = new Stack<Item>();
            stack.Push(start);
            int count = 0;

            while (stack.Count > 0 && count < max)
            {
                Item item = stack.Pop();
                if (IsIncluded(item))
                {
                    count++;
                    yield return item;
                }

                Item[] children = item.GetChildren().ToArray();
                for (int i = children.Length - 1; i >= 0; i--)
                {
                    stack.Push(children[i]);
                }
            }
        }

        /// <summary>ContentSearch query; use for large sites. The index must contain the <c>llmsinclude</c> field.</summary>
        private static IEnumerable<Item> FromIndex(Item start, int max)
        {
            string indexName = MarkdownSettings.LlmsTxtIndexName;
            if (string.IsNullOrEmpty(indexName))
            {
                indexName = "sitecore_" + start.Database.Name.ToLowerInvariant() + "_index";
            }

            ISearchIndex index = ContentSearchManager.GetIndex(indexName);
            List<ID> ids;
            using (IProviderSearchContext context = index.CreateSearchContext())
            {
                string language = start.Language.Name;
                ID root = start.ID;
                ids = context.GetQueryable<LlmsSearchResultItem>()
                    .Where(x => x.Paths.Contains(root) && x.Language == language && x.LlmsInclude)
                    .Take(max)
                    .Select(x => x.ItemId)
                    .ToList();
            }

            return ids
                .Distinct()
                .Select(id => start.Database.GetItem(id, start.Language))
                .Where(IsIncluded)
                .OrderBy(i => i.Paths.FullPath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool IsIncluded(Item item)
        {
            return item != null && item.Versions.Count > 0 && item[LlmsFields.Include] == "1";
        }

        private static LlmsEntry ToEntry(Item item, bool full)
        {
            string pageUrl = RequestUrls.Absolute(LinkManager.GetItemUrl(item));
            var entry = new LlmsEntry
            {
                Title = Value(item, LlmsFields.Title) ?? ItemMetadata.Title(item),
                Summary = Value(item, LlmsFields.Summary) ?? ItemMetadata.Description(item),
                Section = Value(item, LlmsFields.Section),
                Optional = item[LlmsFields.Optional] == "1",
                Url = MarkdownPath.ToMarkdownUrl(pageUrl, MarkdownSettings.Extension)
            };

            if (full)
            {
                Uri uri;
                MarkdownDocument document = Uri.TryCreate(pageUrl, UriKind.Absolute, out uri)
                    ? MarkdownSourceList.Instance.TryCreate(item, uri)
                    : null;
                entry.Markdown = document == null ? null : StripLeadingTitle(document.Body, entry.Title);
            }

            return entry;
        }

        // llms-full.txt writes its own "# Title" line per page.
        private static string StripLeadingTitle(string body, string title)
        {
            string heading = "# " + title;
            body = (body ?? string.Empty).TrimStart();
            return body.StartsWith(heading, StringComparison.Ordinal) ? body.Substring(heading.Length).TrimStart() : body;
        }

        private static string Value(Item item, string field)
        {
            string text = ItemMetadata.PlainText(item[field]);
            return text.Length == 0 ? null : text;
        }
    }

    /// <summary>Search result type used by the index source.</summary>
    public class LlmsSearchResultItem : SearchResultItem
    {
        [IndexField("llmsinclude")]
        public bool LlmsInclude { get; set; }
    }
}
