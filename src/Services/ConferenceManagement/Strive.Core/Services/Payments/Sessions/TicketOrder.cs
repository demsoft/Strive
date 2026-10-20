using System;
using System.Collections.Generic;

namespace Strive.Core.Services.Payments.Sessions
{
    public enum OrderStatus
    {
        /// <summary>Checkout started, not paid (yet)</summary>
        Pending,

        /// <summary>Paid and verified with the provider</summary>
        Paid,

        /// <summary>The provider says the payment failed, or what was paid does not match the order</summary>
        Failed,

        /// <summary>Paid, and given back to the buyer because the session was called off</summary>
        Refunded,
    }

    /// <summary>Who gets how much of a paid order, in minor units. The parts add up to the amount.</summary>
    public record OrderAllocation(string Recipient, long AmountMinor)
    {
        public const string Platform = "platform";
    }

    /// <summary>
    ///     One purchase of a ticket. Everything that matters for the money (price, currency, commission, shares) is copied
    ///     here when the checkout starts, later changes of the session do not touch old orders.
    /// </summary>
    public class TicketOrder
    {
        public TicketOrder(string orderId, string reference, string sessionId, string conferenceId, string buyerUserId,
            string email, long amountMinor, string currency, int commissionPercent, DateTimeOffset createdAt)
        {
            OrderId = orderId;
            Reference = reference;
            SessionId = sessionId;
            ConferenceId = conferenceId;
            BuyerUserId = buyerUserId;
            Email = email;
            AmountMinor = amountMinor;
            Currency = currency;
            CommissionPercent = commissionPercent;
            CreatedAt = createdAt;
        }

        public string OrderId { get; init; }

        /// <summary>The reference at the provider, unique</summary>
        public string Reference { get; init; }

        public string SessionId { get; init; }

        public string ConferenceId { get; init; }

        public string BuyerUserId { get; init; }

        public string Email { get; init; }

        public long AmountMinor { get; init; }

        public string Currency { get; init; }

        public int CommissionPercent { get; init; }

        public IList<RevenueShare> Shares { get; set; } = new List<RevenueShare>();

        public OrderStatus Status { get; set; } = OrderStatus.Pending;

        public IList<OrderAllocation> Allocations { get; set; } = new List<OrderAllocation>();

        public string? ProviderTransactionId { get; set; }

        public string? Channel { get; set; }

        public string? FailureReason { get; set; }

        public DateTimeOffset CreatedAt { get; init; }

        public DateTimeOffset? PaidAt { get; set; }

        public DateTimeOffset? RefundedAt { get; set; }

        /// <summary>The refund at the provider</summary>
        public string? RefundId { get; set; }

        /// <summary>Why the last refund attempt did not work; the order stays paid and is tried again</summary>
        public string? RefundFailure { get; set; }
    }

    /// <summary>The right of a person to join the room of a session. One per paid order.</summary>
    public class Entitlement
    {
        public Entitlement(string entitlementId, string userId, string sessionId, string conferenceId, string orderId,
            DateTimeOffset grantedAt)
        {
            EntitlementId = entitlementId;
            UserId = userId;
            SessionId = sessionId;
            ConferenceId = conferenceId;
            OrderId = orderId;
            GrantedAt = grantedAt;
        }

        public string EntitlementId { get; init; }

        public string UserId { get; init; }

        public string SessionId { get; init; }

        public string ConferenceId { get; init; }

        public string OrderId { get; init; }

        public bool Revoked { get; set; }

        public DateTimeOffset GrantedAt { get; init; }
    }
}
