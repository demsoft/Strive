using System;
using System.Collections.Generic;
using System.Linq;
using Strive.Core.Services.Payments.Sessions;

namespace Strive.Core.Services.Payments
{
    /// <param name="GrossMinor">What the fans paid, without the refunded orders</param>
    /// <param name="PlatformMinor">The commission of the platform on it</param>
    /// <param name="NetMinor">What is for the creator (paid out by the provider to the bank account)</param>
    /// <param name="RefundedMinor">What was given back (whole orders)</param>
    public record EarningsTotals(long GrossMinor, long PlatformMinor, long NetMinor, long RefundedMinor, int Tickets,
        int Passes, int Refunds);

    public record SessionEarnings(string SessionId, string Title, int Tickets, long GrossMinor, long NetMinor);

    /// <param name="Kind">ticket or pass</param>
    /// <param name="Buyer">A masked email: the creator sees that somebody bought, not who</param>
    public record SaleEntry(DateTimeOffset At, string Kind, string? SessionTitle, long AmountMinor, long NetMinor,
        string Buyer, bool Refunded);

    public record EarningsReport(EarningsTotals Totals, IReadOnlyList<SessionEarnings> Sessions,
        IReadOnlyList<SaleEntry> Recent)
    {
        public const int RecentCount = 20;

        /// <summary>
        ///     What a creator earned, from the paid orders that include their share. Refunded orders are not part of the
        ///     totals, they are counted on their own.
        /// </summary>
        /// <param name="sessionTitles">The titles of the sessions of the orders, by session id</param>
        public static EarningsReport Build(string creatorId, IEnumerable<TicketOrder> orders,
            IReadOnlyDictionary<string, string> sessionTitles)
        {
            var list = orders.Where(x => x.Status is OrderStatus.Paid or OrderStatus.Refunded).ToList();

            long Net(TicketOrder order) => order.Allocations.Where(x => x.Recipient == creatorId).Sum(x => x.AmountMinor);
            long Platform(TicketOrder order) =>
                order.Allocations.Where(x => x.Recipient == OrderAllocation.Platform).Sum(x => x.AmountMinor);

            var kept = list.Where(x => x.Status == OrderStatus.Paid).ToList();
            var refunded = list.Where(x => x.Status == OrderStatus.Refunded).ToList();

            var totals = new EarningsTotals(kept.Sum(x => x.AmountMinor), kept.Sum(Platform), kept.Sum(Net),
                refunded.Sum(x => x.AmountMinor), kept.Count(x => x.Kind == OrderKind.Ticket),
                kept.Count(x => x.Kind == OrderKind.Pass), refunded.Count);

            var sessions = kept.Where(x => x.Kind == OrderKind.Ticket)
                .GroupBy(x => x.SessionId)
                .Select(g => new SessionEarnings(g.Key, sessionTitles.GetValueOrDefault(g.Key, ""), g.Count(),
                    g.Sum(x => x.AmountMinor), g.Sum(Net)))
                .OrderByDescending(x => x.NetMinor)
                .ToList();

            var recent = list.OrderByDescending(x => x.PaidAt ?? x.CreatedAt)
                .Take(RecentCount)
                .Select(x => new SaleEntry(x.PaidAt ?? x.CreatedAt, x.Kind == OrderKind.Pass ? "pass" : "ticket",
                    x.Kind == OrderKind.Ticket ? sessionTitles.GetValueOrDefault(x.SessionId) : null, x.AmountMinor,
                    Net(x), MaskEmail(x.Email), x.Status == OrderStatus.Refunded))
                .ToList();

            return new EarningsReport(totals, sessions, recent);
        }

        /// <summary>ada@example.com becomes a**@example.com</summary>
        public static string MaskEmail(string? email)
        {
            if (string.IsNullOrWhiteSpace(email)) return "";
            var at = email.IndexOf('@');
            if (at <= 0) return "***";

            var local = email[..at];
            return local[0] + new string('*', Math.Min(local.Length - 1, 3)) + email[at..];
        }
    }
}
