using Strive.Core.Services.Payments;
using Xunit;

namespace Strive.Core.Tests.Services.Payments
{
    public class SlugRulesTests
    {
        [Theory]
        [InlineData("ada")]
        [InlineData("ada-lovelace")]
        [InlineData("dj-2k")]
        [InlineData("abc123")]
        [InlineData("123")]
        [InlineData("abcdefghijklmnopqrstuvwxyz1234")] // 30
        public void Problem_ValidNames_AreFine(string slug)
        {
            Assert.Null(SlugRules.Problem(slug));
        }

        [Theory]
        [InlineData("ab")]
        [InlineData("abcdefghijklmnopqrstuvwxyz12345")] // 31
        [InlineData("-ada")]
        [InlineData("ada-")]
        [InlineData("ada--lovelace")]
        [InlineData("ada lovelace")]
        [InlineData("ada_lovelace")]
        [InlineData("Ada")]
        [InlineData("adä")]
        [InlineData("ada/../x")]
        [InlineData("ada.lovelace")]
        public void Problem_InvalidNames_SayWhatIsWrong(string slug)
        {
            Assert.NotNull(SlugRules.Problem(slug));
        }

        [Theory]
        [InlineData("admin")]
        [InlineData("gbedu")]
        [InlineData("support")]
        public void Problem_ReservedNames_AreRefused(string slug)
        {
            Assert.Contains("reserved", SlugRules.Problem(slug));
        }

        [Fact]
        public void Normalize_TrimsAndLowersCapitals()
        {
            Assert.Equal("ada-lovelace", SlugRules.Normalize("  Ada-Lovelace "));
            Assert.Equal("", SlugRules.Normalize(null));
        }

        [Fact]
        public void LongestSlug_IsShorterThanAnId()
        {
            // the page of a creator is found by id or by name: a 32 character id can never be a name
            Assert.True(SlugRules.MaxLength < System.Guid.NewGuid().ToString("N").Length);
        }
    }
}
