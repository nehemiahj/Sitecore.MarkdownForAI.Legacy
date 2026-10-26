using Sitecore.MarkdownForAI.Caching;
using Sitecore.MarkdownForAI.Core;
using Sitecore.MarkdownForAI.Web;
using Sitecore.Pipelines.HttpRequest;

namespace Sitecore.MarkdownForAI.Pipelines
{
    /// <summary>
    /// httpRequestBegin, before LayoutResolver (after every item resolver). When the Markdown of the
    /// context item is already cached, writes it straight away: no layout, MVC, rendering or
    /// conversion runs. On a miss the request continues and <c>MarkdownPageRenderer</c> builds and
    /// caches the page. Responding this early also keeps Sitecore's DisableBrowserCaching from
    /// overriding the HTTP cache headers.
    /// </summary>
    public class ServeCachedMarkdown : HttpRequestProcessor
    {
        public override void Process(HttpRequestArgs args)
        {
            if (!MarkdownRequest.IsActive || Context.Item == null)
            {
                return;
            }

            CachedText entry = MarkdownCache.Get(MarkdownCacheKeys.Page(Context.Item));
            if (entry != null)
            {
                ResponseWriter.Cached(args, entry, ResponseWriter.MarkdownContentType, MarkdownSettings.HttpCacheMinutes, RequestUrls.CurrentPageUrl());
            }
        }
    }
}
