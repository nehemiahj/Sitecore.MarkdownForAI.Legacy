using System.Collections.Specialized;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Layouts;
using Sitecore.MarkdownForAI.Core;
using Sitecore.Web;

namespace Sitecore.MarkdownForAI.Rendering
{
    /// <summary>
    /// Decides whether a rendering is part of the Markdown output. First match wins:
    /// <list type="number">
    /// <item>placeholder listed in <c>MarkdownForAI.ExcludedPlaceholders</c> (any level, dynamic suffixes included): exclude</item>
    /// <item><c>md</c> rendering parameter on the placement</item>
    /// <item><c>md</c> in the rendering definition item's Parameters field (the component default)</item>
    /// <item>rendering ID in <c>MarkdownForAI.ExcludedRenderings</c> / <c>IncludedRenderings</c></item>
    /// <item><c>MarkdownForAI.DefaultMode</c></item>
    /// </list>
    /// </summary>
    public static class MarkdownModeResolver
    {
        /// <summary>An MVC rendering (mvc.renderRendering).</summary>
        public static MarkdownMode Resolve(Mvc.Presentation.Rendering rendering)
        {
            string placement = rendering.Parameters == null ? null : rendering.Parameters[MarkdownSettings.ParameterName];
            return Resolve(rendering.Placeholder, placement, rendering.RenderingItem, rendering.RenderingItemPath);
        }

        /// <summary>A WebForms rendering or sublayout (insertRenderings).</summary>
        public static MarkdownMode Resolve(RenderingReference reference)
        {
            string parameters = reference.Settings == null ? null : reference.Settings.Parameters;
            string placement = WebUtil.ParseUrlParameters(parameters ?? string.Empty)[MarkdownSettings.ParameterName];
            return Resolve(reference.Placeholder, placement, reference.RenderingItem, ID.IsNullOrEmpty(reference.RenderingID) ? null : reference.RenderingID.ToString());
        }

        private static MarkdownMode Resolve(string placeholder, string placementValue, RenderingItem definition, string fallbackId)
        {
            if (PlaceholderMatcher.MatchesAny(placeholder, MarkdownSettings.ExcludedPlaceholders))
            {
                return MarkdownMode.Exclude;
            }

            MarkdownMode? placement = ParseValue(placementValue);
            if (placement.HasValue)
            {
                return placement.Value;
            }

            if (definition != null && definition.InnerItem != null)
            {
                NameValueCollection defaults = WebUtil.ParseUrlParameters(definition.InnerItem["Parameters"] ?? string.Empty);
                MarkdownMode? componentDefault = ParseValue(defaults[MarkdownSettings.ParameterName]);
                if (componentDefault.HasValue)
                {
                    return componentDefault.Value;
                }
            }

            string id = definition != null ? definition.ID.ToString() : fallbackId;
            if (!string.IsNullOrEmpty(id))
            {
                if (MarkdownSettings.ExcludedRenderings.Contains(id))
                {
                    return MarkdownMode.Exclude;
                }

                if (MarkdownSettings.IncludedRenderings.Contains(id))
                {
                    return MarkdownMode.Include;
                }
            }

            return MarkdownSettings.DefaultMode;
        }

        /// <summary>Accepts the value of a Droplist (item name) or a Droplink (item ID).</summary>
        private static MarkdownMode? ParseValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (ID.IsID(value) && Context.Database != null)
            {
                Item option = Context.Database.GetItem(ID.Parse(value));
                value = option == null ? null : option.Name;
            }

            return MarkdownModeParser.Parse(value);
        }
    }
}
