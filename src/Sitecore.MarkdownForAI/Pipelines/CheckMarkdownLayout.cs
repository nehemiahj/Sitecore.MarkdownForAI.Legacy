using System;
using Sitecore.Data.Items;
using Sitecore.MarkdownForAI.Caching;
using Sitecore.MarkdownForAI.Core;
using Sitecore.MarkdownForAI.Sources;
using Sitecore.MarkdownForAI.Web;
using Sitecore.Pipelines.HttpRequest;

namespace Sitecore.MarkdownForAI.Pipelines
{
    /// <summary>
    /// httpRequestBegin, after LayoutResolver. Routes a Markdown request by layout type:
    /// <list type="bullet">
    /// <item>MVC (<c>.cshtml</c>): continues to MarkdownPageRenderer.</item>
    /// <item>WebForms (<c>.aspx</c>): a source such as a field mapping answers here, without rendering
    /// the page; otherwise the page renders and PrepareMarkdownPage converts it.</item>
    /// <item>Anything else, or WebForms with <c>MarkdownForAI.WebForms.Enabled</c> false: <c>/page.md</c>
    /// returns 404 and <c>?format=md</c> serves the normal page, rather than HTML under a Markdown URL.</item>
    /// </list>
    /// Pages without a layout are left to Sitecore.
    /// </summary>
    public class CheckMarkdownLayout : HttpRequestProcessor
    {
        public override void Process(HttpRequestArgs args)
        {
            if (!MarkdownRequest.IsActive || Context.Page == null)
            {
                return;
            }

            string layoutPath = Context.Page.FilePath;
            if (string.IsNullOrEmpty(layoutPath) || layoutPath.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (layoutPath.EndsWith(".aspx", StringComparison.OrdinalIgnoreCase) && MarkdownSettings.WebFormsEnabled)
            {
                AnswerFromSource(args);
                return;
            }

            if (MarkdownRequest.FromExtension)
            {
                ResponseWriter.NotFound(args);
            }
            else
            {
                MarkdownRequest.Cancel();
            }
        }

        private static void AnswerFromSource(HttpRequestArgs args)
        {
            Item item = Context.Item;
            Uri pageUrl = RequestUrls.CurrentPageUrl();
            MarkdownDocument document = MarkdownSourceList.Instance.TryCreate(item, pageUrl);
            if (document == null)
            {
                return;
            }

            CachedText entry = MarkdownCache.GetOrCreate(MarkdownCacheKeys.Page(item), MarkdownSettings.CacheMinutes, document.ToString);
            ResponseWriter.Cached(args, entry, ResponseWriter.MarkdownContentType, MarkdownSettings.HttpCacheMinutes, pageUrl);
        }
    }
}
