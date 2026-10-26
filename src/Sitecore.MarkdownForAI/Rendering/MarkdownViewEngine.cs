using System;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using Sitecore.MarkdownForAI.Core;

namespace Sitecore.MarkdownForAI.Rendering
{
    /// <summary>
    /// On Markdown requests, prefers <c>Foo.md.cshtml</c> over <c>Foo.cshtml</c> for any view
    /// or partial, including views returned by controller renderings. Its output is passed
    /// through as Markdown instead of being converted from HTML.
    /// </summary>
    public class MarkdownViewEngine : IViewEngine
    {
        public ViewEngineResult FindPartialView(ControllerContext controllerContext, string partialViewName, bool useCache)
        {
            return Find(controllerContext, partialViewName, null, useCache, true);
        }

        public ViewEngineResult FindView(ControllerContext controllerContext, string viewName, string masterName, bool useCache)
        {
            return Find(controllerContext, viewName, masterName, useCache, false);
        }

        public void ReleaseView(ControllerContext controllerContext, IView view)
        {
            var disposable = view as IDisposable;
            if (disposable != null)
            {
                disposable.Dispose();
            }
        }

        /// <summary><c>~/Views/A/B.cshtml</c> becomes <c>~/Views/A/B.md.cshtml</c>; <c>B</c> becomes <c>B.md</c>.</summary>
        public static string MarkdownViewName(string viewName)
        {
            if (viewName.EndsWith(".md.cshtml", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (viewName.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase))
            {
                return viewName.Substring(0, viewName.Length - ".cshtml".Length) + ".md.cshtml";
            }

            return viewName.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? null : viewName + ".md";
        }

        private ViewEngineResult Find(ControllerContext controllerContext, string name, string masterName, bool useCache, bool partial)
        {
            string markdownName = MarkdownRequest.IsActive && !string.IsNullOrEmpty(name) ? MarkdownViewName(name) : null;
            if (markdownName == null)
            {
                return new ViewEngineResult(new string[0]);
            }

            foreach (IViewEngine engine in ViewEngines.Engines)
            {
                if (engine is MarkdownViewEngine)
                {
                    continue;
                }

                ViewEngineResult result = partial
                    ? engine.FindPartialView(controllerContext, markdownName, useCache)
                    : engine.FindView(controllerContext, markdownName, masterName, useCache);

                if (result != null && result.View != null)
                {
                    return new ViewEngineResult(new MarkdownRawView(result.View, engine, controllerContext), this);
                }
            }

            return new ViewEngineResult(new string[0]);
        }
    }

    /// <summary>Wraps a Markdown view so the page converter emits its output verbatim.</summary>
    public sealed class MarkdownRawView : IView, IDisposable
    {
        private static readonly Regex ScriptClose = new Regex("</script", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly IView _inner;
        private readonly IViewEngine _engine;
        private readonly ControllerContext _controllerContext;

        public MarkdownRawView(IView inner, IViewEngine engine, ControllerContext controllerContext)
        {
            _inner = inner;
            _engine = engine;
            _controllerContext = controllerContext;
        }

        public void Render(ViewContext viewContext, TextWriter writer)
        {
            using (var buffer = new StringWriter())
            {
                _inner.Render(viewContext, buffer);

                // Razor HTML-encodes @Model values; Markdown wants the plain characters back.
                string markdown = WebUtility.HtmlDecode(buffer.ToString());
                writer.Write("<script type=\"" + MarkdownConverter.RawMarkdownScriptType + "\">");
                writer.Write(ScriptClose.Replace(markdown, "<\\/script"));
                writer.Write("</script>");
            }
        }

        public void Dispose()
        {
            _engine.ReleaseView(_controllerContext, _inner);
        }
    }
}
