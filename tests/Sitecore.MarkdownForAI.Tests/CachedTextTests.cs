using System;
using Sitecore.MarkdownForAI.Core;
using Xunit;

namespace Sitecore.MarkdownForAI.Tests
{
    public class CachedTextTests
    {
        private static readonly DateTime Generated = new DateTime(2026, 10, 28, 12, 30, 45, 678, DateTimeKind.Utc);

        [Fact]
        public void ETag_depends_only_on_the_body()
        {
            var a = new CachedText("# Title\n\nBody", Generated);
            var b = new CachedText("# Title\n\nBody", Generated.AddHours(1));
            var c = new CachedText("# Title\n\nBody!", Generated);

            Assert.Equal(a.ETag, b.ETag);
            Assert.NotEqual(a.ETag, c.ETag);
            Assert.StartsWith("\"", a.ETag);
            Assert.EndsWith("\"", a.ETag);
        }

        [Fact]
        public void Last_modified_is_truncated_to_seconds()
        {
            Assert.Equal(new DateTime(2026, 10, 28, 12, 30, 45, DateTimeKind.Utc), new CachedText("x", Generated).LastModified);
        }

        [Fact]
        public void If_none_match_decides_first()
        {
            var entry = new CachedText("body", Generated);

            Assert.True(entry.IsNotModified(entry.ETag, null));
            Assert.True(entry.IsNotModified("\"other\", " + entry.ETag, null));
            Assert.True(entry.IsNotModified("W/" + entry.ETag, null));
            Assert.True(entry.IsNotModified("*", null));
            Assert.False(entry.IsNotModified("\"other\"", "Wed, 28 Oct 2026 13:00:00 GMT"));
        }

        [Theory]
        [InlineData("Wed, 28 Oct 2026 12:30:45 GMT", true)]
        [InlineData("Wed, 28 Oct 2026 13:00:00 GMT", true)]
        [InlineData("Wed, 28 Oct 2026 12:30:44 GMT", false)]
        [InlineData("not a date", false)]
        [InlineData(null, false)]
        public void If_modified_since_is_used_without_an_etag(string ifModifiedSince, bool expected)
        {
            Assert.Equal(expected, new CachedText("body", Generated).IsNotModified(null, ifModifiedSince));
        }
    }
}
