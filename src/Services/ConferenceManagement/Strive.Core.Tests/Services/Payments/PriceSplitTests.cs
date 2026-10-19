using System;
using System.Linq;
using Strive.Core.Services.Payments.Sessions;
using Xunit;

namespace Strive.Core.Tests.Services.Payments
{
    public class PriceSplitTests
    {
        private static RevenueShare Share(string creator, int percent) => new(creator, "ACCT_" + creator, percent);

        [Theory]
        [InlineData(500_000, 10, 50_000)]
        [InlineData(10_000, 10, 1_000)]
        [InlineData(10_001, 10, 1_000)] // 1000.1 kobo rounds down
        [InlineData(10_005, 10, 1_001)] // 1000.5 rounds up
        [InlineData(333_333, 10, 33_333)]
        [InlineData(500_000, 0, 0)]
        public void PlatformAmount_IsTheCommissionInWholeKobo(long price, int percent, long expected)
        {
            Assert.Equal(expected, PriceSplit.PlatformAmount(price, percent));
        }

        [Fact]
        public void Allocate_OneCreator_GetsEverythingAfterTheCommission()
        {
            var parts = PriceSplit.Allocate(500_000, 10, new[] {Share("ada", 100)});

            Assert.Equal(new OrderAllocation(OrderAllocation.Platform, 50_000), parts[0]);
            Assert.Equal(new OrderAllocation("ada", 450_000), parts[1]);
        }

        [Fact]
        public void Allocate_SeveralCreators_ShareWhatIsLeftByTheirPercentages()
        {
            var parts = PriceSplit.Allocate(500_000, 10, new[] {Share("ada", 60), Share("kofi", 40)});

            Assert.Equal(50_000, parts.Single(x => x.Recipient == OrderAllocation.Platform).AmountMinor);
            Assert.Equal(270_000, parts.Single(x => x.Recipient == "ada").AmountMinor);
            Assert.Equal(180_000, parts.Single(x => x.Recipient == "kofi").AmountMinor);
        }

        [Theory]
        [InlineData(10_001L, 7)]
        [InlineData(99_999L, 13)]
        [InlineData(123_457L, 10)]
        [InlineData(10_000L, 33)]
        public void Allocate_NeverLosesOrInventsAKobo(long price, int commission)
        {
            var parts = PriceSplit.Allocate(price, commission, new[] {Share("a", 33), Share("b", 33), Share("c", 34)});

            Assert.Equal(price, parts.Sum(x => x.AmountMinor));
            Assert.All(parts, x => Assert.True(x.AmountMinor >= 0));
        }

        [Theory]
        [InlineData(99)]
        [InlineData(101)]
        public void Allocate_SharesNotAddingUpTo100_Refused(int percent)
        {
            Assert.Throws<ArgumentException>(() => PriceSplit.Allocate(100_000, 10, new[] {Share("ada", percent)}));
        }

        [Fact]
        public void Allocate_NoShares_Refused()
        {
            Assert.Throws<ArgumentException>(() => PriceSplit.Allocate(100_000, 10, Array.Empty<RevenueShare>()));
        }
    }
}
