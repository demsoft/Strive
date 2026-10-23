using System;
using System.Collections.Generic;

namespace Strive.Core.Services.Payments.Sessions
{
    public enum SessionStatus
    {
        /// <summary>For sale (or already running)</summary>
        Published,

        /// <summary>Called off by the creator: nobody can buy a ticket or join</summary>
        Cancelled,
    }

    /// <summary>What a creator offers. Shown on the page of the session and used to filter later.</summary>
    public static class SessionCategories
    {
        public const string Teach = "teach";
        public const string Perform = "perform";
        public const string Expertise = "expertise";
        public const string Community = "community";
        public const string Music = "music";

        public static readonly IReadOnlyList<string> All = new[] {Teach, Perform, Expertise, Community, Music};
    }

    /// <summary>One creator's part of the ticket money. Today a session has one share of 100%.</summary>
    /// <param name="CreatorId">The creator that receives it</param>
    /// <param name="SubaccountCode">The sub account at the provider that is paid, copied when the session was made</param>
    /// <param name="Percent">The part of what is left after the platform commission</param>
    public record RevenueShare(string CreatorId, string SubaccountCode, int Percent);

    /// <summary>
    ///     A session that people buy a ticket for. It has a conference (the room) of its own; the ticket gives access to it.
    ///     The price and the shares are fixed when the session is published and kept on every order.
    /// </summary>
    public class PaidSession
    {
        public PaidSession(string sessionId, string conferenceId, string creatorId, string creatorUserId, string title,
            long priceMinor, string currency, DateTimeOffset createdAt)
        {
            SessionId = sessionId;
            ConferenceId = conferenceId;
            CreatorId = creatorId;
            CreatorUserId = creatorUserId;
            Title = title;
            PriceMinor = priceMinor;
            Currency = currency;
            CreatedAt = createdAt;
        }

        /// <summary>The short code in the link that is shared</summary>
        public string SessionId { get; init; }

        public string ConferenceId { get; init; }

        public string CreatorId { get; init; }

        /// <summary>The account of the host: can always join</summary>
        public string CreatorUserId { get; init; }

        public string Title { get; set; }

        public string? Description { get; set; }

        public string Category { get; set; } = SessionCategories.Teach;

        /// <summary>The price in minor units (kobo): 500000 is ₦5,000</summary>
        public long PriceMinor { get; init; }

        public string Currency { get; init; }

        public IList<RevenueShare> Shares { get; set; } = new List<RevenueShare>();

        public SessionStatus Status { get; set; } = SessionStatus.Published;

        /// <summary>When it starts, as shown to the people (the room is opened by the host)</summary>
        public DateTimeOffset? StartsAt { get; set; }

        /// <summary>How long it is meant to last, in minutes. The room stays open as long as the host wants.</summary>
        public int DurationMinutes { get; set; } = 60;

        /// <summary>When it is meant to end; unknown without a start time</summary>
        public DateTimeOffset? EndsAt => StartsAt?.AddMinutes(DurationMinutes);

        public DateTimeOffset CreatedAt { get; init; }

        /// <summary>When the mail that reminds the ticket holders was claimed; null: not sent yet</summary>
        public DateTimeOffset? ReminderSentAt { get; set; }

        /// <summary>When the followers who asked for mail were told about the session; null: not yet</summary>
        public DateTimeOffset? FollowersNoticedAt { get; set; }
    }
}
