using System;
using System.Globalization;
using System.Linq;

namespace Sitecore.MarkdownForAI.Core
{
    /// <summary>A cached response body with the validators sent to HTTP clients.</summary>
    public sealed class CachedText
    {
        public CachedText(string body)
            : this(body, DateTime.UtcNow)
        {
        }

        public CachedText(string body, DateTime generatedUtc)
        {
            Body = body ?? string.Empty;
            ETag = "\"" + Hash(Body) + "\"";
            LastModified = new DateTime(generatedUtc.Year, generatedUtc.Month, generatedUtc.Day,
                generatedUtc.Hour, generatedUtc.Minute, generatedUtc.Second, DateTimeKind.Utc);
        }

        public string Body { get; private set; }

        /// <summary>Strong validator: a hash of the body, so it only changes when the output does.</summary>
        public string ETag { get; private set; }

        /// <summary>When the body was generated, in UTC, to whole seconds (HTTP date precision).</summary>
        public DateTime LastModified { get; private set; }

        /// <summary>
        /// True when the client's copy is current, so a 304 Not Modified can be sent.
        /// If-None-Match wins over If-Modified-Since, as RFC 9110 requires.
        /// </summary>
        public bool IsNotModified(string ifNoneMatch, string ifModifiedSince)
        {
            if (!string.IsNullOrWhiteSpace(ifNoneMatch))
            {
                return ifNoneMatch.Split(',')
                    .Select(tag => tag.Trim())
                    .Any(tag => tag == "*" || tag == ETag || tag == "W/" + ETag);
            }

            DateTime since;
            return !string.IsNullOrWhiteSpace(ifModifiedSince) &&
                   DateTime.TryParse(ifModifiedSince, CultureInfo.InvariantCulture,
                       DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out since) &&
                   LastModified <= since;
        }

        // FNV-1a, 64 bit. Not a security hash; avoids the crypto providers that FIPS mode disables.
        private static string Hash(string text)
        {
            ulong hash = 14695981039346656037;
            foreach (char c in text)
            {
                hash = unchecked((hash ^ c) * 1099511628211);
            }

            return hash.ToString("x16", CultureInfo.InvariantCulture) + "-" + text.Length.ToString("x", CultureInfo.InvariantCulture);
        }
    }
}
