using System;
using System.Collections.Generic;
using System.Linq;
using Strive.Core.Services.Payments.Sessions;
using Strive.Core.Services.Reminders;

namespace Strive.Core.Services.Payments
{
    /// <param name="Days">The period reaches this many days back</param>
    /// <param name="GrossMinor">What was paid, without refunded orders</param>
    /// <param name="PlatformMinor">The commission of the platform on it</param>
    /// <param name="CreatorsMinor">What belongs to the creators</param>
    /// <param name="Renewals">Passes that were renewed by themselves (part of <paramref name="Passes" />)</param>
    public record PaymentsPeriod(int Days, long GrossMinor, long PlatformMinor, long CreatorsMinor,
        long RefundedMinor, int Tickets, int Passes, int Renewals, int Refunds);

    /// <param name="Kind">ticket, pass or renewal</param>
    public record OrderRow(DateTimeOffset At, string Kind, string Status, string CreatorName, string? SessionTitle,
        long AmountMinor, string Buyer, string? Problem);

    public record RefundProblem(string Reference, string CreatorName, string? SessionTitle, long AmountMinor,
        string Buyer, string? Reason, DateTimeOffset At);

    public record SubscriptionProblem(string SubscriptionId, string CreatorName, string Buyer, string Status,
        int FailedAttempts, DateTimeOffset NextChargeAt, DateTimeOffset? EndedAt);

    /// <summary>What the administrators of the server see of the payments</summary>
    public static class PaymentsOverview
    {
        public static PaymentsPeriod Period(IEnumerable<TicketOrder> orders, DateTimeOffset now, int days)
        {
            var since = now.AddDays(-days);
            var list = orders.Where(x => x.CreatedAt >= since &&
                                         x.Status is OrderStatus.Paid or OrderStatus.Refunded).ToList();
            var kept = list.Where(x => x.Status == OrderStatus.Paid).ToList();
            var refunded = list.Where(x => x.Status == OrderStatus.Refunded).ToList();

            long Platform(TicketOrder o) =>
                o.Allocations.Where(x => x.Recipient == OrderAllocation.Platform).Sum(x => x.AmountMinor);

            return new PaymentsPeriod(days, kept.Sum(x => x.AmountMinor), kept.Sum(Platform),
                kept.Sum(x => x.AmountMinor - Platform(x)), refunded.Sum(x => x.AmountMinor),
                kept.Count(x => x.Kind == OrderKind.Ticket), kept.Count(x => x.Kind == OrderKind.Pass),
                kept.Count(x => x.IsRenewal), refunded.Count);
        }

        /// <summary>The creator of an order: its pass, or the first share of a ticket</summary>
        public static string? CreatorOf(TicketOrder order) =>
            order.Kind == OrderKind.Pass ? order.PassCreatorId : order.Shares.FirstOrDefault()?.CreatorId;

        public static OrderRow Row(TicketOrder order, IReadOnlyDictionary<string, string> creatorNames,
            IReadOnlyDictionary<string, string> sessionTitles)
        {
            var creatorId = CreatorOf(order);
            var kind = order.Kind == OrderKind.Pass ? (order.IsRenewal ? "renewal" : "pass") : "ticket";
            var problem = order.Status == OrderStatus.Failed
                ? order.FailureReason ?? "failed"
                : order.RefundFailure != null && order.Status == OrderStatus.Paid
                    ? "refund: " + order.RefundFailure
                    : null;

            return new OrderRow(order.PaidAt ?? order.CreatedAt, kind, order.Status.ToString().ToLowerInvariant(),
                creatorId != null ? creatorNames.GetValueOrDefault(creatorId, "") : "",
                order.Kind == OrderKind.Ticket ? sessionTitles.GetValueOrDefault(order.SessionId) : null,
                order.AmountMinor, EarningsReport.MaskEmail(order.Email), problem);
        }

        public static RefundProblem Refund(TicketOrder order, IReadOnlyDictionary<string, string> creatorNames,
            IReadOnlyDictionary<string, string> sessionTitles)
        {
            var creatorId = CreatorOf(order);
            return new RefundProblem(order.Reference, creatorId != null ? creatorNames.GetValueOrDefault(creatorId, "") : "",
                sessionTitles.GetValueOrDefault(order.SessionId), order.AmountMinor,
                EarningsReport.MaskEmail(order.Email), order.RefundFailure, order.PaidAt ?? order.CreatedAt);
        }

        public static SubscriptionProblem Subscription(PassSubscription subscription,
            IReadOnlyDictionary<string, string> creatorNames) =>
            new(subscription.SubscriptionId, creatorNames.GetValueOrDefault(subscription.CreatorId, ""),
                EarningsReport.MaskEmail(subscription.Email), subscription.Status.ToString().ToLowerInvariant(),
                subscription.FailedAttempts, subscription.NextChargeAt, subscription.EndedAt);
    }
}
