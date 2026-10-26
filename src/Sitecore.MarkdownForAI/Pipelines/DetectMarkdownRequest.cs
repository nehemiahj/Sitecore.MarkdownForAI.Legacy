using System;
using System.IO;
using System.Linq;
using System.Web;
using Sitecore.Diagnostics;
using Sitecore.MarkdownForAI.Core;
using Sitecore.MarkdownForAI.Web;
using Sitecore.Pipelines.PreprocessRequest;

namespace Sitecore.MarkdownForAI.Pipelines
{
    /// <summary>
    /// preprocessRequest, before FilterUrlExtensions. Recognises <c>/page.md</c>,
    /// <c>?format=md</c>, <c>Accept: text/markdown</c>, <c>/llms.txt</c> and
    /// <c>/llms-full.txt</c> (unless a static file of that name exists, which IIS then serves). Extension requests are rewritten to an extensionless path so
    /// Sitecore's normal site, language and item resolution (including custom resolvers and
    /// wildcards) runs unchanged.
    /// </summary>
    public class DetectMarkdownRequest : PreprocessRequestProcessor
    {
        public override void Process(PreprocessRequestArgs args)
        {
            HttpContext context = HttpContext.Current;
            if (context == null || !MarkdownSettings.Enabled)
            {
                return;
            }

            try
            {
                HttpRequest request = context.Request;
                string path = request.FilePath ?? string.Empty;

                if (MarkdownSettings.ExcludedPathPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                {
                    return;
                }

                if (MarkdownSettings.LlmsTxtEnabled && string.Equals(path, "/llms.txt", StringComparison.OrdinalIgnoreCase) &&
                    !File.Exists(request.PhysicalPath))
                {
                    MarkdownRequest.Begin(context, MarkdownRequestKind.LlmsTxt, true);
                    context.RewritePath("/", string.Empty, string.Empty);
                    return;
                }

                if (MarkdownSettings.LlmsTxtEnabled && MarkdownSettings.LlmsFullTxtEnabled &&
                    string.Equals(path, "/llms-full.txt", StringComparison.OrdinalIgnoreCase) && !File.Exists(request.PhysicalPath))
                {
                    MarkdownRequest.Begin(context, MarkdownRequestKind.LlmsFullTxt, true);
                    context.RewritePath("/", string.Empty, string.Empty);
                    return;
                }

                string pagePath;
                if (MarkdownPath.TryGetPagePath(path, MarkdownSettings.Extension, out pagePath) && !File.Exists(request.PhysicalPath))
                {
                    MarkdownRequest.Begin(context, MarkdownRequestKind.Page, true);
                    context.RewritePath(pagePath, string.Empty, request.QueryString.ToString());
                    return;
                }

                string key = MarkdownSettings.QueryStringKey;
                if (!string.IsNullOrEmpty(key) &&
                    string.Equals(request.QueryString[key], MarkdownSettings.QueryStringValue, StringComparison.OrdinalIgnoreCase))
                {
                    MarkdownRequest.Begin(context, MarkdownRequestKind.Page, false);
                    return;
                }

                if (MarkdownSettings.AcceptHeader && request.AcceptTypes != null &&
                    request.AcceptTypes.Any(t => t.StartsWith(ResponseWriter.MarkdownContentType, StringComparison.OrdinalIgnoreCase)))
                {
                    MarkdownRequest.Begin(context, MarkdownRequestKind.Page, false);
                }
            }
            catch (Exception ex)
            {
                Log.Error("MarkdownForAI: failed to inspect request " + context.Request.RawUrl, ex, this);
                MarkdownRequest.Cancel();
            }
        }
    }
}
