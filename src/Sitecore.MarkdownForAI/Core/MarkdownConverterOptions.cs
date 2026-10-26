using System;
using System.Collections.Generic;

namespace Sitecore.MarkdownForAI.Core
{
    /// <summary>Options for <see cref="MarkdownConverter"/>.</summary>
    public sealed class MarkdownConverterOptions
    {
        /// <summary>Elements removed with all their content.</summary>
        public static readonly string[] DefaultSkipTags =
        {
            "script", "style", "noscript", "template", "iframe", "svg", "canvas", "form", "button", "input", "select",
            "textarea", "object", "embed", "video", "audio", "head", "meta", "link", "title", "nav", "map", "dialog"
        };

        public MarkdownConverterOptions()
        {
            SkipTags = new HashSet<string>(DefaultSkipTags, StringComparer.OrdinalIgnoreCase);
            IgnoreClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            IgnoreAttribute = "data-md-ignore";
            IncludeImages = true;
        }

        /// <summary>Base URL used to turn relative links and image sources into absolute URLs.</summary>
        public Uri BaseUri { get; set; }

        public ISet<string> SkipTags { get; private set; }

        /// <summary>Elements carrying any of these CSS classes are removed.</summary>
        public ISet<string> IgnoreClasses { get; private set; }

        /// <summary>Elements carrying this attribute are removed. Default <c>data-md-ignore</c>.</summary>
        public string IgnoreAttribute { get; set; }

        public bool IncludeImages { get; set; }
    }
}
