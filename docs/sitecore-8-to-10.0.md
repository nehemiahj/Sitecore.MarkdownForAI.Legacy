# Sitecore 8.0 to 10.0

This edition has the same conversion, caching and llms.txt code as Sitecore.MarkdownForAI (10.1 and later), for MVC and WebForms pages, packaged for releases without Sitecore CLI serialization: a Sitecore package, with a startup installer as a fallback. The Ask AI button items are in the 10.1+ edition only.

## How it differs from Sitecore.MarkdownForAI

| | Sitecore.MarkdownForAI (10.1 to 10.5) | Legacy (8.0 to 10.0) |
|---|---|---|
| DLL | `net48`, compiled against Sitecore 10.1 | `net45`, compiled against Sitecore 8.0 |
| ASP.NET MVC reference | 5.2.4 (what 10.1 to 10.5 ship) | 5.1.0 (8.1 and later redirect to their own 5.2.x in web.config) |
| Config folder | `App_Config/Modules/MarkdownForAI` | `App_Config/Include/MarkdownForAI` |
| Role handling | `role:require` in the config | You choose which servers get which file |
| Items | Sitecore package or Sitecore CLI YAML, under `/sitecore/templates/Modules` | Sitecore package or startup installer, under `/sitecore/templates/Modules` |
| Ask AI button | Items, rendering and sublayout included | Not included (the web component works on its own) |

## Which files go where

| Server | `Sitecore.MarkdownForAI.config` and `z.MarkdownForAI.Site.config` | `Sitecore.MarkdownForAI.Installer.config` |
|---|---|---|
| Content delivery | Yes | Optional (does nothing without a master database) |
| Content management | Only if you want Markdown answered on CM | Yes, unless you installed the Sitecore package |
| Single instance | Yes | Yes, unless you installed the Sitecore package |

On 9.0 to 10.0 you can add `role:require` to the files instead of deploying them selectively.

## Notes

- `App_Config/Include` files load alphabetically after Sitecore's own files, and the module patches by processor type (FilterUrlExtensions, ItemResolver, LayoutResolver) or by position (`processor[1]`), so the folder name does not affect ordering.
- WebForms support uses the `insertRenderings` and `renderLayout` pipelines and `RenderingReference`, which exist unchanged from 8.0 to 10.5.
- Sitecore assemblies are not strong-named, so the 8.0 build loads on later releases without redirects. Only APIs present from 8.0 are used.
- Lucene is the usual search provider on 8.x. If you use `MarkdownForAI.LlmsTxt.Source=index`, make sure the `llmsinclude` field is in the index, or keep the default `tree` source.
- The item installer uses only APIs available in 8.0 (`ItemManager.AddFromTemplate` with fixed IDs, `EditContext`). Every item gets a fixed ID; `/sitecore/templates/Modules`, when it has to be created, gets the ID Sitecore PowerShell Extensions uses for it.
- **Moving to 10.1 or later:** switch to Sitecore.MarkdownForAI, remove the installer config and move your settings file to the same name in the new edition (rewrite `patch:attribute` as `set:value` if you like; both work). The items have the same IDs and paths in both editions, so nothing moves and pages that inherit `_LlmsPage` keep their values.
- Test on your exact version: this build is compiled against 8.0 and has been checked to compile, but not yet run on every release from 8.0 to 10.0.
