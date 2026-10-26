# Writing a good llms.txt

`/llms.txt` is a plain Markdown file at the root of a site that tells AI agents what the site contains and where the clean versions of its pages are. The format comes from [llmstxt.org](https://llmstxt.org). This module generates it from items; this page is about making it useful.

## The format

```markdown
# Site name

> One paragraph: what the site is, who it is for, and any important caveat.

Optional notes in plain paragraphs or lists (no headings).

## Section name

- [Page title](https://www.example.com/section/page.md): One-sentence summary.

## Optional

- [Less important page](https://www.example.com/careers.md): Agents may skip this section.
```

Rules that matter:

- **One H1**, then a **blockquote summary**, then **H2 sections of link lists**. Agents parse this structure.
- **Absolute URLs** that point at the **Markdown** version (`.md`), not the HTML page.
- **`## Optional`** is special: it marks links an agent can drop when its context is short.

## How the module fills it

| llms.txt part | Comes from |
|---|---|
| H1 | Start item **LlmsTitle**, else the site name |
| Blockquote | Start item **LlmsSummary** |
| Note | Automatic: "append `.md` to any page URL" |
| Sections | **LlmsSection** of each included page, in first-seen order (content tree order with `LlmsTxt.Source=tree`, item path order with `index`) |
| Link text | **LlmsTitle**, else `MarkdownForAI.TitleFields`, else display name |
| Summary | **LlmsSummary**, else `MarkdownForAI.DescriptionFields` |
| `## Optional` | Pages with **LlmsOptional** checked |

## Best practices for a content library (e.g. a pet health library)

1. **Write the summary for an agent deciding whether to read on.** Say what the library covers, who wrote or reviewed it, and that it is not a substitute for professional advice:
   > Veterinarian-reviewed articles on dog and cat health, nutrition, behavior and senior care. General information only; it does not replace an examination by a veterinarian.
2. **Group by the reader's question, not by your CMS tree.** Sections such as `Cats: Kidney and urinary`, `Dogs: Toxins and poisoning`, `Nutrition` beat `Articles 2019`.
3. **Make every summary a real sentence with the key terms.** "Stages, diet and home care for chronic kidney disease (CKD) in cats." An agent picks pages from these lines alone.
4. **Keep llms.txt an index.** Put full text in `llms-full.txt` and the per-page `.md` URLs. A huge llms.txt defeats its purpose.
5. **List only canonical pages.** Leave out tag pages, search results, paginated listings, duplicates under other URLs (for example the same article mounted under many microsites) and anything marked `noindex`.
6. **Use `## Optional`** for pages that are useful but not core: careers, press, locations, legal.
7. **Exclude content you cannot redistribute.** Syndicated or licensed articles may not allow AI reuse. Leave **LlmsInclude** unchecked for them.
8. **Keep it fresh.** The module caches llms.txt (`MarkdownForAI.LlmsTxt.CacheMinutes`) and clears it on publish. Set `last_reviewed` dates in your content so agents can judge age.
9. **Large libraries:** switch `MarkdownForAI.LlmsTxt.Source` to `index`, and if there are thousands of articles, list hub pages per topic in llms.txt and let each hub's `.md` link to its articles.
10. **Point to it.** Add a comment line to `robots.txt` (`# LLM index: https://www.example.com/llms.txt`) and mention it in your developer or partner documentation.

## llms-full.txt

`/llms-full.txt` puts every included page in one file, separated by `---`, each with a `# Title` and `Source:` line. Agents that can take a large context read the whole library in one request.

- Full bodies come from field mappings or custom sources. Pages without one contribute their summary only.
- The size is capped by `MarkdownForAI.LlmsTxt.FullMaxChars` (default 5,000,000 characters). When the cap is reached the file says how many pages were left out.

## Example

See [`examples/llms.txt`](../examples/llms.txt).
