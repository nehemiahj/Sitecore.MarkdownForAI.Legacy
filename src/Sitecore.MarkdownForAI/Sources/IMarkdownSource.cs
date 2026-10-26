using System;
using System.Collections.Generic;
using Sitecore.Configuration;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.MarkdownForAI.Core;

namespace Sitecore.MarkdownForAI.Sources
{
    /// <summary>
    /// Builds Markdown for an item without rendering the page. Return null to let the next
    /// source (and finally the rendering-based conversion) handle the item. Register custom
    /// sources under <c>markdownForAI/sources</c> in config.
    /// </summary>
    public interface IMarkdownSource
    {
        MarkdownDocument TryCreate(Item item, Uri pageUrl);
    }

    /// <summary>The configured sources, in config order.</summary>
    public class MarkdownSourceList
    {
        private static readonly Lazy<MarkdownSourceList> Configured = new Lazy<MarkdownSourceList>(Load);
        private readonly List<IMarkdownSource> _sources = new List<IMarkdownSource>();

        public static MarkdownSourceList Instance
        {
            get { return Configured.Value; }
        }

        public IEnumerable<IMarkdownSource> Sources
        {
            get { return _sources; }
        }

        /// <summary>Called by the config factory (<c>hint="list:AddSource"</c>).</summary>
        public void AddSource(IMarkdownSource source)
        {
            if (source != null)
            {
                _sources.Add(source);
            }
        }

        public MarkdownDocument TryCreate(Item item, Uri pageUrl)
        {
            if (item == null)
            {
                return null;
            }

            foreach (IMarkdownSource source in _sources)
            {
                try
                {
                    MarkdownDocument document = source.TryCreate(item, pageUrl);
                    if (document != null)
                    {
                        return document;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error("MarkdownForAI: source " + source.GetType().FullName + " failed for " + item.Paths.FullPath, ex, typeof(MarkdownSourceList));
                }
            }

            return null;
        }

        private static MarkdownSourceList Load()
        {
            return Factory.CreateObject("markdownForAI/sourceList", false) as MarkdownSourceList ?? new MarkdownSourceList();
        }
    }
}
