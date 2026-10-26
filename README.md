# Sitecore.MarkdownForAI.Legacy

> [!NOTE]
> **Archived, not maintained.** This is the full-feature Markdown edition for Sitecore 8.0 to 10.0 (`.md` pages for MVC and WebForms, llms.txt, field mappings, component rules). It has no Ask AI button. For the Ask AI button on Sitecore 10.1 to 10.5, use [Sitecore.MarkdownForAI](https://github.com/nehemiahj/Sitecore.MarkdownForAI).

**Give AI agents and AI assistants a clean Markdown copy of every page on a server-rendered Sitecore site: MVC or WebForms.**

This is the **legacy edition for Sitecore 8.0 to 10.0**. On Sitecore 10.1 and later, use [Sitecore.MarkdownForAI](https://github.com/nehemiahj/Sitecore.MarkdownForAI), which ships its items as Sitecore CLI serialization and adds the Ask AI button.

```
https://www.example.com/pet-health/cat-kidney-disease       normal HTML page, unchanged
https://www.example.com/pet-health/cat-kidney-disease.md    the same page as Markdown: no header, footer, menus or scripts
https://www.example.com/llms.txt                            an index of your articles for AI agents
```

## Why

Headless sites (JSS, SitecoreAI, formerly XM Cloud) already have Layout Service JSON that an AI tool can read. Server-rendered sites, MVC and WebForms alike, only have full HTML pages: the article is buried between navigation, cookie banners, footers and kilobytes of script. AI agents and assistants such as ChatGPT, Claude and Perplexity read that noise, waste their context on it and sometimes quote your footer instead of your article.

Many sites on Sitecore 8 and 9 are content-heavy and still use WebForms sublayouts, MVC renderings or a mix of both. This edition gives them a Markdown version of every page without an upgrade.

## What you get

| Feature | What it does |
|---|---|
| **Markdown pages** | `/any-page.md` (or `?format=md`) returns the page as Markdown with YAML front matter, for MVC and WebForms layouts. Only the content components are rendered. |
| **Component control** | Choose per placeholder, per component or per placement what goes into the Markdown. Header and footer are out by default. |
| **llms.txt** | `/llms.txt` lists the pages you opt in, grouped by section, with links to their `.md` versions. `/llms-full.txt` holds the full text. |
| **Caching** | The finished Markdown is cached and served before Sitecore resolves the layout, with ETags and `304 Not Modified`. Your HTML rendering cache is not touched. |
| **SEO safe** | Every Markdown response sends `Link: rel="canonical"` to the HTML page and `X-Robots-Tag: noindex`. |

No third-party dependencies: the HTML parser and converter are built in, so there are no HtmlAgilityPack version clashes with any Sitecore release.

## Quick start

The fastest way to see it working, on one CM or a single instance:

1. **Install** `Sitecore.MarkdownForAI.Legacy-<version>.zip` with the **Installation Wizard**.
2. **Configure:** rename `App_Config/Include/MarkdownForAI/z.MarkdownForAI.Site.config.example` to `z.MarkdownForAI.Site.config` and set two values:
   - `MarkdownForAI.Sites`: your site name(s) from `<sites>`, for example `mysite`.
   - `MarkdownForAI.ExcludedPlaceholders`: the placeholders that hold your header, navigation and footer.
3. **Publish** `/sitecore/templates/Modules/Markdown For AI` and `/sitecore/system/Modules/Markdown For AI`.
4. **Open** any page with `.md` added: `https://yoursite/about-us.md`, or `/index.md` for the home page.

The config has no role rules, so it answers on every server it is deployed to. For CI deployments, see [Install](#install). Something not working? See [Troubleshooting](#troubleshooting).

## Supported versions

| Repository | Sitecore | .NET | Config | Items |
|---|---|---|---|---|
| **Sitecore.MarkdownForAI.Legacy** (this one) | 8.0 to 10.0, MVC and WebForms | `net45`, compiled against 8.0 | Include files in `App_Config/Include/MarkdownForAI` | Sitecore package, or created at startup by a built-in installer |
| **Sitecore.MarkdownForAI** | 10.1 to 10.5, MVC and WebForms | `net48`, compiled against 10.1 | Rule-based (`role:require`) | Sitecore package or Sitecore CLI serialization |

Sitecore CLI serialization only exists from 10.1, so this edition ships its items as a Sitecore package, with a startup installer as a fallback. Both editions build `Sitecore.MarkdownForAI.dll` with the same types and settings, and their items have the same IDs and paths, so moving to 10.1 later means swapping the DLL and config. Version notes: [docs/sitecore-8-to-10.0.md](docs/sitecore-8-to-10.0.md).

## Install

Each release has three files. Use the one that matches how you deploy:

| File | Use it when |
|---|---|
| `Sitecore.MarkdownForAI.Legacy-<version>.zip` | **Sitecore package.** You install modules on the CM with the Installation Wizard. Contains the DLL, the config and the items. |
| `Sitecore.MarkdownForAI.Legacy-<version>-files.zip` | You deploy from source control and CI. The same files laid out like the web root, plus the item installer config. |
| `Sitecore.MarkdownForAI.Legacy.<version>.nupkg` | You reference the assembly from your solution through NuGet instead of committing the DLL. |

Run `./build.ps1` to build all three yourself (see [Building from source](#building-from-source)).

### Option A: Sitecore package (quickest)

1. On the CM, open the **Installation Wizard** and install `Sitecore.MarkdownForAI.Legacy-<version>.zip`.
2. Rename `App_Config/Include/MarkdownForAI/z.MarkdownForAI.Site.config.example` to `z.MarkdownForAI.Site.config` and set your sites (see [Your settings](#your-settings)).
3. Publish `/sitecore/templates/Modules/Markdown For AI` and `/sitecore/system/Modules/Markdown For AI`.
4. Copy `bin/Sitecore.MarkdownForAI.dll` and both config files to every CD server. The config has no role rules, so on the CM it answers Markdown too; delete `Sitecore.MarkdownForAI.config` there if you do not want that.

### Option B: CI deployment

1. **Assembly:** add the NuGet package to the project you deploy, or copy `bin/Sitecore.MarkdownForAI.dll` from the files zip into your solution.
2. **Config:** commit `App_Config/Include/MarkdownForAI/` from the files zip:

   | File | Content delivery | Content management | Single instance |
   |---|---|---|---|
   | `Sitecore.MarkdownForAI.config` (do not edit) | Yes | Only if you want Markdown answered on CM | Yes |
   | `z.MarkdownForAI.Site.config` (your settings, renamed from `.example`) | Yes | With the file above | Yes |
   | `Sitecore.MarkdownForAI.Installer.config` | Optional (does nothing without `master`) | Yes, unless you install the Sitecore package | Yes |

3. **Items:** install the Sitecore package on the CM once, or let the installer config create them when the CM starts. Either way they are:
   - `/sitecore/templates/Modules/Markdown For AI/_LlmsPage`
   - `/sitecore/templates/Modules/Markdown For AI/_MarkdownRenderingParameters`
   - `/sitecore/system/Modules/Markdown For AI/Modes/include` and `exclude`

   Both use the same fixed IDs. Once the items are in your own serialization (Unicorn, TDS or a package), set `MarkdownForAI.InstallItems` to `false` or remove the installer config.
4. Publish the items.

The items sit under `/sitecore/templates/Modules`, outside the `Foundation`, `Feature` and `Project` folders that Helix solutions serialize, so your own serialization cannot delete them. `/sitecore/templates/Modules` is created if missing, with the same ID Sitecore PowerShell Extensions uses, and is never overwritten.

### Your settings

Keep every change in `App_Config/Include/MarkdownForAI/z.MarkdownForAI.Site.config`, never in `Sitecore.MarkdownForAI.config`. The `z.` prefix makes it load after the module config, and upgrades leave it alone. It uses `patch:attribute`, which works on every release from 8.0:

```xml
<configuration xmlns:patch="http://www.sitecore.net/xmlconfig/">
  <sitecore>
    <settings>
      <setting name="MarkdownForAI.Sites">
        <patch:attribute name="value">website|blog</patch:attribute>
      </setting>
    </settings>
  </sitecore>
</configuration>
```

The `.example` file lists the settings most sites change (sites, excluded placeholders, default mode, rendering lists) and a field mapping. Check the result in `/sitecore/admin/showconfig.aspx`.

## Next steps

1. Open a few page types with `.md` added and check the output. If the header, menus or footer still appear, add their placeholders to `MarkdownForAI.ExcludedPlaceholders` in [your settings file](#your-settings).
2. Add `_LlmsPage` as a base template to your article templates, check **LlmsInclude** on the articles, publish, and open `/llms.txt`.
3. Optional, MVC layouts: add the discovery link to the `<head>`. Every page already sends it as a `Link` HTTP header; the tag also helps agents that only read the HTML:

   ```cshtml
   @using Sitecore.MarkdownForAI.Html
   @Html.MarkdownAlternateLink()
   ```

## How agents find the Markdown

There is no single standard yet, so the module offers every common route:

| Route | How it works |
|---|---|
| `Link` header | Every HTML page the module can convert answers with `Link: <https://host/page.md>; rel="alternate"; type="text/markdown"`. Agents see it in the response headers without parsing the page. On by default (`MarkdownForAI.LinkHeader`). |
| `<link>` in the `<head>` | `@Html.MarkdownAlternateLink()` writes the same link into the HTML, like an RSS feed link. |
| `/llms.txt` | One index of the article library with `.md` links and summaries ([llms.txt](#llmstxt)). |
| `Accept: text/markdown` | Optional content negotiation on the normal URL (`MarkdownForAI.AcceptHeader`, off by default because CDNs must then vary on `Accept`). When it is on, the module sends `Vary: Accept` on both the HTML and the Markdown responses. |

Search engine crawlers mostly still fetch the HTML; the `.md` responses carry `X-Robots-Tag: noindex` so the HTML page keeps ranking.

## Choosing what goes into the Markdown

Only the content of placeholders is converted. Every rendering or sublayout in them is checked in this order, and the first rule that matches wins:

| # | Rule | Set in | Example |
|---|---|---|---|
| 1 | **Excluded placeholder** | `MarkdownForAI.ExcludedPlaceholders` | `header\|footer` (any depth; dynamic suffixes such as `footer-{GUID}-0` match) |
| 2 | **Per placement** | the **md** rendering parameter | `include` or `exclude` |
| 3 | **Per component** | the rendering or sublayout item's **Parameters** field | `md=exclude` |
| 4 | **Config lists** | `MarkdownForAI.ExcludedRenderings` / `IncludedRenderings` | rendering item IDs |
| 5 | **Default** | `MarkdownForAI.DefaultMode` | `include` |

An excluded rendering is skipped before its controller, view or sublayout code runs. Inside an included one, **`data-md-ignore`** on any element removes it. On MVC, **`Foo.md.cshtml`** next to `Foo.cshtml` is used instead on Markdown requests.

**Only the article component:** set `DefaultMode` to `exclude` and put `md=include` on the article rendering. Then nothing else goes in. Include the main component of every page type you care about, and any container rendering the article sits in. More examples: [docs/component-controls.md](docs/component-controls.md).

**Field mapping:** for templates where the content lives in fields, build the Markdown straight from them, with no rendering at all:

```xml
<!-- in z.MarkdownForAI.Site.config, inside <sitecore> (the .example file has this block commented out) -->
<markdownForAI>
  <sourceList>
    <sources>
      <fieldMapping>
        <mappings>
          <mapping template="{YOUR-ARTICLE-TEMPLATE-ID}" title="Title" summary="Summary"
                   body="Body" meta="Author|Published Date|Topics" />
        </mappings>
      </fieldMapping>
    </sources>
  </sourceList>
</markdownForAI>
```

## MVC and WebForms

| | MVC layout (`.cshtml`) | WebForms layout (`.aspx`) |
|---|---|---|
| Layout markup | Never rendered | Rendered, but only the content of the top-level `sc:placeholder` controls is converted |
| Excluded components | Skipped in `mvc.renderRendering` | Removed in `insertRenderings`, after personalization |
| Hand-written Markdown | `Foo.md.cshtml` | Not available; use `data-md-ignore` and the rules above |
| No placeholders on the layout | n/a | Falls back to the page body, minus the `<form runat="server">` wrapper |

Field mappings, caching, headers and `llms.txt` work the same for both. `MarkdownForAI.WebForms.Enabled` set to `false` turns WebForms conversion off; `/page.md` then returns 404 on WebForms pages instead of serving HTML. Layouts that are neither MVC nor WebForms also get a 404.

## llms.txt

- **`/llms.txt`** lists pages that inherit `_LlmsPage` and have **LlmsInclude** checked, grouped by **LlmsSection**, with links to their `.md` URLs. Pages with **LlmsOptional** checked go under `## Optional`.
- **`/llms-full.txt`** has the full Markdown of listed pages that use a field mapping (other pages contribute their summary), capped by `MarkdownForAI.LlmsTxt.FullMaxChars`.
- The site start item's **LlmsTitle** and **LlmsSummary** become the header.
- **LlmsTitle**, **LlmsSummary** and **LlmsSection** are unversioned, so each language can have its own text. **LlmsInclude** and **LlmsOptional** are shared.
- A static `llms.txt` or `llms-full.txt` file in the web root takes precedence on every site.
- `MarkdownForAI.LlmsTxt.Source` is `tree` (walks the content tree) or `index` (ContentSearch).

Writing a good llms.txt: [docs/llms-txt.md](docs/llms-txt.md).

## Caching

| Level | What happens | Settings |
|---|---|---|
| **Page** | Finished Markdown per site, database, language, item, version, device, host and URL path, in memory on each server, answered before Sitecore resolves the layout. Concurrent requests for an uncached page build it once. Logged-in visitors are never cached, and neither is content from `master` (local development and preview sites, where a save changes the page and no publish clears the cache). Cleared on `publish:end` and `publish:end:remote`. | `CacheMinutes` (30), `CacheSizeMB` (100), `CacheVaryByQueryString` (false), `NoCacheDatabases` (master) |
| **HTTP** | `ETag`, `Last-Modified` and `304 Not Modified`; `Cache-Control: public, max-age` for anonymous visitors. | `HttpCacheMinutes` (10) |
| **llms.txt** | Built once per site, language and host. Never cached for logged-in users. | `LlmsTxt.CacheMinutes` (60) |

Only `200` responses are cached: not-found and error pages are never stored or replayed.

Markdown requests never read or write Sitecore's rendering (HTML) cache, on MVC or WebForms, so they cannot mix with the cached HTML of your pages. This does not depend on the position of `SetCacheability` or `GenerateCacheKey`, so solutions that replace those processors are fine.

## The Ask AI button

The **Ask AI** button (ChatGPT, Claude, Perplexity or any assistant, configured as Sitecore items) ships with [Sitecore.MarkdownForAI](https://github.com/nehemiahj/Sitecore.MarkdownForAI) (10.1 and later) only. Its web component, `js/ask-ai` in that repository, works on any page, including Sitecore 8 and 9: include the script and add `<ask-ai></ask-ai>`. It finds the `.md` URL through the tag that `@Html.MarkdownAlternateLink()` writes.

## Troubleshooting

| What you see | Why | What to do |
|---|---|---|
| `/page.md` returns 404 | The site is not in `MarkdownForAI.Sites`, or the layout is neither MVC nor WebForms | Add the site name. If IIS or a CDN blocks `.md`, allow it or use `?format=md` |
| `?format=md` shows the normal HTML page, and HTML pages have no `Link` header | The module is not loaded on this server (DLL or config missing) | Check `/sitecore/admin/showconfig.aspx` for `DetectMarkdownRequest` |
| The header, menu or footer is in the Markdown | Their placeholders are not excluded, or they sit inside an included container | Add the placeholder keys to `ExcludedPlaceholders`, or mark the components `md=exclude` |
| A page returns an almost empty Markdown body | `DefaultMode` is `exclude` and the page's main component (or its container) is not included | Mark the component and its container `md=include` |
| Edits do not show in the `.md`, but the HTML page has them | The Markdown is cached until the next publish, and your browser or CDN may keep it for `HttpCacheMinutes` | Publish. Sites served from `master` are never cached. Hard refresh (Ctrl+F5) to skip the browser copy |
| `/llms.txt` lists nothing | No published page inherits `_LlmsPage` with **LlmsInclude** checked, under this site's start item | Add the base template, check the box, publish |

## Settings reference

The settings and their defaults are the same as in [Sitecore.MarkdownForAI](https://github.com/nehemiahj/Sitecore.MarkdownForAI#settings-reference), without `AskAi.ScriptUrl`, plus `MarkdownForAI.InstallItems` (`true`: the startup installer creates missing items on servers with a `master` database). Every setting is described in `Sitecore.MarkdownForAI.config`.

## Limitations

- **Server-rendered sites only.** Headless sites (JSS, SitecoreAI, formerly XM Cloud) should use Layout Service.
- **Personalization:** the Markdown follows the same rules as the HTML for the visitor making the request. Only anonymous output is cached.
- **`Accept: text/markdown`** is off by default. If you enable it, your CDN must vary on `Accept`.
- **`llms-full.txt`** includes full text only for pages with a field mapping or a custom source.
- **The item installer** writes to `master` at startup. Turn it off once your items are serialized.

## Building from source

```powershell
./build.ps1             # build, run the .NET tests, write dist/
./build.ps1 -SkipTests
```

Requirements: .NET SDK 6 or later (builds the `net45` target with reference packages) and access to `https://nuget.sitecore.com/resources/v3/index.json`.

Folders: `src/` module, `tests/` xUnit tests for the Sitecore-free core, `config/` App_Config include files, `package/items/` the items in Sitecore CLI YAML format (the source of the Sitecore package), `tools/New-SitecorePackage.ps1` builds the package from them without a Sitecore instance, `docs/`, `examples/`.

## Content licensing

Serving pages as Markdown, listing them in `llms.txt` and sending them to AI tools is a new form of distribution. If parts of your content are licensed from a third party (syndicated articles, stock text), check that the licence allows it. Exclude those pages with `md=exclude`, a placeholder rule, or by leaving **LlmsInclude** unchecked.

## License

[MIT](LICENSE)
