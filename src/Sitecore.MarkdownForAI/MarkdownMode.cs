using System;

namespace Sitecore.MarkdownForAI
{
    /// <summary>Whether a rendering contributes to the Markdown version of a page.</summary>
    public enum MarkdownMode
    {
        Include,
        Exclude
    }

    public static class MarkdownModeParser
    {
        /// <summary>Parses <c>include</c>/<c>exclude</c> (also <c>1</c>/<c>0</c>, <c>true</c>/<c>false</c>). Null when empty or unknown.</summary>
        public static MarkdownMode? Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            switch (value.Trim().ToLowerInvariant())
            {
                case "include":
                case "1":
                case "true":
                case "yes":
                    return MarkdownMode.Include;
                case "exclude":
                case "0":
                case "false":
                case "no":
                    return MarkdownMode.Exclude;
                default:
                    return null;
            }
        }
    }
}
