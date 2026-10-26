using System;
using System.Web;
using Sitecore.MarkdownForAI.Core;
using Sitecore.Pipelines.HttpRequest;

namespace Sitecore.MarkdownForAI.Web
{
    /// <summary>Writes complete responses from inside httpRequestBegin and sets Markdown headers.</summary>
    public static class ResponseWriter
    {
        public const string MarkdownContentType = "text/markdown";

        public static void Text(HttpRequestArgs args, int statusCode, string contentType, string body, int cacheMinutes)
        {
            HttpResponse response = Begin(statusCode, contentType);

            if (statusCode == 200 && cacheMinutes > 0)
            {
                response.Cache.SetCacheability(HttpCacheability.Public);
                response.Cache.SetMaxAge(TimeSpan.FromMinutes(cacheMinutes));
            }
            else
            {
                response.Cache.SetCacheability(HttpCacheability.NoCache);
            }

            response.Write(body ?? string.Empty);
            End(args);
        }

        /// <summary>
        /// Writes a cached body with an ETag and Last-Modified, answering 304 Not Modified when the
        /// client already has it. A status set earlier in the request (such as 404 from a not-found
        /// resolver) is kept. Shared caches (CDN, proxies) may keep it for
        /// <paramref name="httpCacheMinutes"/> unless the visitor is logged in or the content comes from a
        /// database in <c>MarkdownForAI.NoCacheDatabases</c>.
        /// </summary>
        public static void Cached(HttpRequestArgs args, CachedText entry, string contentType, int httpCacheMinutes, Uri canonical)
        {
            HttpRequest request = HttpContext.Current.Request;
            int status = HttpContext.Current.Response.StatusCode;
            bool success = status == 200;
            bool notModified = success && entry.IsNotModified(request.Headers["If-None-Match"], request.Headers["If-Modified-Since"]);
            HttpResponse response = Begin(notModified ? 304 : status, contentType);

            if (contentType == MarkdownContentType)
            {
                ApplyMarkdownHeaders(response, canonical);
            }

            if (success && httpCacheMinutes > 0 && !Context.IsLoggedIn && Context.Database != null && MarkdownSettings.IsCacheableDatabase(Context.Database.Name))
            {
                response.Cache.SetCacheability(HttpCacheability.Public);
                response.Cache.SetMaxAge(TimeSpan.FromMinutes(httpCacheMinutes));
            }
            else
            {
                // Clients may keep a copy but must revalidate it; the ETag makes that a cheap 304.
                response.Cache.SetCacheability(HttpCacheability.Private);
                response.Cache.SetMaxAge(TimeSpan.Zero);
                response.Cache.AppendCacheExtension("must-revalidate");
            }

            response.Cache.SetETag(entry.ETag);
            response.Cache.SetLastModified(entry.LastModified);

            if (!notModified)
            {
                response.Write(entry.Body);
            }

            End(args);
        }

        public static void NotFound(HttpRequestArgs args)
        {
            Text(args, 404, "text/plain", "Not found", 0);
        }

        /// <summary>Headers for a Markdown page: type, canonical link to the HTML page, and noindex.</summary>
        public static void ApplyMarkdownHeaders(HttpResponse response, Uri canonical)
        {
            response.ContentType = MarkdownContentType;
            response.Charset = "utf-8";

            if (canonical != null)
            {
                response.AppendHeader("Link", "<" + canonical.AbsoluteUri + ">; rel=\"canonical\"");
            }

            if (MarkdownSettings.NoIndexHeader)
            {
                response.AppendHeader("X-Robots-Tag", "noindex");
            }

            if (MarkdownSettings.AcceptHeader)
            {
                response.AppendHeader("Vary", "Accept");
            }
        }

        private static HttpResponse Begin(int statusCode, string contentType)
        {
            HttpResponse response = HttpContext.Current.Response;
            response.Clear();
            response.StatusCode = statusCode;
            response.TrySkipIisCustomErrors = true;
            response.ContentType = contentType;
            response.Charset = "utf-8";
            return response;
        }

        private static void End(HttpRequestArgs args)
        {
            HttpContext.Current.ApplicationInstance.CompleteRequest();
            args.AbortPipeline();
        }
    }
}
