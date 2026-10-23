using System;
using System.Collections.Generic;
using System.Linq;
using Strive.Core.Services.Payments;
using Strive.Core.Services.Payments.Sessions;
using Xunit;

namespace Strive.Core.Tests.Services.Payments
{
    public class EarningsReportTests
    {
        private static readonly DateTimeOffset T0 = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

        private static TicketOrder Order(string id, long amount, OrderStatus status = OrderStatus.Paid,
            string session = "s1", OrderKind kind = OrderKind.Ticket, int minutes = 0, string email = "fan@example.com")
        {
            var order = new TicketOrder(id, "ref" + id, kind == OrderKind.Pass ? "" : session, "", "buyer", email,
                amount, "NGN", 10, T0.AddMinutes(minutes))
            {
                Kind = kind,
                Status = status,
                PaidAt = T0.AddMinutes(minutes),
            };
            order.Allocations = PriceSplit.Allocate(amount, 10, new[] {new RevenueShare("c1", "ACCT_1", 100)}).ToList();
            return order;
        }

        private static readonly IReadOnlyDictionary<string, string> Titles =
            new Dictionary<string, string> {{"s1", "Piano"}, {"s2", "Drums"}};

        [Fact]
        public void Build_TotalsSplitGrossIntoCommissionAndCreatorShare()
        {
            var report = EarningsReport.Build("c1", new[] {Order("a", 500_000), Order("b", 500_000)}, Titles);

            Assert.Equal(1_000_000, report.Totals.GrossMinor);
            Assert.Equal(100_000, report.Totals.PlatformMinor);
            Assert.Equal(900_000, report.Totals.NetMinor);
            Assert.Equal(2, report.Totals.Tickets);
            Assert.Equal(0, report.Totals.Passes);
        }

        [Fact]
        public void Build_RefundedOrders_AreNotEarnings_ButAreCounted()
        {
            var report = EarningsReport.Build("c1",
                new[] {Order("a", 500_000), Order("b", 300_000, OrderStatus.Refunded)}, Titles);

            Assert.Equal(500_000, report.Totals.GrossMinor);
            Assert.Equal(450_000, report.Totals.NetMinor);
            Assert.Equal(300_000, report.Totals.RefundedMinor);
            Assert.Equal(1, report.Totals.Refunds);
            Assert.Equal(1, report.Totals.Tickets);
        }

        [Fact]
        public void Build_PendingAndFailedOrders_AreIgnored()
        {
            var report = EarningsReport.Build("c1",
                new[] {Order("a", 500_000, OrderStatus.Pending), Order("b", 500_000, OrderStatus.Failed)}, Titles);

            Assert.Equal(0, report.Totals.GrossMinor);
            Assert.Empty(report.Recent);
        }

        [Fact]
        public void Build_PassesAreCountedButBelongToNoSession()
        {
            var report = EarningsReport.Build("c1",
                new[] {Order("a", 2_000_000, kind: OrderKind.Pass), Order("b", 500_000)}, Titles);

            Assert.Equal(1, report.Totals.Passes);
            Assert.Equal(2_500_000, report.Totals.GrossMinor);
            var session = Assert.Single(report.Sessions);
            Assert.Equal("s1", session.SessionId);
        }

        [Fact]
        public void Build_SessionsAreGroupedAndTheBestComesFirst()
        {
            var report = EarningsReport.Build("c1", new[]
            {
                Order("a", 100_000, session: "s1"), Order("b", 900_000, session: "s2"),
                Order("c", 100_000, session: "s1"),
            }, Titles);

            Assert.Equal(new[] {"s2", "s1"}, report.Sessions.Select(x => x.SessionId));
            Assert.Equal("Drums", report.Sessions[0].Title);
            Assert.Equal(2, report.Sessions[1].Tickets);
            Assert.Equal(180_000, report.Sessions[1].NetMinor);
        }

        [Fact]
        public void Build_RecentSalesAreNewestFirst_LimitedAndMasked()
        {
            var orders = Enumerable.Range(0, 30).Select(i => Order("o" + i, 100_000, minutes: i)).ToList();

            var report = EarningsReport.Build("c1", orders, Titles);

            Assert.Equal(EarningsReport.RecentCount, report.Recent.Count);
            Assert.Equal(T0.AddMinutes(29), report.Recent[0].At);
            Assert.Equal("f**@example.com", report.Recent[0].Buyer);
            Assert.Equal("Piano", report.Recent[0].SessionTitle);
        }

        [Fact]
        public void Build_OtherCreatorsShareOfTheOrder_IsNotMine()
        {
            var order = Order("a", 1_000_000);
            order.Allocations = PriceSplit.Allocate(1_000_000, 10,
                new[] {new RevenueShare("c1", "A", 60), new RevenueShare("c2", "B", 40)}).ToList();

            var report = EarningsReport.Build("c2", new[] {order}, Titles);

            Assert.Equal(360_000, report.Totals.NetMinor);
        }

        [Fact]
        public void Build_VideoSales_AreCountedGroupedByVideoAndNamedInTheRecentList()
        {
            var video1 = Order("v1", 300_000, kind: OrderKind.Video);
            video1.VideoId = "vid1";
            var video2 = Order("v2", 300_000, kind: OrderKind.Video, minutes: 5);
            video2.VideoId = "vid1";
            var video3 = Order("v3", 100_000, kind: OrderKind.Video, minutes: 9);
            video3.VideoId = "vid2";

            var report = EarningsReport.Build("c1", new[] {video1, video2, video3, Order("t", 100_000)}, Titles,
                new Dictionary<string, string> {{"vid1", "Piano lesson"}, {"vid2", "Drum lesson"}});

            Assert.Equal(3, report.Totals.Videos);
            Assert.Equal(1, report.Totals.Tickets);
            Assert.Equal(3, report.Videos!.Sum(x => x.Sales));
            var best = report.Videos![0];
            Assert.Equal(("vid1", "Piano lesson", 2, 540_000), (best.VideoId, best.Title, best.Sales, best.NetMinor));
            var recent = report.Recent.First(x => x.Kind == "video");
            Assert.Equal("Drum lesson", recent.SessionTitle);
            // video sales are no session rows
            Assert.DoesNotContain(report.Sessions, x => x.SessionId == "");
        }

        [Fact]
        public void Build_ARefundedVideoOrder_IsNotASale()
        {
            var order = Order("v1", 300_000, OrderStatus.Refunded, kind: OrderKind.Video);
            order.VideoId = "vid1";

            var report = EarningsReport.Build("c1", new[] {order}, Titles);

            Assert.Equal(0, report.Totals.Videos);
            Assert.Empty(report.Videos!);
        }

        [Theory]
        [InlineData("ada@example.com", "a**@example.com")]
        [InlineData("a@example.com", "a@example.com")]
        [InlineData("verylongname@example.com", "v***@example.com")]
        [InlineData("no-at-sign", "***")]
        [InlineData("", "")]
        [InlineData(null, "")]
        public void MaskEmail_ShowsOnlyTheFirstLetter(string? email, string expected)
        {
            Assert.Equal(expected, EarningsReport.MaskEmail(email));
        }
    }
}
