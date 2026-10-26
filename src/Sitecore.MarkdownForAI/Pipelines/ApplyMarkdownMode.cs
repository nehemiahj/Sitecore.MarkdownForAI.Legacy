using System;
using Sitecore.Diagnostics;
using Sitecore.MarkdownForAI.Rendering;
using Sitecore.Mvc.Pipelines.Response.RenderRendering;
using Sitecore.Mvc.Presentation;

namespace Sitecore.MarkdownForAI.Pipelines
{
    /// <summary>
    /// mvc.renderRendering, first processor. Aborts renderings whose resolved mode is
    /// Exclude before their controller or view runs, and keeps included renderings out of
    /// Sitecore's HTML (rendering) cache.
    /// </summary>
    /// <remarks>
    /// Markdown output must never share a rendering cache entry with the HTML page: a
    /// <c>.md.cshtml</c> view renders different markup, and a cached container would carry
    /// child renderings that one of the two versions excludes. Turning caching off on the
    /// rendering itself, here at the start of the pipeline, means SetCacheability and any
    /// GenerateCacheKey (stock or a solution's own replacement) see a non-cacheable rendering,
    /// so the module does not depend on the position or type of those processors.
    /// The finished page is cached instead (see MarkdownCache and ServeCachedMarkdown).
    /// </remarks>
    public class ApplyMarkdownMode : RenderRenderingProcessor
    {
        public override void Process(RenderRenderingArgs args)
        {
            if (!MarkdownRequest.IsActive || args.Rendering == null ||
                string.Equals(args.Rendering.RenderingType, "Layout", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                if (MarkdownModeResolver.Resolve(args.Rendering) == MarkdownMode.Exclude)
                {
                    args.AbortPipeline();
                    return;
                }

                RenderingCachingDefinition caching = args.Rendering.Caching;
                if (caching != null && caching.Cacheable)
                {
                    caching.Cacheable = false;
                    args.Rendering.Caching = caching;
                }

                args.Cacheable = false;
            }
            catch (Exception ex)
            {
                Log.Error("MarkdownForAI: failed to resolve the Markdown mode of rendering " + args.Rendering.RenderingItemPath, ex, this);
            }
        }
    }
}
