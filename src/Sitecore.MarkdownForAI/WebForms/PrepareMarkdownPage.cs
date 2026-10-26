using System;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.UI;
using Sitecore.Data.Items;
using Sitecore.MarkdownForAI.Caching;
using Sitecore.MarkdownForAI.Core;
using Sitecore.MarkdownForAI.Rendering;
using Sitecore.MarkdownForAI.Web;
using Sitecore.Pipelines.RenderLayout;
using SitecorePlaceholder = Sitecore.Web.UI.WebControls.Placeholder;

namespace Sitecore.MarkdownForAI.WebForms
{
    /// <summary>
    /// renderLayout, last processor. Turns a WebForms page into Markdown: the page renders as usual
    /// (so server forms and sublayouts behave), each top-level Sitecore placeholder is wrapped in
    /// comment markers, and a response filter converts only the marked HTML. Markup typed straight
    /// into the .aspx layout (header, footer, scripts) is left out, as the layout of an MVC page is
    /// never rendered. A layout without placeholders falls back to the page body.
    /// </summary>
    public class PrepareMarkdownPage
    {
        public void Process(RenderLayoutArgs args)
        {
            HttpContext context = HttpContext.Current;
            Page page = Context.Page == null ? null : Context.Page.Page;
            if (!MarkdownRequest.IsActive || context == null || page == null)
            {
                return;
            }

            string token = Guid.NewGuid().ToString("N");
            MarkPlaceholders(page, token);

            Item item = Context.Item;
            Uri pageUrl = RequestUrls.CurrentPageUrl();
            string cacheKey = MarkdownCacheKeys.Page(item);
            HttpResponse response = context.Response;

            response.Filter = new MarkdownResponseFilter(response.Filter, response, html =>
                MarkdownCache.GetOrCreate(cacheKey, MarkdownSettings.CacheMinutes, () => Convert(html, token, item, pageUrl)).Body);

            // After the page's own code has run, so the Markdown headers win.
            page.PreRenderComplete += (sender, e) =>
            {
                response.ContentEncoding = Encoding.UTF8;
                ResponseWriter.ApplyMarkdownHeaders(response, pageUrl);
            };
        }

        private static string Convert(string html, string token, Item item, Uri pageUrl)
        {
            string fragment = MarkedRegions.Extract(html, token) ?? MarkedRegions.BodyWithoutForms(html);
            MarkdownDocument document = ItemMetadata.CreateDocument(item, pageUrl);
            document.Body = Converters.Create(pageUrl).Convert(fragment);
            return document.ToString();
        }

        /// <summary>Wraps every top-level Sitecore placeholder that is not excluded; nested ones are inside it.</summary>
        private static void MarkPlaceholders(Control control, string token)
        {
            foreach (Control child in control.Controls.Cast<Control>().ToList())
            {
                var placeholder = child as SitecorePlaceholder;
                if (placeholder == null)
                {
                    MarkPlaceholders(child, token);
                }
                else if (IsRendered(placeholder.Key))
                {
                    Wrap(placeholder, token);
                }
            }
        }

        private static bool IsRendered(string key)
        {
            if (PlaceholderMatcher.MatchesAny(key, MarkdownSettings.ExcludedPlaceholders))
            {
                return false;
            }

            var configured = MarkdownSettings.Placeholders;
            return configured.Count == 0 || configured.Any(k => PlaceholderMatcher.SegmentMatches(key, k));
        }

        private static void Wrap(Control placeholder, string token)
        {
            Control parent = placeholder.Parent;
            try
            {
                int index = parent.Controls.IndexOf(placeholder);
                parent.Controls.AddAt(index, new LiteralControl(MarkedRegions.BeginMarker(token)));
                parent.Controls.AddAt(index + 2, new LiteralControl(MarkedRegions.EndMarker(token)));
            }
            catch (HttpException)
            {
                // The parent contains <%= %> code blocks, so its Controls collection is read-only.
                // Mark the placeholder's own content instead.
                placeholder.Controls.AddAt(0, new LiteralControl(MarkedRegions.BeginMarker(token)));
                placeholder.Controls.Add(new LiteralControl(MarkedRegions.EndMarker(token)));
            }
        }
    }
}
