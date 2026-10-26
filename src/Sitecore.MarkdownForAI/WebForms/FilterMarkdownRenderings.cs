using System;
using Sitecore.Diagnostics;
using Sitecore.Layouts;
using Sitecore.MarkdownForAI.Rendering;
using Sitecore.Pipelines.InsertRenderings;

namespace Sitecore.MarkdownForAI.WebForms
{
    /// <summary>
    /// insertRenderings, last processor (after personalization). On Markdown requests for WebForms
    /// pages it removes excluded renderings and sublayouts before their controls are created, with
    /// the same rules as for MVC (placeholder, md parameter, component default, ID lists, default
    /// mode). The remaining renderings bypass the HTML cache: a cached sublayout would carry nested
    /// renderings that one of the two versions excludes.
    /// </summary>
    public class FilterMarkdownRenderings
    {
        public void Process(InsertRenderingsArgs args)
        {
            if (!MarkdownRequest.IsActive || args == null || args.Renderings == null)
            {
                return;
            }

            args.Renderings.RemoveAll(IsExcluded);

            foreach (RenderingReference rendering in args.Renderings)
            {
                if (rendering.Settings != null && rendering.Settings.Caching != null)
                {
                    rendering.Settings.Caching.Cacheable = false;
                }
            }
        }

        private bool IsExcluded(RenderingReference rendering)
        {
            try
            {
                return MarkdownModeResolver.Resolve(rendering) == MarkdownMode.Exclude;
            }
            catch (Exception ex)
            {
                Log.Error("MarkdownForAI: failed to resolve the Markdown mode of rendering " + rendering.RenderingID, ex, this);
                return false;
            }
        }
    }
}
