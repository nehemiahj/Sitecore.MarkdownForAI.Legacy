using System;
using Sitecore.Diagnostics;
using Sitecore.MarkdownForAI.Caching;
using Sitecore.MarkdownForAI.Core;
using Sitecore.MarkdownForAI.LlmsTxt;
using Sitecore.MarkdownForAI.Web;
using Sitecore.Pipelines.HttpRequest;
using Sitecore.Sites;

namespace Sitecore.MarkdownForAI.Pipelines
{
    /// <summary>
    /// httpRequestBegin, before ItemResolver. Once the site, database and language are known,
    /// it checks the site is enabled and the request is in normal mode, and answers
    /// llms.txt / llms-full.txt directly.
    /// </summary>
    public class ValidateMarkdownRequest : HttpRequestProcessor
    {
        public override void Process(HttpRequestArgs args)
        {
            MarkdownRequestKind kind = MarkdownRequest.Kind;
            if (kind == MarkdownRequestKind.None)
            {
                return;
            }

            SiteContext site = Context.Site;
            bool allowed = site != null && Context.Database != null &&
                           MarkdownSettings.IsSiteAllowed(site.Name) && Context.PageMode.IsNormal;

            if (kind == MarkdownRequestKind.LlmsTxt || kind == MarkdownRequestKind.LlmsFullTxt)
            {
                if (!allowed)
                {
                    ResponseWriter.NotFound(args);
                    return;
                }

                try
                {
                    CachedText body = LlmsTxtService.Get(kind == MarkdownRequestKind.LlmsFullTxt);
                    ResponseWriter.Cached(args, body, "text/plain", MarkdownSettings.HttpCacheMinutes, null);
                }
                catch (Exception ex)
                {
                    Log.Error("MarkdownForAI: failed to build " + kind + " for site " + site.Name, ex, this);
                    ResponseWriter.Text(args, 500, "text/plain", "llms.txt is temporarily unavailable.", 0);
                }

                return;
            }

            if (!allowed)
            {
                if (MarkdownRequest.FromExtension)
                {
                    // The .md path was rewritten; do not serve the HTML page under a Markdown URL.
                    ResponseWriter.NotFound(args);
                }
                else
                {
                    MarkdownRequest.Cancel();
                }
            }
        }
    }
}
