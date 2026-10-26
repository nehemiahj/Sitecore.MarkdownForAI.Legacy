using Sitecore.MarkdownForAI.Core;
using Xunit;

namespace Sitecore.MarkdownForAI.Tests
{
    public class MarkedRegionsTests
    {
        private const string Token = "abc123";

        private static string Marked(string inner)
        {
            return MarkedRegions.BeginMarker(Token) + inner + MarkedRegions.EndMarker(Token);
        }

        [Fact]
        public void Only_marked_regions_are_returned_in_page_order()
        {
            string html = "<body><form id=\"mainform\"><header>Logo and menu</header>" +
                          Marked("<h1>Title</h1>") + "<aside>Static sidebar</aside>" + Marked("<p>Body</p>") +
                          "<footer>Footer</footer></form></body>";

            Assert.Equal("<h1>Title</h1>\n<p>Body</p>\n", MarkedRegions.Extract(html, Token));
        }

        [Fact]
        public void Markers_with_another_token_are_ignored()
        {
            string html = MarkedRegions.BeginMarker("other") + "<p>x</p>" + MarkedRegions.EndMarker("other");

            Assert.Null(MarkedRegions.Extract(html, Token));
            Assert.Null(MarkedRegions.Extract("<p>no markers</p>", Token));
            Assert.Null(MarkedRegions.Extract(MarkedRegions.BeginMarker(Token) + "<p>never closed</p>", Token));
        }

        [Fact]
        public void Body_fallback_drops_the_server_form_wrapper()
        {
            string html = "<html><head><title>T</title></head><body class=\"x\">\n<form method=\"post\" action=\"./\" id=\"mainform\">" +
                          "<div><input type=\"hidden\" name=\"__VIEWSTATE\" /></div><p>Hello</p></form></body></html>";

            Assert.Equal("\n<div><input type=\"hidden\" name=\"__VIEWSTATE\" /></div><p>Hello</p>", MarkedRegions.BodyWithoutForms(html));
        }
    }
}
