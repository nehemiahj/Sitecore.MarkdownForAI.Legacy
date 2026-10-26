using Sitecore.MarkdownForAI.Rendering;
using Sitecore.Mvc.Pipelines.Response.GetPageRendering;

namespace Sitecore.MarkdownForAI.Pipelines
{
    /// <summary>
    /// mvc.getPageRendering, after GetLayoutRendering. Swaps the page layout's renderer for
    /// <see cref="MarkdownPageRenderer"/>, so the layout view (and anything it binds
    /// statically, such as a header or footer) never runs. Only placeholders are rendered.
    /// </summary>
    public class UseMarkdownPageRenderer : GetPageRenderingProcessor
    {
        public override void Process(GetPageRenderingArgs args)
        {
            if (!MarkdownRequest.IsActive || args.Result == null)
            {
                return;
            }

            args.Result.Renderer = new MarkdownPageRenderer(args.Result, args.PageDefinition);
        }
    }
}
