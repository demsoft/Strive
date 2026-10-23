using System.Linq;
using System.Text;
using Strive.Core.Services.Payments;
using Xunit;

namespace Strive.Core.Tests.Services.Payments
{
    public class PhotoRulesTests
    {
        private static byte[] Pad(params byte[] head) => head.Concat(Enumerable.Repeat((byte) 1, 40)).ToArray();

        [Fact]
        public void Sniff_RecognisesJpegPngAndWebp()
        {
            Assert.Equal("image/jpeg", PhotoRules.Sniff(Pad(0xFF, 0xD8, 0xFF, 0xE1)));
            Assert.Equal("image/png", PhotoRules.Sniff(Pad(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A)));
            Assert.Equal("image/webp", PhotoRules.Sniff(
                Encoding.ASCII.GetBytes("RIFF").Concat(new byte[] {1, 2, 3, 4}).Concat(Encoding.ASCII.GetBytes("WEBPVP8 "))
                    .ToArray()));
        }

        [Theory]
        [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>")]
        [InlineData("<html><body>hello</body></html>")]
        [InlineData("GIF89a......................")]
        [InlineData("RIFF....WAVEfmt ......")] // a RIFF file that is not an image
        public void Sniff_EverythingElseIsNoPhoto(string text)
        {
            Assert.Null(PhotoRules.Sniff(Encoding.ASCII.GetBytes(text)));
        }

        [Fact]
        public void Sniff_TooShortOrEmpty_IsNoPhoto()
        {
            Assert.Null(PhotoRules.Sniff(new byte[0]));
            Assert.Null(PhotoRules.Sniff(new byte[] {0xFF, 0xD8, 0xFF}));
        }

        [Fact]
        public void Sniff_PngThatIsCutAfterTheFirstBytes_IsNoPhoto()
        {
            // the whole signature is checked, not just the first byte
            Assert.Null(PhotoRules.Sniff(Pad(0x89, 0x50, 0x4E, 0x47, 0x00, 0x00, 0x00, 0x00)));
        }
    }
}
