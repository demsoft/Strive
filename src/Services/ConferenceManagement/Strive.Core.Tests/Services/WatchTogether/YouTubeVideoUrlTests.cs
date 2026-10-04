using Strive.Core.Services.WatchTogether;
using Xunit;

namespace Strive.Core.Tests.Services.WatchTogether
{
    public class YouTubeVideoUrlTests
    {
        [Theory]
        [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
        [InlineData("https://youtube.com/watch?v=dQw4w9WgXcQ&list=PL123&index=2", "dQw4w9WgXcQ")]
        [InlineData("https://www.youtube.com/watch?feature=share&v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
        [InlineData("https://m.youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
        [InlineData("https://music.youtube.com/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
        [InlineData("https://youtu.be/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
        [InlineData("https://youtu.be/dQw4w9WgXcQ?si=abc", "dQw4w9WgXcQ")]
        [InlineData("https://www.youtube.com/live/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
        [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
        [InlineData("https://www.youtube.com/embed/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
        [InlineData("https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ", "dQw4w9WgXcQ")]
        [InlineData("  https://youtu.be/dQw4w9WgXcQ  ", "dQw4w9WgXcQ")]
        [InlineData("HTTPS://WWW.YOUTUBE.COM/watch?v=dQw4w9WgXcQ", "dQw4w9WgXcQ")]
        public void TryParse_LinkToAVideo_FindsTheId(string url, string expected)
        {
            Assert.True(YouTubeVideoUrl.TryParse(url, out var id, out _));
            Assert.Equal(expected, id);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        [InlineData("dQw4w9WgXcQ")]
        [InlineData("not a url")]
        [InlineData("https://www.youtube.com/")]
        [InlineData("https://www.youtube.com/watch")]
        [InlineData("https://www.youtube.com/watch?v=short")]
        [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQextra")]
        [InlineData("https://www.youtube.com/watch?v=dQw4w9Wg<>>")]
        [InlineData("https://www.youtube.com/playlist?list=PL123")]
        [InlineData("https://www.youtube.com/@channel")]
        [InlineData("https://vimeo.com/123456789")]
        [InlineData("https://evil.com/watch?v=dQw4w9WgXcQ")]
        [InlineData("https://youtube.com.evil.com/watch?v=dQw4w9WgXcQ")]
        [InlineData("https://notyoutube.com/watch?v=dQw4w9WgXcQ")]
        [InlineData("javascript:alert(1)")]
        [InlineData("ftp://youtu.be/dQw4w9WgXcQ")]
        public void TryParse_SomethingElse_IsRejected(string? url)
        {
            Assert.False(YouTubeVideoUrl.TryParse(url, out var id, out var start));
            Assert.Equal(string.Empty, id);
            Assert.Equal(0, start);
        }

        [Fact]
        public void TryParse_VeryLongUrl_IsRejected()
        {
            Assert.False(YouTubeVideoUrl.TryParse("https://youtu.be/dQw4w9WgXcQ?x=" + new string('a', 3000), out _, out _));
        }

        [Theory]
        [InlineData("https://youtu.be/dQw4w9WgXcQ", 0)]
        [InlineData("https://youtu.be/dQw4w9WgXcQ?t=90", 90)]
        [InlineData("https://youtu.be/dQw4w9WgXcQ?t=90s", 90)]
        [InlineData("https://youtu.be/dQw4w9WgXcQ?t=1m30s", 90)]
        [InlineData("https://youtu.be/dQw4w9WgXcQ?t=1h2m3s", 3723)]
        [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ&t=45", 45)]
        [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ&start=45", 45)]
        [InlineData("https://youtu.be/dQw4w9WgXcQ#t=30", 30)]
        [InlineData("https://youtu.be/dQw4w9WgXcQ?t=-5", 0)]
        [InlineData("https://youtu.be/dQw4w9WgXcQ?t=abc", 0)]
        [InlineData("https://youtu.be/dQw4w9WgXcQ?t=999999999", 0)]
        public void TryParse_StartTime(string url, double expectedSeconds)
        {
            Assert.True(YouTubeVideoUrl.TryParse(url, out _, out var start));
            Assert.Equal(expectedSeconds, start);
        }
    }
}
