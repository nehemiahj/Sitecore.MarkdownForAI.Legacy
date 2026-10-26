using System;
using Sitecore.MarkdownForAI.Core;
using Xunit;

namespace Sitecore.MarkdownForAI.Tests
{
    public class MarkdownConverterTests
    {
        private static string Convert(string html, string baseUrl = "https://www.example.com/pets/cats/")
        {
            var options = new MarkdownConverterOptions { BaseUri = new Uri(baseUrl) };
            return new MarkdownConverter(options).Convert(html).Replace("\r\n", "\n");
        }

        [Fact]
        public void Headings_paragraphs_and_inline_formatting()
        {
            string md = Convert("<h1>Kidney care</h1><p>Cats with <strong>CKD</strong> need <em>more</em> water.</p><h3> Signs </h3>");
            Assert.Equal("# Kidney care\n\nCats with **CKD** need *more* water.\n\n### Signs", md);
        }

        [Fact]
        public void Emphasis_keeps_surrounding_spaces_outside_the_markers()
        {
            Assert.Equal("Drink **plenty** of water", Convert("<p>Drink<b> plenty </b>of water</p>"));
        }

        [Fact]
        public void Nested_bold_is_not_doubled()
        {
            Assert.Equal("**x**", Convert("<p><b><strong>x</strong></b></p>"));
        }

        [Fact]
        public void Links_and_images_are_made_absolute()
        {
            string md = Convert("<p><a href=\"../dogs/\">Dogs</a> <img src=\"/-/media/cat.jpg\" alt=\"A cat\"></p>");
            Assert.Equal("[Dogs](https://www.example.com/pets/dogs/) ![A cat](https://www.example.com/-/media/cat.jpg)", md);
        }

        [Fact]
        public void Javascript_and_anchor_links_keep_only_their_text()
        {
            Assert.Equal("Top and print", Convert("<p><a href=\"#top\">Top</a> and <a href=\"javascript:print()\">print</a></p>"));
        }

        [Fact]
        public void Lazy_loaded_images_use_data_src_and_data_uris_are_dropped()
        {
            Assert.Equal("![x](https://cdn.example.com/a.png)", Convert("<img alt=\"x\" src=\"data:image/gif;base64,R0l\" data-src=\"https://cdn.example.com/a.png\">"));
            Assert.Equal(string.Empty, Convert("<img src=\"data:image/gif;base64,R0l\">"));
        }

        [Fact]
        public void Unordered_and_nested_lists()
        {
            string md = Convert("<ul><li>Water<ul><li>Fountain</li><li>Wet food</li></ul></li><li>Diet</li></ul>");
            Assert.Equal("- Water\n  - Fountain\n  - Wet food\n- Diet", md);
        }

        [Fact]
        public void Ordered_list_honours_start_and_unclosed_items()
        {
            string md = Convert("<ol start=\"3\"><li>Weigh<li>Measure</ol>");
            Assert.Equal("3. Weigh\n4. Measure", md);
        }

        [Fact]
        public void List_item_with_paragraphs()
        {
            string md = Convert("<ul><li><p>First</p><p>Second</p></li></ul>");
            Assert.Equal("- First\n\n  Second", md);
        }

        [Fact]
        public void Tables_become_pipe_tables_and_escape_pipes()
        {
            string html = "<table><thead><tr><th>Stage</th><th>Creatinine</th></tr></thead>" +
                          "<tbody><tr><td>1</td><td>&lt; 1.6</td></tr><tr><td>2</td><td>1.6 | 2.8</td></tr></tbody></table>";
            Assert.Equal("| Stage | Creatinine |\n| --- | --- |\n| 1 | < 1.6 |\n| 2 | 1.6 \\| 2.8 |", Convert(html));
        }

        [Fact]
        public void One_column_layout_tables_are_unwrapped()
        {
            Assert.Equal("Hello\n\nWorld", Convert("<table><tr><td><p>Hello</p></td></tr><tr><td>World</td></tr></table>"));
        }

