using System.Collections.Generic;
using Sitecore.MarkdownForAI.Core;
using Xunit;

namespace Sitecore.MarkdownForAI.Tests
{
    public class MarkdownPathTests
    {
        [Theory]
        [InlineData("/about/team.md", "/about/team")]
        [InlineData("/About/Team.MD", "/About/Team")]
        [InlineData("/index.md", "/")]
        [InlineData("/blog/index.md", "/blog/")]
        [InlineData("/.md", "/")]
        public void Page_path_from_markdown_path(string path, string expected)
        {
            string page;
            Assert.True(MarkdownPath.TryGetPagePath(path, ".md", out page));
            Assert.Equal(expected, page);
        }

        [Theory]
        [InlineData("/about/team")]
        [InlineData("/readme.mdx")]
        [InlineData(".md")]
        [InlineData("")]
        public void Non_markdown_paths(string path)
        {
            string page;
            Assert.False(MarkdownPath.TryGetPagePath(path, ".md", out page));
        }

        [Theory]
        [InlineData("https://x.com/about", "https://x.com/about.md")]
        [InlineData("https://x.com/about?a=1#top", "https://x.com/about.md?a=1#top")]
        [InlineData("https://x.com/", "https://x.com/index.md")]
        [InlineData("https://x.com", "https://x.com/index.md")]
        [InlineData("/blog/", "/blog/index.md")]
        public void Markdown_url_from_page_url(string url, string expected)
        {
            Assert.Equal(expected, MarkdownPath.ToMarkdownUrl(url, ".md"));
        }

        [Theory]
        [InlineData("?format=md", "")]
        [InlineData("?a=1&format=md&b=2", "?a=1&b=2")]
        [InlineData("a=1", "?a=1")]
        [InlineData("", "")]
        public void Remove_query_parameter(string query, string expected)
        {
            Assert.Equal(expected, MarkdownPath.RemoveQueryParameter(query, "format"));
        }
    }

    public class PlaceholderMatcherTests
    {
        [Theory]
        [InlineData("header", true)]
        [InlineData("/main/header", true)]
        [InlineData("/main/Header", true)]
        [InlineData("/main/header-{0B1C2D3E-1111-2222-3333-444455556666}-0", true)]
        [InlineData("/main/header_0b1c2d3e11112222333344445555666", true)]
        [InlineData("/main/headerimage", false)]
        [InlineData("/main/header-image", false)]
        [InlineData("/main", false)]
        [InlineData("", false)]
        public void Matches_static_and_dynamic_placeholders(string path, bool expected)
        {
            Assert.Equal(expected, PlaceholderMatcher.MatchesAny(path, new List<string> { "header", "footer" }));
        }

        [Fact]
        public void Top_level_key()
        {
            Assert.Equal("main", PlaceholderMatcher.TopLevelKey("/main/content"));
            Assert.Equal("main", PlaceholderMatcher.TopLevelKey("main"));
            Assert.Null(PlaceholderMatcher.TopLevelKey(""));
        }
    }

    public class MarkdownDocumentTests
    {
        [Fact]
        public void Front_matter_is_quoted_and_lists_are_inline()
        {
            var doc = new MarkdownDocument("# Hi")
                .Set("title", "Cats \"and\" kidneys")
                .Set("topics", new[] { "Kidney", "Diet" })
                .Set("empty", "  ");

            Assert.Equal("---\ntitle: \"Cats \\\"and\\\" kidneys\"\ntopics: [\"Kidney\", \"Diet\"]\n---\n\n# Hi\n", doc.ToString());
        }

        [Fact]
        public void No_front_matter_when_empty()
        {
            Assert.Equal("Body\n", new MarkdownDocument("Body").ToString());
        }
    }

    public class LlmsTxtWriterTests
    {
        private static readonly LlmsEntry[] Entries =
        {
            new LlmsEntry { Title = "Kidney disease", Url = "https://x.com/cats/ckd.md", Summary = "Stages and diet.", Section = "Cats" },
            new LlmsEntry { Title = "Toxic plants [list]", Url = "https://x.com/dogs/plants.md", Section = "Dogs" },
            new LlmsEntry { Title = "Hydration", Url = "https://x.com/cats/water.md", Section = "cats" },
            new LlmsEntry { Title = "Careers", Url = "https://x.com/careers.md", Optional = true }
        };

        [Fact]
        public void Index_groups_sections_in_first_seen_order()
        {
            string txt = LlmsTxtWriter.WriteIndex("Pet Library", "Vet-reviewed articles.", new[] { "Append .md to any URL." }, Entries);
            Assert.Equal(
                "# Pet Library\n\n> Vet-reviewed articles.\n\nAppend .md to any URL.\n\n" +
                "## Cats\n\n- [Kidney disease](https://x.com/cats/ckd.md): Stages and diet.\n- [Hydration](https://x.com/cats/water.md)\n\n" +
                "## Dogs\n\n- [Toxic plants \\[list\\]](https://x.com/dogs/plants.md)\n\n" +
                "## Optional\n\n- [Careers](https://x.com/careers.md)\n",
                txt);
        }

        [Fact]
        public void Full_text_truncates_and_reports_the_rest()
        {
            var entries = new[]
            {
                new LlmsEntry { Title = "A", Url = "https://x.com/a.md", Markdown = new string('a', 50) },
                new LlmsEntry { Title = "B", Url = "https://x.com/b.md", Markdown = new string('b', 50) }
            };

            string txt = LlmsTxtWriter.WriteFull("Site", null, entries, 100);
            Assert.Contains("# A", txt);
            Assert.DoesNotContain("# B", txt);
            Assert.Contains("Truncated: 1 more pages", txt);
        }
    }
}
