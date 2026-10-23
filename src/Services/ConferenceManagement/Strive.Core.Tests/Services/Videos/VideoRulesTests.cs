using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Moq;
using Strive.Core.Services.Payments;
using Strive.Core.Services.Payments.Gateways;
using Strive.Core.Services.Videos;
using Xunit;

namespace Strive.Core.Tests.Services.Videos
{
    public class VideoRulesTests
    {
        [Theory]
        [InlineData(0, true)]
        [InlineData(10_000, true)]
        [InlineData(100_000_000, true)]
        [InlineData(9_999, false)]
        [InlineData(1, false)]
        [InlineData(-1, false)]
        [InlineData(100_000_001, false)]
        public void IsValidPrice_FreeOrBetweenTheLimitsOfATicket(long price, bool valid)
        {
            Assert.Equal(valid, VideoRules.IsValidPrice(price));
        }

        private static CreatorProfile Creator(bool canSell) =>
            new("c1", "u1", "Ada", DateTimeOffset.UtcNow) {ProviderSubaccountCode = canSell ? "ACCT_1" : null};

        [Fact]
        public void Problem_FreeIsAlwaysFine_EvenForACreatorWhoCannotBePaid()
        {
            Assert.Null(VideoPrices.Problem(0, Creator(false), new PaymentsOptions {Enabled = false}));
        }

        [Fact]
        public void Problem_APriceNeedsACreatorWhoCanBePaid_AndPaymentsSwitchedOn()
        {
            Assert.NotNull(VideoPrices.Problem(300_000, Creator(false), new PaymentsOptions {Enabled = true}));
            Assert.NotNull(VideoPrices.Problem(300_000, Creator(true), new PaymentsOptions {Enabled = false}));
            Assert.Null(VideoPrices.Problem(300_000, Creator(true), new PaymentsOptions {Enabled = true}));
        }

        [Fact]
        public void Problem_OutOfRange_IsRefusedBeforeAnythingElse()
        {
            Assert.NotNull(VideoPrices.Problem(50, Creator(true), new PaymentsOptions {Enabled = true}));
        }

        [Fact]
        public void TheKeysOfAVideoAreUnderItsOwnFolder()
        {
            Assert.Equal("videos/v1/video.mp4", VideoRules.VideoKey("v1"));
            Assert.Equal("videos/v1/poster.jpg", VideoRules.PosterKey("v1"));
        }
    }

    public class VideoAccessTests
    {
        private static Video V(string id, string creator, string owner, long price) =>
            new(id, creator, owner, "t", DateTimeOffset.UtcNow) {PriceMinor = price};

        private static VideoAccess Access(IEnumerable<string>? held = null, IEnumerable<string>? passOf = null,
            Mock<IVideoEntitlementRepo>? entitlements = null, Mock<IPassRepo>? passes = null)
        {
            entitlements ??= new Mock<IVideoEntitlementRepo>();
            entitlements.Setup(x => x.HeldAmong(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
                .ReturnsAsync((string _, IEnumerable<string> ids) => ids.Intersect(held ?? Array.Empty<string>()).ToList());
            passes ??= new Mock<IPassRepo>();
            passes.Setup(x => x.ActiveUntil(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTimeOffset>()))
                .ReturnsAsync((string _, string creatorId, DateTimeOffset now) =>
                    (passOf ?? Array.Empty<string>()).Contains(creatorId) ? now.AddDays(5) : null);
            return new VideoAccess(entitlements.Object, passes.Object, TimeProvider.System);
        }

        [Fact]
        public async Task Free_IsForEverybody_EvenWhenNotSignedIn()
        {
            var free = V("a", "c1", "u1", 0);

            Assert.True(await Access().CanWatch(free, null));
            Assert.True(await Access().CanWatch(free, "somebody"));
        }

        [Fact]
        public async Task Paid_NotSignedIn_IsLocked_AndNothingIsAsked()
        {
            var entitlements = new Mock<IVideoEntitlementRepo>();
            var passes = new Mock<IPassRepo>();

            Assert.False(await Access(entitlements: entitlements, passes: passes).CanWatch(V("a", "c1", "u1", 5000), null));

            entitlements.VerifyNoOtherCalls();
            passes.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task Paid_TheOwnerBuyerAndPassHolderMayWatch_NobodyElse()
        {
            var video = V("a", "c1", "owner", 5000);

            Assert.True(await Access().CanWatch(video, "owner"));
            Assert.True(await Access(held: new[] {"a"}).CanWatch(video, "buyer"));
            Assert.True(await Access(passOf: new[] {"c1"}).CanWatch(video, "passholder"));
            Assert.False(await Access(passOf: new[] {"c2"}).CanWatch(video, "otherpass"));
            Assert.False(await Access().CanWatch(video, "stranger"));
        }

        [Fact]
        public async Task ForAList_EachPaidVideoIsDecidedOnItsOwn_AndAPassIsAskedOncePerCreator()
        {
            var videos = new[]
            {
                V("free", "c1", "u1", 0), V("own", "c9", "me", 5000), V("bought", "c2", "u2", 5000),
                V("locked", "c3", "u3", 5000), V("p1", "c4", "u4", 5000), V("p2", "c4", "u4", 5000),
            };
            var passes = new Mock<IPassRepo>();

            var unlocked = await Access(held: new[] {"bought"}, passOf: new[] {"c4"}, passes: passes)
                .Unlocked(videos, "me");

            Assert.Equal(new[] {"bought", "free", "own", "p1", "p2"}, unlocked.OrderBy(x => x));
            // the passes of c3 and c4 were asked, once each; c1, c9 and c2 needed no question
            passes.Verify(x => x.ActiveUntil("me", "c4", It.IsAny<DateTimeOffset>()), Times.Once);
            passes.Verify(x => x.ActiveUntil("me", "c3", It.IsAny<DateTimeOffset>()), Times.Once);
            passes.Verify(x => x.ActiveUntil("me", "c2", It.IsAny<DateTimeOffset>()), Times.Never);
        }
    }
}
