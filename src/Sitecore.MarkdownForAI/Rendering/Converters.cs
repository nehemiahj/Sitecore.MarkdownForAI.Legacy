using System;
using Sitecore.MarkdownForAI.Core;

namespace Sitecore.MarkdownForAI.Rendering
{
    /// <summary>Creates converters configured from <see cref="MarkdownSettings"/>.</summary>
    public static class Converters
    {
        public static MarkdownConverter Create(Uri baseUri)
        {
            var options = new MarkdownConverterOptions
            {
                BaseUri = baseUri,
                IncludeImages = MarkdownSettings.IncludeImages
            };

            options.SkipTags.Clear();
            foreach (string tag in MarkdownSettings.SkipTags)
            {
                options.SkipTags.Add(tag);
            }

            foreach (string cls in MarkdownSettings.IgnoreClasses)
            {
                options.IgnoreClasses.Add(cls);
            }

            return new MarkdownConverter(options);
        }
    }
}
