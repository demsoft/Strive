using Strive.Core.Services.Payments;
using Strive.Core.Services.Reminders;
using Xunit;

namespace Strive.Core.Tests.Services.Reminders
{
    public class PlatformBrandTests
    {
        private static RemindersOptions Options(string arena = "https://arena.test") =>
            new() {LinkBaseUrl = "https://gbedu.test/", ArenaLinkBaseUrl = arena};

        private static CreatorProfile Creator(OrganizerStatus status) =>
            new("c1", "u1", "Ada", System.DateTimeOffset.UtcNow) {OrganizerStatus = status};

        [Fact]
        public void Of_ACreator_IsGbeduWithItsOwnAddress()
        {
            var brand = PlatformBrand.Of(Options(), Creator(OrganizerStatus.None));

            Assert.Equal("Gbedu", brand.Name);
            Assert.Equal("https://gbedu.test/s/abc", brand.Link("s/abc"));
        }

        [Fact]
        public void Of_AnApprovedOrganizer_IsArena()
        {
            var brand = PlatformBrand.Of(Options(), Creator(OrganizerStatus.Approved));

            Assert.Equal("Arena", brand.Name);
            Assert.Equal("https://arena.test/tickets", brand.Link("tickets"));
        }

        [Theory]
        [InlineData(OrganizerStatus.Requested)]
        [InlineData(OrganizerStatus.Rejected)]
        public void Of_AnOrganizerThatIsNotApproved_IsGbedu(OrganizerStatus status)
        {
            Assert.Equal("Gbedu", PlatformBrand.Of(Options(), Creator(status)).Name);
        }

        [Fact]
        public void Of_ArenaWithoutAnAddressOfItsOwn_UsesTheUsualOne()
        {
            var brand = PlatformBrand.Of(Options(arena: ""), Creator(OrganizerStatus.Approved));

            Assert.Equal("Arena", brand.Name);
            Assert.Equal("https://gbedu.test/tickets", brand.Link("tickets"));
        }
    }
}
