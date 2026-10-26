using System.Linq;
using System.Web.Mvc;
using Sitecore.MarkdownForAI.Rendering;
using Sitecore.Pipelines;

namespace Sitecore.MarkdownForAI.Pipelines
{
    /// <summary>initialize pipeline. Puts <see cref="MarkdownViewEngine"/> first so <c>*.md.cshtml</c> views win on Markdown requests.</summary>
    public class RegisterMarkdownViewEngine
    {
        public void Process(PipelineArgs args)
        {
            if (!ViewEngines.Engines.OfType<MarkdownViewEngine>().Any())
            {
                ViewEngines.Engines.Insert(0, new MarkdownViewEngine());
            }
        }
    }
}
