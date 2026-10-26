using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using Sitecore.Data.Items;
using Sitecore.MarkdownForAI.Caching;
using Sitecore.MarkdownForAI.Core;
using Sitecore.MarkdownForAI.Sources;
using Sitecore.MarkdownForAI.Web;
using Sitecore.Mvc.Pipelines;
using Sitecore.Mvc.Pipelines.Response.RenderPlaceholder;
using Sitecore.Mvc.Presentation;

namespace Sitecore.MarkdownForAI.Rendering
{
    /// <summary>
    /// Replaces the page layout on Markdown requests. It asks the configured sources first
    /// (for example a field mapping); otherwise it renders the page's top-level placeholders
    /// through the normal mvc.renderPlaceholder pipeline, where <c>ApplyMarkdownMode</c>
    /// filters the components, and converts the HTML to Markdown.
    /// </summary>
    public class MarkdownPageRenderer : Renderer
    {
        private readonly Mvc.Presentation.Rendering _pageRendering;
        private readonly PageDefinition _pageDefinition;

        public MarkdownPageRenderer(Mvc.Presentation.Rendering pageRendering, PageDefinition pageDefinition)
        {
            _pageRendering = pageRendering;
            _pageDefinition = pageDefinition;
        }

        public override void Render(TextWriter writer)
        {
            Item item = Context.Item ?? (PageContext.CurrentOrNull == null ? null : PageContext.Current.Item);
            Uri pageUrl = RequestUrls.CurrentPageUrl();

            // Usually a miss: ServeCachedMarkdown answers hits in httpRequestBegin. Concurrent misses for
            // the same page wait for one build instead of each rendering and converting it.
            string markdown = MarkdownCache.GetOrCreate(MarkdownCacheKeys.Page(item), MarkdownSettings.CacheMinutes, () =>
            {
                MarkdownDocument document = MarkdownSourceList.Instance.TryCreate(item, pageUrl) ?? FromRenderings(item, pageUrl);
                return document.ToString();
            }).Body;

            HttpContext context = HttpContext.Current;
            if (context != null)
            {
                ResponseWriter.ApplyMarkdownHeaders(context.Response, pageUrl);
            }

            writer.Write(markdown);
        }

        private MarkdownDocument FromRenderings(Item item, Uri pageUrl)
        {
            var html = new StringBuilder();
            foreach (string key in PlaceholderKeys())
            {
                using (var placeholderWriter = new StringWriter())
                {
                    PipelineService.Get().RunPipeline("mvc.renderPlaceholder", new RenderPlaceholderArgs(key, placeholderWriter, _pageRendering));
                    html.Append(placeholderWriter).Append('\n');
                }
            }

            MarkdownDocument document = ItemMetadata.CreateDocument(item, pageUrl);
            document.Body = Converters.Create(pageUrl).Convert(html.ToString());
            return document;
        }

        /// <summary>Configured placeholders, or every top-level placeholder used on the page, minus excluded ones.</summary>
        private IEnumerable<string> PlaceholderKeys()
        {
            ICollection<string> excluded = MarkdownSettings.ExcludedPlaceholders;
            ICollection<string> configured = MarkdownSettings.Placeholders;

            IEnumerable<string> keys = configured.Count > 0
                ? configured
                : (_pageDefinition == null ? Enumerable.Empty<Mvc.Presentation.Rendering>() : _pageDefinition.Renderings)
                    .Where(r => !string.Equals(r.RenderingType, "Layout", StringComparison.OrdinalIgnoreCase))
                    .Select(r => PlaceholderMatcher.TopLevelKey(r.Placeholder))
                    .Where(k => !string.IsNullOrEmpty(k));

            return keys
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Where(k => !excluded.Any(x => PlaceholderMatcher.SegmentMatches(k, x)))
                .ToList();
        }
    }
}
