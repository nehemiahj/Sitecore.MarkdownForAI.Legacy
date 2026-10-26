using System;
using System.Web;

namespace Sitecore.MarkdownForAI
{
    /// <summary>What kind of special response the current request asked for.</summary>
    public enum MarkdownRequestKind
    {
        None,
        Page,
        LlmsTxt,
        LlmsFullTxt
    }

    /// <summary>Per-request state, kept in <see cref="HttpContext.Items"/>.</summary>
    public static class MarkdownRequest
    {
        private const string KindKey = "MarkdownForAI.Kind";
        private const string OriginalUrlKey = "MarkdownForAI.OriginalUrl";
        private const string FromExtensionKey = "MarkdownForAI.FromExtension";

        public static MarkdownRequestKind Kind
        {
            get
            {
                HttpContext context = HttpContext.Current;
                object value = context == null ? null : context.Items[KindKey];
                return value is MarkdownRequestKind ? (MarkdownRequestKind)value : MarkdownRequestKind.None;
            }
        }

        /// <summary>True while a page is being rendered as Markdown.</summary>
        public static bool IsActive
        {
            get { return Kind == MarkdownRequestKind.Page; }
        }

        /// <summary>True when the request used the <c>.md</c> extension (as opposed to <c>?format=md</c>).</summary>
        public static bool FromExtension
        {
            get
            {
                HttpContext context = HttpContext.Current;
                return context != null && context.Items[FromExtensionKey] is bool && (bool)context.Items[FromExtensionKey];
            }
        }

        /// <summary>The URL the client asked for, before the module rewrote it.</summary>
        public static Uri OriginalUrl
        {
            get
            {
                HttpContext context = HttpContext.Current;
                return context == null ? null : (context.Items[OriginalUrlKey] as Uri ?? context.Request.Url);
            }
        }

        public static void Begin(HttpContext context, MarkdownRequestKind kind, bool fromExtension)
        {
            context.Items[KindKey] = kind;
            context.Items[FromExtensionKey] = fromExtension;
            context.Items[OriginalUrlKey] = context.Request.Url;
        }

        public static void Cancel()
        {
            HttpContext context = HttpContext.Current;
            if (context != null)
            {
                context.Items[KindKey] = MarkdownRequestKind.None;
            }
        }
    }
}
