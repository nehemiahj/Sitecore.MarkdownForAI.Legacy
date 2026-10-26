using System;
using Sitecore.Data.Items;
using Sitecore.MarkdownForAI.Web;

namespace Sitecore.MarkdownForAI.Caching
{
    /// <summary>Cache keys for generated Markdown pages.</summary>
    public static class MarkdownCacheKeys
    {
        /// <summary>
        /// Key for the Markdown of <paramref name="item"/> in the current request, or null when it must not
        /// be cached (caching off, a logged-in user who may see personal content, or a database in
        /// <c>MarkdownForAI.NoCacheDatabases</c> such as master). The host is part of
        /// the key because the output contains absolute URLs, and the path because one item can serve
        /// many URLs (wildcard items). The query string is ignored unless
        /// <c>MarkdownForAI.CacheVaryByQueryString</c> is true, so tracking parameters such as
        /// <c>?utm_source=</c> cannot fill the cache.
        /// </summary>
        public static string Page(Item item)
        {
            if (item == null || MarkdownSettings.CacheMinutes <= 0 || Context.IsLoggedIn || !MarkdownSettings.IsCacheableDatabase(item.Database.Name))
            {
                return null;
            }

            Uri baseUri = RequestUrls.SiteBaseUri();
            Uri pageUrl = RequestUrls.CurrentPageUrl();

            return string.Join("|",
                "page",
                Context.Site == null ? string.Empty : Context.Site.Name,
                item.Database.Name,
                item.Language.Name,
                item.ID.ToString(),
                item.Version.Number.ToString(),
                Context.Device == null ? string.Empty : Context.Device.ID.ToString(),
                baseUri == null ? string.Empty : baseUri.GetLeftPart(UriPartial.Authority).ToLowerInvariant(),
                pageUrl == null ? string.Empty : pageUrl.AbsolutePath.ToLowerInvariant(),
                pageUrl == null || !MarkdownSettings.CacheVaryByQueryString ? string.Empty : pageUrl.Query);
        }
    }
}
