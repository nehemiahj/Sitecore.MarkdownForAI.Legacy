# Changelog

## 0.1.0 (unreleased)

- WebForms pages: excluded renderings and sublayouts removed in `insertRenderings`; only top-level `sc:placeholder` content converted; field mappings answer before rendering. Other layout types answer `/page.md` with 404.
- Caching: finished Markdown cached per site, database, language, item, version, device and host, served before layout resolution; one build per page under concurrency; ETag, Last-Modified and 304; `Cache-Control: public` for anonymous visitors; size limit; query strings ignored by default.
- Content from `master` is never cached (`MarkdownForAI.NoCacheDatabases`), so local development and preview sites show edits immediately; content delivery (`web`) is cached as before.
- The page cache key includes the URL path, so an item that serves many URLs (such as a wildcard) is cached per URL.
- `MarkdownForAI.FrontMatterFields` chooses the front matter lines (`none` for no front matter); a title of `*` is always left out.
- Markdown requests never use the rendering (HTML) cache; works with custom `SetCacheability` and `GenerateCacheKey` processors.
- GitHub alerts (`> [!NOTE]`, `> [!WARNING]`, ...) are kept intact.
- Only `200` responses are cached; a status set earlier in the request (such as `404`) is kept on cached and source answers.
- `llms.txt` and `llms-full.txt` are never cached for logged-in users.
- A page build still running when a publish clears the cache does not store its result in the new cache.
- With `MarkdownForAI.AcceptHeader` on, HTML pages also send `Vary: Accept`.
- A static `llms.txt` or `llms-full.txt` in the web root is served instead of the generated one.
- `LlmsTitle`, `LlmsSummary` and `LlmsSection` are unversioned, so they can be translated. The startup installer only creates missing fields and never changes existing ones.
- List settings are parsed once per value instead of on every read; front matter dates use the invariant culture.
- Markdown versions of Sitecore MVC and WebForms pages: `/page.md`, `?format=md`, optional `Accept: text/markdown`.
- Component controls: excluded placeholders, `md` rendering parameter, component default in the rendering definition's Parameters field, config ID lists, default mode, `data-md-ignore`, `*.md.cshtml` views.
- Field mapping source and `IMarkdownSource` extension point.
- `/llms.txt` and `/llms-full.txt` from items inheriting `_LlmsPage`.
- Discovery: HTML pages send `Link: <page.md>; rel="alternate"; type="text/markdown"` (`MarkdownForAI.LinkHeader`, on by default), alongside `@Html.MarkdownAlternateLink()`.
- Items (templates and option items, fixed IDs, same IDs and paths as Sitecore.MarkdownForAI) under `/sitecore/templates/Modules` and `/sitecore/system/Modules`, outside the Helix paths that solutions serialize.
- Release files: a Sitecore package for the Installation Wizard (built offline from `package/items` by `tools/New-SitecorePackage.ps1`; module items are merged on upgrade, the shared parent folder is skipped when it exists), a files zip, and a NuGet package with the assembly. The startup item installer remains as a fallback.
- Settings go in `z.MarkdownForAI.Site.config` (shipped as `.example`, `patch:attribute` syntax), so upgrades can replace the module config.
- Sitecore 8.0 to 10.0, plain include config, compiled against Sitecore 8.0. Sitecore 10.1 and later is covered by the Sitecore.MarkdownForAI repository.
