# Component controls

This page explains how the module decides which parts of a page end up in the Markdown, and how editors and developers control it.

## What is rendered at all

On a Markdown request the page's **layout file is not executed**. The module replaces the layout renderer and renders only placeholders:

- **`MarkdownForAI.Placeholders` empty (default):** every top-level placeholder that has renderings on the page, in the order they appear in the layout definition.
- **`MarkdownForAI.Placeholders` set** (e.g. `main|content`): only those, in that order.

Anything the layout writes itself is therefore never output: header and footer partials, `@Html.Sitecore().Rendering(...)` calls in the layout, analytics and cookie scripts.

## The decision for each rendering

| Order | Control | Set by | Values |
|---|---|---|---|
| 1 | `MarkdownForAI.ExcludedPlaceholders` | Developer, config | Placeholder keys. Matched at any depth; `key-{GUID}-0`, `key_{GUID}` and `key{GUID}` dynamic forms match too. |
| 2 | `md` rendering parameter | Editor, per placement | `include` / `exclude` (empty = not set) |
| 3 | `md` in the rendering definition's **Parameters** field | Developer, per component | `md=include` / `md=exclude` |
| 4 | `MarkdownForAI.ExcludedRenderings` / `IncludedRenderings` | Developer, config | Rendering definition IDs, pipe separated |
| 5 | `MarkdownForAI.DefaultMode` | Developer, config | `include` (default) / `exclude` |

Excluded renderings are aborted **before** they execute, so their controllers, repository calls and search queries do not run.

> **Containers:** if a container rendering (a grid, a tab set) is excluded, everything inside it is excluded too.

### Opt-in instead of opt-out

For a site where most components are decorative, set this in your settings file (`z.MarkdownForAI.Site.config`):

```xml
<setting name="MarkdownForAI.DefaultMode">
  <patch:attribute name="value">exclude</patch:attribute>
</setting>
```

and put `md=include` in the **Parameters** field of the content components (rich text, article body, FAQ, table). Remember to include the containers that hold them.

## Editors: per-placement override

The Sitecore package (or the startup installer) creates `_MarkdownRenderingParameters`, a rendering parameters template with one droplist, **md** (Markdown mode), whose options are `include` and `exclude`.

- **Rendering without a parameters template:** set its **Parameters Template** field to `_MarkdownRenderingParameters`.
- **Rendering with its own parameters template:** add `_MarkdownRenderingParameters` to that template's base templates.

Editors then see **Markdown mode** in Control Properties. Leaving it empty uses the component default.

## Developers: inside a view

### Hide part of a component

```cshtml
<div class="article">
    <h1>@Model.Title</h1>
    <div class="share-buttons" data-md-ignore>...</div>
    @Html.Raw(Model.Body)
    <aside class="related" data-md-ignore>...</aside>
</div>
```

Also removed automatically: `hidden`, `aria-hidden="true"`, inline `display:none` / `visibility:hidden`, the tags in `MarkdownForAI.SkipTags` (scripts, forms, `nav`, media embeds and similar), and any CSS class in `MarkdownForAI.IgnoreClasses`.

### Write the Markdown yourself

Create `Article.md.cshtml` next to `Article.cshtml`. On Markdown requests it is used instead, for view renderings and for views returned by controller renderings, with the same model:

```cshtml
@model MySite.Models.ArticleViewModel
# @Model.Title

@Model.Summary

| Stage | Creatinine (mg/dL) |
|---|---|
@foreach (var stage in Model.Stages)
{
@:| @stage.Name | @stage.Range |
}
```

The output is HTML-decoded and passed through untouched, so `@Model.Title` containing `&` comes out as `&`. Indentation shared by all lines is removed.

See `examples/sitecore/ArticleBody.md.cshtml`.

## Structured content: skip rendering entirely

When a template's content is in a few fields, a field mapping is faster and cleaner than rendering:

```xml
<fieldMapping type="Sitecore.MarkdownForAI.Sources.FieldMappingSource, Sitecore.MarkdownForAI">
  <mappings hint="raw:AddMapping">
    <mapping template="{ARTICLE-TEMPLATE-ID}" title="Title" summary="Summary" body="Body|Footnotes"
             meta="Author|Published Date|Topics" inherited="true" summaryInBody="true" />
  </mappings>
</fieldMapping>
```

- Mappings are checked first. If one matches the context item, no rendering runs.
- `inherited="true"` (default) also matches templates that inherit from the mapped template.
- Body fields are rendered with `FieldRenderer`, so internal links and media URLs are expanded, then converted.

For logic that does not fit a mapping (for example an article resolved by a wildcard item), implement `IMarkdownSource`:

```csharp
public class ArticleSource : IMarkdownSource
{
    public MarkdownDocument TryCreate(Item item, Uri pageUrl)
    {
        Item article = ResolveArticle(item);   // your logic
        if (article == null) return null;        // fall back to rendering
        var doc = ItemMetadata.CreateDocument(article, pageUrl);
        // FieldRenderer expands internal links and media URLs, as the field mapping does.
        string body = FieldRenderer.Render(article, "Body");
        doc.Body = "# " + article["Title"] + "\n\n" + Converters.Create(pageUrl).Convert(body);
        return doc;
    }
}
```

and register it in the `sources` list before `fieldMapping`.

## Checking the result

- **`/page.md`:** the Markdown with front matter.
- **`/page?format=md`:** the same, without the extension (useful when a CDN blocks unknown extensions).
- **`/sitecore/admin/showconfig.aspx`:** confirm `DetectMarkdownRequest` sits before `FilterUrlExtensions`, `ValidateMarkdownRequest` before `ItemResolver`, and `ApplyMarkdownMode` first in `mvc.renderRendering`.
