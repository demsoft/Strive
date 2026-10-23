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

    public enum OrderKind
    {
        /// <summary>A ticket for one session</summary>
        Ticket,

        /// <summary>A pass: all sessions of one creator for a while</summary>
        Pass,

        /// <summary>One video of a creator, kept for good</summary>
        Video,
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

        /// <summary>What was bought. Older orders have no value and are tickets.</summary>
        public OrderKind Kind { get; set; } = OrderKind.Ticket;

        /// <summary>
        ///     Of a pass: the person agreed that it renews itself (the card is kept for it, if it can be charged again).
        ///     Set at checkout, from the choice of the person.
        /// </summary>
        public bool AutoRenew { get; set; }

        /// <summary>Of a pass: this order is a renewal that was charged without the person being there</summary>
        public bool IsRenewal { get; set; }

        /// <summary>Of a video: which one (a video order has no session, SessionId and ConferenceId are empty)</summary>
        public string? VideoId { get; set; }

        /// <summary>Of a pass: the creator it is for (a pass has no session, SessionId and ConferenceId are empty)</summary>
        public string? PassCreatorId { get; set; }

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

    /// <summary>The right of a person to join every session of a creator until <see cref="ValidUntil" />. One per paid pass order.</summary>
    public class PassEntitlement
    {
        public PassEntitlement(string passId, string userId, string creatorId, string orderId,
            DateTimeOffset grantedAt, DateTimeOffset validUntil)
        {
            PassId = passId;
            UserId = userId;
            CreatorId = creatorId;
            OrderId = orderId;
            GrantedAt = grantedAt;
            ValidUntil = validUntil;
        }

        public string PassId { get; init; }

        public string UserId { get; init; }

        public string CreatorId { get; init; }

        public string OrderId { get; init; }

        public DateTimeOffset GrantedAt { get; init; }

        public DateTimeOffset ValidUntil { get; init; }
    }

    public enum SubscriptionStatus
    {
        /// <summary>Renews itself</summary>
        Active,

        /// <summary>Stopped by the person (the pass runs until its end)</summary>
        Cancelled,

        /// <summary>Stopped because the renewal did not work after all the tries, or the creator stopped selling it</summary>
        Ended,
    }

    /// <summary>
    ///     A pass that renews itself: the card token of the first payment and what was agreed (the price at that time:
    ///     the creator cannot raise it for people who did not see the new one). The token is a secret, it is never sent
    ///     to a browser.
    /// </summary>
    public class PassSubscription
    {
        public PassSubscription(string subscriptionId, string userId, string creatorId, string email,
            string authorizationCode, long amountMinor, string currency, DateTimeOffset createdAt,
            DateTimeOffset nextChargeAt)
        {
            SubscriptionId = subscriptionId;
            UserId = userId;
            CreatorId = creatorId;
            Email = email;
            AuthorizationCode = authorizationCode;
            AmountMinor = amountMinor;
            Currency = currency;
            CreatedAt = createdAt;
            NextChargeAt = nextChargeAt;
        }

        public string SubscriptionId { get; init; }

        public string UserId { get; init; }

        public string CreatorId { get; init; }

        public string Email { get; set; }

        public string AuthorizationCode { get; set; }

        public long AmountMinor { get; set; }

        public string Currency { get; init; }

        public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;

        /// <summary>When the next renewal is charged (a little before the pass ends, so that nobody loses a day)</summary>
        public DateTimeOffset NextChargeAt { get; set; }

        /// <summary>How many renewals in a row did not work</summary>
        public int FailedAttempts { get; set; }

        /// <summary>A charge that was started and whose outcome is not known yet: it is looked at before anything else</summary>
        public string? PendingReference { get; set; }

        /// <summary>The renewal (its time) that the person was told about by mail</summary>
        public DateTimeOffset? NoticeSentFor { get; set; }

        public DateTimeOffset CreatedAt { get; init; }

        public DateTimeOffset? EndedAt { get; set; }
    }
}
