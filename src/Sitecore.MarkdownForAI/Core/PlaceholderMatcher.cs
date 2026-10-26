using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Sitecore.MarkdownForAI.Core
{
    /// <summary>
    /// Matches rendering placeholder paths such as <c>/main/sidebar-{GUID}-0</c> against
    /// configured placeholder keys, including Sitecore and SXA dynamic placeholder suffixes.
    /// </summary>
    public static class PlaceholderMatcher
    {
        // What may follow a key in a dynamic placeholder: -{guid}-0, _{guid}, {guid}, _0b1c2d3e...
        private static readonly Regex DynamicSuffix = new Regex(@"^[-_]?(\{|[0-9a-fA-F]{8}-?[0-9a-fA-F]{4})", RegexOptions.Compiled);

        public static IEnumerable<string> Segments(string placeholderPath)
        {
            return (placeholderPath ?? string.Empty).Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        }

        /// <summary>First segment of a placeholder path, or null for the layout itself.</summary>
        public static string TopLevelKey(string placeholderPath)
        {
            return Segments(placeholderPath).FirstOrDefault();
        }

        /// <summary>True when any segment of the path matches any of the keys.</summary>
        public static bool MatchesAny(string placeholderPath, ICollection<string> keys)
        {
            if (keys == null || keys.Count == 0)
            {
                return false;
            }

            return Segments(placeholderPath).Any(segment => keys.Any(key => SegmentMatches(segment, key)));
        }

        public static bool SegmentMatches(string segment, string key)
        {
            if (string.IsNullOrEmpty(segment) || string.IsNullOrEmpty(key))
            {
                return false;
            }

            if (string.Equals(segment, key, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return segment.StartsWith(key, StringComparison.OrdinalIgnoreCase) &&
                   DynamicSuffix.IsMatch(segment.Substring(key.Length));
        }
    }
}
