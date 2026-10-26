using System;
using System.Web;
using Sitecore.MarkdownForAI.Core;

namespace Sitecore.MarkdownForAI.Web
{
    /// <summary>Builds absolute URLs for the current request, honouring the scheme setting and X-Forwarded-Proto.</summary>
    public static class RequestUrls
    {
        /// <summary>URL of the HTML page the current request is about, without the Markdown extension or switch.</summary>
        public static Uri CurrentPageUrl()
        {
            Uri original = MarkdownRequest.OriginalUrl;
            if (original == null)
            {
                return null;
            }

            string path = original.AbsolutePath;
            string pagePath;
            if (MarkdownPath.TryGetPagePath(path, MarkdownSettings.Extension, out pagePath))
            {
                path = pagePath;
            }

            string query = MarkdownPath.RemoveQueryParameter(original.Query, MarkdownSettings.QueryStringKey);
            return Build(original, path, query);
        }

        /// <summary>Scheme and host of the current request, e.g. <c>https://www.example.com/</c>.</summary>
        public static Uri SiteBaseUri()
        {
            HttpContext context = HttpContext.Current;
            Uri source = MarkdownRequest.OriginalUrl ?? (context == null ? null : context.Request.Url);
            return source == null ? null : Build(source, "/", string.Empty);
        }

        /// <summary>Makes a link-manager URL absolute against the current host.</summary>
        public static string Absolute(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return url;
            }

            Uri baseUri = SiteBaseUri();
            if (url.StartsWith("//", StringComparison.Ordinal))
            {
                return (baseUri == null ? "https" : baseUri.Scheme) + ":" + url;
            }

            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                string scheme = PreferredScheme(null);
                if (!string.IsNullOrEmpty(scheme) && !url.StartsWith(scheme + "://", StringComparison.OrdinalIgnoreCase))
                {
                    url = scheme + url.Substring(url.IndexOf("://", StringComparison.Ordinal));
                }

                return url;
            }

            Uri absolute;
            return baseUri != null && Uri.TryCreate(baseUri, url, out absolute) ? absolute.AbsoluteUri : url;
        }

        private static Uri Build(Uri source, string path, string query)
        {
            var builder = new UriBuilder(source)
            {
                Path = Uri.UnescapeDataString(path),
                Query = (query ?? string.Empty).TrimStart('?'),
                Fragment = string.Empty
            };

            string scheme = PreferredScheme(source.Scheme);
            if (!string.Equals(scheme, builder.Scheme, StringComparison.OrdinalIgnoreCase))
            {
                builder.Scheme = scheme;
                builder.Port = -1;
            }
            else if (source.IsDefaultPort)
            {
                builder.Port = -1;
            }

            return builder.Uri;
        }

        private static string PreferredScheme(string fallback)
        {
            string configured = MarkdownSettings.Scheme;
            if (!string.IsNullOrWhiteSpace(configured))
            {
                return configured.Trim().ToLowerInvariant();
            }

            HttpContext context = HttpContext.Current;
            string forwarded = context == null ? null : context.Request.Headers["X-Forwarded-Proto"];
            if (!string.IsNullOrWhiteSpace(forwarded))
            {
                return forwarded.Split(',')[0].Trim().ToLowerInvariant();
            }

            return fallback;
        }
    }
}
