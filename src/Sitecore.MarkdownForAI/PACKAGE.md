# Sitecore.MarkdownForAI.Legacy

Serves Markdown versions of Sitecore MVC and WebForms pages for AI agents: `/page.md`, `?format=md`, `/llms.txt` and `/llms-full.txt`. Sitecore 8.0 to 10.0. For Sitecore 10.1 and later, use `Sitecore.MarkdownForAI`.

This package contains the assembly only, for solutions that build and deploy in CI. Reference it from the project you deploy (it has no dependencies; your solution already references Sitecore and ASP.NET MVC).

You also need, from the release on GitHub:

- **Config:** `App_Config/Include/MarkdownForAI/Sitecore.MarkdownForAI.config` (do not edit) and `z.MarkdownForAI.Site.config.example` (rename to `.config` and set your sites and excluded placeholders).
- **Items:** install the Sitecore package on your CM, or deploy `Sitecore.MarkdownForAI.Installer.config`, which creates them at startup.

All of these are in `Sitecore.MarkdownForAI.Legacy-<version>-files.zip`. Full documentation: https://github.com/nehemiahj/Sitecore.MarkdownForAI.Legacy