        [Fact]
        public void Skipped_and_hidden_elements_are_removed()
        {
            string html = "<nav>Menu</nav><p>Keep</p><script>alert(1)</script><style>p{}</style>" +
                          "<div data-md-ignore>Ignore</div><div hidden>Hidden</div><span aria-hidden=\"true\">x</span>" +
                          "<div style=\"display: none\">None</div><form><input></form><!-- comment -->";
            Assert.Equal("Keep", Convert(html));
        }

        [Fact]
        public void Ignore_classes_are_configurable()
        {
            var options = new MarkdownConverterOptions();
            options.IgnoreClasses.Add("print-only");
            Assert.Equal("Keep", new MarkdownConverter(options).Convert("<p class=\"a print-only\">Drop</p><p>Keep</p>"));
        }

        [Fact]
        public void Raw_markdown_scripts_are_emitted_as_is_and_dedented()
        {
            string html = "<div><script type=\"text/x-markdown\">\n    ## Raw\n\n    - **a**\n    - b\n  </script></div>";
            Assert.Equal("## Raw\n\n- **a**\n- b", Convert(html));
        }

        [Fact]
        public void Escaped_script_close_inside_raw_markdown_is_restored()
        {
            Assert.Equal("a </script> b", Convert("<script type=\"text/x-markdown\">a <\\/script> b</script>"));
        }

        [Fact]
        public void Code_blocks_keep_whitespace_and_language()
        {
            string md = Convert("<pre><code class=\"language-json\">{\n  \"a\": 1\n}</code></pre>");
            Assert.Equal("```json\n{\n  \"a\": 1\n}\n```", md);
        }

        [Fact]
        public void Inline_code_and_text_escaping()
        {
            Assert.Equal("Use `a*b` not a\\*b or \\[x\\] or snake_case", Convert("<p>Use <code>a*b</code> not a*b or [x] or snake_case</p>"));
        }

        [Fact]
        public void Paragraphs_that_look_like_markdown_are_escaped()
        {
            Assert.Equal("\\# not a heading", Convert("<p># not a heading</p>"));
            Assert.Equal("1\\. not a list", Convert("<p>1. not a list</p>"));
        }

        [Fact]
        public void Line_breaks_become_hard_breaks()
        {
            Assert.Equal("Line one  \nLine two", Convert("<p>Line one<br/> Line two</p>"));
        }

        [Fact]
        public void Blockquotes_are_prefixed()
        {
            Assert.Equal("> Call your vet\n>\n> Today", Convert("<blockquote><p>Call your vet</p><p>Today</p></blockquote>"));
        }

        [Fact]
        public void Alert_markers_are_kept()
        {
            Assert.Equal("> [!WARNING]\n> **Hot cars** kill.", Convert("<blockquote><p>[!WARNING]\n<strong>Hot cars</strong> kill.</p></blockquote>"));
            Assert.Equal("> [!NOTE]\n> Read this.", Convert("<blockquote><p>[!note] Read this.</p></blockquote>"));
            Assert.Equal("> \\[!SOON\\] Not an alert.", Convert("<blockquote><p>[!SOON] Not an alert.</p></blockquote>"));
        }

        [Fact]
        public void Unclosed_paragraphs_and_entities()
        {
            Assert.Equal("One &amp; two\n\nThree", Convert("<p>One &amp;amp; two<p>Three"));
            Assert.Equal("Tom & Jerry", Convert("<p>Tom &amp; Jerry&nbsp;</p>"));
        }

        [Fact]
        public void Custom_elements_wrapping_blocks_are_blocks()
        {
            Assert.Equal("Intro\n\n## Title\n\nBody", Convert("<my-card>Intro<h2>Title</h2><p>Body</p></my-card>"));
        }

        [Fact]
        public void Definition_lists_and_horizontal_rules()
        {
            Assert.Equal("**SDMA**\n\nA kidney marker\n\n---", Convert("<dl><dt>SDMA</dt><dd>A kidney marker</dd></dl><hr>"));
        }

        [Fact]
        public void Empty_input()
        {
            Assert.Equal(string.Empty, Convert(string.Empty));
            Assert.Equal(string.Empty, new MarkdownConverter().Convert((string)null));
        }
    }
}
