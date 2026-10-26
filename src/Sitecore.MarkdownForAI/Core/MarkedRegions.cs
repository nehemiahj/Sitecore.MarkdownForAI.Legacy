using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Sitecore.MarkdownForAI.Core
{
    /// <summary>
    /// Finds the parts of a rendered WebForms page that came from Sitecore placeholders. The page
    /// is rendered as usual, with comment markers around each top-level placeholder; only the HTML
    /// between the markers is converted, so markup typed straight into the .aspx layout (header,
    /// footer, scripts) stays out, just as the layout of an MVC page is never rendered.
    /// </summary>
    public static class MarkedRegions
    {
        private static readonly Regex Body = new Regex(@"<body\b[^>]*>(?<body>.*)</body\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex FormTags = new Regex(@"</?form\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static string BeginMarker(string token)
        {
            return "<!--markdownforai:begin:" + token + "-->";
        }

        public static string EndMarker(string token)
        {
            return "<!--markdownforai:end:" + token + "-->";
        }

        /// <summary>
        /// The HTML of every marked region, in page order, or null when the page has none
        /// (for example a layout without Sitecore placeholders).
        /// </summary>
        public static string Extract(string html, string token)
        {
            if (string.IsNullOrEmpty(html) || string.IsNullOrEmpty(token))
            {
                return null;
            }

            string begin = BeginMarker(token);
            string end = EndMarker(token);
            var sb = new StringBuilder();
            bool found = false;
            int position = 0;

            while (true)
            {
                int start = html.IndexOf(begin, position, StringComparison.Ordinal);
                if (start < 0)
                {
                    break;
                }

                start += begin.Length;
                int stop = html.IndexOf(end, start, StringComparison.Ordinal);
                if (stop < 0)
                {
                    break;
                }

                sb.Append(html, start, stop - start).Append('\n');
                found = true;
                position = stop + end.Length;
            }

            return found ? sb.ToString() : null;
        }

        /// <summary>
        /// Fallback when nothing is marked: the body, with the &lt;form runat="server"&gt; wrapper
        /// removed (forms are skipped by the converter, and WebForms wraps the whole page in one).
        /// </summary>
        public static string BodyWithoutForms(string html)
        {
            if (string.IsNullOrEmpty(html))
            {
                return string.Empty;
            }

            Match match = Body.Match(html);
            string body = match.Success ? match.Groups["body"].Value : html;
            return FormTags.Replace(body, string.Empty);
        }
    }
}
