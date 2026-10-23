using System;
using System.Linq;
using Strive.Core.Services.Payments;
using Strive.Core.Services.Payments.Sessions;

namespace Strive.Models.Response
{
    public record CreateSessionDto(string Title, string? Description, string Category, long PriceMinor,
        DateTimeOffset? StartsAt, int? DurationMinutes = null);

    /// <summary>A session as its host sees it</summary>
    public record SessionDto(string SessionId, string ConferenceId, string Title, string? Description,
        string Category, long PriceMinor, string Currency, SessionStatus Status, DateTimeOffset? StartsAt,
        DateTimeOffset CreatedAt, int Refunded = 0, int RefundsPending = 0, int DurationMinutes = 60)
    {
        public static SessionDto From(PaidSession session, int refunded = 0, int refundsPending = 0)
        {
            return new SessionDto(session.SessionId, session.ConferenceId, session.Title, session.Description,
                session.Category, session.PriceMinor, session.Currency, session.Status, session.StartsAt,
                session.CreatedAt, refunded, refundsPending, session.DurationMinutes);
        }
    }

    /// <summary>
    ///     What the page of a session shows to everybody. The room (conference id) is only given to the host and to people
    ///     with a ticket.
    /// </summary>
    public record PublicSessionDto(string SessionId, string Title, string? Description, string Category,
        long PriceMinor, string Currency, SessionStatus Status, DateTimeOffset? StartsAt, PublicCreatorDto Creator,
        bool IsHost, bool HasTicket, string? ConferenceId, int DurationMinutes = 60, bool IsLive = false);

    /// <summary>A session in the list of what is on, for everybody to see (the room is not part of it)</summary>
    public record DiscoverSessionDto(string SessionId, string Title, string? Description, string Category,
        long PriceMinor, string Currency, DateTimeOffset? StartsAt, string CreatorName, bool IsHost, bool HasTicket,
        int DurationMinutes = 60, bool IsLive = false, string? CreatorId = null, string? CreatorPhotoVersion = null);

    /// <summary>The page of a creator: who they are and what they have on</summary>
    public record CreatorPageDto(PublicCreatorDto Creator, DiscoverSessionDto[] Sessions, bool HasPass = false,
        DateTimeOffset? PassValidUntil = null, bool AutoRenewAvailable = false, DateTimeOffset? PassRenewsAt = null,
        VideoDto[]? Videos = null, long Followers = 0, bool Following = false,
        bool IsYou = false, string Notice = "off", bool NoticesAvailable = false);

    /// <summary>A pass of the person that is running</summary>
    /// <param name="SubscriptionId">Set if the pass renews itself: the person can stop it with this</param>
    /// <param name="RenewsAt">When the card is charged for the next pass</param>
    public record PassDto(string CreatorId, string CreatorName, DateTimeOffset ValidUntil,
        string? SubscriptionId = null, DateTimeOffset? RenewsAt = null, string? CreatorSlug = null);

    /// <summary>What a creator earned: the totals, per session, and the latest sales</summary>
    public record EarningsDto(string Currency, EarningsTotals Totals, System.Collections.Generic.IReadOnlyList<SessionEarnings> Sessions,
        System.Collections.Generic.IReadOnlyList<SaleEntry> Recent,
        System.Collections.Generic.IReadOnlyList<VideoEarnings>? Videos = null);

    /// <summary>The payments as the administrators of the server see them</summary>
    public record AdminPaymentsDto(string Currency, bool PaymentsEnabled, bool AutoRenewEnabled, long Creators,
        int ActiveSubscriptions, PaymentsPeriod Week, PaymentsPeriod Month,
        System.Collections.Generic.IReadOnlyList<OrderRow> Recent,
        System.Collections.Generic.IReadOnlyList<RefundProblem> RefundProblems,
        System.Collections.Generic.IReadOnlyList<SubscriptionProblem> SubscriptionProblems);

    public record CheckoutDto(string Email, bool AutoRenew = false);

    public record CheckoutResultDto(string? AuthorizationUrl, string? Reference, bool AlreadyHasTicket);

    /// <summary>The state of a payment, for the page the person comes back to after paying</summary>
    public record PaymentStatusDto(string Reference, string Status, string SessionId, bool HasTicket,
        string? ConferenceId, string? FailureReason, string Kind = "ticket", string? CreatorId = null,
        string? VideoId = null);

    public record TicketDto(string SessionId, string ConferenceId, string Title, string Category,
        DateTimeOffset? StartsAt, string CreatorName, DateTimeOffset GrantedAt, SessionStatus Status,
        bool Refunded = false, int DurationMinutes = 60, bool IsLive = false, string? CreatorId = null,
        string? CreatorPhotoVersion = null);
}

namespace Strive.Models.Response
{
    public record StartNoticesDto(string? Email);

    /// <param name="Status">on or off</param>
    public record NoticeAnswerDto(string CreatorName, string Status);
}
