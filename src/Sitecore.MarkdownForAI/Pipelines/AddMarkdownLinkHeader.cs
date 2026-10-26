using System;
using System.Linq;
using System.Web;
using Sitecore.MarkdownForAI.Core;
using Sitecore.MarkdownForAI.Web;
using Sitecore.Pipelines.HttpRequest;

namespace Sitecore.MarkdownForAI.Pipelines
{
    /// <summary>
    /// httpRequestBegin, after LayoutResolver. On HTML page requests, sends
    /// <c>Link: &lt;https://host/page.md&gt;; rel="alternate"; type="text/markdown"</c> so agents find the
    /// Markdown version from the response headers alone, without parsing the HTML or relying on
    /// <c>@Html.MarkdownAlternateLink()</c> in the layout. Only pages the module can convert get the
    /// header: allowed sites, normal page mode, and an MVC layout (or WebForms when enabled).
    /// With <c>MarkdownForAI.AcceptHeader</c> on, the same pages also get <c>Vary: Accept</c>, because
    /// their URL then serves HTML or Markdown depending on that header, and shared caches must keep both.
    /// </summary>
    public class AddMarkdownLinkHeader : HttpRequestProcessor
    {
        public override void Process(HttpRequestArgs args)
        {
            HttpContext context = HttpContext.Current;
            if (context == null || !MarkdownSettings.Enabled || MarkdownRequest.IsActive ||
                (!MarkdownSettings.LinkHeader && !MarkdownSettings.AcceptHeader))
            {
                return;
            }

            if (Context.Item == null || Context.Site == null || Context.Page == null || !Context.PageMode.IsNormal ||
                !MarkdownSettings.IsSiteAllowed(Context.Site.Name))
            {
                return;
            }

            string method = context.Request.HttpMethod;
            if (!string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase) && !string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            string layoutPath = Context.Page.FilePath;
            bool convertible = !string.IsNullOrEmpty(layoutPath) &&
                               (layoutPath.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase) ||
                                (MarkdownSettings.WebFormsEnabled && layoutPath.EndsWith(".aspx", StringComparison.OrdinalIgnoreCase)));
            if (!convertible)
            {
                return;
            }

            Uri page = RequestUrls.CurrentPageUrl();
            if (page == null || MarkdownSettings.ExcludedPathPrefixes.Any(prefix => page.AbsolutePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            if (MarkdownSettings.AcceptHeader)
            {
                context.Response.AppendHeader("Vary", "Accept");
            }

            if (MarkdownSettings.LinkHeader)
            {
                string markdownUrl = MarkdownPath.ToMarkdownUrl(page.GetLeftPart(UriPartial.Path), MarkdownSettings.Extension);
                context.Response.AppendHeader("Link", "<" + markdownUrl + ">; rel=\"alternate\"; type=\"text/markdown\"");
            }
        }
    }
}
