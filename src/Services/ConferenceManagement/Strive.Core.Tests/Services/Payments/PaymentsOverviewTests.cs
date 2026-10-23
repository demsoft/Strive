using System;
using System.Collections.Generic;
using System.Linq;
using Strive.Core.Services.Payments;
using Strive.Core.Services.Payments.Sessions;
using Xunit;

namespace Strive.Core.Tests.Services.Payments
{
    public class PaymentsOverviewTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

        private static TicketOrder Order(string id, long amount, int daysAgo, OrderStatus status = OrderStatus.Paid,
            OrderKind kind = OrderKind.Ticket, bool renewal = false)
        {
            var order = new TicketOrder(id, "ref" + id, kind == OrderKind.Pass ? "" : "s1", "", "buyer",
                "fan@example.com", amount, "NGN", 10, Now.AddDays(-daysAgo))
            {
                Kind = kind,
                Status = status,
                IsRenewal = renewal,
                PassCreatorId = kind == OrderKind.Pass ? "c1" : null,
                Shares = new List<RevenueShare> {new("c1", "ACCT_1", 100)},
            };
            order.Allocations = PriceSplit.Allocate(amount, 10, order.Shares.ToList()).ToList();
            return order;
        }

        [Fact]
        public void Period_CountsOnlyWhatIsInsideTheDays()
        {
            var orders = new[] {Order("a", 100_000, 1), Order("b", 100_000, 6), Order("c", 100_000, 8)};

            var week = PaymentsOverview.Period(orders, Now, 7);
            var month = PaymentsOverview.Period(orders, Now, 30);

            Assert.Equal(2, week.Tickets);
            Assert.Equal(200_000, week.GrossMinor);
            Assert.Equal(3, month.Tickets);
        }

        [Fact]
        public void Period_SplitsGrossIntoCommissionAndCreators_RefundedAndUnpaidAreLeftOut()
        {
            var orders = new[]
            {
                Order("a", 1_000_000, 1), Order("b", 500_000, 1, OrderStatus.Refunded),
                Order("c", 500_000, 1, OrderStatus.Pending), Order("d", 500_000, 1, OrderStatus.Failed),
            };

            var period = PaymentsOverview.Period(orders, Now, 7);

            Assert.Equal(1_000_000, period.GrossMinor);
            Assert.Equal(100_000, period.PlatformMinor);
            Assert.Equal(900_000, period.CreatorsMinor);
            Assert.Equal(500_000, period.RefundedMinor);
            Assert.Equal(1, period.Refunds);
        }

        [Fact]
        public void Period_PassesAndRenewalsAreCountedApart()
        {
            var orders = new[]
            {
                Order("a", 2_000_000, 1, kind: OrderKind.Pass),
                Order("b", 2_000_000, 1, kind: OrderKind.Pass, renewal: true),
                Order("c", 100_000, 1),
            };

            var period = PaymentsOverview.Period(orders, Now, 7);

            Assert.Equal(2, period.Passes);
            Assert.Equal(1, period.Renewals);
            Assert.Equal(1, period.Tickets);
        }

        [Fact]
        public void Row_NamesTheKindTheCreatorAndTheProblem_AndMasksTheBuyer()
        {
            var names = new Dictionary<string, string> {{"c1", "Ada"}};
            var titles = new Dictionary<string, string> {{"s1", "Piano"}};
            var failed = Order("a", 100_000, 1, OrderStatus.Failed);
            failed.FailureReason = "The payment failed.";
            var refund = Order("b", 100_000, 1);
            refund.RefundFailure = "Insufficient balance.";

            var failedRow = PaymentsOverview.Row(failed, names, titles);
            var refundRow = PaymentsOverview.Row(refund, names, titles);
            var renewalRow = PaymentsOverview.Row(Order("c", 1, 1, kind: OrderKind.Pass, renewal: true), names, titles);

            Assert.Equal("ticket", failedRow.Kind);
            Assert.Equal("Ada", failedRow.CreatorName);
            Assert.Equal("Piano", failedRow.SessionTitle);
            Assert.Equal("The payment failed.", failedRow.Problem);
            Assert.Equal("f**@example.com", failedRow.Buyer);
            Assert.Equal("refund: Insufficient balance.", refundRow.Problem);
            Assert.Equal("renewal", renewalRow.Kind);
            Assert.Null(renewalRow.SessionTitle);
        }
    }
}
