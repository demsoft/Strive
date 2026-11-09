using Strive.Core.Services.Payments.Sessions;
using Xunit;

namespace Strive.Core.Tests.Services.Payments
{
    public class MatchRulesTests
    {
        private static MatchInput Input(string home = "Lagos FC", string away = "Abuja United", string? competition = "Cup",
            string basis = RightsBasis.Own, string? note = null, bool accepted = true) =>
            new(home, away, competition, basis, note, accepted);

        [Fact]
        public void Validate_OwnMatch_NeedsNoNote()
        {
            var (match, note, error) = MatchRules.Validate(Input(home: "  Lagos FC ", competition: " "));

            Assert.Null(error);
            Assert.Null(note);
            Assert.Equal(new MatchDetails("Lagos FC", "Abuja United", null), match);
        }

        [Theory]
        [InlineData(RightsBasis.Licensed)]
        [InlineData(RightsBasis.Permission)]
        public void Validate_LicenceOrPermission_NeedsWhoGaveIt(string basis)
        {
            Assert.NotNull(MatchRules.Validate(Input(basis: basis)).Error);
            Assert.NotNull(MatchRules.Validate(Input(basis: basis, note: "ab")).Error);
            Assert.Null(MatchRules.Validate(Input(basis: basis, note: "Lagos State FA")).Error);
        }

        [Fact]
        public void Validate_WithoutTheDeclaration_IsRefused()
        {
            Assert.NotNull(MatchRules.Validate(Input(accepted: false)).Error);
            Assert.NotNull(MatchRules.Validate(Input(basis: "pirated")).Error);
        }

        [Theory]
        [InlineData("", "Abuja")]
        [InlineData("A", "Abuja")]
        [InlineData("Lagos FC", "lagos fc")]
        public void Validate_BadTeams_AreRefused(string home, string away)
        {
            Assert.NotNull(MatchRules.Validate(Input(home: home, away: away)).Error);
        }

        [Fact]
        public void Validate_TooLongText_IsRefused()
        {
            Assert.NotNull(MatchRules.Validate(Input(home: new string('a', 41))).Error);
            Assert.NotNull(MatchRules.Validate(Input(competition: new string('a', 61))).Error);
            Assert.NotNull(MatchRules.Validate(Input(basis: RightsBasis.Own, note: new string('a', 301))).Error);
        }
    }
}
