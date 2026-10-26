using System;
using System.Web;
using System.Web.Mvc;
using Sitecore.Data.Items;
using Sitecore.Links;
using Sitecore.MarkdownForAI.Core;
using Sitecore.MarkdownForAI.Web;

namespace Sitecore.MarkdownForAI.Html
{
    /// <summary>Razor helpers. Add <c>@using Sitecore.MarkdownForAI.Html</c> to the layout.</summary>
    public static class HtmlHelperExtensions
    {
        /// <summary>
        /// <c>&lt;link rel="alternate" type="text/markdown" href="..."&gt;</c> for the current page,
        /// so agents that load the HTML page can find the Markdown version. Empty when the
        /// module is off for this site or the page is in the Experience Editor.
        /// </summary>
        public static IHtmlString MarkdownAlternateLink(this HtmlHelper html)
        {
            string url = MarkdownUrl(html);
            return url == null
                ? MvcHtmlString.Empty
                : new MvcHtmlString("<link rel=\"alternate\" type=\"text/markdown\" href=\"" + HttpUtility.HtmlAttributeEncode(url) + "\" />");
        }

        /// <summary>Absolute Markdown URL of the current page, or null when the module is off for this request.</summary>
        public static string MarkdownUrl(this HtmlHelper html)
        {
            if (!IsAvailable())
            {
                return null;
            }

            Uri page = RequestUrls.CurrentPageUrl();
            return page == null ? null : MarkdownPath.ToMarkdownUrl(page.GetLeftPart(UriPartial.Path), MarkdownSettings.Extension);
        }

        /// <summary>Absolute Markdown URL of any item.</summary>
        public static string MarkdownUrl(this HtmlHelper html, Item item)
        {
            if (item == null || !IsAvailable())
            {
                return null;
            }

            return MarkdownPath.ToMarkdownUrl(RequestUrls.Absolute(LinkManager.GetItemUrl(item)), MarkdownSettings.Extension);
        }

        /// <summary>
        /// Converts an HTML fragment (e.g. a rendered rich text field) to Markdown. Meant for
        /// <c>*.md.cshtml</c> views: <c>@Html.ToMarkdown(Html.Sitecore().Field("Body").ToString())</c>.
        /// </summary>
        public static IHtmlString ToMarkdown(this HtmlHelper html, string fragment)
        {
            if (string.IsNullOrWhiteSpace(fragment))
            {
                return MvcHtmlString.Empty;
            }

            return new MvcHtmlString(Rendering.Converters.Create(RequestUrls.CurrentPageUrl()).Convert(fragment));
        }

        private static bool IsAvailable()
        {
            return MarkdownSettings.Enabled && Context.Site != null && Context.PageMode.IsNormal &&
                   MarkdownSettings.IsSiteAllowed(Context.Site.Name);
        }
    }
}
